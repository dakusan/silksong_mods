using SilkDev.Textures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using StringBuilder = System.Text.StringBuilder;

namespace SilkDev.Windows;

/*
 * This draws a GUI.Label or GUILayout.Label. Links, denoted by “<LinkID=LINKID>...</LinkID>”, will be turned into a Link object. Link objects are clickable, colored LinkColor, and change to LinkHoverColor on mouse hover.
 * Links can have a list of attributes “<ATTR=AttrName>AttrVal</ATTR>” fields preceeding their text in the LinkID block. These attributes are stored in the link and not displayed. Names cannot contain a “>” sign.
 * Everytime a member of ClickableLabel changes which will change the rendering of the label, it has to redetermine all the info for the Links, which is a bit expensive.
 *	- When this happens, it has to determine which Link is which. It does this via the LinkID for each Link, which needs to be unique.
 * Link labels nesting is not supported.
*/
public class ClickableLabel
{
	//If any of the following are updated, the links have to be reextracted
	public GUIStyle Style		{ get; set { if(field!=value) Clear(false); field=value; } } = null!;
	public   string Text		{ get; set { if(field!=value) Clear(true ); field=value; } } = null!;
	public  Vector2 RectSize	{ get; set { if(field!=value) Clear(false); field=value; } } = Vector2.zero;

	//These do not affect live styles so do not require reextraction when changed
	public Vector2 Pos { get; internal set; }
	public Color LinkColor
	{
		get;
		set {
			if(field==value)
				return;
			LinkColorHex=(field=value).ToHex();

			//Recreate the render string with the new colors
			RenderString=LiveLinks.AsEnumerable()
				.Reverse()
				.Aggregate(RenderString, (Str, L) =>
					 Str[..L.StringStartPos]
					+$"<color=#{LinkColorHex}>{L.Text}</color>"
					+Str[L.StringEndPos..]
				);
		}
	} = Color.cyan;
	public Color HoverColor
	{
		get;
		set => HoverColorHex=(field=value).ToHex();
	} = Color.yellow;
	private string LinkColorHex=null!, HoverColorHex=null!;

	public ClickableLabel()
	{
		LinkColorHex=LinkColor.ToHex();
		HoverColorHex=HoverColor.ToHex();
	}

	//This holds the links by their LinkID
	private readonly Dictionary<string, Link> Links=[];
	private readonly List<Link> LiveLinks=[];
	public Link[] ActiveLinks => [..LiveLinks];

	//Information about a found link. All fields are updated during extraction.
	public class Link
	{
		internal Link(ClickableLabel Parent, string LinkID) =>
			(this.Parent, this.LinkID)=(Parent, LinkID);

		//Used to make sure this link stays up to date
		private readonly ClickableLabel Parent;

		//Updated during extraction
		public readonly string LinkID;
		public string Text { get; internal set; } = null!;
		public bool IsLive { get; internal set; } = false; //If the link is being rendered inside the current label
		internal Rect[] Boxes=null!; //Relative to the upper left corner of the ClickableLabel
		internal readonly Dictionary<string, string> Attrs=[];
		public int StringStartPos, StringEndPos;

		//Public getters
		public System.Collections.ObjectModel.ReadOnlyDictionary<string, string> Attributes => new(Attrs);
		public Rect[] Rects => Misc.PassThru(Parent.Extract, IsLive ? [..Boxes] : (Rect[])[]);
	}

	//Marks that the links need to be reextracted. If you change something inside your LabelStyle, this may be needed.
	public void Clear(bool NeedsParsing) => (this.NeedsParsing, NeedsExtracting)=(NeedsParsing|this.NeedsParsing, true);
	private bool NeedsParsing=true, NeedsExtracting=true; //This keeps track of when parsing or link rects need to be refreshed.

	//Information needed for rendering
	private string RenderString=null!;

