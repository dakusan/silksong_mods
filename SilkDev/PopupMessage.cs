using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilkDev;

//Popup message
public class PopupMessage
{
	private static Texture2D BorderTex=null!, BackgroundTex=null!;
	private readonly DateTime InitTime=DateTime.Now;
	private DateTime CloseTime=DateTime.MinValue;

	protected const string PressAnyKeyString="<color=red><size=20>Press any key to close this message.</size></color>";
	protected static readonly DrawPopups DW=new();

	public string Message;
	public int FullWidth=1200, FullHeight=800;
	public bool IsShowing { get; private set; } = true; //Purely for reference. Does not affect anything.
	public static GUIStyle TextStyle=null!;

	//Warning: Message is in rich text so use Misc.SanitizeRichString if it could contain html tags
	public PopupMessage(string Message)
	{
		this.Message=Message;
		DW.AddPopup(this);
	}

	//Create the background textures
	static PopupMessage() =>
		Window.OnNextFrame(() => {
			BackgroundTex=Color.black.MakeTexture();
			BorderTex=Color.grey.MakeTexture();
			TextStyle=new GUIStyle(GUI.skin.label) { fontSize=50, alignment=TextAnchor.MiddleCenter, wordWrap=true, richText=true };
		});

	//Default contents drawer that shows the message. This can be overwritten in a derived class
	protected virtual void DrawContents() =>
		GUILayout.Label(
			$"{PressAnyKeyString}{Misc.NewLine}{Message}",
			TextStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)
		);

	//Other virtual functions
	protected virtual void OnClosing() { } //When close starts
	protected virtual void OnClosed() { } //Once the window has completely closed
	protected virtual bool BlockAnyKeyClose => false; //Prevent window from closing when any key is pressed

	//Get the size of the window rectangle, accouting for the WindowGrowTime
	private Vector2 SizeAtPercent(float PercentPassed) =>
		new(FullWidth*PercentPassed, FullHeight*PercentPassed);

	//Draws the window and clips the screen to the contents
	private void DrawWindow(float PercentPassed)
	{
		//Do some math to get rectangles
		const int BorderWidth=2, Padding=5;
		int BorderAndPadding=BorderWidth+Padding;
		Vector2 FullSize=SizeAtPercent(1), CurrentSize=SizeAtPercent(PercentPassed);
		Rect LocalWinRect=CurrentSize.CenterIn(FullSize);
		Rect ContentRectOffset=new(BorderAndPadding, BorderAndPadding, BorderAndPadding*-2, BorderAndPadding*-2);

		//Draw the window and border
		GUI.DrawTexture(LocalWinRect, BorderTex);
		GUI.DrawTexture(LocalWinRect.Add(new Rect(BorderWidth, BorderWidth, BorderWidth*-2, BorderWidth*-2)), BackgroundTex);

		//If IsStillGrowing then set up a clip rectangle and adjust the final region for it
		bool IsStillGrowing=(PercentPassed<1);
		if(IsStillGrowing) {
			Rect ClipRect=LocalWinRect.Add(ContentRectOffset);
			GUI.BeginClip(ClipRect);
			GUI.BeginClip(new Rect(-ClipRect.position, FullSize));
		}

		//Draw the full size window contents (which will be clipped if PercentPassed<1)
		GUILayout.BeginArea(new Rect(Vector2.zero, FullSize).Add(ContentRectOffset));
		GUILayout.BeginVertical();
		DrawContents();
		GUILayout.EndVertical();
		GUILayout.EndArea();

		//If IsStillGrowing then set end the clip
		if(IsStillGrowing) {
			GUI.EndClip();
			GUI.EndClip();
		}
	}

	//Called on the currently visible PopupMessage
	protected virtual void OnUpdate() { }

	//Draw last window. If a window exists behind the initializing one, and we are in the WindowGrowTime phase, then draw it also
	protected class DrawPopups() : Window("PopupMessage", false, 1500)
	{
		private const float TimeoutBeforeButtonActivates=0.5f, WindowGrowTime=0.33f;
		private readonly Stack<PopupMessage> Popups=new(); //Need to release in reverse order
		private DateTime LastAction; //This holds when the last popup message was created or released (so multiple don’t close at once)
		private PopupMessage? MinimizingPopup {
			get;
			set {
				field?.OnClosed();
				field=value;
			}
		} = null;

		//Get the current popup message
		public PopupMessage? LastPopup => Popups.Count==0 ? null : Popups.Peek();

		internal void AddPopup(PopupMessage M) => OnNextFrame(() => {
			DW.LastAction=DateTime.Now;
			if(Popups.Count==0)
				Visible=true;
			MinimizingPopup=null;
			Popups.Push(M);
		});

		protected override void PreOnGUI(Event Ev) => Misc.IFF(
			Ev.type==EventType.Layout,
			() => WindowRect=new Rect(
				(Misc.ScreenSize-WindowRect.size)/2,
				(MinimizingPopup ?? Popups.Peek()).SizeAtPercent(1)
			)
		);

		protected override void DoLayout(int ID, Event Ev)
		{
			//TODO: In the future I may try to make sure any active windows are visible, but in general, this class is designed to only show 1 popup at once
			//Handle minimizing windows differently
			float PercentPassed;
			if(MinimizingPopup!=null) {
				PercentPassed=(float)(DateTime.Now-MinimizingPopup.CloseTime).TotalSeconds/WindowGrowTime;
				if(PercentPassed<1) {
					DrawWindows(
						MinimizingPopup, 1-PercentPassed,
						Popups.Count>0 ? Popups.Peek() : null
					);
					return;
				}

				MinimizingPopup=null;
				if(Popups.Count==0)
					Visible=false;
			}
			if(Popups.Count==0)
				return;

			PopupMessage Current=Popups.Peek();
			PercentPassed=(float)(DateTime.Now-Current.InitTime).TotalSeconds/WindowGrowTime;
			DrawWindows(
				Current, PercentPassed,
				PercentPassed<1 && Popups.Count>1 ? Popups.ElementAt(1) : null //Draw window behind currently initializing one
			);
		}

		private void DrawWindows(PopupMessage Front, float PercentPassed, PopupMessage? Back)
		{
			Back?.DrawWindow(1);
			Front.DrawWindow(Mathf.Clamp(PercentPassed, .05f, 1));
		}

		//Watch for a key/mouse/controller press to close a message
		protected override void OnUpdate()
		{
			if(Popups.Count==0)
				return;
			PopupMessage LastWin=Popups.Peek();
			LastWin.OnUpdate();
			if(DevInput.Util.AnyKeyOrButtonPressed && !LastWin.BlockAnyKeyClose)
				CloseLastPopup();
		}
		protected override void OnMouseEvent(Event Ev) =>
			Misc.IFF(Ev.type==EventType.MouseDown, CloseLastPopup);

		//Close the latest popup
		private void CloseLastPopup() =>
			OnNextFrame(CloseLastPopupReal);
		private void CloseLastPopupReal()
		{
			if(Popups.Count==0 || (DateTime.Now-LastAction).TotalSeconds<TimeoutBeforeButtonActivates) //Not enough time has passed to detect an input event
				return;
			MinimizingPopup=Popups.Pop();
			MinimizingPopup.IsShowing=false;
			MinimizingPopup.OnClosing();
			MinimizingPopup.CloseTime=LastAction=DateTime.Now;
		}
	}
}