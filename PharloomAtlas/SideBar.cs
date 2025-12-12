using SilkDev;
using SilkDev.Textures;
using SilkDev.Windows;
using System;
using System.Linq;
using UnityEngine;
using static SilkDev.DevInput.Joystick;
using Dragger=SilkDev.DevInput.Mouse.Dragger;

namespace PharloomAtlas;

public partial class SideBar : Window
{
	private static bool HasInitialized=false;
	private static Config Conf => Config.C;

	//Resize the sidebar
	private Dragger ResizeDrag=new();
	private int StartDragWidth;

	//Styling stuff
	private const int AreaMargin=2;
	public int Width; //Updated via config
	private int PickerContentHeight=690; //Measured beforehand but updated every frame
	private readonly Sprite Arrow;
	private readonly DataStorage DS;
	private readonly Texture2D CTexRightBorder=Color.white.MakeTexture();
	private readonly System.Collections.Generic.List<Texture2D> StoreTextures=[];
	private Vector2 ScrollPosition=Vector2.zero;
	private readonly GUISkin CustomSkin;

	//Create the sidebar
	internal SideBar(DataStorage DS) : base(nameof(SideBar), false, -50)
	{
		if(HasInitialized)
			throw new InvalidOperationException("Sidebar already initialized");
		HasInitialized=true;

		//Initializing different parts of the interface
		int ArrowWidth=7, ArrowHeight=12;
		(this.DS, Width, AlwaysCallPreOnGUI)=(DS, Conf.SideBarWidth, true);
		Arrow=Sprite.Create( //Arrow is always in the last icon at the top right corner
			DS.IconPicsTex,
			DataStorage.IconSprites.GetIconRectByID(DataStorage.IconLenX*DataStorage.IconLenY-1).
				AddX(DataStorage.IconWidth-ArrowWidth).
				AddY(DataStorage.IconHeight-ArrowHeight).
				SetWidth(ArrowWidth).SetHeight(ArrowHeight),
			new Vector2(1, 0.5f), 100f
		);

		//Set up the skin
		if(CustomSkin==null) {
			CustomSkin=ScriptableObject.CreateInstance<GUISkin>();
			CustomSkin=UnityEngine.Object.Instantiate(GUI.skin);
		}
		StoreTextures.Add(CustomSkin.box.normal.background=Conf.Color_SideBar_Background.V.MakeTexture());
		foreach(GUIStyle S in new GUIStyle[] { CustomSkin.verticalScrollbar, CustomSkin.verticalScrollbarThumb, CustomSkin.button })
			StoreTextures.Add(S.normal.background=CreateTintedTexture(S.normal.background, Conf.Color_SideBar_Interface));
		CustomSkin.box.padding=CustomSkin.box.margin=new RectOffset(0, 0, 0, 0);

		//Set up the top sidebar sections
		IIS=new ItemInfoSection(this);
		CurrentSection=new ButtonsRowSection("ToggleGroups", "Toggle Groups", this, [
			new ButtonsRowSection.CreateButton("Show All",			() => DS.SetAllCategoriesStates(CategoryToggleState.All)),
			new ButtonsRowSection.CreateButton("Show Incomplete",	() => DS.SetAllCategoriesStates(CategoryToggleState.Incomplete)),
			new ButtonsRowSection.CreateButton("Hide all",			() => DS.SetAllCategoriesStates(CategoryToggleState.None)),
			new ButtonsRowSection.CreateButton("Needed for 100%",		  DS.SetCategoriesStatesFor100Percent),
		]);
		_=new ButtonsRowSection("IconValues", "Values Window", this, [
			new ButtonsRowSection.CreateButton("Show",		static	() => SaveValuesWindow.Self.Visible=!SaveValuesWindow.Self.Visible),
			new ButtonsRowSection.CreateButton("Goto Top",				  SaveValuesWindow.Self.MoveToTopItem),
			new ButtonsRowSection.CreateButton("Save",		static	() => MonitorSaveValues.Self.SaveIconValue(false)),
			new ButtonsRowSection.CreateButton("Save+Send",	static	() => MonitorSaveValues.Self.SaveIconValue(true)),
			new ButtonsRowSection.CreateButton("Copy",		static	() => Copy(SaveValuesWindow.Self.SelectedItem?.ToString() ?? Misc.Empty)),
			new ButtonsRowSection.CreateButton("Copy All",	static	() => Copy(SaveValuesWindow.Self.AllAsString))
		]);
		_=new ButtonsRowSection("Other", "Other", this, []);
		FixUnlockedButtons();

		//Create the category group sections
		int FirstCategoryIndex=SectionsList.Count;
		foreach(CategoryGroup CategoryGroupInfo in DS.CategoryGroups)
			_=new CategoryGroupSection(CategoryGroupInfo, this);
		SectionsList[FirstCategoryIndex].BeforeDraw = ClientWidth => {
			GUILayout.EndVertical();
			StartScrollView(new Rect(0, 0, Width, Screen.height), ClientWidth);
		};

		//Hook up interface changers
		Conf.Color_SideBar_Background.SettingChanged += (_, _) => OnNextFrame(() => UpdateColor(UpdateColorType.Background));
		Conf.Color_SideBar_Interface .SettingChanged += (_, _) => OnNextFrame(() => UpdateColor(UpdateColorType.Interface ));
		Conf.Color_SideBar_Highlight .SettingChanged += (_, _) => OnNextFrame(() => UpdateColor(UpdateColorType.Highlight ));
		Conf.SideBarWidth			 .SettingChanged += (_, _) => Width=Conf.SideBarWidth;
	}

