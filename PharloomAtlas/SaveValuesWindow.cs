using InControl;
using SilkDev;
using SilkDev.DevInput.Mouse;
using System;
using System.Linq;
using UnityEngine;
using SaveItem=PharloomAtlas.MonitorSaveValues.SaveItem;

namespace PharloomAtlas;

public class SaveValuesWindow : SilkDev.Windows.Window
{
	//Toggle visibility
	public override bool Visible
	{
		get => base.Visible;
		set {
			if(base.Visible==value)
				return;
			base.Visible=value;
			void FixValWindow() => MapControl.Self?.SideBar.ValuesWindowToggled(value);
			OnNextFrame(FixValWindow);
		}
	}

	//Constants
	private const float MainSectionHeightOffset=31; //Precalculated amount buttons and window dressing take up - 64 with buttons
	private readonly float ScrollbarWidth=GUI.skin.verticalScrollbar.fixedWidth;
	private readonly float LineHeight;
	private readonly GUIStyle TextStyle=new(GUI.skin.label) { fontSize=14, wordWrap=false, margin=new RectOffset(5, 5, 0, 0), padding=new RectOffset(5, 5, 0, 0) };
	private readonly GUIStyle ScrollbarStyle=new(GUI.skin.verticalScrollbar) { margin=new RectOffset(0, 0, 0, 0) };
	private readonly GUIStyle NoScrollStyle=new(GUI.skin.scrollView) { margin=new RectOffset(0, 0, 0, 0) };
	private readonly Texture2D SelectTex=new Color(1, 1, 0, 0.5f).MakeTexture();

	//Members
	private readonly System.Collections.Generic.List<SaveItem> SavedItems=[];
	private float ScrollPosition=0;

	//Scroll positioning
	private int NumLinesVisible	=> (int)((WindowRect.height-MainSectionHeightOffset)/LineHeight);
	private int TopLineInView	=> (int)ScrollPosition;
	private int BottomLineInView=> TopLineInView+NumLinesVisible;

	//Selection stuff
	private int SelectedLine=0;
	public SaveItem? SelectedItem		=> SavedItems.Count==0 ? null : SavedItems[SavedItems.Count-SelectedLine-1];
	private bool IsSelectedLineInView	=> SelectedLine>=TopLineInView && SelectedLine<=BottomLineInView;

	//Initialization
	private static SaveValuesWindow _Self=null!; public static SaveValuesWindow Self => _Self; //Singleton
	internal static void Init() => _=new SaveValuesWindow();
	private SaveValuesWindow() : base("Saved values", Config.C.Rect_SaveValuesWindow)
	{
		Misc.InitSingleton(this, ref _Self);
		(LineHeight, AlwaysCallUpdate)=(TextStyle.lineHeight, true);
		MonitorSaveValues.Self.OnValueChanged += AddItem;
		TextStyle.normal.background=BGTex=new Color(0, 0, 0, .75f).MakeTexture();
	}

	//Add an item to the contents
	private void AddItem(SaveItem Item)
	{
		SavedItems.Add(Item);

		//Stay with the current line when not scrolled to top item
		if(TopLineInView!=0) {
			ScrollPosition++;
			SelectedLine++;
			return;
		}

		//Advance the scroll position if scrolled to top line and selected line was in view and goes out of view
		if(DateTime.Now.Year==0) { //Permanently turned off
			bool IsPreviousLineInView=IsSelectedLineInView;
			SelectedLine+=2; //Add an extra line for the in view calculation
			if(IsPreviousLineInView && !IsSelectedLineInView)
				ScrollPosition++;
			SelectedLine--;
			if(SavedItems.Count==1)
				SelectedLine--;
		}

		//I decided to keep the scroll at the top and let the selected line move
		if(SavedItems.Count!=1)
			SelectedLine++;
	}

