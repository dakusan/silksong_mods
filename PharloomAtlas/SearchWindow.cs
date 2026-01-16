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
	private readonly GUIStyle TextStyle=new(GUI.skin.label) { fontSize=14, wordWrap=true };
	private readonly GUIStyle MouseOnlyStyle=new(GUI.skin.label) { fontSize=14, wordWrap=false, richText=false, alignment=TextAnchor.UpperCenter, normal={textColor=Color.red} };
	private readonly GUIStyle SearchHereStyle=new(GUI.skin.label) { fontSize=13, richText=false, alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold, normal={textColor=Color.grey} };
	private readonly GUIStyle NumResults=new(GUI.skin.label) { fontSize=13, alignment=TextAnchor.MiddleRight, normal={textColor=Color.magenta} };
	private readonly Color SelectCol=new(70/255f, 120/255f, 200/255f, 0.5f); //Desaturated, mid-luminance blue goes well with: red, teal, plum, yellow, cyan, white, black, green
	private const int MaxSearchResults=50, MaxLinesPerItem=7;
	private const string TextHighlightColor="green";

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
	private string SearchText=Misc.Empty;
	private bool HadOverflow=false;
	private int ItemOverID=-1;
	private bool SearchTextHasFocus { set => Misc.IFF(
		field!=value,
		() => Check_Actions.Toggle(BlockActions, field=value)
	); } = false;

	//Initialization
	internal static void Init() => _=new SearchWindow();
	private SearchWindow() : base(Misc.Empty, Config.C.Rect_SearchWindow, 0, 0)
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
		const string SearchFieldName="SearchWindowSearchField";
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
				: NumFoundItems+(HadOverflow ? "+" : Misc.Empty),
			NoResults ? SearchHereStyle : NumResults
		);
		SearchTextHasFocus=GUI.GetNameOfFocusedControl()==SearchFieldName;

		//Draw the found items
		ScrollPosition=GUILayout.BeginScrollView(ScrollPosition, GUILayout.ExpandHeight(true));
		foreach(SearchedItem SI in SearchedItems)
			DrawItem(SI);

		//Add the window dragging/resizing
		GUILayout.EndScrollView();
		GUILayout.EndVertical();
	}

	//Split the search text into terms and find any item that has all search terms
	private static readonly Regex RegEx_RemoveHTMLTags=new(@"</?\w[^>]+>", RegexOptions.Compiled), RegEx_SplitAroundSpaces=new(@"\s+", RegexOptions.Compiled);
	private void RunSearch(string SearchText)
	{
		//Empty search yields nothing
		SearchedItems.ForEach(static SI => SI.Dispose()); //Dispose previous results
		if(SearchText.Length==0) {
			SearchedItems=[];
			return;
		}

		//Run the search
		string[] Terms=RegEx_SplitAroundSpaces.Split(SearchText.ToLower());
		Dictionary<int, Category> Cats=MapControl.Self.DS.Categories;
		List<Item> NewItems=[.. MapControl.Self.DS.Items.Values.Where(I => {
			string SearchItemInfo=RegEx_RemoveHTMLTags.Replace((I.Title+I.Description+Cats[I.CategoryID].Title).ToLower(), Misc.Empty);
			foreach(string Term in Terms)
				if(!SearchItemInfo.Contains(Term))
					return false;
			return true;
		}).Take(MaxSearchResults+1)];

		//Account for overflow
		if(HadOverflow=(NewItems.Count>MaxSearchResults))
			NewItems.RemoveAt(NewItems.Count-1);

		//Transform the items into rich strings and store with their IDs
		Regex EscapedTermsRegEx=new("("+string.Join('|', Terms.Select(static T => Regex.Escape(DevStrings.SanitizeRichString(T))))+")", RegexOptions.IgnoreCase); //Create regular expression to colorize the strings
		SearchedItems=[.. NewItems.Select(I => CreateSearchedItem(I.ID, string.Join(Misc.NewLine, new string[] {
			MakeItemInfoLine("Title", I.Title, EscapedTermsRegEx),
			MakeItemInfoLine("Category", Cats[I.CategoryID].Title, EscapedTermsRegEx),
			I.IgnPageName==null ? Misc.Empty : MakeItemInfoLine("IGN Page", "https://www.ign.com/wikis/hollow-knight-silksong/"+I.IgnPageName, EscapedTermsRegEx),
			I.Description==null ? Misc.Empty : MakeItemInfoLine("Description", I.Description, EscapedTermsRegEx),
		}.Where(static S => S!=Misc.Empty)
		)))];
	}

	//Create a string with sized down title and Info with highlighted search terms
	private static string MakeItemInfoLine(string Title, string Info, Regex EscapedTerms)
	{
		IEnumerator<Match> TagEnum=(IEnumerator<Match>)RegEx_RemoveHTMLTags.Matches(Info).GetEnumerator();
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
		string ColorTag=$"<b><color={TextHighlightColor}>", ColorEndTag="</color></b>";
		return $"<size=-2>{Tr.T(Title, "ItemFields", true)}</size>: "+EscapedTerms.Replace(
			Info, M => InTag(M.Index) ? M.Value : ColorTag+M.Value+ColorEndTag
		);
	}

	//Compile a SearchedItem
	private SearchedItem CreateSearchedItem(int ID, string RichText)
	{
		int Count=0, Index=-1;
		while(Count<MaxLinesPerItem && (Index=RichText.IndexOf(Misc.NewLine, Index+1))!=-1)
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
			SelectCol.DrawRect(LabelRect);
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
		set => Misc.IFF(
			!(base.Visible=value),
			() => SearchTextHasFocus=false
		);
	}

	//Allow only cancel action while search field is focused
	private static CAResults BlockActions(CAParams P) => AllowAction(P, "Cancel");
}