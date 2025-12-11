using BepInEx.Configuration;
using SilkDev;
using SilkDev.Configs;
using UnityEngine;

namespace NoClip;

public class Config
{
	private static Config _C=null!; public static Config C => _C; //Singleton
	public readonly ConfigEntryT<bool> ToggleNoClip;
	public readonly ConfigEntryTKeyboardShortcut Key_ToggleNoClip;
	public readonly ConfigEntryT<float> NoClipScale;
	public readonly DynamicEnumConfig Language;
	public readonly Translations Tr;

	internal Config(ConfigFile PConfig)
	{
		Misc.InitSingleton(this, ref _C);
		using TypedDisposer<TranslatedConfig> TCon=new(
			new(PConfig, Tr=Translations.StandardCreate("NoClip")),
			static LCon => LCon.Complete()
		);
		TranslatedConfig Con=TCon.Target;

		//Shortcuts
		string Title="No Clip";
		Key_ToggleNoClip=Con.Bind(Title, "No clip shortcut key", new KeyboardShortcut(KeyCode.F3), "Key to press to turn on and off noclip");
		ToggleNoClip	=Con.Bind(Title, "Turn on no clip", false, "Turns off collision detection and velocity, turns on invincibility and infinite jump");
		NoClipScale		=Con.Bind(Title, "No clip movement speed", 1.5f, new ConfigDescription("How fast you want your character to move while no clipping", new AcceptableValueRange<float>(.5f, 5f)));
		ToggleNoClip.V	=false; //Do not start with this on
		ToggleNoClip.SettingChanged += (_, _) => NCActivate.Self.Toggle(ToggleNoClip);

		//General
		Title="General";
		Language=Con.BindLanguage(Title);
	}
}