	//Draw the window contents
	protected override void DoLayout(int ID, Event CurEv)
	{
		GUILayout.BeginVertical();

		//Area for label and scrollbar
		float MainSectionHeight=WindowRect.height-MainSectionHeightOffset;
		int NumLines=NumLinesVisible, IntScrollPos=TopLineInView;
		GUILayout.BeginHorizontal();

		//Put label inside a scrollview to clip everything inside it
		_=GUILayout.BeginScrollView(
			Vector2.zero, false, false, GUIStyle.none, GUIStyle.none, NoScrollStyle,
			GUILayout.ExpandWidth(true), GUILayout.Height(MainSectionHeight), GUILayout.MaxHeight(MainSectionHeight)
		);

		//Main label section
		TextStyle.padding.top=(int)((ScrollPosition-IntScrollPos)*-LineHeight);
		GUILayout.Label(
			string.Join(Misc.NewLine, SavedItems.AsEnumerable().Reverse().Skip(IntScrollPos).Take(NumLines+1)),
			TextStyle, GUILayout.ExpandWidth(true), GUILayout.Height(MainSectionHeight), GUILayout.MaxHeight(MainSectionHeight)
		);
		Rect MainLabelRect=GUILayoutUtility.GetLastRect();

		//Check for item selection
		const int ScrollWheelInterval=3;
		int NumItems=Mathf.Max(SavedItems.Count, 1);
		if(Event.current.type==EventType.MouseDown && Button.CurrentButton==Button.Enum.Left && MainLabelRect.Contains(Event.current.mousePosition))
			SelectedLine=Mathf.Min((int)(ScrollPosition+(Event.current.mousePosition.y-MainLabelRect.y)/LineHeight), NumItems-1);

		//Check for item scrolling
		if(Event.current.type==EventType.ScrollWheel && Event.current.delta.y!=0 && MainLabelRect.Contains(Event.current.mousePosition)) {
			SelectedLine=Mathf.Clamp(SelectedLine+(int)(Event.current.delta.y/ScrollWheelInterval), 0, NumItems-1);
			MoveSelectedLineIntoView();
		}

		//Highlight the selected item
		if(SavedItems.Count>0 && IsSelectedLineInView)
			GUI.DrawTexture(
				MainLabelRect.AddY((SelectedLine-ScrollPosition)*LineHeight).SetHeight(LineHeight),
				SelectTex
			);

		//Vertical scrollbar
		GUILayout.EndScrollView();
		ScrollPosition=GUILayout.VerticalScrollbar(
			ScrollPosition, Mathf.Min(NumLines, NumItems), 0f, NumItems, ScrollbarStyle,
			GUILayout.Width(ScrollbarWidth), GUILayout.Height(MainSectionHeight)
		);
		Rect VerticalScrollRect=GUILayoutUtility.GetLastRect();
		GUILayout.EndHorizontal();

		//Check for scrollwheel when over the vertical scrollbar
		if(Event.current.type==EventType.ScrollWheel && Event.current.delta.y!=0 && VerticalScrollRect.Contains(Event.current.mousePosition))
			ScrollPosition=Mathf.Clamp(ScrollPosition+(int)(Event.current.delta.y/ScrollWheelInterval), 0, Mathf.Max(SavedItems.Count-NumLines, 0));

		//Save buttons
/*			const int ButtonHeight=30;
		GUILayout.BeginHorizontal();
		if(GUILayout.Button("Save And Send", GUILayout.Height(ButtonHeight))) { }
		if(GUILayout.Button("Save", GUILayout.Height(ButtonHeight))) { }
		if(GUILayout.Button("Copy", GUILayout.Height(ButtonHeight))) { }
		GUILayout.EndHorizontal();*/

		//Add the window dragging/resizing
		GUILayout.EndVertical();
	}

	//Move selected line into view
	private void MoveSelectedLineIntoView() =>
		ScrollPosition=
			  SelectedLine<TopLineInView ? SelectedLine
			: SelectedLine>BottomLineInView-1 ? Mathf.Max(SelectedLine-NumLinesVisible+1, 0)
			: ScrollPosition;

	//Watch for key presses
	private static InputDevice AD => InputManager.ActiveDevice;
	private DateTime LastKeyPress=DateTime.MinValue;
	private const float KeyPressInterval=.05f;
	protected override void OnUpdate()
	{
		//Window visibility (global key)
		Config C=Config.C;
		if(Config.C.Shortcut_SaveValueWindow.IsDown())
			Visible=!Visible;

		//Check to see if value window scroll actions are in play
		if(!(
			Visible &&														//Is open
			(DateTime.Now-LastKeyPress).TotalSeconds>KeyPressInterval	&&	//KeyPressInterval seconds has passed since the last line selection change
			(MapControl.Self?.IsMapOpened ?? false)						&& (//Map is also open
				AD.LeftTrigger.IsPressed || AD.RightTrigger.IsPressed	||	//Is Pressed: Left trigger or right trigger...
				C.Shortcut_Val_ScrollUp.IsPressed()						||	//or keyboard scroll up shortcut
				C.Shortcut_Val_ScrollDown.IsPressed()						//or keyboard scroll down shortcut
			)
		))
			return;

		//Bumper actions move the selected item
		SelectedLine=Mathf.Clamp(SelectedLine+(AD.LeftTrigger.IsPressed || C.Shortcut_Val_ScrollUp.IsPressed() ? -1 : 1), 0, Mathf.Max(SavedItems.Count-1, 0));
		MoveSelectedLineIntoView();
		LastKeyPress=DateTime.Now;
	}

	public void MoveToTopItem() => ScrollPosition=SelectedLine=0; //Move to the top of the list
	public string AllAsString => string.Join(Misc.NewLine, SavedItems); //Return a copy of all the values in the window
	protected override void CloseButton() => Visible=false;
	protected override void OnGameLoaded(int _) => MonitorSaveValues.Self.UpdateAllUsedValuesOnLoad();
}