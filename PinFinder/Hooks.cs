using HarmonyLib;

namespace PinFinder;

//Block the keyboard from getting to the game
[HarmonyPatch(typeof(InControl.InputControlState), nameof(InControl.InputControlState.Set), [typeof(float), typeof(float)])]
internal static class Patch_InputControlState_Set {
	private static bool Prefix() => !FindPins.CurrentlyRunning;
}