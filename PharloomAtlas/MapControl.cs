using SilkDev;
using SilkDev.DevInput.Mouse;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using static SilkDev.DevInput.Joystick;

namespace PharloomAtlas;

//All public functions accept/return zoomed out (normalized) positions
public class MapControl : SilkDev.Windows.Window
{
	private static MapControl _Self=null!; public static MapControl Self => _Self; //Singleton

	//Public members
	public enum MapStateEnum	{ Closed=0, Open, Marker }
	public GameMap GameMap		{ get; private set; } = null!;
	public DataStorage DS		{ get; private set; } = null!;
	public Transform AllIcons	{ get; private set; } = null!;
	public SideBar SideBar		{ get; private set; } = null!;
	public Item? HoverItem		{ get; private set; } = null;
	public Item? SelectedItem	{ get; private set; } = null;
	public MapMarkerMenu MMM	{ get; private set; } = null!;
	public MapStateEnum MapState{ get; private set; } = MapStateEnum.Closed;
	public bool IsMapOpened		=> MapState!=MapStateEnum.Closed;

	//Private members
	internal MarkerLabels MarkerLabels=null!;
	private readonly HornetIconAnimators HornetIconAnimators=new();
	private bool SideBarWasOpen=false, SearchWasOpen=false;
	private static Config Conf => Config.C;
	private static PlayerData PData => PlayerData.instance;

	//Zoom states and variables
	public float ZoomScale
	{
		get;
		private set {
			field=value;
			if(IconSizeScalesWithZoom)
				SetIconSize(Conf.IconSize);
		}
	}
	private bool IconSizeScalesWithZoom;
	private const float MarkerCursorRadius=0.55f, IconRadius=.325f;
	private readonly float StartZoomScale;
	private readonly Reflectors.RField<InventoryMapManager, Vector3> IMM_SceneMapEndScale=new(null, "SceneMapEndScale");
	private readonly Reflectors.RField<InventoryMapManager, Vector3> IMM_SceneMapMarkerZoomScale=new(null, "SceneMapMarkerZoomScale");
	private Transform DummyTransform=null!;

	//Drag states and variables
	private Vector2 DragScale=new(0.016095f, -0.016095f); //I got this through trial and error. It’s easier than figuring out how to derive it
	private readonly Dragger Drag=new();
	private Vector2 StartDragPos;

	//Called the first time the map is ever opened after game start
	internal MapControl(GameMap CurMap) : base(nameof(MapControl), false, -200)
	{
		Misc.InitSingleton(this, ref _Self);
		StartZoomScale=InventoryMapManager.SceneMapEndScale.x;
		InitNewMap(CurMap);
		MonitorSaveValues.Self.UpdateAllUsedValuesOnLoad();
		StopMouseEventsIfMouseOver=false;

		//Handle keys and settings changes
		Conf.PanSpeed				.SettingChanged +=			(_, _) => SetPanSpeed(Conf.PanSpeed);
		Conf.MarkerPanSpeed			.SettingChanged +=			(_, _) => SetMarkerPanSpeed(Conf.MarkerPanSpeed);
		Conf.IconSize				.SettingChanged +=			(_, _) => SetIconSize(Conf.IconSize);
		Conf.ForceDisplayCompass	.SettingChanged += static	(_, _) => DisplayingCompass=Conf.ForceDisplayCompass;
		Conf.MapInAbyss				.SettingChanged +=			(_, _) => ExitMap(true, true, true);
		if(!MapIcon.HasMaterial)
			Conf.Color_FoundIcon	.SettingChanged	+=			(_, _) =>
				(DS==null || GameMap==null ? Enumerable.Empty<Item>() : DS.Items.Values)
					.Where  (static I => I.IsFound && I.CurrentToggleState==CategoryToggleState.All)
					.ForEach(static I => I.MapIcon!.SetIconColor());
		Conf.IconSizeScalesWithZoom	.SettingChanged +=			(_, _) => {
			SetIconSize(Conf.IconSize);
			IconSizeScalesWithZoom=Conf.IconSizeScalesWithZoom;
		};
		IconSizeScalesWithZoom=Conf.IconSizeScalesWithZoom;
	}

	//Called every time the map is opened
	internal static void MapOpenedEvent(GameMap CurMap)
	{
		if(Self==null)
			_=new MapControl(CurMap);
		else if(Self.GameMap!=CurMap)
			Self.InitNewMap(CurMap);
		else
			Self.AllIcons.gameObject.SetActive(true);
	}

