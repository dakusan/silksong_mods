using BepInEx.Configuration;
using SilkDev.DevInput.Mouse;
using SilkDev.Events;
using SilkDev.Textures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using OA=System.ObsoleteAttribute;

//TODO: Catch events before UniverseLib so we can cancel events to their focused windows.
//TODO: Catch all windows and insert them into the chain, even if they aren’t made as Windows.

/*
OnGUI ordering is as follows:
Update (Non-OnGUI)
	OnUpdate() (Windows: AlwaysCallUpdate|Visible)
	  - You should take care of GUI changing events here when possible
Layout (Sometimes the loop can restart and layout can be called again for the frame)
	Update delayed setting of window visibility
	Update window ordering via Priority
	PreOnGUI() [Layout] (Windows: AlwaysCallPreOnGUI|Visible)
	OnNextFrame functions (CallBeforeLayout=true)
	  - Functions are cached before the calls start, so new NextFrame functions will not run until the actual next frame
	OnMouseEvent() (Custom events: MouseMove|MouseEnterWindow|MouseLeaveWindow|TouchStationary)
	  - CurWinOver static member is determined here
	  - Won’t be called a second time if Layout is called again as delta will read as zero
	  - TouchStationary event is not called here. It is called any time HasMouseFocus() is called.
	  - NOTES: Only for IsMouseOverWindow() windows in LastPaintOrder. Stops when event is used or if AlwaysCallPreOnGUI.
	IsInDrawPrepPhase=true
	  - Ater this, Visible changes delayed until the next frame
	  - Do not make changes to the GUI state after this that would affect the number of drawn GUI objects
	GUI.Window [Layout] (Visible windows)
		OnLayout()
		DrawPhase()
Next received OnGUI event
	PreOnGUI() [...]
	OnNextFrame functions (CallBeforeLayout=false)
	...
Input Events (Mouse and keyboard events are run in the order received by Unity)
	Keyboard
		PreOnGUI() [KeyUp|KeyDown]
		GUI.Window [KeyUp|KeyDown]
			OnKeyPress()
			DrawPhase()
	Mouse or ScrollWheel
		PreOnGUI() [MouseEvent]
		OnMouseEvent() (Engine supplied mouse events)
		  - See NOTES for previous OnMouseEvent()
		GUI.Window [MouseEvent] (All Visible windows)
			DrawPhase()
Repaint
	PreOnGUI() [Repaint]
	IsInDrawPrepPhase=false
	  - Users should not be making GUI changes during Repaint anyways.
	Clear window order list
	GUI.Window [Repaint]
		Add to window order list
		DrawPhase()

DrawPhase() (inside GUI.Window):
	Draw close button (unless HasNoStyling) and run CloseButton() if clicked
	If UnboundDraw: Remove window clipping
	DoLayout()
	If UnboundDraw: Restore window clipping
	Resize and move window via drag
*/

namespace SilkDev.Windows;

/* Abstract class for a Unity based GUI.Window. Features:
	* Makes sure windows have a unique ID and custom handle all mouse events in order of zOrder. All other events are processed naturally.
	* Mouse events are only called if the mouse is over the window, or it is dragging. Also adds MouseMove, MouseEnterWindow, and MouseLeaveWindow.
	* Safe window moving and resizing.
	* Optionally saves/restores window position via a ConfigEntry.
	* Has a close button with optional event action.
	* Can give priority that sets windows to bottom or topmost.
	* Takes into account UniverseLib (Unity Explorer) windows at Priority=-100 since they do not cancel the mouse themselves.
	* Strict event call ordering by window order and priority. Full event system call ordering available at the top of this file.
	* Options to call PreOnGUI and Update even if not visible.
	* Fake windows can be created just for mouse handling.
	* Overridable event callbacks for GameEvents and OnGUI event types.
*/
public abstract class Window
{
	//Each window needs its own ID so Unity can order windows
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0025:Use expression body for property", Justification = "Invalid due to auto-property accessor")]
	protected static int GetNextID { get => field++; } = 57819; //Random large ID so as to not conflict with other windows
	public readonly int ID=GetNextID;

	//Toggle visibility. If in the middle of a draw phase it will wait until the very beginning of the next frame to change it.
	public static bool IsInDrawPrepPhase{ get; private set; } = false;
	public bool NextFrameVisibility		{ get; private set; } = false;
	private bool _Visible=false;
	public virtual bool Visible {
		get => _Visible;
		set {
			if(!IsInDrawPrepPhase)
				_Visible=value;
			NextFrameVisibility=value;
		}
	}