	//Make sure only needed buttons are shown
	internal void FixUnlockedButtons()
	{
		ButtonsRowSection Sec=(ButtonsRowSection)Sections["Other"];
		Sec.Buttons.Clear();
		_=new ButtonsRowSection.Button("Help", static () => OnNextFrame(static () => new HelpWindow()), Sec);
		_=new ButtonsRowSection.Button("Search", static () => SearchWindow.Self.Visible=!SearchWindow.Self.Visible, Sec);
		if(!MapControl.Self.AreAllMapsUnlocked())
			_=new ButtonsRowSection.Button("Unlock all maps", MapControl.Self.UnlockAllMaps, Sec);
		if(!MapControl.Self.AreAllGameMarkersUnlocked())
			_=new ButtonsRowSection.Button("Unlock all markers", MapControl.Self.UnlockAllGameMarkers, Sec);
		_=new ButtonsRowSection.Button(
			MapControl.Self.ShowLinkedStatus ? "Hide Unlinked" : "Show Unlinked",
			static () => MapControl.Self.ShowLinkedStatus=!MapControl.Self.ShowLinkedStatus,
			Sec
		);
		if(Sec==CurrentSection)
			Sec.CheckSelectedIndex();
	}
	protected override void OnGameLoaded(int _) => FixUnlockedButtons();

	//When value window visibility is toggled, need to change the buttons text
	internal void ValuesWindowToggled(bool Visible) =>
		((ButtonsRowSection)Sections["IconValues"]).Buttons[0].Title=(Visible ? "Hide" : "Show");

	//Draw the sidebar
	protected override void PreOnGUI(Event Ev) =>
		Misc.IFF(MapControl.Self.IsMapOpened, DrawArrow);
	protected override void DoLayout(int ID, Event Ev)
	{
		//Draw the static GUI area
		GUISkin OriginalSkin=GUI.skin;
		GUI.skin=CustomSkin;
		WindowRect=new Rect(0, 0, Width, Screen.height);
		GUI.Box(WindowRect, GUIContent.none);
		GUILayout.BeginArea(WindowRect);

		//Start the top area above the scroll window
		int ClientWidth=(int)(WindowRect.width-GUI.skin.verticalScrollbar.fixedWidth-AreaMargin*2);
		GUILayout.BeginVertical(GUILayout.Width(Width));
		GUILayout.Space(AreaMargin);
		IIS.Draw(ClientWidth);
		SectionsList.ForEach(S => S.Draw(ClientWidth));

		//Get the height of the categories section
		GUILayout.EndVertical();
		int NewPickerHeight=(int)GUILayoutUtility.GetLastRect().height;
		if(PickerContentHeight!=NewPickerHeight && NewPickerHeight>10)
			PickerContentHeight=NewPickerHeight;

		//End the scroll area
		GUI.EndScrollView();

		//Check to see if the right border is being hovered and if so make it green
		Rect RightBorderRect=new(WindowRect.width-1, 0, 1, WindowRect.height);
		Rect HoverBorderRect=RightBorderRect.AddWidth(10).AddX(-10);
		bool MouseOverResize=HoverBorderRect.Contains(Event.current.mousePosition) || ResizeDrag.IsDragging;
		if(MouseOverResize)
			GUI.color=Color.green;

		//Draw the right border for the area that can drag resize
		GUI.DrawTexture(RightBorderRect, CTexRightBorder);
		if(MouseOverResize) {
			switch(ResizeDrag.UpdateState(HoverBorderRect, false)) {
				case Dragger.State.None		:																		break;
				case Dragger.State.Start	: Ev.Use(); StartDragWidth=Conf.SideBarWidth;							break;
				case Dragger.State.Dragging	: Ev.Use(); Conf.SideBarWidth.V=StartDragWidth+(int)ResizeDrag.Delta.x;	break;
				case Dragger.State.Done		: Ev.Use();																break;
			}
			GUI.color=Color.white;
		}

		//Reset state
		GUILayout.EndArea();
		GUI.skin=OriginalSkin;
	}

