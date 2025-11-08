using InControl;
using UnityEngine;

namespace SilkDev.DevInput;

public static class Joystick
{
	public enum Direction { None, Up, Down, Left, Right }

	//Get the direction a joystick is pointed in and the magnitude
	public static Direction GetOrdinalDirectionAndMagnitude(
		bool IsLeftStick,		//Left or right stick?
		float AngleDeviation,	//The deviation the angles can be to trigger a direction. Don’t set it higher than 45 or there will be conflicts.
		float MinMagnitude,		//A direction will only be returned if the magnitude is greater than this
		out float Magnitude		//The current magnitude
	) {
		Vector2 StickInput=(IsLeftStick ? ActiveDevice.LeftStick : ActiveDevice.RightStick);
		Magnitude=StickInput.magnitude;
		if(Magnitude<MinMagnitude)
			return Direction.None;

		//Calculate angle in degrees
		float Angle=Mathf.Atan2(StickInput.y, StickInput.x)*Mathf.Rad2Deg;
		if(Angle<0)
			Angle+=360;

		//Confirm if angle is within a deviation of a given angle
		bool CheckAngle(float CenterAngle) =>
			Mathf.Abs((Angle-CenterAngle+540)%360-180)<=AngleDeviation; //Normalize delta to [-180, 180]

		return
			  CheckAngle(0  ) ? Direction.Right
			: CheckAngle(90 ) ? Direction.Up
			: CheckAngle(180) ? Direction.Left
			: CheckAngle(270) ? Direction.Down
			:					Direction.None;
	}

	public static InputDevice ActiveDevice => InputManager.ActiveDevice;
}