	//Styling stuff
	public readonly bool HasNoStyling=false; //If the default empty constructor was called, this window has no styling
	public GUIStyle? GUIStyle=null; //Set to GUIStyle.none to draw your own window completely. Uses GUI.skin.window if null.
	public bool UnboundDraw=false; //If true, you can draw outside the window bounds
	public bool IsClosed { get; private set; } = false; //If the window is closed
	public int CloseButtonSize=15, CloseButtonPadding=2;
	public string Title;
	public Texture2D? BGTex { //For now this is only kept here as an automatically disposable texture. It’s not used elsewise.
		get;
		set {
			field?.TDestroy();
			field=value;
		}
	} = null;

	//Resizing/Moving
	protected readonly ConfigEntry<Rect>? SavePosConfig;
	public readonly ResizeDragControl? Resizer=new();
	public Rect WindowRect; //For fake windows or windows that you want to ignore the mouse set this to Rect.zero and override IsMouseOverWindow(). Otherwise Unity will block your mouse use.

	//Event calling flags
	public int Priority=0; //Priority 0 windows are ordered naturally by Unity window focus. Other windows are sent to the front (if positive) or back (if negative) in descending value order during the first Layout call to the plugin. UniverseLib windows have priority -100.
	public bool AlwaysCallPreOnGUI=false; //If true, the PreOnGUI() function is called even if not visible
	public bool AlwaysCallUpdate=false; //If true, the Update() function is called even if not visible

	//Initialization
	protected Window(string Title, ConfigEntry<Rect>? ConfigValue=null, int DefaultWidth=800, int DefaultHeight=400) : this(Title, -1)
	{
		WindowRect=new Vector2(DefaultWidth, DefaultHeight).CenterIn(Screen.Size); //Centers window if no config value given

		_=Resizer?.SaveEntry=ConfigValue;
		if((SavePosConfig=ConfigValue)?.Value!=null) {
			WindowRect=ConfigValue!.Value;
			if(
				WindowRect==Rect.zero //When rect is zero, that means it has not been set yet, so for default, center on the screen using the given default width/height
				&& DefaultWidth!=0 && DefaultHeight!=0 //If default width or height is 0, that signifies that the derived window wishes to set their own default dimensions. Width and height will stay at 0 for the derived window to check.
			)
				WindowRect=new Vector2(DefaultWidth, DefaultHeight).CenterIn(Screen.Size);
			Resizer?.CheckWindowRect(ref WindowRect);
		}
	}
	protected Window(string Title, bool Visible, int Priority, bool AlwaysCallUpdate=false) : this(Title, -1) => //A window with no styling. Sets WindowRect to Rect.zero by default.
		(HasNoStyling, GUIStyle, Resizer, WindowRect, this.Visible, this.Priority, this.AlwaysCallUpdate)=(true, GUIStyle.none, null, Rect.zero, Visible, Priority, AlwaysCallUpdate);
	private Window(string Title, int _) //Only callable by other constructors
	{
		this.Title=Title;
		OnNextFrame(() => {
			WinList.Add(this);
			WinOrderList.Add(this);
			OFCall(OFuncs.OnInit);
		});
		FillOverriddenEvents();
	}

	//Handle custom mouse events
	public static System.Collections.ObjectModel.ReadOnlyCollection<Window> GetWindowEventOrder => WinOrderList.AsReadOnly(); //Order of windows during previous frame repaint, used to determine send order of mouse events. This is undefined during Repaint
	private static readonly List<Window> WinOrderList=[]; //See GetWindowEventOrder
	private static Vector2 LastFrameMouseCoord=Vector2.zero; //Event.delta does not reset so keep track of this so we can create our own delta
	public static Window? CurWinOver { get; private set; } = null; //Used to send MouseEnterWindow and MouseLeaveWindow events
	public static bool ForceMouseMove=false; //If true, the next time a mouse move event would be sent, it is forced
	public bool StopMouseEventsIfMouseOver=true; //If true, mouse events will still be stopped even if the OnMouseEvent() function does not call Event.Use()

	//Required override functions
	protected abstract void DoLayout(int ID, Event Ev);						 //Visible.AllDrawEvents.WindowOnGUI: After below virtual event functions are called

	//Do not call attributes and functions
	private const string DN="Do not call base (do override though!)";
	private const bool T=true;
	private static bool DNC([System.Runtime.CompilerServices.CallerMemberName] string Caller=Misc.Empty) { throw new NotImplementedException($"Do not call the base implementation of {Caller}()"); }