	//Draw the arrow pointed left or right depending on if the sidebar is visible
	private static readonly Vector2 ArrowPos=new(10, 20);
	private void DrawArrow()
	{
		Rect ArrowTexRect=Arrow.textureRect;
		if(Visible)
			ArrowTexRect=ArrowTexRect.AddX(Arrow.rect.width).SetWidth(static W => W*-1);

		//Make the arrow green when mouse is over
		Rect ArrowRect=new Rect(ArrowPos, Arrow.rect.size).AddX(Visible ? Width : 0);
		bool OverArrow=ArrowRect.Contains(Event.current.mousePosition);
		if(OverArrow)
			GUI.color=Color.green;

		GUI.DrawTextureWithTexCoords(
			ArrowRect, Arrow.texture, ArrowTexRect.ConvertTexCoords(Arrow.texture)
		);

		//If mouse is over and clicked then toggle the sidebar visibility
		if(OverArrow && Event.current.type==EventType.MouseDown)
			Visible=!Visible;

		//Restore the color
		if(OverArrow)
			GUI.color=Color.white;
	}

	//Initiates a vertical scroll area in case the sidebar is too short (Fills remaining space to bottom)
	private void StartScrollView(Rect AreaRect, int ClientWidth)
	{
		int TopAreaHeight=(int)GUILayoutUtility.GetLastRect().height;
		ScrollPosition=GUI.BeginScrollView(
			CategoryGroupSection.ScrollAreaRect=new Rect(0, TopAreaHeight, AreaRect.width, AreaRect.height-TopAreaHeight),
			ScrollPosition,
			new Rect(0, TopAreaHeight, ClientWidth, PickerContentHeight)
		);
		GUILayout.BeginVertical(GUILayout.Width(ClientWidth));
	}

	//Handle key presses
	private static InControl.InputDevice AD => ActiveDevice;
	public  enum KRType { X=0, Y, Scroll, NUM_ENUMS }
	private record struct KeyResults(KRType Type, int Value);
	private readonly SilkDev.DevInput.InputRepeatDelay<KeyResults> KeysCheck=new(.075f,
		(Conf			.Shortcut_SB_Down		, new(KRType.Y,		 1)),
		(Conf			.Shortcut_SB_Up			, new(KRType.Y,		-1)),
		(static AD => AD.DPadUp					, new(KRType.Y,		-1)),
		(static AD => AD.DPadDown				, new(KRType.Y,		 1)),
		(Conf			.Shortcut_SB_Right		, new(KRType.X,		 1)),
		(Conf			.Shortcut_SB_Left		, new(KRType.X,		-1)),
		(static AD => AD.DPadLeft				, new(KRType.X,		-1)),
		(static AD => AD.DPadRight				, new(KRType.X,		 1)),
		(Conf			.Shortcut_SB_ScrollUp	, new(KRType.Scroll,-1)),
		(Conf			.Shortcut_SB_ScrollDown	, new(KRType.Scroll, 1)),
		(false,Direction.Left					, new(KRType.Scroll,-1)),
		(false,Direction.Right					, new(KRType.Scroll, 1))
	);
	protected override void OnUpdate()
	{
		//Check execute button
		if(Conf.Shortcut_SB_ToggleItem.IsDown() || AD.RightCommand.WasPressed)
			CurrentSection.ExecSelected();

		//Get input presses
		if(KeysCheck.IsReadyInputTypes is not { } Keys)
			return;

		//Combine inputs into a result array (duplicates overwrite each other)
		Span<int> KRTypes=stackalloc int[(int)KRType.NUM_ENUMS];
		foreach(var KR in Keys)
			KRTypes[(int)KR.ReturnValue.Type]=KR.ReturnValue.Value;

		//Run the functions for the cooresponding keys
		int TempVal;
		if((TempVal=KRTypes[(int)KRType.Y])!=0)
			CurrentSection.MoveVer(TempVal<0);
		if((TempVal=KRTypes[(int)KRType.X])!=0)
			CurrentSection.MoveHor(TempVal<0);
		if(!HelpWindow.HasAnyOpen && (TempVal=KRTypes[(int)KRType.Scroll])!=0)
			ScrollPosition.y=Mathf.Max(ScrollPosition.y+TempVal*30, 0);
	}

