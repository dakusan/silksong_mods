using UnityEngine;

namespace SilkDev.DevInput.Mouse;

//Keeps track of mouse dragging states and distances
public class Dragger
{
	public static readonly int ResizeControlID=GUIUtility.GetControlID(FocusType.Passive);
	public Vector2 StartPos	{ get; private set; }
	public Vector2 Delta	{ get; private set; }
	public bool IsDragging	{ get; private set; } = false;

	//Called during OnDraw(). Returns if dragging
	public enum State { None, Start, Dragging, Done }
	public State UpdateState(Rect InitBox, bool UseUpEvent=true)
	{
		Event Ev=Event.current;
		if(Ev.type==EventType.MouseDown && Button.CurrentButton==Button.Enum.Left && InitBox.Contains(Ev.mousePosition)) {
			IsDragging=true;
			GUIUtility.hotControl=ResizeControlID; //Capture mouse control
			StartPos=GUIUtility.GUIToScreenPoint(Ev.mousePosition);
			if(UseUpEvent)
				Ev.Use();
			return State.Start;
		} else if(Ev.type==EventType.MouseDrag && IsDragging && GUIUtility.hotControl==ResizeControlID) {
			Delta=GUIUtility.GUIToScreenPoint(Ev.mousePosition)-StartPos;
			if(UseUpEvent)
				Ev.Use();
			return State.Dragging;
		} else if(Ev.type==EventType.MouseUp && Button.CurrentButton==Button.Enum.Left && IsDragging) {
			IsDragging=false;
			GUIUtility.hotControl=0; //Release mouse control
			Delta=Vector2.zero;
			if(UseUpEvent)
				Ev.Use();
			return State.Done;
		}
		return State.None;
	}
}