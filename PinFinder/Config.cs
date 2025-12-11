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
	public readonly ConfigEntryTKeyboardShortcut Key_StartPinProcess;
	public readonly ConfigEntryT<string> SkipScenes, SkipKeywords;
	public readonly DynamicEnumConfig Language;
	public readonly Translations Tr;

	internal Config(ConfigFile PConfig)
	{
		Misc.InitSingleton(this, ref _C);
		using TypedDisposer<TranslatedConfig> TCon=new(
			new(PConfig, Tr=Translations.StandardCreate("PinFinder")),
			static LCon => LCon.Complete()
		);
		TranslatedConfig Con=TCon.Target;

		//Pin Finding
		string Title="Pin Finding";
		StartPinFindingProcess=Con.Bind(
			Title, "Start Process", false, string.Join(Misc.Empty+Misc.NewLine+Misc.NewLine, [
				"Check this and the plugin will start cycling through maps to find all the pins. It does this by identifying UnityEngine.GameObject’s that have saveable values. Once it’s done it will write a new {0} for you.",
				"WARNING: YOU CANNOT CONTINUE YOUR GAME AFTER INITIATING THIS PROCESS. SAVE BEFORE STARTING IT.\nYou can pause it once it starts, but you’ll need to alt+f4 before continuing the game. This should take 13-15 minutes.",
				"The processing seems to usually freeze around 400 scenes, so when this happens it will prompt you to exit your game.",
				"Scene-by-scene files are temporarily written during the process so it can pick back up where you left off. Once the full process is done the temporary files will be deleted and a final {0} will be written.",
			])
		);
		Tr.AddFormatParameters("Start Process", TranslatedConfig.SettingTranslationSections.Descriptions.TranslationName(), PinsJson);

		SkipScenes=Con.Bind(
			Title, "Skipped scene bundles", string.Join(Misc.NewLine, [
				"menu_title.bundle", "quit_to_menu.bundle", "opening_sequence.bundle", "opening_sequence_act3.bundle", "pre_menu_intro.bundle", "permadeath.bundle",
				"demostart.bundle", "demoend.bundle", "last_dive_return.bundle",
				"room_caravan_spa.bundle", "room_caravan_interior_travel.bundle",
			]),
			"Scene bundles that need to be skipped since they cause the process to freeze");
		SkipKeywords=Con.Bind(
			Title, "Skipped keywords", string.Join(Misc.NewLine, [
				"Remasker", //Remaskers seem to be spots that signal to show a part of the map
			]),
			"Keywords to skip when finding persistent objects");

		//Keyboard Shortcuts
		Title="Shortcut Keys";
		Key_StartPinProcess=Con.Bind(Title, "Start Pin Finding Process", KeyboardShortcut.Empty, "See “Pin Finding” section");

		//General
		Title="General";
		Language=Con.BindLanguage(Title);
	}
}