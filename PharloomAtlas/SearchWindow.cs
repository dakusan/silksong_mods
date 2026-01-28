using SilkDev;
using SilkDev.Textures;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static SilkDev.DevInput.BlockInput;
using MButton=SilkDev.DevInput.Mouse.Button;

namespace PharloomAtlas;

public class SearchWindow : SilkDev.Windows.Window
{
	private static SearchWindow _Self=null!; public static SearchWindow Self => _Self; //Singleton

	//Constants
	private static readonly Translations Tr=Config.C.Tr;
	private static DataStorage DS=null!;
	private readonly GUIStyle TextStyle=new(GUI.skin.label) { fontSize=14, wordWrap=true };
	private readonly GUIStyle MouseOnlyStyle=new(GUI.skin.label) { fontSize=14, wordWrap=false, richText=false, alignment=TextAnchor.UpperCenter, normal={textColor=Color.red} };
	private readonly GUIStyle SearchHereStyle=new(GUI.skin.label) { fontSize=13, richText=false, alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold, normal={textColor=Color.grey} };
	private readonly GUIStyle NumResults=new(GUI.skin.label) { fontSize=13, alignment=TextAnchor.MiddleRight, normal={textColor=Color.magenta} };
	private const int MaxSearchResults=50, MaxLinesPerItem=7;

	//Information about a searched item
	private class SearchedItem : System.IDisposable
	{
		//Public members
		public readonly int ID, CutoffPoint;
		public readonly string RichText;
		public bool HasCutoff => CutoffPoint>0;
		public RicherLabel RLabel { get; private set; } = null!;
		public string CurrentText { get; private set; } = null!;
		public bool IsCurrentlyOver { get; set {
			if(field==value)
				return;
			field=value;
			RLabel		=(value ? RLabelFull: RLabelPartial  );
			CurrentText	=(value ? RichText	: RichTextPartial);
		} } = true; //Set to false in ctor

		//Private members
		private readonly RicherLabel RLabelFull=new(), RLabelPartial;
		private readonly string RichTextPartial;
		public SearchedItem(int ID, string RichText, int CutoffPoint=-1)
		{
			(this.ID, this.RichText, this.CutoffPoint)=(ID, RichText, CutoffPoint);
			RichTextPartial	=(!HasCutoff ? RichText		: RichText[..CutoffPoint]+"<color=red>...</color>");
			RLabelPartial	=(!HasCutoff ? RLabelFull	: new RicherLabel());
			IsCurrentlyOver=false;
		}

		public void Dispose()
		{
			RLabelFull.Dispose();
			if(HasCutoff)
				RLabelPartial.Dispose();
		}
	}

	//Members
	private SearchedItem[] SearchedItems=[];
	private Vector2 ScrollPosition=Vector2.zero;
	private string SearchText=string.Empty;
	private bool HadOverflow=false;
	private int ItemOverID=-1;
	private bool SearchTextHasFocus { set => Misc.IFF(
		field!=value,
		() => {
			_=Check_Actions.Toggle(BlockActions, field=value);
			GUI.FocusWindow(FocusedWindowID);
			GUI.FocusControl(value ? SearchFieldName : null);
		}
	); } = false;

	//Initialization
	internal static void Init() => _=new SearchWindow();
	private SearchWindow() : base(null!, Config.C.Rect_SearchWindow, 0, 0)
	{
		Misc.InitSingleton(this, ref _Self);
		TextStyle.normal.background=BGTex?.Tex;
		NumResults.padding.right+=8;
		if(WindowRect.width==0)
			WindowRect=new Rect(Screen.width-800-45, 42+179+10, 800, 600); //Set just below the default for SaveValuesWindow and aligned on the right side
		UpdateLangs();
		Tr.LanguageChanged += UpdateLangs;
	}
	private void UpdateLangs()
	{
		Title=Tr.TDef("SearchWindow.Title", Default:"Search icons");
		MessageOverrides[BlockActions]=Tr.TDef("BlockActions.OnlyCancel", null, "Only cancel allowed", true);
	}

