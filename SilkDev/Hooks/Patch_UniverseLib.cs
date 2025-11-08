//TODO: Remove me
#if NO_COMPILE
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilkDev.Hooks;

internal class Patch_UniverseLib(Harmony Harmony) : DynamicHook(Harmony, "UniverseLib.UI.Models.UIBehaviourModel", "Update", Config.C.BlockMouse_UnityExplorer)
{
	public override bool HasBeenInitialized { get => PanelBaseType!=null; }
	private static Type PanelBaseType=null!;
	private static PropertyInfo PI_Rect=null!;
	private static readonly List<Rect> Rects=[];
	private static int LastFrame=0;

	protected override void RunPatch(Harmony Harmony, Type SearchedClass, string FuncName)
	{
		PanelBaseType=FindType("UniverseLib.UI.Panels.PanelBase") ?? throw new Exception("Could not find PanelBase");
		PI_Rect=PanelBaseType.GetProperty("Rect", BindingFlags.Instance | BindingFlags.Public) ?? throw new Exception("Could not find PanelBase.Rect");
		base.RunPatch(Harmony, SearchedClass, FuncName);
		DevEvents.OnDraw += (BlockMouseOnRects, 100); //Patch_UniverseLib
	}

	protected static void Postfix(object __instance)
	{
		if(!PanelBaseType.IsInstanceOfType(__instance))
			return;

		if(PI_Rect.GetValue(__instance) is not RectTransform RectT)
			return;

		if(LastFrame<Time.frameCount) {
			Rects.Clear();
			LastFrame=Time.frameCount;
		}

		Rect WinRect=RectT.GetRectInParentSpace();
		WinRect.y=Screen.height-WinRect.y-WinRect.height;
		Rects.Add(WinRect);
	}

	internal static void BlockMouseOnRects() =>
		Rects.ForEach(static R => MEvent.Consume(R));
}
#endif