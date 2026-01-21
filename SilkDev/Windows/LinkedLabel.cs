using SilkDev.Textures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using StringBuilder = System.Text.StringBuilder;

#if DEBUG
	using SafeTexture2D = SilkDev.Textures.SafeTexture2D;
#else
	using SafeTexture2D = UnityEngine.Texture2D;
#endif
using RTexture2D = UnityEngine.Texture2D;

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
	public GUIStyle Style		{ get; set { if(field==value) return; Clear(false); field=value; } } = null!;
	public   string Text		{ get; set { if(field==value) return; Clear(true ); field=value; } } = null!;
	public  Vector2 RectSize	{ get; set { if(field==value) return; Clear(false); field=value; } } = Vector2.zero;

	//These do not affect live styles so do not require reextraction when changed
	public Vector2 Pos { get; internal set; }
	public Vector2 ScrollPosOffset; //If inside a GUILayout.BeginScrollView then give its ScrollPosition here
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
		public bool SquiggleStrike=false; //Make the strikethrough line a sqiuggle
		internal string? NormalColorHex, HoverColorHex;

		//Get the rectangles
		public Rect[] Rects { get {
			if(!IsLive || Boxes!=null)
				return !IsLive ? [] : Boxes!;
			if(Event.current.type==EventType.Repaint)
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

		if(NeedsExtracting && GetRequiredRectsOnDraw)
			return;
		GetRequiredRectsOnDraw=NeedsExtracting=true;
		foreach(Link L in LiveLinks)
			L.Boxes=null;
	}

	//This keeps track of when parsing or link rects need to be refreshed.
	private bool NeedsParsing=true, NeedsExtracting=true, GetRequiredRectsOnDraw=true;

	//Information needed for rendering
	private string RenderString=null!;

	//Renders the label. Full reextraction is only ever ran if the mouse is over the label. Does not return Link on layout phase
	public Link? GUILabel(Rect Rect, string Content, GUIStyle? NewGUIStyle=null, params Link[] SelectedItems)
	{
		//Do not perform rendering operations on layout stage
		if(Event.current.type==EventType.Layout) {
			GUI.Label(Rect, RenderString, Style);
			return null;
		}

		//Update fields to make sure we don’t need a reparse or rerender
		if(Event.current.type==EventType.Repaint) {
			RectSize=Rect.size;
			Pos=Rect.position;
		}
		Text=Content;
		Style=NewGUIStyle ?? Style;
		if(NeedsParsing)
			Parse();

		//Check for hovered item
		Link? HoveredLink=null;
		Vector2 RelativeMousePos=Event.current.mousePosition;
		if(Rect.Contains(RelativeMousePos)) {
			if(NeedsExtracting)
				IExtract();
			Vector2 LocalMPos=RelativeMousePos-Pos;
			HoveredLink=LiveLinks.FirstOrDefault(L => L.Boxes!=null && L.Boxes.Any(R => R.Contains(LocalMPos)));
			if(HoveredLink!=null && !SelectedItems.Contains(HoveredLink))
				SelectedItems=[..SelectedItems, HoveredLink];
		}

		//Render the label
		GUI.Label(Rect, SelectedItems.Aggregate(RenderString, static (Str, L) => Str=L.UpdateColor(Str, true)), Style);

		//Get all the rects at once for any links that have a strike in them
		if(NeedsExtracting && GetRequiredRectsOnDraw) {
			GetRequiredRectsOnDraw=false;
			IExtract(RequiredRects([]).Union(LiveLinks.Where(static L => L.StrikeColor!=null)));
		}

		//Draw strikes
		foreach(Link L in LiveLinks) {
			if(L.StrikeColor==null)
				continue;
			GUI.color=L.StrikeColor.Value;
			int LineHeight=L.SquiggleStrike ? SStrike.Height : 1;
			RTexture2D Tex=L.SquiggleStrike ? SStrike.Tex : RTexture2D.whiteTexture;
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
		return GUILabel(GUILayoutUtility.GetLastRect(), Content, NewGUIStyle, ExtraSelectedItems ?? []);
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

	private static readonly Regex LinkRegEx		=new(@"<LinkID\s*=([^>]+)>(.*?)</LinkID>",				RegexOptions.IgnoreCase|RegexOptions.Compiled);
	private static readonly Regex AttrRegEx		=new(@"^(<ATTR\s*=\s*([^>]+)\s*>\s*(.*?)\s*</ATTR>)*",	RegexOptions.IgnoreCase|RegexOptions.Compiled);
	private static readonly Regex FixColorRegEx	=new(@"<(/color|color\s*=[^>]*)>",						RegexOptions.IgnoreCase|RegexOptions.Compiled);
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

		ParseComplete();
	}

	public void Extract(params IEnumerable<Link> WhichLinks) => IExtract(WhichLinks);

	//This extracts the rectangles for the links.
	private void IExtract(IEnumerable<Link>? WhichLinks=null)
	{
		//Find links which need to be extracted
		if(!NeedsExtracting || Event.current.type!=EventType.Repaint)
			return;
		Link[] FinalLinkList=[..(WhichLinks ?? LiveLinks).Where(static L => L.Boxes==null)];
		if(FinalLinkList.Length==0)
			return;

		DateTime StartTime=DateTime.Now;

		//Make a full render string with redetermined link placement for quick string rendering
		const string MakeTransparent="xcolor=transparent>"; //Faking one of the color characters so the color regex doesn’t pick it up
		List<(Link L, int StartPos, int EndPos)> CreateStrings=new(LiveLinks.Count);
		StringBuilder SB=new(MakeTransparent.Length+RenderString.Length-LiveLinks.Sum(static L => L.StringEndPos-L.StringStartPos-L.Text.Length)); //Preallocate to the final length
		int PrevPos=0;
		_=SB.Append(MakeTransparent);
		foreach((int Index, Link L) in LiveLinks.Entries) {
			_=SB.Append(RenderString, PrevPos, L.StringStartPos-PrevPos).Append(L.Text);
			if(FinalLinkList.Contains(L))
				CreateStrings.Add((L, SB.Length-L.Text.Length, SB.Length));
			PrevPos=L.StringEndPos;
		}
		_=SB.Append(RenderString, PrevPos, RenderString.Length-PrevPos);

		//Replace “<color=...>” and “</color>” tags with “<b=   ></b>” [keep the length the same as the tag] so they don’t interfere with the box measuring process
		const string ColorReplPrefix="<b=", ColorReplSuffix="></b>";
		string FinalStr=FixColorRegEx.Replace(
			SB.ToString(),
			static M => ColorReplPrefix+new string(' ', M.Length-ColorReplPrefix.Length-ColorReplSuffix.Length)+ColorReplSuffix
		);
		FinalStr='<'+FinalStr[1..]; //Replace the MakeTransparent first character

		//Create the separate strings as textures with just their text visible
		using GetStringTextures GST=new((int)RectSize.x, (int)RectSize.y, ScrollPosOffset);
		List<GetStringRects> GSRs=new(CreateStrings.Count);
		foreach((Link L, int StartPos, int EndPos) in CreateStrings)
			GSRs.Add(new GetStringRects(
				this, L, (int)RectSize.x, (int)RectSize.y,
				GST.GetStringAsTexture(FinalStr[..StartPos]+$"<color=black>{L.Text}</color>"+FinalStr[EndPos..], Style)
			));
		GST.Dispose(); //Disposing early so GUI.Matrix is reset

		//Get the boxes from the textures
		foreach(GetStringRects GSR in GSRs)
			GSR.StartProcess();
		GetStringRects.Finish(this);

		//If all links now have rects, no longer need to Extract again
		NeedsExtracting=LiveLinks.Any(static L => L.Boxes==null);

		//Output the time to complete the process
		double RenderTime=(DateTime.Now-StartTime).TotalSeconds;
		Log.Debug($"Time to extract {CreateStrings.Count} ClickLabel Link Rects: {RenderTime:F4} seconds [{RenderTime/CreateStrings.Count:F4}/link]");
	}

	//This class takes a string and renders it to a texture
	private class GetStringTextures : IDisposable
	{
		//Instance members
		private readonly float X, Y;
		private readonly int Width, Height;
		private readonly RenderTexture PrevRT=RenderTexture.active;
		private Matrix4x4 PrevMatrix=GUI.matrix;
		private bool IsDisposed=false;

		public GetStringTextures(int Width, int Height, Vector2 ScrollPosOffset)
		{
			(this.Width, this.Height, X, Y)=(Width, Height, ScrollPosOffset.x, ScrollPosOffset.y);
			GUI.matrix=
				//GUI expects to render to a texture of Screen Width*Height, so we need to scale to the render texture size
				Matrix4x4.Scale(new Vector3((float)Screen.width/Width, (float)Screen.height/Height, 1f))*
				//Offset render back to upper left corner as origin when GUI Clipped
				Matrix4x4.Translate(GetClipPosition*-1);
		}

		public static Vector2 GetClipPosition => new Reflectors.RProp<GUIClip, Rect>(null, "topmostRect").Get().position;

		//The StrText needs to ONLY show the text of the rectangle we are measuring. The alpha channel is used to determine the rectangles
		public RenderTexture GetStringAsTexture(string RenderStr, GUIStyle Style)
		{
			RenderTexture Tex=RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			RenderTexture.active=Tex;
			GL.Clear(true, true, Color.clear);
			GUI.Label(new(X, Y, Width, Height), RenderStr, Style);
			return Tex;
		}

		//IMGUI can leave its internal GUIClip transform/cache desynced after temporarily changing GUI.matrix (especially when rendering to a RenderTexture).
		//Force a GUIClip reapply to restore correct coordinates. If the internal API isn’t available, poke the clip stack as a best-effort fallback.
		[System.Diagnostics.Conditional("FORCE_GUI_CLIP_REAPPLY")]
		private static void RefreshGuiClip()
		{
			if(GUIClipReapply!=null)
				try {
					GUIClipReapply();
					return;
				} catch { }
			GUI.BeginClip(new Rect(0, 0, 1, 1));
			GUI.EndClip();
		}
		private static Action? TryGetGuiClipReapply() {
			try {
				return (Action?)
					typeof(GUI).Assembly.GetType("UnityEngine.GUIClip")
					?.GetMethod("Reapply", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
					?.CreateDelegate(typeof(Action));
			} catch { return null; }
		}
		private static readonly Action? GUIClipReapply=TryGetGuiClipReapply();

		public void Dispose()
		{
			if(IsDisposed)
				return;
			IsDisposed=true;
			RenderTexture.active=PrevRT; //Restore RT before GUI.matrix; reversing this can leave GUIClip with a stale transform.
			GUI.matrix=PrevMatrix;
			RefreshGuiClip();
		}
	}

	//This class determines the rects of the visible sections of the given texture (from GetStringTextures)
	private class GetStringRects
	{
		//Static shader members
		private const string BundleFile="SilkDev.bundle", ShaderFile="RowBounds.compute";
		private static readonly ComputeShader RowBoundsCS;
		private static readonly int KInitRows, KScanRows;
		static GetStringRects()
		{
			try {
				using TypedDisposer<AssetBundle> Bundle=new(
					AssetBundle.LoadFromStream(FileOps.LoadLocalFileOrResource(BundleFile)),
					static Target => Target.Unload(false)
				);
				RowBoundsCS=Bundle.Target.LoadAsset<ComputeShader>(ShaderFile);
				KInitRows=RowBoundsCS.FindKernel("InitRows");
				KScanRows=RowBoundsCS.FindKernel("ScanRows");
			} catch(Exception e) {
				UseShader=false;
				RowBoundsCS=null!;
				Log.Error($"Could not load shader: {e.Message}");
				return;
			}
		}
		public static bool HasShader => RowBoundsCS!=null;

		//Normal members
		private readonly LinkedLabel Parent;
		private readonly Link L;
		private readonly RenderTexture Tex;
		private readonly int Width, Height;

		//Members when using shader method
		public static bool UseShader=true; //***DO NOT SET DURING DRAW PHASE***
		private readonly ComputeBuffer RowMinBuf=null!, RowMaxBuf=null!;

		//Members when using non-shader method
		public static bool										UseParallel=false;
		private readonly SafeTexture2D							TempTex=null!;
		private readonly Unity.Collections.NativeArray<Color32>	PixelsNative;

		//Executes a process over the texture to get the per-y-line x bounds
		public GetStringRects(LinkedLabel Parent, Link L, int Width, int Height, RenderTexture Tex)
		{
			(this.Parent, this.L, this.Tex, this.Width, this.Height)=(Parent, L, Tex, Width, Height);

			//Non shader method
			if(!UseShader) {
				TempTex=SafeTexture2D.New(Width, Height);
				RenderTexture PrevRT=RenderTexture.active;
				RenderTexture.active=Tex;
				TempTex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
				RenderTexture.active=PrevRT;
				PixelsNative=TempTex.GetPixelData<Color32>(0);
				return;
			}

			//Shader method
			RowMinBuf=new(Height, sizeof(int), ComputeBufferType.Structured);
			RowMaxBuf=new(Height, sizeof(int), ComputeBufferType.Structured);
			RowBoundsCS.SetBuffer(KInitRows, "_RowMinX", RowMinBuf);
			RowBoundsCS.SetBuffer(KInitRows, "_RowMaxX", RowMaxBuf);
			RowBoundsCS.SetBuffer(KScanRows, "_RowMinX", RowMinBuf);
			RowBoundsCS.SetBuffer(KScanRows, "_RowMaxX", RowMaxBuf);

			RowBoundsCS.SetInt("_Width", Width);
			RowBoundsCS.SetInt("_Height", Height);
			RowBoundsCS.SetTexture(KScanRows, "_Tex", Tex);

			RowBoundsCS.Dispatch(KInitRows, (Height+255)/256, 1, 1);
			RowBoundsCS.Dispatch(KScanRows, (Width +255)/256, Height, 1);
		}

		//Start the process (And finish for all but GetRects_Parallel())
		public void StartProcess()
		{
			if(UseShader)
				GetRects_Shader();
			else if(UseParallel)
				GetRects_Parallel();
			else
				GetRects_Serial();
		}

		//Executes a shader over the texture to get the per-y-line x bounds
		private void GetRects_Shader()
		{
			//Pull in the data from the shader
			int[] RowMinCpu=new int[Height];
			int[] RowMaxCpu=new int[Height];
			RowMinBuf.GetData(RowMinCpu);
			RowMaxBuf.GetData(RowMaxCpu);
			RowMinBuf.Dispose();
			RowMaxBuf.Dispose();

			//Organize the data from the shader
			Dictionary<int, (int minX, int maxX)> LineData=[];
			int Max;
			for(int y=0; y<Height; y++)
				if((Max=RowMaxCpu[y])>=0)
					LineData[y]=(RowMinCpu[y], Max);

			L.Boxes=CreateRects(LineData);
			Parent.RectsGenerated(L, Tex);
		}

		private void GetRects_Serial()
		{
			L.Boxes=CreateRects(ProcessPixelRows());
			TempTex.TDestroy();
			Parent.RectsGenerated(L, Tex);
		}

		//Get per-y-line x bounds of the texture via cpu
		private Dictionary<int, (int minX, int maxX)> ProcessPixelRows()
		{
			Dictionary<int, (int minX, int maxX)> LineData=[];
			for(int y=0; y<Height; y++) {
				int MinX=Width, MaxX=0;
				bool HasPixels=false;
				for(int x=0; x<Width; x++)
					if(PixelsNative[y*Width+x].rgba>0) {
						HasPixels=true;
						MinX=Mathf.Min(MinX, x);
						MaxX=Mathf.Max(MaxX, x);
					}
				if(HasPixels)
					LineData[y]=(MinX, MaxX);
			}
			return LineData;
		}

		//Group pixel bounds into lines
		private Rect[] CreateRects(Dictionary<int, (int minX, int maxX)> LineData)
		{
			if(LineData.Count<1)
				return [];

			//Group lines by consecutive y with small gaps
			List<Rect> Rects=[];
			List<int> SortedYs=[..LineData.Keys];
			SortedYs.Sort();
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

		//-----------------Parallel--------------------
		//Start a BackgroundJobRunner to process texture pixel bounds in the background
		private static readonly Dictionary<LinkedLabel, Events.BackgroundJobRunner<GetStringRects, object>> LLRunner=[];
		private void GetRects_Parallel()
		{
			//Create the BackgroundJobRunner
			if(!LLRunner.TryGetValue(Parent, out var Runner))
				(Runner=LLRunner[Parent]=new(
					(GSR, _) => GSR.L.Boxes=GSR.CreateRects(GSR.ProcessPixelRows()),
					(GSR, _, _, Ex) => {
						GSR.TempTex.TDestroy();
						GSR.Parent.RectsGenerated(GSR.L, GSR.Tex);
					}
				)).Init();

			//Add to the BackgroundJobRunner and process results if available
			Runner.Add(this);
			Runner.ProcessResults();
		}

		//Finish the processes (Only used fort GetRects_Parallel())
		public static void Finish(LinkedLabel LL)
		{
			if(UseShader || !UseParallel || !LLRunner.TryGetValue(LL, out var Runner))
				return;
			_=LLRunner.Remove(LL);
			Runner.Finish();
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
		public  RTexture2D Tex => _Tex?.Tex ?? MakeSquiggleStrike();
		private SafeTexture2D? _Tex=null;
		private RTexture2D MakeSquiggleStrike()
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
			_Tex=SafeTexture2D.New(Width, Height);
			_Tex.wrapMode=TextureWrapMode.Repeat;
			_Tex.filterMode=FilterMode.Point;
			_Tex.SetPixels(Colors);
			_Tex.Apply();
			return _Tex;
		}
		public void Dispose() => _Tex?.TDestroy();
	}

	//----------Overridable functions----------
	public virtual void Dispose() => SStrike.Dispose();

	//Returns Links whose Rects need to be extracted for drawing. Overrides should pass through the links they receive when calling their base.
	//Called after NeedsExtracting is set to true, during the first GUILabel (if NeedsExtracting is still true).
	protected virtual IEnumerable<Link> RequiredRects(IEnumerable<Link> Links) { return Links; }

	//Called when a Rect is generated for a Link. Includes the temperary Texture used to determine the Rect.
	protected virtual void RectsGenerated(Link L, RenderTexture Tex) { RenderTexture.ReleaseTemporary(Tex); }

	//Called after parsing has complete
	protected virtual void ParseComplete() { }
}