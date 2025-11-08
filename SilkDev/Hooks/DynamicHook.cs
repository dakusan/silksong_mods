using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SilkDev.Hooks;

public abstract class DynamicHook
{
	//Will only run the search and hook if there is no config or when the config is true
	protected DynamicHook(Harmony Harmony, string ClassName, string FuncName, ConfigEntry<bool>? CE=null)
	{
		Action ExecSearch=() => Catcher.ExecCoroutine($"{GetType().Name}.{nameof(RunSearch)}", RunSearch(Harmony, ClassName, FuncName));
		if(CE?.Value ?? true)
			ExecSearch();
		CE?.SettingChanged += (_, _) =>
			Misc.IFF(CE.Value, ExecSearch);
	}

	public abstract bool HasBeenInitialized { get; }
	protected static void Prefix () => throw new MethodAccessException("This should never get called"); //You can include 1 instance of Prefix () with the parameters you want (Don’t use override)
	protected static void Postfix() => throw new MethodAccessException("This should never get called"); //You can include 1 instance of Postfix() with the parameters you want (Don’t use override)

	//Attempt to find UIBehaviourModel in UniverseLib once a second for 5 seconds
	private IEnumerator RunSearch(Harmony Harmony, string ClassName, string FuncName)
	{
		//If already initialized, nothing to do
		if(HasBeenInitialized)
			yield break;

		//Run the search
		Type? FoundClass=null!;
		string Result=$"Could not find {ClassName}. Patch failed.";
		for(int i=0; i<5; i++)
			if((FoundClass=FindType(ClassName))==null)
				yield return new WaitForSecondsRealtime(1f);
			else
				break;

		//If the user tried initiating multiple times within 5 seconds, this could happen. Exit early.
		if(HasBeenInitialized)
			yield break;

		//If UniverseLib found, finish the patching process
		if(FoundClass!=null)
			try {
				RunPatch(Harmony, FoundClass, FuncName);
				Result="Patched";
			} catch(Exception e) {
				Result=e.Message;
			}

		Log.Info($"{GetType().Name}: {Result}");
	}

	//Call this from your override function after you’ve finished setting up the patch.
	//Throw errors to stop the process.
	protected virtual void RunPatch(Harmony Harmony, Type SearchedClass, string FuncName)
	{
		_=Harmony.Patch(
			SearchedClass.GetMethod(FuncName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
			postfix: FindFuncInSelf("Postfix"),
			prefix: FindFuncInSelf("Prefix")
		);
	}

	private HarmonyMethod? FindFuncInSelf(string MethodName)
	{
		try {
			return new HarmonyMethod(GetType().GetMethod(MethodName, BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.DeclaredOnly));
		} catch {
			return null;
		}
	}

	public static Type? FindType(string FullName)
	{
		Type T=null!;
		foreach(Assembly Asm in AppDomain.CurrentDomain.GetAssemblies())
			try {
				if((T=Asm.GetType(FullName, throwOnError: false, ignoreCase: false))!=null)
					break;
			} catch {} //Ignore dynamic assemblies
		return T;
	}
}