using SilkDev.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Button=SilkDev.DevInput.Mouse.Button;

namespace SilkDev.Textures;

//Opens the “Game Object Sprites” window on keyboard shortcut, which allows you to browse and save the sprites/textures that were under your mouse.
public class GameObjectSprites : Window
{
	//Helper classes
	public record class FoundObj(string Name, string ParentTree, GameObject GO) {
		public Rect ScreenPos => GO==null ? Rect.zero : WorldBoundsToScreenRect(GO.GetComponent<SpriteRenderer>().bounds);
	}
	private record class LabelInfo(string Label, GUIStyle Style, float Width);
	private static Internal.Config Conf => Internal.Config.C;

	//Constants
	private const string EllipsesStr="...";
	private readonly GUIStyle EllipsesStyle, LabelStyleBold, LabelStyle=new(GUI.skin.label) { fontSize=14, wordWrap=false, richText=false, margin=new RectOffset(0, 0, 0, 0), padding=new RectOffset(0, 0, 0, 0) };
	private readonly Texture2D SelectTex=new Color(1, 1, 0, 0.5f).MakeTexture(), TooltipBorderTex=Color.black.MakeTexture();
	private readonly float EllipsesWidth;

	//Members
	private readonly List<FoundObj> FOList=[];
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
	public FoundObj? CurFoundObj {
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
				SpriteRenderer SR=value.GO.GetComponent<SpriteRenderer>();
				TexImage=GetTexFromSprite ? SR.CaptureToTexture() : SR.sprite.texture.ToReadable(TexCoords: SR.sprite.textureRect);
			} catch(Exception e) {
				_=new PopupMessage($"Sprite load failed:\n<size=25>{Misc.SanitizeRichString(e.Message)}</size>");
			}
		}
	}

	internal GameObjectSprites() : base("FILLED IN BELOW", Conf.Rect_GameObjectSprites)
	{
		LabelStyleBold=new GUIStyle(LabelStyle) { fontStyle=FontStyle.Bold };
		EllipsesStyle=new GUIStyle(LabelStyle) { normal={ textColor=Color.red } };
		EllipsesWidth=EllipsesStyle.CalcSize(new GUIContent(EllipsesStr)).x;
		AlwaysCallUpdate=true;
		Resizer!.MinSize=new Vector2(MinListWidth+50, MinListWidth+50);

		LR=new(this);
	}

	protected override void OnUpdate()
	{
		if(Conf.Key_GameObjectSprites.IsDown()) //Shortcut key pressed to run update
			OnNextFrame(RunUpdate);
		if(CurFoundObj!=null) {
			if(Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) //Update the selected object
				CurFoundObj=FOList[Mathf.Clamp(FOList.IndexOf(CurFoundObj)+(Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1), 0, FOList.Count-1)];
			ShowSelection.Rect=CurFoundObj.ScreenPos; //Update the position of the selection window
		}
	}

	private void RunUpdate()
	{
		//Get the new list
		Vector2 MP=DevInput.Util.MousePos;
		CurFoundObj=null;
		FOList.Clear();
		FOList.AddRange(GetObjectsUnderCursor().OrderBy(
			FO => (FO.ScreenPos.center-MP).magnitude
		));

		//Make sure the window is visible and select the first item if available
		Visible=true;
		if(FOList.Count>0)
			(CurFoundObj, GetTexFromSprite)=(FOList[0], false);
	}

	//Find all the GameObjects under the cursor
	private record class FindData(List<FoundObj> ObjList, Camera Camera, Vector2 MousePos);
	public static List<FoundObj> GetObjectsUnderCursor()
	{
		//Recursively check through all the root game objects for the scene
		List<FoundObj> ObjList=[];
		FindData FD=new(ObjList, Camera.allCameras[0], DevInput.Util.MousePos);
		foreach(GameObject GO in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
			FindObjectsRecurse(FD, GO.transform, "");

		return ObjList;
	}

	//Go through visibility trees and find visible sprites
	private static void FindObjectsRecurse(FindData FD, Transform CurObject, string ParentTree)
	{
		//If not visible then stop here
		if(!CurObject.gameObject.activeSelf)
			return;

		//If a SpriteRenderer then add it if its projected Rect contains the mouse cursor
		if(
			   CurObject.GetComponent<SpriteRenderer>() is SpriteRenderer SR
			&& WorldBoundsToScreenRect(FD.Camera, SR.bounds).Contains(FD.MousePos)
		)
			FD.ObjList.Add(new FoundObj(CurObject.gameObject.name, ParentTree, CurObject.gameObject));

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
		foreach(FoundObj FO in FOList) {
			//Get the label parts to draw
			float ParentNameWidth=LabelStyle.CalcSize(new GUIContent(FO.ParentTree)).x;
			float NameWidth=LabelStyleBold.CalcSize(new GUIContent(FO.Name)).x;
			LabelInfo[] LI=
				ParentNameWidth+NameWidth<=LabelMaxWidth ?
					[
						new LabelInfo(FO.ParentTree, LabelStyle, ParentNameWidth),
						new LabelInfo(FO.Name, LabelStyleBold, NameWidth),
					]
				:
					[
						new LabelInfo(FO.ParentTree, LabelStyle, LabelMaxWidth-NameWidth-EllipsesWidth),
						new LabelInfo(EllipsesStr, EllipsesStyle, EllipsesWidth),
						new LabelInfo(FO.Name, LabelStyleBold, NameWidth),
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
				TooltipString=(FO.ParentTree, FO.Name);

			//Check to see if clicked
			if(IsMouseOver && Event.current.type==EventType.MouseDown && Button.CurrentButton!=Button.Enum.Middle) {
				GetTexFromSprite=Button.CurrentButton!=Button.Enum.Left;
				OnNextFrame(() => CurFoundObj=FO);
			}

			//Highlight the label if selected
			if(CurFoundObj==FO)
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
		if(GUI.Button(new Rect(WindowRect.width-(CloseButtonSize-CloseButtonPadding)*3, CloseButtonPadding, CloseButtonSize, CloseButtonSize), "?"))
			_=new PopupMessage("\n"+string.Join(""+Misc.NewLine+Misc.NewLine, [
					"Left click a line to get its direct texture.",
					"Right click a line to get its rendered sprite (this feature is twitchy).",
					"Left click a picture to save it to:",
					"<size=25>"+Misc.SanitizeRichString(FileOps.PathCombine(Misc.GetPluginPath, ExtractAllTextures.TextureDirectory, " "))+"\n<b>[YYYY-MM-DD_HH_mm_SS SPRITE_NAME].png</b></size>"
			]));
	}

	public override void Close() => Visible=false;

	public override bool Visible {
		get => base.Visible;
		set {
			LR.Visible=base.Visible=value;
			if(!value)
				CurFoundObj=null;
		}
	}

	//On mouse move, show boxes for all game objects we are over
	private readonly LiveRectangles LR;
	private class LiveRectangles(GameObjectSprites Parent) : Window("Live GameObjectSprite Rectangles", false, -300)
	{
		private readonly Texture2D BoxTex=Color.red.MakeTexture(), SelectedTex=new Color(0, 0, 1, 0.35f).MakeTexture();
		private record class MouseOverObjects(DrawGeometry.Rectangle R, FoundObj FO, Misc.Ref<DateTime> LastUpdate);
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
			foreach(FoundObj FO in GetObjectsUnderCursor())
				if(MOOList.TryGetValue(FO.GO, out MouseOverObjects AlreadyObj))
					AlreadyObj.LastUpdate.Value=Now;
				else
					MOOList[FO.GO]=new MouseOverObjects(new DrawGeometry.Rectangle(FO.ScreenPos, BoxTex, 2), FO, new(Now));

			//Swap the closest object to having a different background color
			Vector2 MP=DevInput.Util.MousePos;
			MouseOverObjects? NewClosestObj=
				  MOOList.Count==0 ? null
				: MOOList.Values.OrderBy(MOO => (MOO.FO.ScreenPos.center-MP).magnitude).FirstOrDefault();
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
			Vector2 LabelSize=Parent.LabelStyle.CalcSize(new GUIContent(ClosestObj.FO.Name))+new Vector2(LabelPaddingX*2, LabelPaddingY*2);
			Rect BoxRect=LabelSize.CenterIn(ClosestObj.R.Rect.size);
			BoxRect.position += ClosestObj.R.Rect.position;
			GUI.DrawTexture(BoxRect.Grow(1, 1), Texture2D.whiteTexture);
			GUI.DrawTexture(BoxRect, Parent.TooltipBorderTex);
			GUI.Label(BoxRect.Grow(-LabelPaddingX, -LabelPaddingY), ClosestObj.FO.Name, Parent.LabelStyle);
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
				MOO.R.Rect=MOO.FO.ScreenPos;
		}

		//Keep the list clear when not in use
		public void ClearList()
		{
			MOOList.Values.ForEach(LR => LR.R.Close());
			MOOList.Clear();
			ClosestObj=null;
		}
		public override bool Visible
		{
			get => base.Visible;
			set {
				ClearList();
				base.Visible=value;
			}
		}
	}
}