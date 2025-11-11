using System;
using System.Reflection;
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

	//Short circuiting function for cleaner code
	public static void IFF(bool Cond, Action CallOnTrue) {
		if(Cond)
			CallOnTrue();
	}

	//Get plugin path
	public static string GetPluginPath =>
		FileOps.GetDirectoryName(Assembly.GetCallingAssembly().Location);

	//Simple reference class
	public class Ref<T>(T Value) {
		public T Value { get; set; } = Value;
	}

	public static Vector2 ScreenSize => new(Screen.width, Screen.height);
	public const char NewLine='\n';
	public const string Empty="";
}