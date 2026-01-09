using SilkDev.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button=SilkDev.DevInput.Mouse.Button;

#if DEBUG
	//using SafeTexture2D = SilkDev.Textures.SafeTexture2D;
#else
	using SafeTexture2D = UnityEngine.Texture2D;
#endif
using RTexture2D = UnityEngine.Texture2D;

namespace SilkDev.Textures;

//Opens the “Extract Sprites” window on keyboard shortcut, which allows you to browse and save the sprites/textures that were under your mouse.
public class ExtractSpritesWindow : Window
{
	//Only allow 1 instance and 1 initialization
	private static ExtractSpritesWindow? CurWin=null;
	private static bool AlreadyInitialized=false;
	internal static void Init()
	{
		//Only allow initialization once
		if(AlreadyInitialized)
			return;
		AlreadyInitialized=true;

		//Handle shortcut key to open window
		Events.GameEvents.OnUpdate += static () => Misc.IFF(
			Conf.Key_ExtractSprites.IsDown(),
			static () => OnNextFrame(
				CurWin != null
					? static () => CurWin.RunUpdate()
					: static () => CurWin=new ExtractSpritesWindow()
			)
		);

		//Handle setting changed of whether to show LiveRectangles
		Conf.ESWindow_ShowMouseOver.SettingChanged += static (_, _) => {
			if(CurWin==null)
				return;
			if(Conf.ESWindow_ShowMouseOver)
				CurWin.LR ??= new(CurWin);
			else {
				CurWin.LR?.Close();
				CurWin.LR=null;
			}
		};

		//Close window on scene change
		SceneManager.sceneLoaded += static (_, _) => CurWin?.Close();
	}

	//Helper classes
	private static readonly Translations Tr=Internal.Config.C.Tr;
	private record class LabelInfo(string Label, GUIStyle Style, float Width);
	private static Internal.Config Conf => Internal.Config.C;

	//Constants
	private const string EllipsesStr="...", WindowTitle="Extract Sprites: ";
	private readonly GUIStyle EllipsesStyle, LabelStyleBold, LabelStyle=new(GUI.skin.label) { fontSize=14, wordWrap=false, richText=false, margin=new RectOffset(0, 0, 0, 0), padding=new RectOffset(0, 0, 0, 0) };
	private readonly Color SelectCol=new(1, 1, 0, 0.5f), TooltipBorderCol=Color.black;
	private readonly float EllipsesWidth;

	//Members
	private readonly List<SpriteObject> SOList=[];
	private readonly DrawGeometry.Rectangle ShowSelection=new(0, 0, 0, 0, new Color(0, 1, 0, 0.35f)) { Visible=false, Priority=-5 };
	private Vector2 ScrollPosition=Vector2.zeroVector;
	public int MinListWidth {
		get;
		set => Resizer!.MinSize=new Vector2((field=value)+50, value+50);
	} = 200;

	//Set the currently selected object
	public class CurrentObj
	{
		//Members
		public enum Type { None, Failed, CroppedTexture, Rendered, FullTexture };
		public readonly SpriteObject SO;
		public readonly SafeTexture2D Tex;
		public readonly Type TexType;
		public readonly DateTime CreationTime=DateTime.Now;
		public bool HighlightSpriteOnSheet=true;

	#if DEBUG
		private static SafeTexture2D GetSafeTex(RTexture2D NewTex, bool IsDisposable) => new(NewTex, !IsDisposable, 0);
	#else
		private static RTexture2D GetSafeTex(RTexture2D NewTex, bool _) => NewTex;
	#endif