	//Functions expected to be overwritten without calling a base. Comments are in format “WhichWindows.Events.Order: CallTime”.
	[OA(DN,T)] protected virtual void OnUpdate()				  =>DNC(); //(AlwaysCallUpdate|Visible).Update.LastPaintOrder: When plugin receives the Update() event. You should take care of GUI changing events here when possible.
	[OA(DN,T)] protected virtual void PreOnGUI		(Event Ev	 )=>DNC(); //(AlwaysCallPreOnGUI|Visible).AllDrawEvents.LastPaintOrder: When plugin receives the event OnGUI() call (before GUI.Window is called)
	[OA(DN,T)] protected virtual void OnLayout		(Event Ev	 )=>DNC(); //Visible.Layout.WindowOnGUI: Window.OnGUI
	[OA(DN,T)] protected virtual void OnKeyPress	(Event Ev	 )=>DNC(); //Visible.(KeyDown|KeyUp).WindowOnGUI: Window.OnGUI
	[OA(DN,T)] protected virtual void OnRepaint		(Event Ev	 )=>DNC(); //Visible.Repaint.WindowOnGUI: Window.OnGUI
	[OA(DN,T)] protected virtual void OnGameLoaded	(int SaveSlot)=>DNC(); //All.GameLoaded.Any: Immediately
	[OA(DN,T)] protected virtual void OnGameSaved	(int SaveSlot)=>DNC(); //All.GameSaved.Any: Immediately
  /*[OA(DN,T)] protected virtual void OnMouseEvent	(Event Ev)↓↓↓↓=>DNC()*///(Visible&IsMouseOverWindow).(isMouse|isScrollWheel).LastPaintOrderReverse: Immediately. Also see StopMouseEventsIfMouseOver.
	//Custom mouse events (MouseMove|MouseEnterWindow|MouseLeaveWindow) are called when the Plugin receives Layout, before the pre-draw phrase starts.
	//TouchStationary event calls are during HasMouseFocus().
	[OA(DN,T)] protected virtual void OnMouseEvent	(Event Ev	 )=>DNC();
	[OA(DN,T)] protected virtual void OnInit		(			 )=>DNC(); //Called at the beginning of the next frame

	//Functions that have default code that can be overwritten
	protected virtual void CloseButton() => Close(); //Called when the close button is clicked. If you just want to hide it, change this to => Visible=false;
	protected virtual bool IsMouseOverWindow(Vector2 MPos) => //Called to determine if the mouse is over this window
		Visible && WindowRect.Contains(MPos);

	//Make sure to call the base
	public virtual void Close()
	{
		if(IsClosed)
			return;
		AlwaysCallPreOnGUI=AlwaysCallUpdate=Visible=false;
		IsClosed=true;
		OnNextFrame(() => {
			BGTex=null;
			if(!WinList.Remove(this))
				Log.Error($"Window deletion failed: {Title}");
			if(!WinOrderList.Remove(this))
				Log.Error($"Window order deletion failed: {Title}");
		});
	}

	//Execute the beggining and ending layout stuff
	private void LayoutWrapper(int ID)
	{
		//On repaint fix the window order
		Event CurEv=Event.current;
		if(CurEv.type==EventType.Repaint) {
			if(Visible)
				OFCall(OFuncs.OnRepaint, CurEv);
			WinOrderList.Add(this);
		}

		//If this window is not visible, exit here
		if(!Visible)
			return;

		//Send input events
		if(CurEv.type==EventType.Layout)
			OFCall(OFuncs.OnLayout, CurEv);
		else if(CurEv.isKey)
			OFCall(OFuncs.OnKeyPress, CurEv);

		//Show the close button and draw the rest of the layout
		if(!HasNoStyling && GUI.Button(new Rect(WindowRect.width-CloseButtonSize-CloseButtonPadding, CloseButtonPadding, CloseButtonSize, CloseButtonSize), "X"))
			Catcher.Run(() => $"“{Title}”.{nameof(CloseButton)}", CloseButton);

		if(UnboundDraw)
			GUI.EndClip();
		Catcher.Run(() => $"“{Title}”.{nameof(DoLayout)}", () => DoLayout(ID, CurEv));
		if(UnboundDraw)
			GUI.BeginClip(WindowRect);

		//Handle moving and resizing
		Resizer?.OnDraw(ref WindowRect);
		Resizer?.HandleDrag(ref WindowRect);
	}

