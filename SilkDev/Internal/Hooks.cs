using HarmonyLib;
using UnityEngine;

namespace SilkDev.Internal;

//Skip game intro
[HarmonyPatch(typeof(StartManager), "Start")]
internal static class Patch_StartManager_Start
{
	private static void Postfix(StartManager __instance) => Misc.IFF(
		Config.C.SkipIntro,
		() => __instance.gameObject.GetComponent<Animator>().speed=1000f
	);
}

//Auto load save slot
[HarmonyPatch(typeof(GameManager), "Start")]
internal static class Patch_GameManager_Start
{
	private static void Postfix(GameManager __instance)
	{
		if(__instance.GameState!=GlobalEnums.GameState.MAIN_MENU)
			return;
		Config.AutoLoadSaveSlotNumber Slot=Config.C.AutoLoadSaveSlot;
		if(Slot!=Config.AutoLoadSaveSlotNumber.None)
			GameManager._instance.LoadGameFromUI((int)Slot);
	}
}