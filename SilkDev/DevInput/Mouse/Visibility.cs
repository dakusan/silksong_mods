using HarmonyLib;
using System;
using UnityEngine;
using static SilkDev.Internal.Config;

namespace SilkDev.DevInput.Mouse;

//Handle mouse cursor visibility
public static class Visibility
{
	//Public access
	public static event Func<bool> ForceEvent=delegate { return false; }; //If any of these functions return true, the cursor is visible
	public static bool IsForced { get {
		foreach(Delegate FE in ForceEvent.GetInvocationList())
			try {
				if(((Func<bool>)FE)())
					return true;
			} catch(Exception e) {
				Catcher.OutputException("Mouse.IsForce", FE, e);
			}
		return false;
	} }
	public static bool IsVisible => Cursor.visible;

	//Private stuff
	private static bool CurrentDevForceState=false; //Keeps the last state of the DevConfig.C.ForceShowMouse

	//Initialization
	internal static void Init() {}
	static Visibility()
	{
		Events.GameEvents.OnUpdate += OnKeyEvent;
		C.ForceShowMouse.SettingChanged += ForceShowMouse_Changed;
		ForceShowMouse_Changed(null, null);
	}

	//Checks for shortcut key to toggle the mouse
	private static void OnKeyEvent() => Misc.IFF(
		C.Key_ToggleMouse.IsDown(),
		() => C.ForceShowMouse.V=!C.ForceShowMouse
	);

	//Handles ForceShowMouse config change by adding/removing ForceFunc
	private static void ForceShowMouse_Changed(object? _, EventArgs? e) => Misc.IFF(
		CurrentDevForceState!=C.ForceShowMouse,
		() => ForceEvent=ForceEvent.Toggle(ForceFunc, CurrentDevForceState=C.ForceShowMouse)
	);

	//When added to the event always returns true
	private static bool ForceFunc() => true;
}

//Hook Cursor.(lockState|visible)=*
[HarmonyPatch(typeof(Cursor))]
internal static class Patch_Cursor
{
	[HarmonyPrefix]
	[HarmonyPatch(nameof(Cursor.lockState), MethodType.Setter)]
	private static void LockState_Prefix(ref CursorLockMode value)
	{
		try { LockState_Exec(ref value); }
		catch(Exception e) { Catcher.OutputException($"{nameof(Patch_Cursor)}_{nameof(LockState_Prefix)}", e); }
	}
	private static void LockState_Exec(ref CursorLockMode value) =>
		value=Visibility.IsForced ? CursorLockMode.None : value;

	[HarmonyPrefix]
	[HarmonyPatch(nameof(Cursor.visible), MethodType.Setter)]
	public static void Visible_Prefix(ref bool value)
	{
		try { Visible_Exec(ref value); }
		catch(Exception e) { Catcher.OutputException($"{nameof(Patch_Cursor)}_{nameof(Visible_Prefix)}", e); }
	}
	private static void Visible_Exec(ref bool value) =>
		value=Visibility.IsForced||value;
}