using HarmonyLib;
using SilkDev;
using SilkDev.Hooks;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PharloomAtlas;

internal class MoreMarkers
{
	private static MoreMarkers _Self=null!; public static MoreMarkers MM => _Self; //Singleton
	private const int NumMarkers=99, NumOriginalMarkers=9;
	private readonly Fix_GameMap_OnAwake FGMO=null!;
	private bool IsEnabled=false;

	internal MoreMarkers()
	{
		Misc.InitSingleton(this, ref _Self);

		try {
			FGMO=new Fix_GameMap_OnAwake();
		} catch(Exception e) {
			Catcher.OutputException(nameof(Fix_GameMap_OnAwake), e);
		}

		//Handle changing the option to use this
		IsEnabled=FGMO.ConfigTurnedOn=Config.C.MoreMarkers;
		Config.C.MoreMarkers.SettingChanged += (_, _) => {
			IsEnabled=FGMO.ConfigTurnedOn=Config.C.MoreMarkers;
			if(MapControl.Self?.GameMap==null)
				return;
			MapControl.Self.ExitMap(true, true, true); //Kick the user out of the map

			//Unfortunately, GameMap.OnAwake only happens once per game when the map is loaded, and it is what creates the spawned marker GameObjects.
			//I could check to see if SpawnedMapMarkers is already the correct size here... but it’s safer to just restart the save, and users generally won’t be swapping this on more than once
			if(IsEnabled)
				_=new PopupMessage("Reload your save game for this to properly take effect");
		};
	}

	private class Fix_GameMap_OnAwake() : LiveHook(
		new Harmony("temp.patcher.dakusan.Fix_GameMap_OnAwake"),
		typeof(Array).GetMethod(nameof(Array.GetLength), BindingFlags.Instance|BindingFlags.Public),
		typeof(Fix_GameMap_OnAwake).GetMethod(nameof(Pre_Array_GetLength), BindingFlags.Static|BindingFlags.NonPublic)
	) {
		private readonly Reflectors.RField<GameMap, GameObject[,]> FSpawnedMapMarkers=new(null, "spawnedMapMarkers");
		public bool ConfigTurnedOn=false;

		private void DoPatch(GameMap __instance)
		{
			FSpawnedMapMarkers.Obj=__instance;
			if(ConfigTurnedOn)
				IsEnabled=true;
		}

		//Create the new array
		private bool Run_Array_GetLength(Array __instance, int dimension, ref int __result)
		{
			//Makes sure we are in the right state
			if(!IsHooked || __instance!=FSpawnedMapMarkers.Get())
				return true;

			//Make sure we are on the correct array
			var Arr=(GameObject[,])__instance;
			if(Arr==null) {
				Log.Error("Array is null? What?");
				return true;
			}

			//Create the new array (if not already the right size) and return the length result
			IsEnabled=false;
			int NumFirstRows=Arr.GetLength(0);
			if(Arr.GetLength(1)!=NumMarkers)
				FSpawnedMapMarkers.Set(new GameObject[NumFirstRows, NumMarkers]);
			__result=dimension==0 ? NumFirstRows : NumMarkers;
			return false; //Do not run original
		}

		//Patches
		[HarmonyPatch(typeof(GameMap), nameof(GameMap.OnAwake))]
		private static class Patch_GameMap_OnAwake
		{
			private static void Prefix(GameMap __instance) => MM.FGMO.DoPatch(__instance);
			private static void Postfix() => MM.FGMO.IsEnabled=false;
		}
		private static bool Pre_Array_GetLength(Array __instance, int dimension, ref int __result) =>
			MM.FGMO.Run_Array_GetLength(__instance, dimension, ref __result);
	}

	//Patches for GetMarkerList to fake marker counts
	[HarmonyPatch(typeof(MapMarkerMenu))]
	private static class Patch_MapMarkerMenu_GetMarkerList
	{
		private static bool InPlaceMarker=false;
		private static List<Vector2>? RealList, FakeList;

		[HarmonyPostfix][HarmonyPatch("GetMarkerList")]
		private static void Postfix(ref List<Vector2> __result) {
			//Nothing to do if not enabled
			if(!MM.IsEnabled)
				return;

			//If not in PlaceMarker, just return a fake list with _size set to what we need it to to get the proper results
			int RealCount=__result.Count;
			if(!InPlaceMarker) {
				bool IsUpdatingList=(__result==RealList);
				__result=[];
				new Reflectors.RField<List<Vector2>, int>(__result, "_size").Set(
					-(NumMarkers-NumOriginalMarkers-RealCount-(IsUpdatingList ? 1 : 0))
				);
				return;
			}

			//If in PlaceMarker...
			//If there is no more room, just send the list as is
			if(RealCount>=NumMarkers)
				return;

			//Send an empty list and we’ll add to the real list at the exit of PlaceMarker
			RealList=__result;
			__result=FakeList=[];
			InPlaceMarker=false;
		}

		[HarmonyPrefix ][HarmonyPatch("PlaceMarker")] private static void PlaceMarker_Prefix () => InPlaceMarker=true;
		[HarmonyPostfix][HarmonyPatch("PlaceMarker")] private static void PlaceMarker_Postfix()
		{
			InPlaceMarker=false;
			if(RealList==null)
				return;
			RealList.Add(FakeList![0]);
			RealList=FakeList=null;
			MapControl.Self?.GameMap.SetupMapMarkers();
		}
	}
}