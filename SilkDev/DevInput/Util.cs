using InControl;

namespace SilkDev.DevInput;

public static class Util
{
	//Check if any key or button is currently pressed
	public static bool AnyKeyOrButtonPressed => AnyKeyOrButtonPressed_Real(InputManager.ActiveDevice);
	private static bool AnyKeyOrButtonPressed_Real(InputDevice Device) =>
		   InputManager.AnyKeyIsPressed
		|| Device.AnyButton.IsPressed
		|| Device.LeftBumper.IsPressed
		|| Device.RightBumper.IsPressed
		|| Device.LeftTrigger.IsPressed
		|| Device.RightTrigger.IsPressed
		|| Device.DPad.IsPressed
		|| (Device.LeftStick.HasChanged && Device.LeftStick.Value.magnitude > 0.1f)
		|| (Device.RightStick.HasChanged && Device.RightStick.Value.magnitude > 0.1f);
}