		//Initialization
		public CurrentObj(SpriteObject SO, Type TexType=Type.CroppedTexture)
		{
			(this.SO, this.TexType)=(SO, TexType);
			try {
				Tex=GetSafeTex(
					TexType switch {
						Type.CroppedTexture	=> SO.Texture.ToReadable(TexCoords: SO.TextureRect),
						Type.Rendered		=> SO.CaptureToTexture(),
						Type.FullTexture	=> SO.Texture,
						_					=> throw new ArgumentException("TexType is not valid"),
					},
					IsDisposable
				);
			} catch(Exception e) {
				if(Tex!=null && IsDisposable) //This should not be possible
					Tex.TDestroy();
				(this.TexType, Tex)=(Type.Failed, Color.red.MakeTexture()); //Add a red color texture, just in case
				_=new PopupMessage($"{Tr.T("Sprite load failed", "Errors", true)}:\n<size=25>{Misc.SanitizeRichString(e.Message)}</size>");
			}
		}

		//Pass through functions
		[SuppressMessage("Style", "IDE1006:Naming Styles", Justification="Wrapped field")] public int width  => Tex.width;
		[SuppressMessage("Style", "IDE1006:Naming Styles", Justification="Wrapped field")] public int height => Tex.height;
		public static implicit operator RTexture2D?(CurrentObj? CO)	=> CO?.Tex?.Tex;
		public Rect		ScreenPos									=> SO.ScreenPos;
		public string	Name										=> TexType!=Type.FullTexture ? SO.Name : Tex.name ?? "NO NAME";

		//Helper functions
		public bool HasTexture		=> TexType is not (Type.None or Type.Failed);
		public bool IsDisposable	=> TexType is not (Type.None or Type.FullTexture);
		[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "When going back to release mode it now gives this message! lol")]
		[SuppressMessage("Style", "IDE0200:Remove unnecessary lambda expression", Justification = "In release mode TDestroy() takes a parameter")]
		public void Destroy()		=> Misc.IFF(IsDisposable, () => Tex.TDestroy());