	//Called when a new icon is selected
	internal void OnNewIconSelected() => IIS.ReleaseTextures();

	//Update skin coloring
	private enum UpdateColorType { Background, Interface, Highlight };
	private void UpdateColor(UpdateColorType CType)
	{
		switch(CType) {
			case UpdateColorType.Background:
				_=CustomSkin.box.normal.background.ReColor(Conf.Color_SideBar_Background);
				break;
			case UpdateColorType.Interface:
				foreach(string StyleName in new string[] { "verticalScrollbar", "verticalScrollbarThumb", "button" }) {
					GUIStyle MyGS=new Reflectors.RProp<GUISkin, GUIStyle>(CustomSkin, StyleName).Get();
					Texture2D MyTex=MyGS.normal.background;
					_=StoreTextures.Remove(MyTex);
					MyTex.TDestroy();
					StoreTextures.Add(MyGS.normal.background=CreateTintedTexture(
						new Reflectors.RProp<GUISkin, GUIStyle>(GUI.skin, StyleName).Get().normal.background,
						Conf.Color_SideBar_Interface)
					);
				}
				break;
			case UpdateColorType.Highlight:
				_=SideBarSection.CTexSelect.ReColor(Conf.Color_SideBar_Highlight);
				break;
			default:
				break;
		}
	}

	//Create a texture copy with all pixels multiplied by Tint
	private static Texture2D CreateTintedTexture(Texture2D Source, Color Tint)
	{
		//Get the textures pixels
		try {
			using TypedDisposer<Texture2D> WorkTexture=new(
				Source.ToReadable(),
				Target => Target.TDestroy()
			);

			//Tint the texture
			Color[] Pixels=WorkTexture.Target.GetPixels();
			for(int i=0; i<Pixels.Length; i++)
				Pixels[i]*=Tint;

			//Return the new texture
			WorkTexture.Target.SetPixels(Pixels);
			WorkTexture.Target.Apply();
			return WorkTexture.Detach();
		} catch(Exception e) {
			Log.Error($"Tint failed: {e.Message}");
			return Source;
		}
	}

	//Keep track of if the sidebar has mouse focus
	private static bool RemHasMouseFocus=false;
	private static int LastMouseFocusCheckFrame=-1;
	public bool CheckHasMouse { get
	{
		if(LastMouseFocusCheckFrame==Time.frameCount)
			return RemHasMouseFocus;
		LastMouseFocusCheckFrame=Time.frameCount;
		return RemHasMouseFocus=HasMouseFocus;
	} }

	//Copy contents to the clipboard and let the user know how many lines were copied
	private static void Copy(string Contents)
	{
		if(Contents==Misc.Empty) {
			_=new PopupMessage(Conf.Tr.T("No values exist to copy", RichSanitize:true));
			return;
		}

		Misc.SaveToClipboard(Contents);
		int NumLines=Contents.Count(static c => c==Misc.NewLine)+1;
		_=new PopupMessage(Conf.Tr.T(NumLines==1 ? "Copied 1 line" : "Copied {0} lines", null, true, NumLines));
	}
}