	//Called every time the map is opened for the first time in a game save session
	private void InitNewMap(GameMap CurMap)
	{
		GameMap=CurMap;
		PData.hasMarker=true; //Allow player to access marker cursor map

		//Get the MapMarkerMenu from the InventoryMapManager to help with zooming
		MMM=new Reflectors.RField<InventoryMapManager, MapMarkerMenu>(QuickField<InventoryMapManager>("mapManager"), "mapMarkerMenu");

		//Makes sure the "All Icons" group that holds our Icons stays active
		AllIcons=new GameObject("All Icons").transform;
		AllIcons.SetParent(GameMap.transform, false);
		AllIcons.localPosition=new Vector3(0, 0, -3);
		AllIcons.gameObject.SetActive(true);
		(DummyTransform=new GameObject("DUMMY").transform).SetParent(AllIcons);

		SetPanSpeed(Conf.PanSpeed);
		SetMarkerPanSpeed(Conf.MarkerPanSpeed);

		//Update window visibility
		SideBarWasOpen|=Conf.ShowSidebarOnGameLoad;

		//Initializes the data storage and icons
		if(DS!=null) {
			DS.LoadIcons();
			SetIconSize(Conf.IconSize);
			SelectedItem?.MapIcon!.SetSelected(true);
			if(SideBarWasOpen)
				SideBar.Visible=true;
			MarkerLabels.Reload();
			return;
		}
		DS=new DataStorage();
		DS.LoadIcons();
		SetIconSize(Conf.IconSize);

		//Initialize the sidebar and marker labels
		OnNextFrame(() => {
			SideBar=new SideBar(DS);
			MarkerLabels=new MarkerLabels();
			if(SideBarWasOpen)
				SideBar.Visible=true;
		});
	}

	//Called when map is moved to different zoom states
	internal void StateChange(MapStateEnum NewState)
	{
		if(MapState==NewState)
			return;
		MapStateEnum OldState=MapState;
		MapState=NewState;

		if(OldState==MapStateEnum.Marker)
			OnMarkerClosed();
		else if(NewState==MapStateEnum.Closed)
			OnMapClosed();
		else if(OldState==MapStateEnum.Closed)
			OnMapOpened();
	}

	//When opening the map
	private void OnMapOpened()
	{
		//Turn on window visibilities
		if(MarkerLabels!=null)
			MarkerLabels.Visible=true;
		else
			OnNextFrame(() => MarkerLabels!.Visible=true, false);
		Visible=true;
		_=SideBar?.Visible=SideBarWasOpen;
		SearchWindow.Self.Visible=SearchWasOpen;

		//Fill in for auto map
		if(Conf.AutoMap) {
			bool PrevQuill=PData.hasQuill;
			PData.hasQuill=true;
			_=GameManager._instance.UpdateGameMap();
			PData.hasQuill=PrevQuill;
		}

		//Other stuff
		ZoomScale=StartZoomScale; //Store the zoom scale
		Visibility.ForceEvent += ForceCursor_Check; //Check cursor display
		DisplayingCompassI=Conf.ForceDisplayCompass;
		HornetIconAnimators.Init();
	}

	//When closing the map
	private void OnMapClosed()
	{
		//Close the sidebar and search window (and store their visibility)
		SearchWasOpen=SearchWindow.Self.Visible;
		SideBarWasOpen=SideBar.Visible;
		Visible=SideBar.Visible=MarkerLabels.Visible=SearchWindow.Self.Visible=false;
		if(Input.GetKey(KeyCode.Escape)) //Search window can’t catch this because it goes invisible before the OnUpdate() key check
			SearchWasOpen=false;

		//Reset zoom numbers
		ZoomScale=StartZoomScale;
		ZoomI(0);

		Visibility.ForceEvent -= ForceCursor_Check; //Turn off forced mouse for the sidebar
		SetHoverItem(null); //Remove hover item status
		HornetIconAnimators.Close();

		//Fix the marker menu seeming to like to stay open sometimes
		if(MMM.NullSafe?.placementCursor.NullSafe?.activeSelf ?? false)
			MMM.NullSafe?.Close();
	}

	//When exiting marker mode
	private void OnMarkerClosed()
	{
		SetHoverItem(null); //Unmark the hovered icon
		ForceMouseMove=true;
	}