	//Watch for escape key
	protected override void OnUpdate() => Misc.IFF(
		Input.GetKey(KeyCode.Escape), //Handle global key check
		() => Visible=false
	);

	//Draw the window contents
	private const string SearchFieldName="SearchWindowSearchField";
	protected override void DoLayout(int ID, Event CurEv)
	{
		GUILayout.BeginVertical();

		//Draw a small label about the window
		Misc.RenderFixedWidthLine(
			Tr.TDef("SearchWindow.NoController", Default:"This window only supports mouse (see config “Show mouse”) and keyboard. You can close me with escape or cancel."),
			MouseOnlyStyle,
			Text => GUILayout.Label(Text, MouseOnlyStyle)
		);

		//Search text
		string OldSearchText=SearchText.Trim();
		GUI.SetNextControlName(SearchFieldName);
		SearchText=GUILayout.TextField(SearchText);
		if(OldSearchText.Trim()!=SearchText.Trim())
			RunSearch(SearchText.Trim());
		bool NoResults=(SearchText.Trim().Length==0);
		int NumFoundItems=SearchedItems.Length;
		GUI.Label(
			GUILayoutUtility.GetLastRect(),
				  NoResults ? Tr.T("Type in here to search")
				: NumFoundItems+(HadOverflow ? "+" : null),
			NoResults ? SearchHereStyle : NumResults
		);
		if(Event.current.type==EventType.Repaint)
			SearchTextHasFocus=(FocusedWindowID==ID);

		//Draw the found items
		ScrollPosition=GUILayout.BeginScrollView(ScrollPosition, GUILayout.ExpandHeight(true));
		foreach(SearchedItem SI in SearchedItems)
			DrawItem(SI);

		//Add the window dragging/resizing
		GUILayout.EndScrollView();
		GUILayout.EndVertical();
	}

	//Split the search text into terms and find any item that has all search terms
	private void RunSearch(string SearchText)
	{
		//Make sure DS is initialized
		DS ??= MapControl.Self.DS;

		//Empty search yields nothing
		SearchedItems.ForEach(static SI => SI.Dispose()); //Dispose previous results
		if(SearchText.Trim().Length==0) {
			SearchedItems=[];
			return;
		}

		//Search item string transformer
		HashSet<int> YieldedMatchedIds=[], MatchingIDs=[];
		Dictionary<int, Category> Cats=DS.Categories;
		string? SearchTransformer(Item I) =>
			  MatchingIDs.Contains(I.ID) && !YieldedMatchedIds.Add(I.ID) ? null //Only process matching IDs once
			: string.Join((char)1, [
				I.ID.ToString(), I.Description,
				DevStrings.SafeRich(I.Title),
				DevStrings.SafeRich(Cats[I.CategoryID].Title),
			]);

		//Get a list of items with exact ID matches
		DevStrings.I18NSearch<Item> DoSearch=new(SearchText, SearchTransformer);
		DoSearch.OriginalTerms
			.Select	(static Str => int.TryParse(Str, out int ID) && DS.Items.ContainsKey(ID) ? ID : -1)
			.Where	(static ID  => ID!=-1)
			.ForEach(		ID  => MatchingIDs.Add(ID));

		//Run the search
		List<Item> NewItems=[.. DoSearch.Execute(
			MatchingIDs.Select(static ItemID => DS.Items[ItemID])
			.Concat(DS.Items.Values))
			.Take(MaxSearchResults+1)
		];

		//Account for overflow
		if(HadOverflow=(NewItems.Count>MaxSearchResults))
			NewItems.RemoveAt(NewItems.Count-1);

		//Transform the items into rich strings and store with their IDs
		SearchedItems=[.. NewItems.Select(I => CreateSearchedItem(I.ID, string.Join(DevStrings.NewLine, new string?[] {
			 	MakeItemInfoLine(			"Title",		DevStrings.SafeRich(I.Title)+$" <size=11>[{I.ID}]</size>",	DoSearch),
				MakeItemInfoLine(			"Category",		DevStrings.SafeRich(Cats[I.CategoryID].Title),				DoSearch),
			I.Description==null ? null :
				MakeItemInfoLine(			"Description",	I.Description,												DoSearch),
		}.Where(static S => !string.IsNullOrEmpty(S))
		)))];
	}

