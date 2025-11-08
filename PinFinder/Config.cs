using BepInEx.Configuration;
using SilkDev;
using SilkDev.Configs;

namespace PinFinder;

public class Config
{
	public const string PinsJson="Pins.json";
	public static readonly string[] PinTempDir=["temp"];
	private static Config _C=null!; public static Config C => _C; //Singleton

	public readonly ConfigEntryT<bool> StartPinFindingProcess;
	public readonly ConfigEntryT<KeyboardShortcut> Key_StartPinProcess;
	public readonly ConfigEntryT<string> SkipScenes, SkipKeywords;

	internal Config(ConfigFile ConfigFile)
	{
		Misc.InitSingleton(this, ref _C);
		OrderedConfig PConfig=new(ConfigFile);

		//Pin Finding
		StartPinFindingProcess=PConfig.Bind(
			"Pin Finding", "Start Process", false, string.Join(Misc.Empty+Misc.NewLine+Misc.NewLine, [
				$"Check this and the plugin will start cycling through maps to find all the pins. It does this by identifying UnityEngine.GameObject’s that have saveable values. Once it’s done it will write a new {PinsJson} for you.",
				"WARNING: THIS IS A DANGEROUS PROCESS THAT MAY HARD FREEZE OR CRASH YOUR GAME.\nOnce you start it, you can’t stop it without alt+f4’ing, and it may take 13-15 minutes.",
				"The processing seems to usually freeze around 400 scenes, so when this happens it will prompt you to kill your game.",
				$"Scene-by-scene files are temporarily written during the process so it can pick back up where you left off after the crash. Once the full process is done the temporary files will be deleted and a final {PinsJson} will be written.",
			])
		);
		SkipScenes=PConfig.Bind(
			"Pin Finding", "Skipped scene bundles", string.Join(Misc.NewLine, [
				"menu_title.bundle", "quit_to_menu.bundle", "opening_sequence.bundle", "opening_sequence_act3.bundle", "pre_menu_intro.bundle", "permadeath.bundle",
				"demostart.bundle", "demoend.bundle", "last_dive_return.bundle",
				"room_caravan_spa.bundle", "room_caravan_interior_travel.bundle",
			]),
			"Scene bundles that need to be skipped since they cause the process to freeze");
		SkipKeywords=PConfig.Bind(
			"Pin Finding", "Skipped keywords", string.Join(Misc.NewLine, [
				"Remasker", //Remaskers seem to be spots that signal to show a part of the map
			]),
			"Keywords to skip when finding persistent objects");

		//Keyboard Shortcuts
		Key_StartPinProcess=PConfig.Bind("Shortcut Keys", "Start Pin Finding Process", KeyboardShortcut.Empty, "See “Pin Finding” section");
	}
}