	//Find the closest icon under the zoom marker circle
	internal void CursorMoveEvent() => Misc.IFF(
		MapState==MapStateEnum.Marker, //Only highlight when in marker mode
		() => SetHoverItem(FindClosestItem(MarkerCursorPosI, MarkerCursorRadius))
	);

	//Find the closest item to the map position within a radius
	public Item? FindClosestItem(Vector2 PosOnMap, float SelectionRadius) =>
		FindClosestVector(DS.Items.Values.Where(static I => I.Visible).Select(static I => new VItem<Item>(I.Pos, I)), PosOnMap, IconRadius, SelectionRadius);

	//Find the closest vector to the map position within a radius, returning its item
	public record struct VItem<T>(Vector2 Pos, T Item) where T: class;
	public T? FindClosestVector<T>(System.Collections.Generic.IEnumerable<VItem<T>> VList, Vector2 PosOnMap, float ItemRadius, float SelectionRadius) where T: class
	{
		//Find the closest item
		VItem<T> ClosestVec=default;
		float ClosestVecDistance=9999f, CurDistance;
		foreach(var VItem in VList) {
			if((CurDistance=(PosOnMap-VItem.Pos).magnitude)>=ClosestVecDistance)
				continue;
			ClosestVec=VItem;
			ClosestVecDistance=CurDistance;
		}
		float CurItemRadius=Conf.IconSize*ItemRadius;
		CurItemRadius=(Conf.IconSizeScalesWithZoom ? ToZoomOut(CurItemRadius) : CurItemRadius);
		return ClosestVecDistance>ToZoomOut(SelectionRadius)+CurItemRadius ? null : ClosestVec.Item;
	}

	//Set the hovered item
	private void SetHoverItem(Item? ClosestItem)
	{
		if(ClosestItem==HoverItem)
			return;

		//Deselect the previous hover item and select the new one
		HoverItem?.MapIcon!.SetHovered(false);
		ClosestItem?.MapIcon!.SetHovered(true);
		HoverItem=ClosestItem;
	}

	//Get the position of the marker cursor
	public  Vector2 MarkerCursorPos  => MMM!=null ? MarkerCursorPosI : Vector2.zero;
	private Vector2 MarkerCursorPosI =>
		ToZoomOut(MMM.placementCursor.transform.position-GameMap.transform.localPosition);
	//Derived from MapMarkerMenu.PlaceMarker()
	//MMM.placementBox.transform.parent=MMM.placementCursor.transform;
	//return GameMap.transform.InverseTransformPoint(MMM.placementBox.transform.position);

	//Handle key presses
	private readonly SilkDev.DevInput.InputRepeatDelay<float> ZoomCheck=new(0,
		(Conf				.Shortcut_ZoomOut	, -1),
		(Conf				.Shortcut_ZoomIn	,  1),
		(false, Direction	.Up					,  1),
		(false, Direction	.Down				, -1)
	);
	protected override void OnUpdate()
	{
		//Toggling sidebar and centering
		InControl.InputDevice AD=ActiveDevice;
		if(Conf.Shortcut_ToggleSideBar.IsDown() || AD.LeftCommand.WasPressed)
			SideBar.Visible=!SideBar.Visible;
		if(
			Conf.Shortcut_CenterOverChar.IsDown() || (
				AD.Action3.WasPressed &&
				MapState==MapStateEnum.Open
			)
		)
			CenterOverCharacterI();

		//Select a new item
		if(
			   MapState==MapStateEnum.Marker			//Must be in marker mode
			&& HoverItem?.MapIcon!=null					//An item is hovered and has an icon
			&&  (  Conf.Shortcut_SB_SelectIcon.IsDown()	//Selected icon shortcut is pressed or...
				|| AD.Action4.WasPressed)				//Y button was pressed
		)
			SelectItemI(HoverItem);

		//Zooming
		if(ZoomCheck.IsReadyValueVType is float ZoomAmount)
			ZoomI(ZoomAmount);

		HornetIconAnimators.Run();
	}

	//Zoom in or out
	public  void Zoom (float Amount) => Misc.IFF(GameMap!=null, () => ZoomI(Amount));
	private void ZoomI(float Amount) =>
		ZoomTowardsPointI(
			Amount,
				  MapState!=MapStateEnum.Marker ? MapPos
				: MarkerCursorPosI,
			MapState==MapStateEnum.Marker
		);

