using UnityEngine;

namespace SilkDev.DevInput.Mouse;

//Mouse button enums
public class Button
{
	public enum Enum { Left=0, Right=1, Middle=2 }
	public static Enum CurrentButton => (Enum)Event.current.button;
}