	//Renders the label. Reextraction is only ever ran if the mouse is over the label. Does not return Link on layout phase
	public Link? GUILabel(Rect Rect, string Content, GUIStyle? NewGUIStyle=null, params Link[] ExtraSelectedItems)
	{
		//Update fields to make sure we don’t need a reparse or rerender
		RectSize=Rect.size;
		Pos=Rect.position;
		Text=Content;
		Style=NewGUIStyle ?? Style;
		if(NeedsParsing)
			Parse();

		//If mouse is not over the label, or in layout phase, then just render the label as is
		if(
			   Event.current.type==EventType.Layout
			|| (!Rect.Contains(DevInput.Util.MousePos) && ExtraSelectedItems.Length==0)
		) {
			GUI.Label(Rect, RenderString, Style);
			return null;
		}

		//Run extraction if needed
		if(NeedsExtracting)
			Extract();

		//Find if any live labels are being hovered
		Vector2 LocalMPos=DevInput.Util.MousePos-Pos;
		Link? HoveredLink=LiveLinks.FirstOrDefault(L => L.Boxes.Any(R => R.Contains(LocalMPos)));

		//Combine hover link with ExtraSelectedItems
		string RenderText=
			((Link[])[.. ExtraSelectedItems, .. HoveredLink!=null ? [HoveredLink] : (Link[])[]])
			.OrderByDescending(static L => L.StringStartPos)
			.Aggregate(RenderString, (Str, L) =>
				 Str[..L.StringStartPos]
				+$"<color=#{HoverColorHex}>{L.Text}</color>"
				+Str[L.StringEndPos..]
			);

		//Render and return
		GUI.Label(Rect, RenderText, Style);
		return HoveredLink;
	}
	public Link? GUILabel(Rect Position, GUIContent Content, GUIStyle? NewGUIStyle=null, params Link[] ExtraSelectedItems) =>
		GUILabel(Position, Content.text, NewGUIStyle, ExtraSelectedItems);
	public Link? GUILabelLayout(string Content, GUIStyle? NewGUIStyle=null, Link[]? ExtraSelectedItems=null, params GUILayoutOption[] Options)
	{
		//Parse first
		Text=Content;
		if(NeedsParsing)
			Parse();

		//Render as transparent
		Color PrevColor=GUI.color;
		GUI.color=new Color(0, 0, 0, 0);
		GUILayout.Label(RenderString, NewGUIStyle ?? Style, Options);
		GUI.color=PrevColor;

		//Render again if not layout phase (when we actually know its positioning)
		return Event.current.type!=EventType.Layout ? GUILabel(GUILayoutUtility.GetLastRect(), Content, NewGUIStyle, ExtraSelectedItems ?? []) : null;
	}

	//Whenever the label’s string changes, this is needed
	private void Parse()
	{
		//Make sure we need to parse
		if(!NeedsParsing)
			return;
		NeedsParsing=false;

		//Mark all links as not live
		foreach(Link L in Links.Values)
			L.IsLive=false;
		LiveLinks.Clear();

		//Extract all <color> blocks
		Regex LinkRegEx=new(@"<LinkID\s*=([^>]+)>(.*?)</LinkID>", RegexOptions.IgnoreCase);
		Regex AttrRegEx=new(@"^(<ATTR\s*=\s*([^>]+)\s*>\s*(.*?)\s*</ATTR>)*", RegexOptions.IgnoreCase);
		StringBuilder Result=new();
		string Contents=Text;
		int CurIndex=0;
		while(true) {
			//Find LinkID tag matches and stop here if there are no more
			Match LinkMatch=LinkRegEx.Match(Contents, CurIndex);
			if(!LinkMatch.Success) {
				_=Result.Append(Contents, CurIndex, Contents.Length-CurIndex);
				break;
			}
			_=Result.Append(Contents, CurIndex, LinkMatch.Index-CurIndex);

			//Extract attributes
			Dictionary<string, string> AttrList=[];
			string OutString=LinkMatch.Groups[2].Value;
			Match AttrMatch=AttrRegEx.Match(OutString);
			for(int i=0; i<AttrMatch.Groups[2].Captures.Count; i++)
				AttrList[AttrMatch.Groups[2].Captures[i].Value]=AttrMatch.Groups[3].Captures[i].Value;
			OutString=OutString[AttrMatch.Length..];

			//Get/create the Link
			string LinkID=LinkMatch.Groups[1].Value.Trim();
			if(!Links.TryGetValue(LinkID, out Link L))
				L=Links[LinkID]=new Link(this, LinkID);
			LiveLinks.Add(L);

			//Copy over the new values to the link
			L.Text=OutString;
			L.Attrs.Clear();
			L.Attrs.AddRange(AttrList);
			L.IsLive=true;
			L.StringStartPos=Result.Length;
			_=Result.Append($"<color=#{LinkColorHex}>{L.Text}</color>");
			L.StringEndPos=Result.Length;
			CurIndex=LinkMatch.Index+LinkMatch.Length;
		}

		LiveLinks.Sort(static (a, b) => a.StringStartPos.CompareTo(b.StringStartPos));
		RenderString=Result.ToString();
	}