		//Encode to PNG
		public byte[] EncodeToPNG()
		{
			if(!HasTexture)
				return [];
			else if(TexType==Type.CroppedTexture && Tex.isReadable)
				return Tex.EncodeToPNG();

			using var CopiedTex=Tex.ToReadable().Disposable;
			return CopiedTex.Target.EncodeToPNG();
		}
	}
	public CurrentObj? CurFoundObj {
		get;
		set {
			field?.Destroy(); //Clear out the current data
			field=value; //Do not check for setting the same thing again as GetTexFromSprite may be different
			ShowSelection.Visible=(value!=null); //Update the visibility of the ShowSelection window
		}
	} = null;

	//Initialization
	private ExtractSpritesWindow() : base(WindowTitle, Conf.Rect_ExtractSprites)
	{
		DevInput.Mouse.Visibility.ForceEvent += ForceCursor; //Keep the mouse visible
		LabelStyleBold=new GUIStyle(LabelStyle) { fontStyle=FontStyle.Bold };
		EllipsesStyle=new GUIStyle(LabelStyle) { normal={ textColor=Color.red } };
		EllipsesWidth=EllipsesStyle.CalcSize(new GUIContent(EllipsesStr)).x;
		Resizer!.MinSize=new Vector2(MinListWidth+50, MinListWidth+50);
		Visible=true;

		if(Conf.ESWindow_ShowMouseOver)
			LR=new(this);
		RunUpdate();
	}

	//Watch for keypress to change selected item
	private readonly DevInput.InputRepeatDelay<int> ScrollKeys=new(0.075f,
		(KeyCode.UpArrow  , -1),
		(KeyCode.DownArrow,  1)
	);
	protected override void OnUpdate()
	{
		if(CurFoundObj==null)
			return;

		if(ScrollKeys.IsReadyValueVType is int SKDir) //Update the selected item
			CurFoundObj=new CurrentObj(SOList[Mathf.Clamp(SOList.IndexOf(CurFoundObj.SO)+SKDir, 0, SOList.Count-1)]);
		ShowSelection.Rect=CurFoundObj.ScreenPos; //Update the position of the selection window
	}

	private void RunUpdate(CurrentObj.Type TexType=CurrentObj.Type.CroppedTexture)
	{
		//Get the new list
		Vector2 MP=DevInput.Util.MousePos;
		CurFoundObj=null;
		SOList.Clear();
		SOList.AddRange(GetSpritesUnderCursor().OrderBy(
			SO => (SO.ScreenPos.center-MP).magnitude
		));

		//Select the first item if available
		if(SOList.Count>0)
			CurFoundObj=new CurrentObj(SOList[0], TexType);
	}

	//Find all the SpriteObjects under the cursor
	private record class FindData(List<SpriteObject> ObjList, Camera Camera, Vector2 MousePos);
	public static List<SpriteObject> GetSpritesUnderCursor()
	{
		//Recursively check through all the root game objects for the scene
		List<SpriteObject> ObjList=[];
		FindData FD=new(ObjList, Camera.allCameras[0], DevInput.Util.MousePos);
		foreach(GameObject GO in SceneManager.GetActiveScene().GetRootGameObjects())
			FindSpritesRecurse(FD, GO.transform, Misc.Empty);

		//Don’t forget our hero!
		GameObject? HeroGO=HeroController.instance?.gameObject;
		if(HeroGO!=null) {
			SpriteObject HeroSO=new SpriteObject_tk2dBaseSprite(HeroGO.name, Misc.Empty, HeroGO.GetComponent<tk2dBaseSprite>());
			if(HeroSO!=null && WorldBoundsToScreenRect(FD.Camera, HeroSO.Bounds).Contains(FD.MousePos))
				FD.ObjList.Add(HeroSO);
		}

		return ObjList;
	}

	//Go through visibility trees and find visible sprites
	private static void FindSpritesRecurse(FindData FD, Transform CurObject, string ParentTree)
	{
		//If not visible then stop here
		if(!CurObject.gameObject.activeSelf)
			return;

		//If a SpriteRenderer then add it if its projected Rect contains the mouse cursor
		SpriteObject? CurSO=CurObject switch {
			Transform G when G.TryGetComponent<SpriteRenderer>(out var SR )
				=> new SpriteObject_SpriteRenderer	(CurObject.gameObject.name, ParentTree, SR ),
			Transform G when G.TryGetComponent<tk2dBaseSprite>(out var TKS)
				=> new SpriteObject_tk2dBaseSprite	(CurObject.gameObject.name, ParentTree, TKS),
			Transform G when G.TryGetComponent<MeshFilter	 >(out var MF )
				=> new SpriteObject_MeshFilter		(CurObject.gameObject.name, ParentTree, MF ),
			_ => null
		};
		if(CurSO!=null && CurSO.IsSafe && WorldBoundsToScreenRect(FD.Camera, CurSO.Bounds).Contains(FD.MousePos))
			FD.ObjList.Add(CurSO);

		//Check all children
		foreach(Transform Child in CurObject)
			FindSpritesRecurse(FD, Child, $"{ParentTree}{CurObject.gameObject.name}/");
	}

	//Convert bounds to a rectangle on the screen
	public static Rect WorldBoundsToScreenRect(Bounds B) => WorldBoundsToScreenRect(Camera.allCameras[0], B);
	private static Rect WorldBoundsToScreenRect(Camera Camera, Bounds B)
	{
		Rect MyRect=default;
		MyRect.min=Camera.WorldToScreenPoint(B.min);
		MyRect.max=Camera.WorldToScreenPoint(B.max);
		MyRect.y=Screen.height-MyRect.y-MyRect.height;
		return MyRect;
	}

	protected override void DoLayout(int ID, Event Ev)
	{
		//Precalculations
		RectOffset WinPad=GUI.skin.window.padding;
		bool HasImage=(CurFoundObj?.HasTexture==true);
		(int TIWidth, int TIHeight)=(HasImage ? (CurFoundObj!.width, CurFoundObj!.height) : (0, 0));
		float AvailableHeight=WindowRect.height-WinPad.vertical-LabelStyle.lineHeight;
		float Aspect=(!HasImage ? 0 : TIWidth/(float)TIHeight);
		float ImageDisplayHeight=!HasImage ? 0 : Mathf.Min(AvailableHeight, TIHeight);
		float ImageDisplayWidth=ImageDisplayHeight*Aspect;
		float LeftWidth=WindowRect.width-ImageDisplayWidth-WinPad.horizontal;
		if(LeftWidth<MinListWidth) {
			LeftWidth=MinListWidth;
			ImageDisplayWidth=WindowRect.width-WinPad.horizontal-LeftWidth;
			ImageDisplayHeight=ImageDisplayWidth/Aspect;
		}
		float LabelMaxWidth=LeftWidth-GUI.skin.verticalScrollbar.fixedWidth-1;

		//Title the window with our sprite and display sizes
		Title=Tr.T(WindowTitle)+(
			  !HasImage ? Tr.T("No sprite selected")
			: $"{TIWidth}*{TIHeight} -> {ImageDisplayWidth}*{ImageDisplayHeight}"
		);

		//Begin the window layout
		GUILayout.BeginHorizontal();

		//Left: ScrollView with labels
		GUILayout.BeginVertical(GUILayout.Width(LeftWidth));
		ScrollPosition=GUILayout.BeginScrollView(ScrollPosition, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView);
		(string? Parent, string? Name) TooltipString=(null, null);
		foreach(SpriteObject SO in SOList) {
			//Get the label parts to draw
			float ParentNameWidth=LabelStyle.CalcSize(new GUIContent(SO.ParentTree)).x;
			float NameWidth=LabelStyleBold.CalcSize(new GUIContent(SO.Name)).x;
			LabelInfo[] LI=
				ParentNameWidth+NameWidth<=LabelMaxWidth ?
					[
						new LabelInfo(SO.ParentTree, LabelStyle, ParentNameWidth),
						new LabelInfo(SO.Name, LabelStyleBold, NameWidth),
					]
				:
					[
						new LabelInfo(SO.ParentTree, LabelStyle, LabelMaxWidth-NameWidth-EllipsesWidth),
						new LabelInfo(EllipsesStr, EllipsesStyle, EllipsesWidth),
						new LabelInfo(SO.Name, LabelStyleBold, NameWidth),
					];

			//Draw the label parts
			GUILayout.BeginHorizontal(GUILayout.Width(LabelMaxWidth));
			foreach(LabelInfo L in LI)
				GUILayout.Label(L.Label, L.Style, GUILayout.Width(L.Width));
			GUILayout.EndHorizontal();

			//Check to see if tooltip is needed
			Rect LabelRect=GUILayoutUtility.GetLastRect();
			bool IsMouseOver=LabelRect.Contains(Event.current.mousePosition);
			if(IsMouseOver && LI.Length==3)
				TooltipString=(SO.ParentTree, SO.Name);

			//Highlight the label if selected
			if(CurFoundObj?.SO==SO)
				SelectCol.DrawRect(LabelRect);

			//Check to see if clicked
			if(!IsMouseOver || Event.current.type!=EventType.MouseDown)
				continue;

			//On double left click open the unity inspector
			if(
				   CurFoundObj?.SO==SO
				&& CurFoundObj.TexType==CurrentObj.Type.CroppedTexture
				&& Button.CurrentButton==Button.Enum.Left
				&& (DateTime.Now-CurFoundObj.CreationTime).TotalSeconds<0.25f
			) {
				Misc.UnityExplorer_Inspect(SO.GO);
				continue;
			}

			//Update the texture
			CurrentObj.Type NewType=MouseClickToTexType;
			OnNextFrame(() => CurFoundObj=new CurrentObj(SO, NewType));
		}
		GUILayout.EndScrollView();
		GUILayout.EndVertical();

		//Right: Box with image
		if(HasImage) {
			//Center the texture vertically
			GUILayout.BeginVertical(GUILayout.Width(ImageDisplayWidth), GUILayout.Height(AvailableHeight));
			GUILayout.Space(Mathf.Max((AvailableHeight-ImageDisplayHeight)/2, 0));
			Rect TextureRect=GUILayoutUtility.GetRect(ImageDisplayWidth, ImageDisplayHeight);
			GUI.DrawTexture(TextureRect, CurFoundObj!.Tex);
			GUILayout.EndVertical();

			//Highlight the sprite on the sprite sheet
			if(CurFoundObj.HighlightSpriteOnSheet && CurFoundObj.TexType==CurrentObj.Type.FullTexture)
				SelectCol.DrawRect(
					CurFoundObj.SO.TextureRect
						.SetY(Y => CurFoundObj.height-Y-CurFoundObj.SO.TextureRect.height) //Adjust for flipped y axis
						.Mul(ImageDisplayWidth/CurFoundObj.width) //Adjust to resized texture size
						.AddPos(TextureRect.position) //Offset to drawn texture
				);

			//Interact with the image
			bool IsMouseInteract=(Event.current.type==EventType.MouseDown && TextureRect.Contains(Event.current.mousePosition));
			if(IsMouseInteract && Button.CurrentButton==Button.Enum.Left) //Save the texture
				try {
					string DirName=FileOps.PathCombine(FileOps.GetPluginPath, ExtractAllTextures.TextureDirectory);
					if(!FileOps.DirectoryExists(DirName))
						_=FileOps.CreateDirectory(DirName);
					string FileName=$"{DateTime.Now:yyyy-MM-dd_HH_mm_ss} {FileOps.FixFileName(CurFoundObj.Name ?? "NO NAME")}.png";
					FileOps.WriteFile(FileOps.PathCombine(DirName, FileName), CurFoundObj.EncodeToPNG());
					_=new PopupMessage($"{Tr.T("File saved to", RichSanitize:true)}:\n<size=25>{Misc.SanitizeRichString(FileOps.PathCombine(DirName, " "))}\n<b>{Misc.SanitizeRichString(FileName)}</b></size>");
				} catch(Exception e) {
					_=new PopupMessage($"{Tr.T("Error saving file", "Errors", true)}:\n<size=25>{Misc.SanitizeRichString(e.Message)}</size>");
				}
			else if(IsMouseInteract && Button.CurrentButton==Button.Enum.Right) //Highlight the sprite on the sprite sheet
				CurFoundObj.HighlightSpriteOnSheet=!CurFoundObj.HighlightSpriteOnSheet;
		}

		//End the window layout
		GUILayout.EndHorizontal();

		//Draw the tooltip label
		if(TooltipString.Parent!=null) {
			const int LabelPaddingX=5, LabelPaddingY=2, TooltipXOffset=9, TooltipYOffset=3;
			int ParentWidth=(int)LabelStyle.CalcSize(new GUIContent(TooltipString.Parent)).x;
			GUI.EndClip();
			Rect BoxRect=new Rect(
				Event.current.mousePosition,
				LabelStyleBold.CalcSize(new GUIContent(TooltipString.Name))
			).AddX(LabelPaddingX+TooltipXOffset).AddY(LabelPaddingY+TooltipYOffset).AddWidth(ParentWidth);
			Color.white.DrawRect(BoxRect.Grow(LabelPaddingX+1, LabelPaddingY+1));
			TooltipBorderCol.DrawRect(BoxRect.Grow(LabelPaddingX, LabelPaddingY));
			GUI.Label(BoxRect, TooltipString.Parent, LabelStyle);
			GUI.Label(BoxRect.AddX(ParentWidth), TooltipString.Name, LabelStyleBold);
			GUI.BeginClip(WindowRect);
		}

		//Add the help button
		if(GUI.Button(new Rect(WindowRect.width-(CloseButtonSize-CloseButtonPadding)*3, CloseButtonPadding, CloseButtonSize, CloseButtonSize), "?"))
			HelpWindow.Init();
	}

	//Destroy the window
	public override void Close() => OnNextFrame(() => {
		DevInput.Mouse.Visibility.ForceEvent -= ForceCursor;
		ShowSelection.Close();
		CurFoundObj=null;
		LR?.Close();
		base.Close();
		CurWin=null;
	});
	private static bool ForceCursor() => true;

	//Determine which TexType we want to display depending on the button clicked
	public static CurrentObj.Type MouseClickToTexType =>
		Button.CurrentButton switch {
			Button.Enum.Left	=> CurrentObj.Type.CroppedTexture,
			Button.Enum.Right	=> CurrentObj.Type.Rendered,
			Button.Enum.Middle	=> CurrentObj.Type.FullTexture,
			_					=> CurrentObj.Type.CroppedTexture,
		};

	//On mouse move, show boxes for all sprites we are over
	private LiveRectangles? LR=null;
	private class LiveRectangles(ExtractSpritesWindow Parent) : Window("Live ExtractSprite Rectangles", true, -3)
	{
		private readonly SafeTexture2D BoxTex=Color.red.MakeTexture(), SelectedTex=new Color(0, 0, 1, 0.35f).MakeTexture();
		private record class MouseOverSprites(DrawGeometry.Rectangle R, SpriteObject SO, Misc.Ref<DateTime> LastUpdate);
		private readonly Dictionary<GameObject, MouseOverSprites> MOOList=[];
		private readonly ExtractSpritesWindow Parent=Parent;
		private MouseOverSprites? ClosestObj=null;

		//Update the lists
		protected override void OnMouseEvent(Event Ev)
		{
			//If click, select a new sprite
			if(Ev.type==EventType.MouseDown)
				Parent.RunUpdate(MouseClickToTexType);

			//Only handle mouse move
			if(Ev.type!=EventType.MouseMove)
				return;

			//Add any new sprites that we weren’t over previously
			DateTime Now=DateTime.Now;
			foreach(SpriteObject SO in GetSpritesUnderCursor())
				if(MOOList.TryGetValue(SO.GO, out MouseOverSprites AlreadyObj))
					AlreadyObj.LastUpdate.Value=Now;
				else
					MOOList[SO.GO]=new MouseOverSprites(new DrawGeometry.Rectangle(SO.ScreenPos, BoxTex, 2) { Priority=-10 }, SO, new(Now));

			//Swap the closest sprite to having a different background color
			Vector2 MP=DevInput.Util.MousePos;
			MouseOverSprites? NewClosestObj=
				  MOOList.Count==0 ? null
				: MOOList.Values.OrderBy(MOO => (MOO.SO.ScreenPos.center-MP).magnitude).FirstOrDefault();
			if(NewClosestObj!=ClosestObj) {
				_=ClosestObj?.R.BGTexture.Texture=null;
				_=NewClosestObj?.R.BGTexture.Texture=SelectedTex;
				ClosestObj=NewClosestObj;
			}

			//Clear sprites we are no longer over
			List<GameObject> KeysToRemove=new(MOOList.Count);
			foreach((GameObject GO, MouseOverSprites MOO) in MOOList) {
				if(MOO.LastUpdate.Value==Now)
					continue;
				KeysToRemove.Add(GO);
				MOO.R.Close();
			}
			foreach(GameObject GO in KeysToRemove)
				_=MOOList.Remove(GO);
		}

		//Draw closest sprite label
		protected override void DoLayout(int ID, Event Ev)
		{
			if(ClosestObj==null)
				return;

			const int LabelPaddingX=5, LabelPaddingY=2;
			Vector2 LabelSize=Parent.LabelStyle.CalcSize(new GUIContent(ClosestObj.SO.Name))+new Vector2(LabelPaddingX*2, LabelPaddingY*2);
			Rect BoxRect=LabelSize.CenterIn(ClosestObj.R.Rect.size).AddPos(ClosestObj.R.Rect.position);
			Color.white.DrawRect(BoxRect.Grow(1, 1));
			Parent.TooltipBorderCol.DrawRect(BoxRect);
			GUI.Label(BoxRect.Grow(-LabelPaddingX, -LabelPaddingY), ClosestObj.SO.Name, Parent.LabelStyle);
		}

		//Handle window events
		protected override void OnInit() => UnboundDraw=true;
		protected override bool IsMouseOverWindow(Vector2 _) => true; //Always take mouse events
		protected override void OnUpdate()
		{
			//Handle empty list and no longer has mouse focus
			if(MOOList.Count==0)
				return;
			if(!HasMouseFocus) {
				ClearList();
				return;
			}

			//Update the positions of the rectangles
			foreach(MouseOverSprites MOO in MOOList.Values)
				MOO.R.Rect=MOO.SO.ScreenPos;
		}

		//Keep the list clear when not in use
		public void ClearList()
		{
			MOOList.Values.ForEach(static LR => LR.R.Close());
			MOOList.Clear();
			ClosestObj=null;
		}

		//Destroy the window
		public override void Close() => OnNextFrame(() => {
			ClearList();
			BoxTex.TDestroy();
			SelectedTex.TDestroy();
			base.Close();
		});
	}

	private class HelpWindow() : PopupMessage(Misc.Empty)
	{
		private static HelpWindow? Self;
		internal static void Init() =>
			OnNextFrame(static () => Misc.IFF(
				Self==null,
				static () => Self=new HelpWindow()
			));

		private readonly GUIStyle MyTextStyle=new(GUI.skin.label) { richText=true, margin=new RectOffset(0,0,1,1), padding=new RectOffset(0,0,0,0) };

		private const string EnglishHelpText=
@"#4
#50Clicking a line item:
* Left click=Display single sprite from sprite sheet
#25-Will probably contain a few other sprite textures parts that you’d need to clip
#1
#50* Right click=Display the rendered sprite
#25-Feature can be twitchy
#1
#50* Middle click=Display the full sprite sheet
#25-You’ll see lots of sprites
#2
#50* Double left click=Open sprite in unity explorer
#25-If installed
#1
#35* Clicking an animated sprite line multiple times can show new frames
#30
#35 * <u>Left click an image to save it to:</u>
+<DIR>
#8
|-#37<PATH>
#6
<#35 * Right click sprite sheet to toggle highlight. Try with animated sprites!
#4
#20 * Once the “Extract Sprites” window is open you can also left/right/middle click on sprites in the game window<size=11> (Config required: Boxes around sprites)</size>";

		protected override void DrawContents(Vector2 AreaSize)
		{
			//Draw the PressAnyKeyString text and reset styles (font size not reset)
			GUILayout.Label(PressAnyKeyString, DefaultTextStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
			MyTextStyle.wordWrap=false;
			MyTextStyle.alignment=TextAnchor.MiddleLeft;

			//Substitute in the directory and path and get the window width
			string LocalText=Tr.TDef("ExtractSpritesWindow.HelpWindow", Default:EnglishHelpText)
				.Replace("<DIR>", Misc.SanitizeRichString(FileOps.PathCombine(FileOps.GetPluginPath, ExtractAllTextures.TextureDirectory, "\u00A0")))
				.Replace("<PATH>", "<b><color=green>[YYYY-MM-DD_HH_mm_SS SPRITE_NAME].png</color></b>");

			//Auto tab asterisks and dashes
			static string AutoIndent(string Str) =>
				  Str.StartsWith("*") ? "<size=50>    *</size>"			+Str[1..]+" " //Add a space at the end to account for width mismatches
				: Str.StartsWith("-") ? "<size=25>             </size>"	+Str[1..]+" " //Add a space at the end to account for width mismatches
				:														 Str;

			DrawByLine(LocalText, MyTextStyle, AreaSize, AutoIndent);
		}

		protected override void OnClosed() => Self=null;
	}
}