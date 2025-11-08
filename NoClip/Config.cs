using BepInEx.Configuration;
using SilkDev.Configs;
using UnityEngine;

namespace NoClip;

public class Config
{
	private static Config _C=null!; public static Config C => _C; //Singleton
	public readonly ConfigEntryT<bool> ToggleNoClip;
	public readonly ConfigEntryT<KeyboardShortcut> Key_ToggleNoClip;
	public readonly ConfigEntryT<float> NoClipScale;

	internal Config(ConfigFile ConfigFile)
	{
		SilkDev.Misc.InitSingleton(this, ref _C);
		OrderedConfig PConfig=new(ConfigFile);

		//Shortcuts
		Key_ToggleNoClip=PConfig.Bind("No Clip", "No clip shortcut key", new KeyboardShortcut(KeyCode.F3), "Key to press to turn on and off noclip");
		ToggleNoClip	=PConfig.Bind("No Clip", "Turn on no clip", false, "Turns off collision detection and velocity, turns on invincibility and infinite jump");
		NoClipScale		=PConfig.Bind("No Clip", "No clip movement speed", 1.5f, new ConfigDescription("How fast you want your character to move while no clipping", new AcceptableValueRange<float>(.5f, 5f)));
		ToggleNoClip.V	=false; //Do not start with this on
		ToggleNoClip.SettingChanged += (_, _) => NCActivate.Self.Toggle(ToggleNoClip);
	}
}