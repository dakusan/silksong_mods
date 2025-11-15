using SilkDev.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button=SilkDev.DevInput.Mouse.Button;

namespace SilkDev.Textures;

//Opens the “Game Object Sprites” window on keyboard shortcut, which allows you to browse and save the sprites/textures that were under your mouse.
public class GameObjectSprites : Window
{
	//Only allow 1 instance and 1 initialization
	private static GameObjectSprites? CurWin=null;
	private static bool AlreadyInitialized=false;
	internal static void Init()
	{
		//Only allow initialization once
		if(AlreadyInitialized)
			return;
		AlreadyInitialized=true;

		//Handle shortcut key to open window
		Events.GameEvents.OnUpdate += static () => Misc.IFF(
			Conf.Key_GameObjectSprites.IsDown(),
			() => OnNextFrame(CurWin != null ? CurWin.RunUpdate : static () => CurWin=new GameObjectSprites())
		);

		//Handle setting changed of whether to show LiveRectangles
		Conf.GOSWindow_ShowMouseOver.SettingChanged += static (_, _) => {
			if(CurWin==null)
				return;
			if(Conf.GOSWindow_ShowMouseOver)
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
	private record class LabelInfo(string Label, GUIStyle Style, float Width);
	private static Internal.Config Conf => Internal.Config.C;

	//Constants
	private const string EllipsesStr="...";
	private readonly GUIStyle EllipsesStyle, LabelStyleBold, LabelStyle=new(GUI.skin.label) { fontSize=14, wordWrap=false, richText=false, margin=new RectOffset(0, 0, 0, 0), padding=new RectOffset(0, 0, 0, 0) };
	private readonly Texture2D SelectTex=new Color(1, 1, 0, 0.5f).MakeTexture(), TooltipBorderTex=Color.black.MakeTexture();
	private readonly float EllipsesWidth;

	//Members
	private readonly List<SpriteObject> SOList=[];
	private readonly DrawGeometry.Rectangle ShowSelection=new(0, 0, 0, 0, new Color(0, 1, 0, 0.35f)) { Visible=false, Priority=-1 };
	public Texture2D? TexImage
	{
		get;
		set {
			field?.TDestroy();
			field=value;
		}
	} = null;
	private Vector2 ScrollPosition=Vector2.zeroVector;
	public int MinListWidth {
		get;
		set => Resizer!.MinSize=new Vector2((field=value)+50, value+50);
	} = 200;

	//Set the currently selected object
	private bool GetTexFromSprite=false;
	public SpriteObject? CurFoundObj {
		get;
		private set {
			//Do not check for setting the same thing again as GetTexFromSprite may be different
			field=value;

			//Clear out the current data and update the visibility of the ShowSelection window
			TexImage=null;
			ShowSelection.Visible=(value!=null);
			if(value==null)
				return;

			//Update the texture image
			try {
				TexImage=GetTexFromSprite ? value.CaptureToTexture() : value.Texture.ToReadable(TexCoords: value.TextureRect);
			} catch(Exception e) {
				_=new PopupMessage($"Sprite load failed:\n<size=25>{Misc.SanitizeRichString(e.Message)}</size>");
			}
		}
	}

	private GameObjectSprites() : base("FILLED IN BELOW", Conf.Rect_GameObjectSprites)
	{
		LabelStyleBold=new GUIStyle(LabelStyle) { fontStyle=FontStyle.Bold };
		EllipsesStyle=new GUIStyle(LabelStyle) { normal={ textColor=Color.red } };
		EllipsesWidth=EllipsesStyle.CalcSize(new GUIContent(EllipsesStr)).x;
		Resizer!.MinSize=new Vector2(MinListWidth+50, MinListWidth+50);
		Visible=true;

		if(Conf.GOSWindow_ShowMouseOver)
			LR=new(this);
		RunUpdate();
	}

	protected override void OnUpdate()
	{
		if(CurFoundObj==null)
			return;
		if(Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) //Update the selected object
			CurFoundObj=SOList[Mathf.Clamp(SOList.IndexOf(CurFoundObj)+(Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1), 0, SOList.Count-1)];
		ShowSelection.Rect=CurFoundObj.ScreenPos; //Update the position of the selection window
	}

	private void RunUpdate()
	{
		//Get the new list
		Vector2 MP=DevInput.Util.MousePos;
		CurFoundObj=null;
		SOList.Clear();
		SOList.AddRange(GetObjectsUnderCursor().OrderBy(
			SO => (SO.ScreenPos.center-MP).magnitude
		));

		//Select the first item if available
		if(SOList.Count>0)
			(CurFoundObj, GetTexFromSprite)=(SOList[0], false);
	}

	//Find all the GameObjects under the cursor
	private record class FindData(List<SpriteObject> ObjList, Camera Camera, Vector2 MousePos);
	public static List<SpriteObject> GetObjectsUnderCursor()
	{
		//Recursively check through all the root game objects for the scene
		List<SpriteObject> ObjList=[];
		FindData FD=new(ObjList, Camera.allCameras[0], DevInput.Util.MousePos);
		foreach(GameObject GO in SceneManager.GetActiveScene().GetRootGameObjects())
			FindObjectsRecurse(FD, GO.transform, "");

		//Don’t forget our hero!
		GameObject HeroGO=HeroController.instance.gameObject;
		SpriteObject HeroSO=new SpriteObject_tk2dBaseSprite(HeroGO.name, Misc.Empty, HeroGO.GetComponent<tk2dBaseSprite>());
		if(HeroSO!=null && WorldBoundsToScreenRect(FD.Camera, HeroSO.Bounds).Contains(FD.MousePos))
			FD.ObjList.Add(HeroSO);

		return ObjList;
	}

	//Go through visibility trees and find visible sprites
	private static void FindObjectsRecurse(FindData FD, Transform CurObject, string ParentTree)
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
			FindObjectsRecurse(FD, Child, $"{ParentTree}{CurObject.gameObject.name}/");
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
		bool HasImage=(TexImage!=null);
		float AvailableHeight=WindowRect.height-WinPad.vertical-LabelStyle.lineHeight;
		float Aspect=(!HasImage ? 0 : TexImage!.width/(float)TexImage.height);
		float ImageDisplayHeight=!HasImage ? 0 : Math.Min(AvailableHeight, TexImage!.height);
		float ImageDisplayWidth=ImageDisplayHeight*Aspect;
		float LeftWidth=WindowRect.width-ImageDisplayWidth-WinPad.horizontal;
		if(LeftWidth<MinListWidth) {
			LeftWidth=MinListWidth;
			ImageDisplayWidth=WindowRect.width-WinPad.horizontal-LeftWidth;
			ImageDisplayHeight=ImageDisplayWidth/Aspect;
		}
		float LabelMaxWidth=LeftWidth-GUI.skin.verticalScrollbar.fixedWidth-1;

		//Title the window with our sprite and display sizes
		Title="Game Object Sprites "+(
			  !HasImage ? "No sprite selected"
			: $"{TexImage!.width}*{TexImage.height} -> {ImageDisplayWidth}*{ImageDisplayHeight}"
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

			//Check to see if clicked
			if(IsMouseOver && Event.current.type==EventType.MouseDown)
				if(Button.CurrentButton==Button.Enum.Middle)
					Misc.UnityExplorer_Inspect(SO.GO);
				else {
					GetTexFromSprite=Button.CurrentButton!=Button.Enum.Left;
					OnNextFrame(() => CurFoundObj=SO);
				}

			//Highlight the label if selected
			if(CurFoundObj==SO)
				GUI.DrawTexture(LabelRect, SelectTex);
		}
		GUILayout.EndScrollView();
		GUILayout.EndVertical();

		//Right: Box with image
		if(HasImage) {
			//Center the texture vertically
			GUILayout.BeginVertical(GUILayout.Width(ImageDisplayWidth), GUILayout.Height(AvailableHeight));
			GUILayout.Space(Math.Max((AvailableHeight-ImageDisplayHeight)/2, 0));
			Rect TextureRect=GUILayoutUtility.GetRect(ImageDisplayWidth, ImageDisplayHeight);
			GUI.DrawTexture(TextureRect, TexImage);
			GUILayout.EndVertical();

			//Save the texture
			if(Event.current.type==EventType.MouseDown && Button.CurrentButton==Button.Enum.Left && TextureRect.Contains(Event.current.mousePosition))
				try {
					string DirName=FileOps.PathCombine(Misc.GetPluginPath, ExtractAllTextures.TextureDirectory);
					if(!FileOps.DirectoryExists(DirName))
						_=FileOps.CreateDirectory(DirName);
					string FileName=$"{DateTime.Now:yyyy-MM-dd_HH_mm_ss} {FileOps.FixFileName(CurFoundObj!.Name ?? "NO NAME")}.png";
					FileOps.WriteFile(FileOps.PathCombine(DirName, FileName), TexImage.EncodeToPNG());
					_=new PopupMessage($"File saved to:\n<size=25>{Misc.SanitizeRichString(FileOps.PathCombine(DirName, " "))}\n<b>{Misc.SanitizeRichString(FileName)}</b></size>");
				} catch(Exception e) {
					_=new PopupMessage($"Error saving file:\n<size=25>{Misc.SanitizeRichString(e.Message)}</size>");
				}
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
			GUI.DrawTexture(BoxRect.Grow(LabelPaddingX+1, LabelPaddingY+1), Texture2D.whiteTexture);
			GUI.DrawTexture(BoxRect.Grow(LabelPaddingX, LabelPaddingY), TooltipBorderTex);
			GUI.Label(BoxRect, TooltipString.Parent, LabelStyle);
			GUI.Label(BoxRect.AddX(ParentWidth), TooltipString.Name, LabelStyleBold);
			GUI.BeginClip(WindowRect);
		}

		//Add the help button
		if(!GUI.Button(new Rect(WindowRect.width-(CloseButtonSize-CloseButtonPadding)*3, CloseButtonPadding, CloseButtonSize, CloseButtonSize), "?"))
			return;
		PopupMessage PM=new(string.Join(Misc.NewLine, ["",
			"Clicking a line item:",
			"    * Left click=Display its direct texture",
			"    * Right click=Display its rendered sprite (this feature is twitchy)",
			"    * Middle click=Open object in unity explorer (if installed)", "",
			"Left click a picture to save it to:",
			"<size=25>"+Misc.SanitizeRichString(FileOps.PathCombine(Misc.GetPluginPath, ExtractAllTextures.TextureDirectory, " "))+"\n<b><color=green>[YYYY-MM-DD_HH_mm_SS SPRITE_NAME].png</color></b></size>"
		]));
		OnNextFrame(() => PM.OverrideTextStyle=new GUIStyle(PopupMessage.DefaultTextStyle) { alignment=TextAnchor.MiddleLeft });
	}

	//Destroy the window
	public override void Close() => OnNextFrame(() => {
		SelectTex.TDestroy();
		TooltipBorderTex.TDestroy();
		ShowSelection.Close();
		CurFoundObj=null;
		LR?.Close();
		base.Close();
		CurWin=null;
	});

	//On mouse move, show boxes for all game objects we are over
	private LiveRectangles? LR=null;
	private class LiveRectangles(GameObjectSprites Parent) : Window("Live GameObjectSprite Rectangles", true, -300)
	{
		private readonly Texture2D BoxTex=Color.red.MakeTexture(), SelectedTex=new Color(0, 0, 1, 0.35f).MakeTexture();
		private record class MouseOverObjects(DrawGeometry.Rectangle R, SpriteObject SO, Misc.Ref<DateTime> LastUpdate);
		private readonly Dictionary<GameObject, MouseOverObjects> MOOList=[];
		private readonly GameObjectSprites Parent=Parent;
		private MouseOverObjects? ClosestObj=null;

		//Update the lists
		protected override void OnMouseEvent(Event Ev)
		{
			//If click, select a new object
			if(Ev.type==EventType.MouseDown && Button.CurrentButton==Button.Enum.Left)
				Parent.RunUpdate();

			//Only handle mouse move
			if(Ev.type!=EventType.MouseMove)
				return;

			//Add any new objects that we weren’t over previously
			DateTime Now=DateTime.Now;
			foreach(SpriteObject SO in GetObjectsUnderCursor())
				if(MOOList.TryGetValue(SO.GO, out MouseOverObjects AlreadyObj))
					AlreadyObj.LastUpdate.Value=Now;
				else
					MOOList[SO.GO]=new MouseOverObjects(new DrawGeometry.Rectangle(SO.ScreenPos, BoxTex, 2) { Priority=Priority-1 }, SO, new(Now));

			//Swap the closest object to having a different background color
			Vector2 MP=DevInput.Util.MousePos;
			MouseOverObjects? NewClosestObj=
				  MOOList.Count==0 ? null
				: MOOList.Values.OrderBy(MOO => (MOO.SO.ScreenPos.center-MP).magnitude).FirstOrDefault();
			if(NewClosestObj!=ClosestObj) {
				_=ClosestObj?.R.BGTexture.Texture=null;
				_=NewClosestObj?.R.BGTexture.Texture=SelectedTex;
				ClosestObj=NewClosestObj;
			}

			//Clear objects we are no longer over
			List<GameObject> KeysToRemove=new(MOOList.Count);
			foreach((GameObject GO, MouseOverObjects MOO) in MOOList) {
				if(MOO.LastUpdate.Value==Now)
					continue;
				KeysToRemove.Add(GO);
				MOO.R.Close();
			}
			foreach(GameObject GO in KeysToRemove)
				_=MOOList.Remove(GO);
		}

		//Draw closest object label
		protected override void DoLayout(int ID, Event Ev)
		{
			if(ClosestObj==null)
				return;

			const int LabelPaddingX=5, LabelPaddingY=2;
			Vector2 LabelSize=Parent.LabelStyle.CalcSize(new GUIContent(ClosestObj.SO.Name))+new Vector2(LabelPaddingX*2, LabelPaddingY*2);
			Rect BoxRect=LabelSize.CenterIn(ClosestObj.R.Rect.size);
			BoxRect.position += ClosestObj.R.Rect.position;
			GUI.DrawTexture(BoxRect.Grow(1, 1), Texture2D.whiteTexture);
			GUI.DrawTexture(BoxRect, Parent.TooltipBorderTex);
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
			foreach(MouseOverObjects MOO in MOOList.Values)
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
}