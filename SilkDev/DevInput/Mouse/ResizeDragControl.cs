using BepInEx.Configuration;
using System;
using UnityEngine;

namespace SilkDev.DevInput.Mouse;

//Adds controls on window’s for drag resizing, and can also handle window moving just like GUI.DragWindow().
//Set GUI.skin.box to change the resize handle style.
//Can save to a config variable when moving/resizing finishes.
public class ResizeDragControl(Func<float, float, Rect> GetResizeRect, Func<float, float, Rect> GetMoveRect)
{
	private readonly Dragger ResizeDrag=new(), MoveDrag=new();
	private Rect StartWindowRect;

	public int MinWindowShown=50, MinDragShown=10; //At least MinDragShown of the dragger must be shown, and MinWindowShown of the window (on both axis)
	public ConfigEntry<Rect>? SaveEntry; //Optional config entry for saving when the window is moved or resized
	public Vector2 MinSize=new(100, 100); //Minimum width and height of the window
	public Func<float, float, Rect> GetResizeRect=GetResizeRect, GetMoveRect=GetMoveRect; //Receives WinWidth and WinHeight and returns the rect where dragging is allowed. Uses Default_GetResizeRect and Default_GetMoveRect if not given.
	public bool HasMouseControl => ResizeDrag.IsDragging || MoveDrag.IsDragging;

	//Returns a square in the bottom right corner of size DefaultResizeHandleSize
	public Rect Default_GetResizeRect(float WinWidth, float WinHeight) =>
		new(WinWidth-Default_ResizeHandleSize, WinHeight-Default_ResizeHandleSize, Default_ResizeHandleSize, Default_ResizeHandleSize);
	public int Default_ResizeHandleSize=20; //See Default_GetResizeRect

	//Returns a strip across the top of height DefaultMoveHandleHeight
	public Rect Default_GetMoveRect(float WinWidth, float WinHeight) =>
		new(0, 0, WinWidth, Default_MoveHandleHeight);
	public int Default_MoveHandleHeight=15; //See Default_GetMoveRect

	public ResizeDragControl() : this(null!, null!)
	{
		GetResizeRect=Default_GetResizeRect;
		GetMoveRect=Default_GetMoveRect;
	}

	public void OnDraw(ref Rect WindowRect)
	{
		//Draw the resizer
		Rect ResizeHandle=GetResizeRect(WindowRect.width, WindowRect.height);
		GUI.Box(ResizeHandle, Misc.Empty, GUI.skin.box);

		//Handle resize drag
		switch(ResizeDrag.UpdateState(ResizeHandle)) {
			case Dragger.State.None:
				break;
			case Dragger.State.Start:
				StartWindowRect=WindowRect;
				break;
			case Dragger.State.Dragging:
				WindowRect.size=Vector2.Max(StartWindowRect.size+ResizeDrag.Delta, MinSize);
				break;
			case Dragger.State.Done:
				_=SaveEntry?.Value=WindowRect;
				break;
		}
	}

	public void HandleDrag(ref Rect WindowRect)
	{
		//Handle window move drag
		Rect DragRect=GetMoveRect(WindowRect.width, WindowRect.height);
		switch(MoveDrag.UpdateState(DragRect)) {
			case Dragger.State.None:
				break;
			case Dragger.State.Start:
				StartWindowRect=WindowRect;
				break;
			case Dragger.State.Dragging:
				WindowRect.x=StartWindowRect.x+MoveDrag.Delta.x;
				WindowRect.y=StartWindowRect.y+MoveDrag.Delta.y;
				CheckWindowRect(ref WindowRect, DragRect);
				break;
			case Dragger.State.Done:
				_=SaveEntry?.Value=WindowRect;
				break;
		}
	}

	//Make sure the window is viewable within the screen
	public void CheckWindowRect(ref Rect WindowRect, Rect DragRect=default)
	{
		if(DragRect==default)
			DragRect=GetMoveRect(WindowRect.width, WindowRect.height);

		WindowRect.x=Mathf.Clamp(
			WindowRect.x,
			Mathf.Max(-DragRect.xMax+MinDragShown, -WindowRect.width+MinWindowShown),
			Screen.width-Mathf.Max(DragRect.x+MinDragShown, MinWindowShown)
		);
		WindowRect.y=Mathf.Clamp(
			WindowRect.y,
			Mathf.Max(-DragRect.yMax+MinDragShown, -WindowRect.height+MinWindowShown),
			Screen.height-Mathf.Max(DragRect.y+MinDragShown, MinWindowShown)
		);
	}
}