	//Initialize static events
	static Window()
	{
		//Run events
		GameEvents.OnUpdate += (
			() =>
				WinOrderList.ForEach(static Win => Misc.IFF(
					Win._Visible || Win.AlwaysCallUpdate,
					() => Win.OFCall(OFuncs.OnUpdate)
				)),
			-3000 //Let all other update events run first
		);
		GameEvents.OnGameLoaded += static SaveSlot =>
			WinList.ForEach(Win => Win.OFCall(OFuncs.OnGameLoaded, SaveSlot));
		GameEvents.OnGameSaved += static SaveSlot =>
			WinList.ForEach(Win => Win.OFCall(OFuncs.OnGameSaved, SaveSlot));

		OnNextFrame(static () => {
			_=new BlockWindows_UnityExplorer(-100);
			_=new BlockWindows_BepInExConfigManager(-100);
		}, false);
	}

	//Excute actions once during the next frame on Layout
	private static readonly List<(bool CallBeforeLayout, Action Action)> NextFrameList=[], ThisFrameList=[];
	public static void OnNextFrame(Action A, bool CallBeforeLayout=true) => //Always called immediately after Layout.PreOnGUI. If CallBeforeLayout=true then called during Layout. Otherwise, when the next non-layout event is received
		NextFrameList.Add((CallBeforeLayout, A));

	//Handle executing window events
	private static readonly List<Window> WinList=[];
	internal static void Handle_OnGUI()
	{
		//If the loop reset before repaint, get everything back into order
		Event CurEv=Event.current;
		if(CurEv.type==EventType.Layout && IsInDrawPrepPhase) {
			IsInDrawPrepPhase=false;
			if(ThisFrameList.Count!=0) {
				NextFrameList.InsertRange(0, ThisFrameList);
				ThisFrameList.Clear();
			}
		}

		//On Layout, update window visibility states
		if(CurEv.type==EventType.Layout)
			foreach(Window Win in WinOrderList)
				Win._Visible=Win.NextFrameVisibility;

		//On Layout, reorder windows with Priority!=0
		if(CurEv.type==EventType.Layout)
			foreach(Window Win in WinList.AsEnumerable().Where(static W => W.Priority!=0).OrderBy(static W => Math.Abs(W.Priority)).ToList()) {
				_=WinOrderList.Remove(Win);
				WinOrderList.Insert(Win.Priority<0 ? 0 : WinOrderList.Count, Win);
				if(Win.Priority<0)
					GUI.BringWindowToBack(Win.ID);
				else
					GUI.BringWindowToFront(Win.ID);
			}

		//Call OnGUI for all (Visible|AlwaysCallPreOnGUI) windows
		foreach(Window Win in WinOrderList)
			if(Win._Visible || Win.AlwaysCallPreOnGUI)
				Win.OFCall(OFuncs.PreOnGUI, CurEv);

		//On layout...
		if(CurEv.type==EventType.Layout) {
			//Call BeforeLayout NextFrame functions
			var CurList=NextFrameList.ToArray();
			NextFrameList.Clear();
			ThisFrameList.AddRange(CurList.AsEnumerable().Where(static Ev => {
				if(Ev.CallBeforeLayout)
					Ev.Action();
				return !Ev.CallBeforeLayout;
			}));

			HandleMouseMove(CurEv); //Call custom mouse events
			IsInDrawPrepPhase=true; //Set in draw-preparing state
		}

		//Call AfterLayout NextFrame events
		if(CurEv.type!=EventType.Layout && ThisFrameList.Count>0) {
			ThisFrameList.ForEach(static Ev => Ev.Action());
			ThisFrameList.Clear();
		}

		//On repaint clear the list WinOrderList so it can be filled in again
		if(CurEv.type==EventType.Repaint) {
			if(!IsInDrawPrepPhase)
				Log.Error("Was repaint called twice in a row?!?");
			IsInDrawPrepPhase=false; //No longer in preparation state. Users should not be making changes during Repaint anyways.
			WinOrderList.Clear();
		}

		//Manually call mouse events
		if(CurEv.isMouse || CurEv.isScrollWheel)
			SendMouseEvents(CurEv);

		//Send window layouts
		foreach(Window Win in WinList)
			_=GUI.Window(
				Win.ID,
				!Win.Visible ? Rect.zero : Win.WindowRect,
				Win.LayoutWrapper,
				!Win.Visible || Win.HasNoStyling ? Misc.Empty : Win.Title,
				!Win.Visible ? GUIStyle.none : Win.GUIStyle ?? GUI.skin.window);
	}