	//Zoom towards a given point
	public  void ZoomTowardsPoint (float Amount, Vector2 ZoomAroundPoint) => Misc.IFF(GameMap!=null, () => ZoomTowardsPointI(Amount, ZoomAroundPoint, false));
	private void ZoomTowardsPointI(float Amount, Vector2 ZoomAroundPoint, bool UseHoveredItem)
	{
		//Set the hover item as the center if toggled
		if(UseHoveredItem && Conf.IconSizeScalesWithZoom && HoverItem!=null)
			ZoomAroundPoint=HoverItem.Pos;

		//Determine how much to zoom
		float ZoomChange=(float)Mathf.Pow(Conf.ZoomSpeed, Mathf.Abs(Amount));
		bool ZoomingOut=(Amount<0);
		if(ZoomingOut) //Negative means zoom out, which is a division instead of a multiplication
			ZoomChange=1/ZoomChange;

		//Get the new zoom and set it on the map
		Vector2 OldZoomedInPoint=ToZoomIn(ZoomAroundPoint);
		ZoomScale*=ZoomChange;
		Vector3 NewScaleVector=new(ZoomScale, ZoomScale, 1);
		GameMap.transform.localScale=NewScaleVector;

		//Keep the zoom point where it is
		MapPos+=ZoomAroundPoint-ToZoomOut(OldZoomedInPoint);

		//Update the readonly scales for the different zoomed mapped states. This way it stays at the scale we want it in between marker zooms
		IMM_SceneMapEndScale.Set(NewScaleVector);
		IMM_SceneMapMarkerZoomScale.Set(NewScaleVector);
	}

