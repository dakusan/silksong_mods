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
 * Links can have a list of attributes “<ATTR=AttrName>AttrVal</ATTR>” fields preceeding their text in the LinkID block. These attributes are stored in the link and not displayed. Names cannot contain a “>” sign. Attributes can be set after link processing.
 * Supported attributes (case sensitive) copied to link members, and removed after processing:
 *   - NormalColor, HoverColor, StrikeColor
 *     - Must be parsable by ColorUtility.TryParseHtmlString()
 *   - SquiggleStrike
 *     - true=“1”, “true”, “TRUE”; false=anything else
 * Everytime a member of ClickableLabel changes which will change the rendering of the label, it has to redetermine all the info for the Links, which is a bit expensive.
 *	- When this happens, it has to determine which Link is which. It does this via the LinkID for each Link, which needs to be unique.
 * Link labels nesting is not supported.
*/
public class LinkedLabel : IDisposable
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
			LinkColorHex=(field=value).Hex;
			LiveLinks.ForEach(L => RenderString=L.UpdateColor(RenderString));
		}
	} = Color.cyan;
	public Color HoverColor
	{
		get;
		set => HoverColorHex=(field=value).Hex;
	} = Color.yellow;
	private string LinkColorHex=null!, HoverColorHex=null!;

	public LinkedLabel()
	{
		LinkColorHex=LinkColor.Hex;
		HoverColorHex=HoverColor.Hex;
	}

	//This holds the links by their LinkID
	private readonly Dictionary<string, Link> Links=[];
	private readonly List<Link> LiveLinks=[];
	public Link[] ActiveLinks => [..LiveLinks];

	//Information about a found link. All fields are updated during extraction.
	public class Link
	{
		internal Link(LinkedLabel Parent, string LinkID) =>
			(this.Parent, this.LinkID)=(Parent, LinkID);

		//Used to make sure this link stays up to date
		private readonly LinkedLabel Parent;

		//Updated during extraction
		public readonly string LinkID;
		public string Text { get; internal set; } = null!;
		public bool IsLive { get; internal set; } = false; //If the link is being rendered inside the current label
		internal Rect[]? Boxes=null; //Relative to the upper left corner of the ClickableLabel
		public Dictionary<string, string> Attributes=[];
		public int StringStartPos, StringEndPos;

		//Override link colors
		public Color? NormalColor { get; set { //Overwrite the default LinkColor
			NormalColorHex=(field=value)?.Hex;
			if(IsLive)
				Parent.RenderString=UpdateColor(Parent.RenderString, false);
		} }
		public Color? HoverColor { get; set => HoverColorHex=(field=value)?.Hex; } //Overwrite the default HoverColor
		public Color? StrikeColor; //Add a strikethrough line
		public bool SquiggleStrike=false;
		internal string? NormalColorHex, HoverColorHex;

		//Get the rectangles
		public Rect[] Rects { get {
			if(!IsLive || Boxes!=null)
				return !IsLive ? [] : Boxes!;
			if(Event.current.type!=EventType.Layout)
				Parent.Extract(this);
			return Boxes ?? [];
		} }

		//Update the color in place in the render string
		public string UpdateColor(string Str, bool UseHoverColor=false) =>
			 Str[..StringStartPos]
			+$"<color=#{(UseHoverColor ? (HoverColorHex ?? Parent.HoverColorHex) : NormalColorHex ?? Parent.LinkColorHex)}>{Text}</color>"
			+Str[StringEndPos..];
	}

	//Marks that the links need to be reextracted. If you change something inside your LabelStyle, this may be needed.
	public void Clear(bool NeedsParsing)
	{
		this.NeedsParsing|=NeedsParsing;

		if(NeedsExtracting)
			return;
		GetLinkRectsOnFrame=NeedsExtracting=true;
		foreach(Link L in LiveLinks)
			L.Boxes=null;
	}

	//This keeps track of when parsing or link rects need to be refreshed.
	private bool NeedsParsing=true, NeedsExtracting=true, GetLinkRectsOnFrame=true;

	//Information needed for rendering
	private string RenderString=null!;

	//Renders the label. Full reextraction is only ever ran if the mouse is over the label. Does not return Link on layout phase
	public Link? GUILabel(Rect Rect, string Content, GUIStyle? NewGUIStyle=null, params Link[] SelectedItems)
	{
		//Update fields to make sure we don’t need a reparse or rerender
		RectSize=Rect.size;
		Pos=Rect.position;
		Text=Content;
		Style=NewGUIStyle ?? Style;
		if(NeedsParsing)
			Parse();

		//Check for hovered item
		bool IsLayout=Event.current.type==EventType.Layout;
		Link? HoveredLink=null;
		if(!IsLayout && Rect.Contains(DevInput.Util.MousePos)) {
			if(NeedsExtracting)
				IExtract();
			Vector2 LocalMPos=DevInput.Util.MousePos-Pos;
			HoveredLink=LiveLinks.FirstOrDefault(L => L.Boxes.Any(R => R.Contains(LocalMPos)));
			if(HoveredLink!=null && !SelectedItems.Contains(HoveredLink))
				SelectedItems=[..SelectedItems, HoveredLink];
		}

		//Render the label
		string RenderText=IsLayout ? RenderString : SelectedItems.Aggregate(RenderString, static (Str, L) => Str=L.UpdateColor(Str, true));
		GUI.Label(Rect, RenderText, Style);

		//Get all the rects at once for any links that have a strike in them
		if(!IsLayout && NeedsExtracting && GetLinkRectsOnFrame) {
			GetLinkRectsOnFrame=false;
			IExtract(LiveLinks.Where(static L => L.StrikeColor!=null));
		}

		//Draw strikes
		foreach(Link L in LiveLinks) {
			if(L.StrikeColor==null)
				continue;
			GUI.color=L.StrikeColor.Value;
			int LineHeight=L.SquiggleStrike ? SStrike.Height : 1;
			Texture2D Tex=L.SquiggleStrike ? SStrike.Tex : Texture2D.whiteTexture;
			foreach(Rect R in L.Rects)
				GUI.DrawTextureWithTexCoords(
					R.AddPos(Pos).AddY((R.height-LineHeight)/2).SetHeight(LineHeight),
					Tex, new Rect(0, 0, R.width/Tex.width, 1f)
				);
		}
		GUI.color=Color.white;

		//Return the hovered link
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
	private record class ParseAttrBase(string Name, object? BaseValue)
	{
		public object? Parse(string StrValue) =>
			  StrValue==null ? null
			: BaseValue is bool ? (StrValue is "1" or "true" or "TRUE")
			: BaseValue is not Color ? null
			: ColorUtility.TryParseHtmlString(StrValue, out Color C) ? C
			: null;
		public bool TrySetValue(Link L, string StrValue)
		{
			object? Value=Parse(StrValue);
			switch(Value) {
				case null: return false;
				case Color C: ((ParseAttr<Color>)this).SetValue(L, C); break;
				case bool  B: ((ParseAttr<bool >)this).SetValue(L, B); break;
			}
			return true;
		}
	}
	private record class ParseAttr<T>(string Name, T Value, Action<Link, T> SetValue) : ParseAttrBase(Name, Value!);

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

		//Color attributes that can be replaced
		ParseAttrBase[] ExtractAttrs=[
			new ParseAttr<Color>(nameof(Link.NormalColor	), Color.white	, static (L, C) => L.NormalColor	=C),
			new ParseAttr<Color>(nameof(Link.HoverColor		), Color.white	, static (L, C) => L.HoverColor		=C),
			new ParseAttr<Color>(nameof(Link.StrikeColor	), Color.white	, static (L, C) => L.StrikeColor	=C),
			new ParseAttr<bool >(nameof(Link.SquiggleStrike	), false		, static (L, B) => L.SquiggleStrike	=B),
		];

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
			if(L.IsLive) {
				Log.Error($"Link ID found more than once, excluding: {LinkID}");
				CurIndex=LinkMatch.Index+LinkMatch.Length;
				continue;
			}
			LiveLinks.Add(L);

			//Copy over the new values to the link
			L.Text=OutString;
			L.Attributes.Clear();
			L.Attributes.AddRange(AttrList);
			L.IsLive=true;
			L.Boxes=null;
			L.StringStartPos=Result.Length;
			_=Result.Append($"<color=#{LinkColorHex}>{L.Text}</color>");
			L.StringEndPos=Result.Length;
			CurIndex=LinkMatch.Index+LinkMatch.Length;
		}

		LiveLinks.Sort(static (a, b) => a.StringStartPos.CompareTo(b.StringStartPos));
		RenderString=Result.ToString();

		//Get color attribute overrides
		foreach(Link L in LiveLinks)
			foreach(ParseAttrBase PAB in ExtractAttrs)
				if(
					   L.Attributes.TryGetValue(PAB.Name, out string StrValue)
					&& PAB.TrySetValue(L, StrValue)
				)
					_=L.Attributes.Remove(PAB.Name);
	}

	public void Extract(params IEnumerable<Link> WhichLinks) => IExtract(WhichLinks);
	public void Extract(Action<Link, Texture2D> GetRenderedTexture, params IEnumerable<Link> WhichLinks) => IExtract(WhichLinks, GetRenderedTexture);

	//This extracts the rectangles for the links.
	//If NeedsExtracting and WhichLinks=null (only ran when the label will render differently visually), it will extract all links and set NeedsExtracting=false.
	private void IExtract(IEnumerable<Link>? WhichLinks=null, Action<Link, Texture2D>? GetRenderedTexture=null)
	{
		//Make sure we need to extract
		if(!(
			   (GetRenderedTexture!=null || NeedsExtracting)
			&& WhichLinks?.Any()!=false
		))
			return;
		if(WhichLinks==null) //Only mark as no longer needing extraction if we are processing all the live links
			NeedsExtracting=false;
		DateTime StartTime=DateTime.Now;
		using GetStringRects GSR=new((int)RectSize.x, (int)RectSize.y);

		//Make a full render string with redetermined link placement for quick string rendering
		const string MakeTransparent="<color=#00000000>";
		var CreateStrings=new (Link L, int StartPos, int EndPos)[LiveLinks.Count];
		StringBuilder SB=new(MakeTransparent.Length+RenderString.Length-LiveLinks.Sum(static L => L.StringEndPos-L.StringStartPos-L.Text.Length)); //Preallocate to the final length
		_=SB.Append(MakeTransparent);
		int PrevPos=0;
		foreach((int Index, Link L) in LiveLinks.Entries) {
			_=SB.Append(RenderString[PrevPos..L.StringStartPos]).Append(L.Text);
			CreateStrings[Index]=(L, SB.Length-L.Text.Length, SB.Length);
			PrevPos=L.StringEndPos;
		}
		_=SB.Append(RenderString[PrevPos..]);
		string FinalStr=SB.ToString();

		//Create the separate strings with colored text to measure boxes
		int NumExtracted=0;
		foreach((Link L, int StartPos, int EndPos) in CreateStrings) {
			//Confirm that we need to render it
			if(!(
				   (L.Boxes==null || GetRenderedTexture!=null) //Either we need the uncalculated boxes or the rendered texture
				&& (WhichLinks==null || WhichLinks.Contains(L)) //Only for requested links
			))
				continue;

			//Render it and update the boxes
			L.Boxes=GSR.Exec(FinalStr[..StartPos]+$"<color=black>{L.Text}</color>"+FinalStr[EndPos..], Style);
			NumExtracted++;
			GetRenderedTexture?.Invoke(L, GSR.Tex);
		}

		//Output the time to complete the process
		double RenderTime=(DateTime.Now-StartTime).TotalSeconds;
		Log.Info($"Time to extract {NumExtracted} ClickLabel Link Rects: {RenderTime:F4} seconds [{RenderTime/MathF.Max(NumExtracted, 1):F4}/link]");
	}

	//This class takes a string, renders it, and determines the rects of the visible sections
	private class GetStringRects : IDisposable
	{
		private readonly int Width, Height;
		private readonly Rect DrawRect;
		public Texture2D Tex { get; }
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
					if(Pixels[y*Width+x].rgba>0) {
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

	//Handle squiggly strikes
	public readonly SquiggleStrike SStrike=new();
	public class SquiggleStrike(int Width=19, int Height=4, float MainAlpha=.9f, float AntiAliasAlpha=.4f) : IDisposable
	{
		public int			  Width	{ get; set { field=value; _=MakeSquiggleStrike(); } } = Width			;
		public int			 Height	{ get; set { field=value; _=MakeSquiggleStrike(); } } = Height			;
		public float	  MainAlpha	{ get; set { field=value; _=MakeSquiggleStrike(); } } = MainAlpha		;
		public float AntiAliasAlpha	{ get; set { field=value; _=MakeSquiggleStrike(); } } = AntiAliasAlpha	;
		public Texture2D Tex => _Tex ?? MakeSquiggleStrike();
		private Texture2D? _Tex=null;
		private Texture2D MakeSquiggleStrike()
		{
			//Create the base color as white transparent
			Color[] Colors=new Color[Width*Height];
			for(int i=0; i<Colors.Length; i++)
				Colors[i]=new Color(1, 1, 1, 0);

			//Create the sine wave
			void SetPixel(int x, int y, float Alpha) => Colors[y*Width+x].a=Alpha;
			float MidY=Height/2f;
			float SinCalc=2f*Mathf.PI/Width;
			for(int x=0; x<Width; x++) {
				int y=Mathf.Clamp(Mathf.RoundToInt(MidY*(1+Mathf.Sin(SinCalc*x))), 0, Height-1);
				 				SetPixel(x, y  , MainAlpha		);
				if(y-1>=0)		SetPixel(x, y-1, AntiAliasAlpha	);
				if(y+1<Height)	SetPixel(x, y+1, AntiAliasAlpha	);
			}

			//Create the texture
			Dispose();
			_Tex=new Texture2D(Width, Height, TextureFormat.ARGB32, false) {
				wrapMode=TextureWrapMode.Repeat,
				filterMode=FilterMode.Point,
			};
			_Tex.SetPixels(Colors);
			_Tex.Apply();
			return _Tex;
		}
		public void Dispose() => _Tex?.TDestroy();
	}
	public void Dispose() => SStrike.Dispose();
}