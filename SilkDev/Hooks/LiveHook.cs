using HarmonyLib;
using SilkDev.Events;
using System.Reflection;

namespace SilkDev.Hooks;

public class LiveHook
{
	protected readonly Harmony Harmony;
	protected readonly MethodInfo PatchedMethod;
	protected readonly HarmonyMethod? PrefixMethod, PostfixMethod;
	protected bool IsHooked=false; //Only true while we are wanting the hook to run (also helps if the unhook fails)
	public bool IsEnabled
	{
		get;
		set {
			if(field==value)
				return;
			field=value;
			AddPatch();
		}
	} = false;

	//Init the class
	protected LiveHook(Harmony Harmony, MethodInfo PatchedMethod, MethodInfo? PrefixMethod=null, MethodInfo? PostfixMethod=null)
	{
		(this.Harmony, this.PatchedMethod)=(Harmony, PatchedMethod);
		this.PrefixMethod =PrefixMethod ==null ? null : new HarmonyMethod(PrefixMethod);
		this.PostfixMethod=PostfixMethod==null ? null : new HarmonyMethod(PostfixMethod);

		//Patch once, otherwise it may not work later
		void PatchOnce()
		{
			GameEvents.OnUpdate -= PatchOnce;
			if(!IsHooked) {
				DoPatch();
				DoUnpatch();
			}
		}
		GameEvents.OnUpdate += PatchOnce;
	}

	//Actual patching and unpatching
	private void DoPatch() => Harmony.Patch(PatchedMethod, prefix: PrefixMethod, postfix: PostfixMethod);
	private void DoUnpatch()
	{
		if(PrefixMethod !=null) Harmony.Unpatch(PatchedMethod, HarmonyPatchType.Prefix , Harmony.Id);
		if(PostfixMethod!=null) Harmony.Unpatch(PatchedMethod, HarmonyPatchType.Postfix, Harmony.Id);
	}

	//Remove the hook
	private void RemovePatch()
	{
		if(IsHooked)
			DoUnpatch();
		IsHooked=false;
	}

	//Execute the hook
	private void AddPatch()
	{
		if(IsHooked)
			RemovePatch();
		if(!IsEnabled)
			return;
		IsHooked=true;
		DoPatch();
	}
}