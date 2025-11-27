using BepInEx.Configuration;
using SilkDev.Configs;
using UnityEngine;

namespace SilkDev.Internal;

public class Config
{
	public enum AutoLoadSaveSlotNumber { None=0, Slot1, Slot2, Slot3, Slot4 }
	private static Config _C=null!; public static Config C => _C; //Singleton

	public readonly ConfigEntryT<bool> BlockGameInput, ForceShowMouse, MessageOnInputBlocked, SkipIntro, ForceStackTrace, BlockMouse_UnityExplorer, BlockMouse_BepInExConfig, RunExtractAllTextures, ESWindow_ShowMouseOver;
	public readonly ConfigEntryT<KeyboardShortcut> Key_BlockInput, Key_ToggleMouse, Key_ExtractSprites;
	public readonly ConfigEntryT<Log.DebugLogLevelEnum> DebugLogLevel;
	public readonly ConfigEntryT<AutoLoadSaveSlotNumber> AutoLoadSaveSlot;
	public readonly ConfigEntryT<Rect> Rect_ExtractSprites;

	internal Config(ConfigFile PConfig)
	{
		Misc.InitSingleton(this, ref _C);
		OrderedConfig Con=new(PConfig);

		//Block keyboard input
		string Title="Block Game Input";
		BlockGameInput			=Con.Bind(Title, "Block all game input", false, "Block keyboard and controllers from affecting the game. Useful when typing in Unity Explorer");
		Key_BlockInput			=Con.Bind(Title, "Block all game input shortcut key", new KeyboardShortcut(KeyCode.None));
		MessageOnInputBlocked	=Con.Bind(Title, "Show message when input blocked", true);

		//Show the mouse
		Title="Force Show Mouse";
		ForceShowMouse			=Con.Bind(Title, "Enable", false, "Show/Hide the mouse (also see DevInput.Mouse.Force)");
		Key_ToggleMouse			=Con.Bind(Title, "Shortcut", new KeyboardShortcut(KeyCode.F10));

		//Quickness settings
		Title="Quickness";
		SkipIntro				=Con.Bind(Title, "Skip intro", true, "If you want to skip the game intro");
		AutoLoadSaveSlot		=Con.Bind(Title, "Auto load save slot", AutoLoadSaveSlotNumber.None, "When the main menu shows this save will auto load (even on save+quit).");

		//Textures
		Title="Textures/Sprites";
		RunExtractAllTextures	=Con.Bind(Title, "Extract all textures", false, $"Extracts all textures IN MEMORY to PLUGIN_PATH/{Textures.ExtractAllTextures.TextureDirectory}/. Textures have md5 appended to name since there are name collisions.");
		Key_ExtractSprites		=Con.Bind(Title, "Opens the “Extract Sprites” window", new KeyboardShortcut(KeyCode.None), "Get textures from sprites under your cursor");
		ESWindow_ShowMouseOver	=Con.Bind(Title, "Show boxes around sprites when “Extract Sprites” is open", true);

		//Development settings
		Title="Development";
		DebugLogLevel			=Con.Bind(Title, "Debugging message log level", Log.DebugLogLevelEnum.Info, "What log level to show this plugin’s information messages at");
		ForceStackTrace			=Con.Bind(Title, "Output stacktrace on exception", true, "Outputs a stacktrace on any exception caught through this plugins interfaces (Catcher or any of the Window events/callbacks)");
		Rect_ExtractSprites		=Con.Bind(Title, "Window Position: Extract Sprites", Rect.zero, null, new() { Browsable=false });

		//Fix problems with other plugins
		Title="Fix other plugins";
		BlockMouse_UnityExplorer=Con.Bind(Title, "No mouse passthrough on Unity Explorer", true, "Unity explorer does not block mouse events from reaching the rest of unity when the mouse is over it. This fixes that. This will run during Window.OnDraw Priority=-100.");
		BlockMouse_BepInExConfig=Con.Bind(Title, "No mouse passthrough on BepInEx Config Manager", true, "See above description.");
	}
}