using SilkDev;
using System;
using System.Collections.Generic;
using UnityEngine;
using static SilkDev.DevInput.Joystick;
using static SilkDev.Misc;

namespace PharloomAtlas;

public partial class SideBar : Window
{
	private static bool HasInitialized=false;
	private static Config Conf => Config.C;

	//Styling stuff
	private const int AreaMargin=2;
	public int Width; //Updated via config
	private int PickerContentHeight=690; //Measured beforehand but updated every frame
	private readonly Sprite Arrow;
	private readonly DataStorage DS;
	private readonly Texture2D CTexRightBorder=Color.white.MakeTexture();
	private readonly List<Texture2D> StoreTextures=[];
	private Vector2 ScrollPosition=Vector2.zero;
	private readonly GUISkin CustomSkin;

	//Keypress delays
	private DateTime LastKeyTime=DateTime.MinValue;
	private string LastKeyOp=Empty;

	//Create the sidebar
	internal SideBar(DataStorage DS) : base(nameof(SideBar), false, -50)
	{
		if(HasInitialized)
			throw new InvalidOperationException("Sidebar already initialized");
		HasInitialized=true;

		//Initializing different parts of the interface
		(this.DS, Width, AlwaysCallPreOnGUI)=(DS, Conf.SideBarWidth, true);
		Arrow=Sprite.Create(DS.IconPicsTex, new Rect(652, DS.IconPicsTex.height-463-12, 7, 12), new Vector2(1, 0.5f), 100f);

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
			new ButtonsRowSection.CreateButton("Copy",		static	() => SaveToClipboard(SaveValuesWindow.Self.SelectedItem?.ToString() ?? "Nothing Selected")),
			new ButtonsRowSection.CreateButton("Copy All",	static	() => SaveToClipboard(SaveValuesWindow.Self.AllAsString))
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
		_=new ButtonsRowSection.Button("Help", static () => new HelpWindow(), Sec);
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
		IFF(MapControl.Self.IsMapOpened, DrawArrow);
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

		//End the scroll area and add a right border for the area
		GUI.EndScrollView();
		GUI.DrawTexture(new Rect(WindowRect.width-1, 0, 1, WindowRect.height), CTexRightBorder);
		GUILayout.EndArea();
		GUI.skin=OriginalSkin;
	}

	//Draw the arrow pointed left or right depending on if the sidebar is visible
	private static readonly Vector2 ArrowPos=new(10, 20);
	private void DrawArrow()
	{
		Rect ArrowTexRect=Arrow.textureRect;
		if(Visible) {
			ArrowTexRect.x+=Arrow.rect.width;
			ArrowTexRect.width*=-1;
		}
		GUI.DrawTextureWithTexCoords(
			new Rect(ArrowPos, Arrow.rect.size).AddX(Visible ? Width : 0),
			Arrow.texture, ArrowTexRect.ConvertTexCoords(Arrow.texture)
		);
	}

	//Initiates a vertical scroll area in case the sidebar is too short (Fills remaining space to bottom)
	private void StartScrollView(Rect AreaRect, int ClientWidth)
	{
		int TopAreaHeight=(int)GUILayoutUtility.GetLastRect().height;
		ScrollPosition=GUI.BeginScrollView(
			new Rect(0, TopAreaHeight, AreaRect.width, AreaRect.height-TopAreaHeight),
			ScrollPosition,
			new Rect(0, TopAreaHeight, ClientWidth, PickerContentHeight)
		);
		GUILayout.BeginVertical(GUILayout.Width(ClientWidth));
	}

	//Handle key presses
	protected override void OnUpdate()
	{
		//Check y axis
		InControl.InputDevice AD=ActiveDevice;
		int y=Conf.Shortcut_SB_Down.IsPressed()	?  1
			: Conf.Shortcut_SB_Up.IsPressed()	? -1
			: AD.DPadUp.IsPressed				? -1
			: AD.DPadDown.IsPressed				?  1
			:									   0;

		//Check x axis
		int x=Conf.Shortcut_SB_Right.IsPressed()?  1
			: Conf.Shortcut_SB_Left.IsPressed() ? -1
			: AD.DPadLeft.IsPressed				? -1
			: AD.DPadRight.IsPressed			?  1
			:									   0;

		//Check execute button
		int Execute=(Conf.Shortcut_SB_ToggleItem.IsDown() || AD.RightCommand.WasPressed) ? 1 : 0;

		//Scroll up/down the sidebar categories
		Direction JD;
		float ScrollDir=
			  HelpWindow.HasAnyOpen ? 0
			: Conf.Shortcut_SB_ScrollUp.IsPressed() ? -1
			: Conf.Shortcut_SB_ScrollDown.IsPressed() ? 1
			: (JD=GetOrdinalDirectionAndMagnitude(false, 20, .4f, out float Magnitude)) is Direction.Left or Direction.Right
				? Magnitude*(JD==Direction.Left ? -1 : 1)
			: 0;

		//If the keys have changed then we don’t need the KeyPressDelay
		string CurKeyOp=$"{x},{y},{Execute},{ScrollDir}";
		if(LastKeyOp!=CurKeyOp)
			(LastKeyOp, LastKeyTime)=(CurKeyOp, DateTime.MinValue);

		//Do not allow keypresses too quickly
		const float KeyPressDelay=.075f;
		if((DateTime.Now-LastKeyTime).TotalSeconds<KeyPressDelay)
			return;
		LastKeyTime=DateTime.Now;

		//Run the functions for the cooresponding keys
		if(y!=0)
			CurrentSection.MoveVer(y<0);
		if(x!=0)
			CurrentSection.MoveHor(x<0);
		if(Execute!=0)
			CurrentSection.ExecSelected();
		if(ScrollDir!=0)
			ScrollPosition.y=Mathf.Max(ScrollPosition.y+ScrollDir*30, 0);
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
}