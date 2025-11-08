using BepInEx.Configuration;
using SilkDev.Configs;

namespace SilkDev.Internal;

public class Config
{
	public enum AutoLoadSaveSlotNumber { None=0, Slot1, Slot2, Slot3, Slot4 }
	private static Config _C=null!; public static Config C => _C; //Singleton

	public readonly ConfigEntryT<bool> BlockGameInput, ForceShowMouse, ShowMessageWhenGameInputBlocked, SkipIntro, ForceStackTrace, BlockMouse_UnityExplorer, BlockMouse_BepInExConfig;
	public readonly ConfigEntryT<KeyboardShortcut> Key_BlockInput, Key_ToggleMouse;
	public readonly ConfigEntryT<Log.DebugLogLevelEnum> DebugLogLevel;
	public readonly ConfigEntryT<AutoLoadSaveSlotNumber> AutoLoadSaveSlot;

	internal Config(ConfigFile PConfig)
	{
		Misc.InitSingleton(this, ref _C);
		OrderedConfig Con=new(PConfig);

		//Block keyboard input
		BlockGameInput=Con.Bind("Block Game Input", "Block game Input", false, "Block keyboard from affecting the game. Useful when typing in Unity Explorer");
		Key_BlockInput=Con.Bind("Block Game Input", "Block game input shortcut key", new KeyboardShortcut(UnityEngine.KeyCode.F4));
		ShowMessageWhenGameInputBlocked=Con.Bind("Block Game Input", "Show message when input blocked", true);

		//Show the mouse
		ForceShowMouse=Con.Bind("Force Show Mouse", "Enable", false, "Show/Hide the mouse (also see DevInput.Mouse.Force)");
		Key_ToggleMouse=Con.Bind("Force Show Mouse", "Shortcut", new KeyboardShortcut(UnityEngine.KeyCode.F10));

		//Quickness settings
		SkipIntro=Con.Bind("Quickness", "Skip intro", true, "If you want to skip the game intro");
		AutoLoadSaveSlot=Con.Bind("Quickness", "Auto load save slot", AutoLoadSaveSlotNumber.None, "When the main menu shows this save will auto load (even on save+quit).");

		//Development settings
		DebugLogLevel=Con.Bind("Development", "Debugging message log level", Log.DebugLogLevelEnum.Info, "What log level to show this plugin’s information messages at");
		ForceStackTrace=Con.Bind("Development", "Output stacktrace on exception", true, "Outputs a stacktrace on any exception caught through this plugins interfaces (Catcher or any of the Window events/callbacks)");

		//Fix problems with other plugins
		BlockMouse_UnityExplorer=Con.Bind("Fix other plugins", "No mouse passthrough on Unity Explorer", true, "Unity explorer does not block mouse events from reaching the rest of unity when the mouse is over it. This fixes that. This will run during Window.OnDraw Priority=-100.");
		BlockMouse_BepInExConfig=Con.Bind("Fix other plugins", "No mouse passthrough on BepInEx Config Manager", true, "See above description.");
	}
}