	//This is only ran when the label will render differently visually. It extracts the rectangles for the links.
	private void Extract()
	{
		//Make sure we need to extract
		if(!NeedsExtracting)
			return;
		NeedsExtracting=false;
		DateTime StartTime=DateTime.Now;
		using GetStringRects GSR=new((int)RectSize.x, (int)RectSize.y);

		//Create a list of the render string sections separated by the links, and all color tags removed
		const char SplitChar='\x01'; //A character that should not be used in the string used for splitting. Too bad we aren’t in UTF8. (Mumble muble Microsoft mumble muble)
		List<string> StringParts=[..
			Regex.Replace(
				LiveLinks.AsEnumerable().Reverse().Aggregate(RenderString, static (Str, L) => Str[..L.StringStartPos]+SplitChar+Str[L.StringEndPos..]),
				@"<color\s*=[^>]+>|</color>",
				Misc.Empty
			).Split(SplitChar)
		];

		//Confirm the splits look good
		if(LiveLinks.Count!=StringParts.Count-1)
			throw new Exception($"Impossible error happened: {LiveLinks.Count}!={StringParts.Count-1}");

		//Make a full render string with redetermined link placement for quick string rendering
		StringBuilder SB=new("<color=#00000000>");
		var CreateStrings=new (Link L, int StartPos, int EndPos)[LiveLinks.Count];
		foreach((int Index, string StrPart) in StringParts.Entries()) {
			_=SB.Append(StrPart);
			if(Index>=LiveLinks.Count)
				break;
			Link L=LiveLinks[Index];
			CreateStrings[Index]=(L, SB.Length, SB.Length+L.Text.Length);
			_=SB.Append(L.Text);
		}
		string FinalStr=SB.ToString();

		//Create the separate strings with colored text to measure boxes
		foreach((Link L, int StartPos, int EndPos) in CreateStrings)
			L.Boxes=GSR.Exec(FinalStr[..StartPos]+$"<color=black>{L.Text}</color>"+FinalStr[EndPos..], Style);

		Log.Info($"Time to extract ClickLabel Rects: {(DateTime.Now-StartTime).TotalSeconds:F3} seconds");
	}

	//This class takes a string, renders it, and determines the rects of the visible sections
	private class GetStringRects : IDisposable
	{
		private readonly int Width, Height;
		private readonly Rect DrawRect;
		private readonly Texture2D Tex;
		private readonly RenderTexture RT, PrevRT;

		public GetStringRects(int Width, int Height)
		{
			//Create/set render textures
			(this.Width, this.Height)=(Width, Height);
			DrawRect=new(0, 0, Width, Height);
			Tex=new(Width, Height, TextureFormat.ARGB32, false);
			RT=RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); //TODO: This might be doable with normal sized texture and a matrix scale
			PrevRT=RenderTexture.active;
			RenderTexture.active=RT;
		}

		//The StrText needs to ONLY show the text of the rectangle we are measuring. The alpha channel is used to determine the rectangles
		public Rect[] Exec(string RenderStr, GUIStyle Style)
		{
			//Draw to RT
			GL.Clear(true, true, Color.clear);
			GUI.Label(DrawRect, RenderStr, Style);
			Tex.ReadPixels(new Rect(0, Screen.height-Height, Width, Height), 0, 0);
			Tex.Apply();

			//Scan for pixel bounds, group into lines
			Unity.Collections.NativeArray<Color32> Pixels=Tex.GetPixelData<Color32>(0);
			List<Rect> Rects=[];
			Dictionary<int, (int minX, int maxX)> LineData=[];
			for(int y=0; y<Height; y++) {
				int MinX=Width, MaxX=0;
				bool HasPixels=false;
				for(int x=0; x<Width; x++)
					if(Pixels[y*Width+x].rgba>0)
					{
						HasPixels=true;
						MinX=Mathf.Min(MinX, x);
						MaxX=Mathf.Max(MaxX, x);
					}
				if(HasPixels)
					LineData[y]=(MinX, MaxX);
			}

			if(LineData.Count<1)
				return [];

			//Group lines by consecutive y with small gaps
			List<int> SortedYs=[.. LineData.Keys.OrderBy(static K => K)];
			List<int> CurrentGroup=[SortedYs[0]];
			for(int i=1; i<SortedYs.Count; i++)
				if(SortedYs[i]-SortedYs[i-1] <= 2) //Threshold
					CurrentGroup.Add(SortedYs[i]);
				else {
					AddGroupRect(CurrentGroup, LineData, Height, Rects);
					CurrentGroup=[SortedYs[i]];
				}
			AddGroupRect(CurrentGroup, LineData, Height, Rects);
			return [.. Rects];
		}

		private void AddGroupRect(List<int> GroupYs, Dictionary<int, (int minX, int maxX)> LineData, int TexHeight, List<Rect> Rects)
		{
			int GroupMinY=GroupYs.Min();
			int GroupMaxY=GroupYs.Max();
			int GroupMinX=int.MaxValue;
			int GroupMaxX=0;
			foreach(int y in GroupYs) {
				(int MinX, int MaxX)=LineData[y];
				GroupMinX = Mathf.Min(GroupMinX, MinX);
				GroupMaxX = Mathf.Max(GroupMaxX, MaxX);
			}

			//Flip y
			float RectY=TexHeight-GroupMaxY-1;
			Rects.Add(new Rect(GroupMinX, RectY, GroupMaxX-GroupMinX+1, GroupMaxY-GroupMinY+1));
		}

		public void Dispose()
		{
			RenderTexture.active=PrevRT;
			Tex.TDestroy();
			RenderTexture.ReleaseTemporary(RT);
		}
	}
}