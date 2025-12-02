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
	private readonly GUIStyle TextStyle=new(GUI.skin.label) { fontSize=14, wordWrap=true };
	private readonly GUIStyle MouseOnlyStyle=new(GUI.skin.label) { fontSize=14, alignment=TextAnchor.UpperCenter, normal={textColor=Color.red} };
	private readonly GUIStyle SearchHereStyle=new(GUI.skin.label) { fontSize=13, alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold, normal={textColor=Color.grey} };
	private readonly GUIStyle NumResults=new(GUI.skin.label) { fontSize=13, alignment=TextAnchor.MiddleRight, normal={textColor=Color.magenta} };
	private readonly Texture2D SelectTex=new Color(1, 1, 0, 0.5f).MakeTexture();
	private const int MaxSearchResults=50, MaxLinesPerItem=7;
	private const string TextHighlightColor="green";

	//Information about a searched item
	private class SearchedItem(int ID, string RichText, int CutoffPoint)
	{
		public readonly int ID=ID, CutoffPoint=CutoffPoint;
		public readonly string RichText=RichText;
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
	private SearchWindow() : base("Search icons", Config.C.Rect_SearchWindow, 0, 0)
	{
		Misc.InitSingleton(this, ref _Self);
		TextStyle.normal.background=BGTex;
		NumResults.padding.right+=8;
		if(WindowRect.width==0)
			WindowRect=new Rect(Screen.width-800-45, 42+179+10, 800, 600); //Set just below the default for SaveValuesWindow and aligned on the right side
	}

	//Watch for escape key
	protected override void OnUpdate()
	{
		//Handle global key check
		if(Input.GetKey(KeyCode.Escape))
			Visible=false;
	}

	//Draw the window contents
	protected override void DoLayout(int ID, Event CurEv)
	{
		GUILayout.BeginVertical();

		//Draw a small label about the window
		GUILayout.Label("This window only supports mouse (see config “Show mouse”) and keyboard. You can close me with escape or cancel.", MouseOnlyStyle);

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
				  NoResults ? "Type in here to search"
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
	private void RunSearch(string SearchText)
	{
		//Empty search yields nothing
		if(SearchText.Length==0) {
			SearchedItems=[];
			return;
		}

		//Run the search
		string[] Terms=Regex.Split(SearchText.ToLower(), @"\s+");
		Dictionary<int, Category> Cats=MapControl.Self.DS.Categories;
		List<Item> NewItems=[.. MapControl.Self.DS.Items.Values.AsEnumerable().Where(I => {
			string SearchItemInfo=$"{I.Title}{I.Description}{Cats[I.CategoryID].Title}".ToLower();
			foreach(string Term in Terms)
				if(!SearchItemInfo.Contains(Term))
					return false;
			return true;
		}).Take(MaxSearchResults+1)];

		//Account for overflow
		if(HadOverflow=(NewItems.Count>MaxSearchResults))
			NewItems.RemoveAt(NewItems.Count-1);

		//Transform the items into rich strings and store with their IDs
		string[] EscapedTerms=[.. Terms.Select(static T => "("+Regex.Escape(Misc.SanitizeRichString(T))+")")]; //Create regular expressions to colorize the strings
		SearchedItems=[.. NewItems.Select(I => CreateSearchedItem(I.ID, string.Join(Misc.NewLine, new string[] {
			MakeItemInfoLine("Title", I.Title, EscapedTerms),
			MakeItemInfoLine("Category", Cats[I.CategoryID].Title, EscapedTerms),
			I.IgnPageName==null ? Misc.Empty : MakeItemInfoLine("IGN Page", "https://www.ign.com/wikis/hollow-knight-silksong/"+I.IgnPageName, EscapedTerms),
			I.Description==null ? Misc.Empty : MakeItemInfoLine("Description", I.Description, EscapedTerms),
		}.Where(static S => S!=Misc.Empty)
		)))];
	}

	//Create a string with sized down title and Info with highlighted search terms
	private static string MakeItemInfoLine(string Title, string Info, string[] EscapedTerms)
	{
		Info=Misc.SanitizeRichString(Info);
		foreach(string Term in EscapedTerms)
			Info=Regex.Replace(Info, Term, (char)1+"$1"+(char)2, RegexOptions.IgnoreCase);
		Info=Info.Replace((char)1+Misc.Empty, $"<color={TextHighlightColor}>").Replace((char)2+Misc.Empty, "</color>");
		return $"<size=-2>{Title}</size>: <b>{Info}</b>";
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
		GUILayout.Label(
			  ItemOverID==SI.ID || SI.CutoffPoint==-1 ? SI.RichText
			: SI.RichText[..SI.CutoffPoint]+"<color=red>...</color>",
			TextStyle
		);

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
			GUI.DrawTexture(LabelRect, SelectTex);
			if(Event.current.type==EventType.MouseUp && MButton.CurrentButton==MButton.Enum.Left)
				MapControl.Self.SelectAndCenterItemI(SI.ID);
		}

		//Draw a line under the label
		GUI.DrawTexture(
			LabelRect.AddY(LabelRect.height+2).SetHeight(1),
			Texture2D.whiteTexture, ScaleMode.StretchToFill
		);
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