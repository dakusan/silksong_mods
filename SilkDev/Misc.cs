using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SilkDev;

public static class Misc
{
	//Implement singletons
	//Singletons will always be set before anything tries to use it
	public static void InitSingleton<T>(T Self, ref T ObjProperty) =>
		ObjProperty=(ObjProperty==null ? Self : throw new InvalidOperationException($"Can only instance class ‘{typeof(T).Name}’ once"));

	//Sanitize a richText string
	public static string SanitizeRichString(string Message) =>
		Message.Replace("<", "<<i></i>"); //Yes, this is really the best way

	//Save to clipboard
	public static void SaveToClipboard(string Value) =>
		GUIUtility.systemCopyBuffer=Value;

	//Get steam username
	public const string UsernameErrorString="*SILKDEV NO NAME*"; //Tells the server the user’s username couldn’t be looked up
	public static string SteamUsername { get
	{
		try {
			return
				!Steamworks.SteamAPI.IsSteamRunning() ? throw new Exception("Steam not running") :
				Steamworks.SteamFriends.GetPersonaName() ?? throw new Exception("Lookup failed");
		} catch {
			return UsernameErrorString;
		}
	} }

	//Short circuiting and return type passthrough functions for cleaner code
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void IFF(bool Cond, Action CallOnTrue) {
		if(Cond)
			CallOnTrue();
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static RetType PassThru<Unused, RetType>(Unused _, RetType Return) => Return;

	//Get plugin path
	public static string GetPluginPath =>
		FileOps.GetDirectoryName(Assembly.GetCallingAssembly().Location);

	//Open Unity Explorer inspection on game object (if plugin is loaded)
	private static Action<GameObject>? Call_UnityExplorer_Inspect=null;
	public static void UnityExplorer_Inspect(GameObject GO)
	{
		//If we already created the function call, nothing else to do
		if(Call_UnityExplorer_Inspect!=null) {
			Call_UnityExplorer_Inspect(GO);
			return;
		}

		//Get the method to open a GameObject in the inspector
		MethodInfo? MI=
			Hooks.DynamicHook.FindType("UnityExplorer.InspectorManager")
			?.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.FirstOrDefault(static m => m.Name=="Inspect" && m.GetParameters().Length==2 && m.GetParameters()[0].ParameterType==typeof(object));
		if(MI==null) {
			Call_UnityExplorer_Inspect=static _ => { };
			Log.Error("Could not find unity explorer");
			return;
		}

		//Create a method to make unity explorer show up
		PropertyInfo? ShowMenuPI=Hooks.DynamicHook.FindType("UnityExplorer.UI.UIManager")?.GetProperty("ShowMenu");
		Action ShowMenuAction=(ShowMenuPI==null ? static () => { } : () => ShowMenuPI.SetValue(null, true));

		//Create and run the delegate for the full action
		Call_UnityExplorer_Inspect=RunGO => { _=MI.Invoke(null, [RunGO, null!]); ShowMenuAction(); };
		Call_UnityExplorer_Inspect(GO);
	}

	//Simple reference class
	public class Ref<T>(T Value) {
		public T Value { get; set; } = Value;
	}

	public const char NewLine='\n';
	public const string Empty="";
}