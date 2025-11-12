using HarmonyLib;
using InControl;
using SilkDev;
using System;
using System.Collections;
using UnityEngine;

namespace PharloomAtlas;

[HarmonyPatch(typeof(GameMap))]
internal static class Patch_GameMap
{
	//Called when the map is zoomed opened/closed
	[HarmonyPostfix][HarmonyPatch(nameof(GameMap.SetIsZoomed))]
	private static void SetIsZoomed_Postfix(GameMap __instance, bool isZoomed) =>
		Catcher.Run($"{nameof(Patch_GameMap)}_{nameof(SetIsZoomed_Postfix)}", () => SetIsZoomed_Exec(__instance, isZoomed));
	private static void SetIsZoomed_Exec(GameMap __instance, bool isZoomed)
	{
		if(isZoomed)
			MapControl.MapOpenedEvent(__instance);
		MapControl.Self.StateChange(isZoomed ? MapControl.MapStateEnum.Open : MapControl.MapStateEnum.Closed);
	}

	//Called when the map is moved in and out of marker mode
	[HarmonyPostfix][HarmonyPatch(nameof(GameMap.SetIsMarkerZoom))]
	private static void SetIsMarkerZoom_Postfix(bool isMarkerZoom) =>
		Catcher.Run($"{nameof(Patch_GameMap)}_{nameof(SetIsMarkerZoom_Postfix)}", () => SetIsMarkerZoom_Exec(isMarkerZoom));
	private static void SetIsMarkerZoom_Exec(bool isMarkerZoom) =>
		MapControl.Self.StateChange(isMarkerZoom ? MapControl.MapStateEnum.Marker : MapControl.MapStateEnum.Open);

	//Remove map pan boundaries
	[HarmonyPrefix][HarmonyPatch(nameof(GameMap.KeepWithinBounds))]
	private static bool KeepWithinBounds_Prefix() => !Config.C.UnlockMapBounds;

	//Always allow panning
	[HarmonyPrefix][HarmonyPatch(nameof(GameMap.CanStartPan))]
	private static bool CanStartPan_Prefix (ref bool __result) => !(__result=true);
	[HarmonyPrefix][HarmonyPatch(nameof(GameMap.CanMarkerPan))]
	private static bool CanMarkerPan_Prefix(ref bool __result) => !(__result=true);

	//Allow map in abyss
	[HarmonyPostfix][HarmonyPatch(nameof(GameMap.IsLostInAbyssPreMap))]
	private static void IsLostInAbyssPreMap_Postfix(ref bool __result) => __result=__result && !Config.C.MapInAbyss;
}

//Block DPad and right stick for map
[HarmonyPatch(typeof(InputHandler), nameof(InputHandler.GetSticksInput))]
internal static class Patch_InputHandler_GetSticksInput
{
	private static void Postfix(ref bool isRightStick, ref Vector2 __result)
	{
		//Intercept the right stick for zooming
		if(isRightStick) {
			if(MapControl.Self?.IsMapOpened ?? false) //If map is up return zero out the vector
				__result=Vector2.zero;
			return;
		}

		//Intercept when the sidebar is up
		if(!(MapControl.Self?.SideBar.Visible ?? false))
			return;

		//If one of the digital pad buttons is down, return a zeroed vector
		InputDevice D=InputManager.ActiveDevice;
		if(D.DPadDown.IsPressed || D.DPadUp.IsPressed || D.DPadLeft.IsPressed || D.DPadRight.IsPressed)
			__result=Vector2.zero;
	}
}

//Block specified controller input when the map is open and in the right state
[HarmonyPatch(typeof(OneAxisInputControl), nameof(OneAxisInputControl.IsPressed), MethodType.Getter)]
internal static class Patch_OneAxisInputControl_IsPressed
{
	private static InputDevice AD => InputManager.ActiveDevice;
	internal static void Postfix(OneAxisInputControl __instance, ref bool __result) =>
		__result=!(!__result || (						//If result is false, automatically return false
			__result								&&	//Button is pressed
			(MapControl.Self?.IsMapOpened ?? false)	&&	//Map must be open
			(__instance is PlayerAction PAction)	&& (//Must be a player action
				(PAction.Name is "openInventory" or "Pause")|| (//"Open inventory" opens sidebar when map is open, "Pause" acts as a selector on the sidebar
					MapControl.Self?.MapState==MapControl.MapStateEnum.Marker	&&	//If in marker mode and
					(MapControl.Self?.SideBar.Visible ?? false)					&&	//Sidebar is visible and
					PAction.Name=="Menu Super"										//"Menu Super" is pressed, then choose icon
				)											|| (
					SaveValuesWindow.Self.Visible								&&	//Save value window (and map) are open
					(PAction.Name=="Pane Right" || PAction.Name=="Pane Left")	&&	//Scrolling the inventory
					(AD.LeftTrigger.IsPressed || AD.RightTrigger.IsPressed)			//One of the triggers is pressed
				)
				//"Menu Extra" doesn’t need to be cancelled since it’s only used when NOT in marker mode, and in that mode it has no function
			)
		));
}

//See Patch_OneAxisInputControl_IsPressed
[HarmonyPatch(typeof(OneAxisInputControl), nameof(OneAxisInputControl.WasPressed), MethodType.Getter)]
internal static class Patch_OneAxisInputControl_WasPressed {
	private static void Postfix(OneAxisInputControl __instance, ref bool __result) =>
		Patch_OneAxisInputControl_IsPressed.Postfix(__instance, ref __result);
}

//Tell MapController when cursor position updates
[HarmonyPatch(typeof(MapMarkerMenu), "PanMap")]
internal static class Patch_MapMarkerMenu_PanMap {
	private static void Postfix(bool __result) =>
		Misc.IFF(__result, () => Catcher.Run(nameof(Patch_MapMarkerMenu_PanMap), Exec));
	private static void Exec() =>
		MapControl.Self?.CursorMoveEvent();
}

//Do not reposition the map position on marker zoom in/out
[HarmonyPatch(typeof(InventoryMapManager), "ZoomInMarkerRoutine")]
internal static class Patch_InventoryMapManager_ZoomInMarkerRoutine
{
	private static bool Prefix(ref IEnumerator __result, bool isPlacementActive)
	{
		try { return Exec(ref __result, isPlacementActive); }
		catch(Exception e) { Catcher.OutputException(nameof(Patch_InventoryMapManager_ZoomInMarkerRoutine), e); return true; }
	}
	private static bool Exec(ref IEnumerator __result, bool isPlacementActive)
	{
		if(!Config.C.MarkerZoomDoesntMove)
			return true;

		static IEnumerator FakeCall() { yield break; }
		__result=FakeCall();
		MapControl.Self.GameMap.SetIsMarkerZoom(isPlacementActive);
		return false;
	}
}