	//Create a string with sized down title and Info with highlighted search terms
	private static string MakeItemInfoLine(string Title, string Info, DevStrings.I18NSearch<Item> DoSearch)
	{
		if(string.IsNullOrEmpty(Info))
			return null!;

		IEnumerator<Match> TagEnum=(IEnumerator<Match>)DevStrings.I18NSearch<int>.RegEx_RemoveHTMLTags.Matches(Info).GetEnumerator();
		bool HasTag=TagEnum.MoveNext();
		Match? CurTag=(HasTag ? TagEnum.Current : null);

		bool InTag(int Pos)
		{
			while(CurTag!=null) {
				if(Pos<CurTag.Index)
					return false;
				if(Pos<CurTag.Index+CurTag.Length)
					return true;
				HasTag=TagEnum.MoveNext();
				CurTag=HasTag ? TagEnum.Current : null;
			}
			return false;
		}
		string ColorTag=$"<b><color={DS.LinkColors.Search_Highlight}>", ColorEndTag="</color></b>";
		return $"<size=-2>{Tr.T(Title, "ItemFields", true)}</size>: "+DoSearch.ReplaceSearchTerms(
			Info, (FindIndex, FindValue) => InTag(FindIndex) ? FindValue : ColorTag+FindValue+ColorEndTag
		);
	}

	//Compile a SearchedItem
	private SearchedItem CreateSearchedItem(int ID, string RichText)
	{
		int Count=0, Index=-1;
		while(Count<MaxLinesPerItem && (Index=RichText.IndexOf(DevStrings.NewLine, Index+1))!=-1)
			Count++;
		return new SearchedItem(ID, RichText, Count==MaxLinesPerItem ? Index : -1);
	}

	//Draw a label with a line under it. Highlight if hovered, and handle click.
	private void DrawItem(SearchedItem SI)
	{
		//Draw the label
		SI.IsCurrentlyOver=(ItemOverID==SI.ID);
		SI.RLabel.ScrollPosOffset=ScrollPosition;
		bool LinkClicked=SI.RLabel.Draw(SI.CurrentText, TextStyle, -1);

		//Set if over the item
		Rect LabelRect=GUILayoutUtility.GetLastRect();
		bool MouseOver=LabelRect.Contains(Event.current.mousePosition);
		if(Event.current.type==EventType.Repaint)
			if(MouseOver)
				ItemOverID=SI.ID;
			else if(ItemOverID==SI.ID)
				ItemOverID=-1;

		//Highlight the label if hovered and check for click
		if(MouseOver) {
			DS.LinkColors.LabelHover.AsColor.DrawRect(LabelRect);
			if(!LinkClicked && Event.current.type==EventType.MouseUp && MButton.CurrentButton==MButton.Enum.Left)
				MapControl.Self.SelectAndCenterItemI(SI.ID);
		}

		//Draw a line under the label
		Color.white.DrawRect(LabelRect.AddY(LabelRect.height+2).SetHeight(1));
	}

	//Setting as invisible
	protected override void CloseButton() => Visible=false;
	public override bool Visible {
		get => base.Visible;
		set {
			if(!value)
				SearchTextHasFocus=false;
			if(base.Visible!=value && (base.Visible=value))
				OnNextFrame(() => GUI.FocusWindow(ID), false);
		}
	}

	//Allow only cancel action while search field is focused
	private static CAResults BlockActions(CAParams P) => AllowAction(P, "Cancel");
}