	//Handle transformations between local and world coordinates (zoomed in and out)
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public Vector2 ToZoomOut(Vector2	Pos) => Pos/ZoomScale; //Normalize a zoomed in position
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public float   ToZoomOut(float	Pos) => Pos/ZoomScale; //Normalize a zoomed in distance
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public Vector2 ToZoomIn (Vector2	Pos) => Pos*ZoomScale; //Recast a normalized position to its zoomed in position
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public float   ToZoomIn (float	Pos) => Pos*ZoomScale; //Recast a normalized distance to its zoomed in distance
	public Vector2 MapPos
	{
	[MethodImpl(MethodImplOptions.AggressiveInlining)] get									 => ToZoomOut(-GameMap.transform.localPosition);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] set									 => GameMap.transform.SetLocalPosition2D(ToZoomIn(-value));
	}

	//Set the pan speeds
	public void SetPanSpeed(float PanSpeed) =>
		QuickFieldT<float>("panSpeed").Set(PanSpeed);
	public void SetMarkerPanSpeed(float MarkerPanSpeed) =>
		MMM.panSpeed=MarkerPanSpeed;

	//Set the size of icons
	public void SetIconSize(float IconSize)
	{
		if(DS==null || GameMap==null)
			return;

		//Set our icons sizes
		float NewScaleSize=(Conf.IconSizeScalesWithZoom ? ToZoomOut(IconSize) : IconSize);
		foreach(Item Item in DS.Items.Values)
			Item.MapIcon!.UpdateSize(NewScaleSize);

		//Set the marker sizes for the rest of the damn engine
		Vector3 NewScaleVector=new(NewScaleSize, NewScaleSize, 1);
		(GameMap.transform.Find("Compass Icon")?.transform ?? DummyTransform).localScale=NewScaleVector;
		foreach(Transform Child in GameMap.transform.Find("Map Markers")?.transform ?? DummyTransform)
			Child.localScale=NewScaleVector;
		foreach(MapPin GamePin in GameMap.GetComponentsInChildren<MapPin>())
			GamePin.transform.localScale=NewScaleVector;
	}

	//Move map to center over your character
	public  void CenterOverCharacter () => Misc.IFF(GameMap!=null, CenterOverCharacterI);
	private void CenterOverCharacterI() => MapPos=CharacterPositionI;

	//Get character position
	public   Vector2 CharacterPosition  => GameMap!=null ? CharacterPositionI : Vector2.zero;
	internal Vector2 CharacterPositionI =>
		new Reflectors.RMethod<GameMap, Vector2>(GameMap, "GetMapPosition").Invoke(
			(Vector2)HeroController.instance.transform.position,
			QuickField<GameMapScene>("currentScene"),
			QuickField<GameObject>("currentSceneObj"),
			QuickField<Vector2>("currentScenePos"),
			QuickField<Vector2>("currentSceneSize")
		);

	//Selects a new item
	public  void SelectItem (Item? NewSelectItem) => Misc.IFF(GameMap!=null, () => SelectItemI(NewSelectItem));
	private void SelectItemI(Item? NewSelectItem)
	{
		//If the same item, nothing to do
		if(SelectedItem==NewSelectItem)
			return;

		//Force the icon to be invisible if it’s not supposed to be visible (if previously selected through the search window)
		if(!(SelectedItem?.Visible ?? false) && (SelectedItem?.MapIcon?.IconGO?.activeSelf ?? false))
			SelectedItem.MapIcon.IconGO.SetActive(false);

		SelectedItem?.MapIcon!.SetSelected(false);
		NewSelectItem?.MapIcon!.SetSelected(true);
		SelectedItem=NewSelectItem;
		SideBar.OnNewIconSelected();
		SideBar.Visible=true;
	}

	//Center over and select an item by its ID
	public   void SelectAndCenterItem (int ItemID) => Misc.IFF(GameMap!=null, () => SelectAndCenterItemI(ItemID));
	internal void SelectAndCenterItemI(int ItemID)
	{
		Item I=DS.Items.Get(ItemID) ?? throw new System.ArgumentOutOfRangeException("ItemID");
		MapPos=I.Pos;
		SelectItemI(I);
		I.MapIcon?.IconGO?.SetActive(true); //Force the icon to be visible
	}

	//Handle mouse events
	protected override void OnMouseEvent(Event Ev)
	{
		//Zoom in or out around the mouse cursor
		const int ScrollWheelInterval=3;
		if(Event.current.type==EventType.ScrollWheel && Event.current.delta.y!=0)
			ZoomTowardsPointI(Event.current.delta.y/-ScrollWheelInterval, MouseCursorWorldPositionI, true);

		//Drag the window (only use MouseUp if dragging occurred)
		bool HasMagnitude=(Drag.IsDragging && Drag.Delta.magnitude>2); //Allow up to 2 pixel moves
		switch(Drag.UpdateState(new Rect(Vector2.zero, Screen.Size), false)) {
			case Dragger.State.None:
				break;
			case Dragger.State.Start:
				StartDragPos=MapPos;
				Ev.Use();
				break;
			case Dragger.State.Dragging:
				MapPos=StartDragPos-ToZoomOut(Drag.Delta*DragScale);
				Ev.Use();
				break;
			case Dragger.State.Done:
				if(HasMagnitude)
					Ev.Use();
				StartDragPos=Vector2.zero;
				break;
		}

		//Check for mouse over for hover selection
		if(
			   (Ev.type==EventType.MouseMove)
			|| (Ev.type==EventType.MouseUp && Button.CurrentButton==Button.Enum.Left)
		) {
			SetHoverItem(FindClosestItem(MouseCursorWorldPositionI, 0));

			//Set select item
			if(Ev.type==EventType.MouseUp)
				SelectItemI(HoverItem);

			//If item is hovered then consider this event used
			if(HoverItem!=null) {
				MarkerLabels.ClearLastMarkerOver();
				Ev.Use();
			}
		}

		//Check for right mouse click for marker add/remove
		if(Ev.type!=EventType.MouseUp || Button.CurrentButton!=Button.Enum.Right)
			return;

		//Move the marker to the mouse position
		void RunMarkerAction()
		{
			void MoveMarker() => MMM.placementCursor.transform.SetPosition2D(ToZoomIn(MouseCursorWorldPositionI-MapPos));
			MoveMarker();

			//The map, for some reason, stops the cursor from moving all the way in 1 frame if it was previously over another cursor. So we have to move it twice.
			OnNextFrame(
				() => {
					MoveMarker();
					OnNextFrame(
						() => new Reflectors.RMethod<MapMarkerMenu, object>(MMM, MMM.collidingMarkers.Count>0 ? "RemoveMarker" : "PlaceMarker").Invoke(),
						false
					);
				},
				false
			);
		}

		//If the marker menu is not yet open then open it and wait a frame to move the marker cursor
		if(MapState==MapStateEnum.Marker)
			RunMarkerAction();
		else {
			MMM.Open();
			OnNextFrame(RunMarkerAction, false);
		}
	}

	//Window stuff
	protected override bool IsMouseOverWindow(Vector2 _) => true; //Watches the entire screen
	protected override void DoLayout(int ID, Event Ev) {}

	//Get the world position of the mouse cursor
	public  Vector2 MouseCursorWorldPosition  => GameMap!=null ? MouseCursorWorldPositionI : Vector2.zero;
	private Vector2 MouseCursorWorldPositionI =>
		ToZoomOut(Camera.allCameras[0].ScreenToWorldPoint(Input.mousePosition))+MapPos;

	//Get the user out of the map
	public void ExitMap(bool ExitMarkerMode, bool ExitMap, bool ExitOverviewMap)
	{
		if(ExitMarkerMode && MapState==MapStateEnum.Marker)
			MMM.NullSafe?.Close();
		if(ExitMap && MapState!=MapStateEnum.Closed && GameMap!=null)
			QuickField<InventoryMapManager>("mapManager").ZoomOut();
		if(ExitOverviewMap)
			CloseInventory();
	}

	//Unlock all maps and markers
	public void UnlockAllMaps()
	{
		AllMapItems.ForEach(static F => F.SetValue(PData, true));
		ExitMap(true, true, false);
		OnNextFrame(SideBar.FixUnlockedButtons);
	}

	public bool AreAllMapsUnlocked() =>
		AllMapItems.Count(static F => !(bool)F.GetValue(PData))==0;

	private FieldInfo[] AllMapItems =>
		[.. PData.GetType().GetFields().Where(static F =>
			System.Text.RegularExpressions.Regex.IsMatch(F.Name, @"^(Has\w+Map|hasPin(?!Flea)|HasSeenMapUpdated)")
		)];

	private FieldInfo[] AllGameMarkers =>
		[.. PData.GetType().GetFields().Where(static F => F.Name.StartsWith("hasMarker"))];
	public bool AreAllGameMarkersUnlocked() =>
		AllGameMarkers.Count(static F => !(bool)F.GetValue(PData))==0;
	public void UnlockAllGameMarkers()
	{
		AllGameMarkers.ForEach(static F => F.SetValue(PData, true));
		SideBar.FixUnlockedButtons();
		ExitMap(true, false, false);
	}

	//Check to see if the cursor needs to be forced
	private bool ForceCursor_Check() =>
		Conf.ShowMouseWhenSBVisible && ((SideBar?.Visible ?? false) || (SearchWindow.Self?.Visible ?? false));

	//Toggle showing if icons have been found yet
	public bool ShowLinkedStatus
	{
		get;
		set {
			if(field==value)
				return;
			field=value;
			foreach(Item Item in DS.Items.Values)
				if(!Item.IsLinked)
					Item.MapIcon!.SetIconColor();
			SideBar.FixUnlockedButtons();
		}
	}

	//Close out the inventory screen
	public static void CloseInventory() => EventRegister.SendEvent("INVENTORY CANCEL");

	//Compass visibility
	internal bool DisplayingCompassI
	{
		get => QuickField<bool>("displayingCompass");
		set {
			bool IsCompassEquipped=(GlobalSettings.Gameplay.CompassTool.NullSafe?.IsEquipped ?? false);
			bool NewVal=(value || IsCompassEquipped) && !GameMap.IsLostInAbyssPreMap();
			QuickFieldT<bool>("displayingCompass").Set(NewVal);
			QuickField<GameObject>("compassIcon").SetActive(NewVal);
		}
	}
	public static bool DisplayingCompass
	{
		get => Self?.GameMap!=null && Self.DisplayingCompassI;
		set => Misc.IFF(Self?.GameMap!=null, () => Self!.DisplayingCompassI=value);
	}

	//Convert map coordinates to screen coordinates
	public   Vector2 MapPositionToScreenCoords (Vector2 Pos) => GameMap!=null ? MapPositionToScreenCoordsI(Pos) : Vector2.zero;
	internal Vector2 MapPositionToScreenCoordsI(Vector2 Pos)
	{
		Vector2 ScreenPoint=Camera.allCameras[0].WorldToScreenPoint(ToZoomIn(Pos-MapPos));
		ScreenPoint.y=Screen.height-ScreenPoint.y;
		return ScreenPoint;
	}

	//Reflecter stuff
	private T QuickField<T>(string FieldName) => QuickFieldT<T>(FieldName).Get();
	private Reflectors.RField<GameMap, T> QuickFieldT<T>(string FieldName) => new(GameMap, FieldName);
}