	//Send custom mouse events to windows
	private static void HandleMouseMove(Event CurEv)
	{
		//If mouse has not moved, or no mouse, nothing to do
		Vector2 MouseMoveVec=CurEv.mousePosition-LastFrameMouseCoord;
		LastFrameMouseCoord=CurEv.mousePosition;
		if(!Visibility.IsVisible || (MouseMoveVec.magnitude<.001f && !ForceMouseMove))
			return;
		ForceMouseMove=false;

		//Handle detecting the first window to send Mouse(Enter|Leave)Window events
		bool HasFoundFirstWindow=false;
		bool HandleFirstWindow(Window Win) {
			if(HasFoundFirstWindow)
				return false;
			HasFoundFirstWindow=true;
			if(CurWinOver==Win)
				return false;
			CurWinOver?.OFCall(OFuncs.OnMouseEvent, new Event(CurEv) { type=EventType.MouseLeaveWindow });
			(CurWinOver=Win).OFCall(OFuncs.OnMouseEvent, new Event(CurEv) { type=EventType.MouseEnterWindow });
			return false;
		};

		//Send the mouse events
		SendMouseEvents(new Event(CurEv) { type=EventType.MouseMove, delta=MouseMoveVec }, HandleFirstWindow);

		//Handle sending Enter/Leave events
		if(HasFoundFirstWindow || CurWinOver==null)
			return;
		CurWinOver?.OFCall(OFuncs.OnMouseEvent, new Event(CurEv) { type=EventType.MouseLeaveWindow });
		CurWinOver=null;
	}

	//Sends TouchStationary events to windows to determine which windows the mouse is over
	public bool HasMouseFocus { get {
		bool FoundSelf=false;
		SendMouseEvents(
			new Event() { type=EventType.TouchStationary, mousePosition=Event.current.mousePosition },
			Win => FoundSelf=(Win==this)
		);
		return FoundSelf;
	} }

	//Send mouse events. If BeforeEventSent is true the function stops (as if the event had been used).
	private static void SendMouseEvents(Event CurEv, Func<Window, bool>? BeforeEventSent=null)
	{
		foreach(Window Win in WinOrderList.AsEnumerable().Reverse()) {
			//Only process if actually over the window
			try {
				if(!Win.Visible || !Win.IsMouseOverWindow(CurEv.mousePosition))
					continue;
			} catch(Exception e) {
				Catcher.OutputException($"“{Win.Title}”.{nameof(IsMouseOverWindow)}.{CurEv.type}", e);
				continue;
			}

			//Call before event sent
			if(BeforeEventSent?.Invoke(Win) ?? false)
				return;

			//Send event
			Win.OFCall(OFuncs.OnMouseEvent, CurEv);
			if(CurEv.type==EventType.Used || Win.StopMouseEventsIfMouseOver) {
				if(CurEv.type!=EventType.Used)
					CurEv.Use();
				return;
			}
		}
	}

	//Calling events
	private readonly Delegate?[] OverriddenFuncs=new Delegate?[(int)OFuncs.NUM_ENUMS];
	private enum OFuncs
	{
		OnUpdate=0,
		OnGameLoaded,
		OnGameSaved,
		OnInit,
		NUM_NON_DRAW_ENUMS,

		PreOnGUI=NUM_NON_DRAW_ENUMS,
		OnLayout,
		OnKeyPress,
		OnMouseEvent,
		OnRepaint,
		NUM_ENUMS,
	}
	private void FillOverriddenEvents() =>
		Enumerable.Range(0, (int)OFuncs.NUM_ENUMS).ForEach(i => {
			MethodInfo MI=GetType().GetMethod(((OFuncs)i).ToString(), BindingFlags.Instance|BindingFlags.NonPublic);
			if(MI==null || MI.DeclaringType==typeof(Window))
				return;

			OverriddenFuncs[i]=
				MI.CreateDelegate(
					  i>=(int)OFuncs.NUM_NON_DRAW_ENUMS ? typeof(Action<Event>)
					: (i is (int)OFuncs.OnUpdate or (int)OFuncs.OnInit) ? typeof(Action)
					: typeof(Action<int>)
					, this
				);
		});
	private void OFCall(OFuncs OF) {
		if(OverriddenFuncs[(int)OF] is Action A)
			Catcher.Run(() => $"“{Title}”.{OF}", A);
	}
	private void OFCall(OFuncs OF, Event CurEv) {
		if(OverriddenFuncs[(int)OF] is Action<Event> A)
			Catcher.Run(() => $"“{Title}”.{OF}.{CurEv.type}", () => A(CurEv));
	}
	private void OFCall<T>(OFuncs OF, T Val) {
		if(OverriddenFuncs[(int)OF] is Action<T> A)
			Catcher.Run(() => $"“{Title}”.{OF}.{Val}", () => A(Val));
	}
}