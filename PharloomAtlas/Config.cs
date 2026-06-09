using BepInEx.Configuration;
using SilkDev;
using SilkDev.Configs;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PharloomAtlas;

public class Config
{
	private static Config _C=null!; public static Config C => _C; //Singleton
	public readonly PerSaveConfig PSC;
	public readonly ConfigEntryT<string> CategoryToggleStates, MarkerLabels;
	public readonly ConfigEntryT<bool> UnlockMap, UnlockMapBounds, ShowSidebarOnGameLoad, ShowSideBarPictures, ShowMouseWhenSBVisible, MarkerZoomDoesntMove, IconSizeScalesWithZoom, AutoMap, MoreMarkers, AlwaysShowMarkerLabels, ForceDisplayCompass, HornetSpinningClockwise, HornetRevolvingClockwise, MapInAbyss, MapInAbyssUnspoiled, AnonymousSubmissions, UseErrorLog, UseErrorLogSend, SaveValues_Monitor;
	public readonly ConfigEntryT<float> ZoomSpeed, PanSpeed, MarkerPanSpeed, IconSize, AutoPanEase, AutoPanTime, QueryTime_PersistentObj, QueryTime_PlayerData, HornetHighlightSpeed, HornetRainbow2WaitTime, HornetRainbow2RunTime, HornetGrowingMax, HornetRevolvingDist, HornetRainbow1Scale, HornetRainbow1Angle;
	public readonly ConfigEntryT<int> SideBarWidth;
	public readonly ConfigEntryTKeyboardShortcut Shortcut_ZoomIn, Shortcut_ZoomOut, Shortcut_CenterOverChar, Shortcut_ToggleSideBar, Shortcut_EditMarkerLabel, Shortcut_SaveValueWindow, Shortcut_Val_ScrollUp, Shortcut_Val_ScrollDown;
	public readonly ConfigEntryTKeyboardShortcut Shortcut_SB_Up, Shortcut_SB_Down, Shortcut_SB_Left, Shortcut_SB_Right, Shortcut_SB_ToggleItem, Shortcut_SelStack_Next, Shortcut_SelStack_Prev, Shortcut_SB_SelectIcon;
	public readonly ConfigEntryT<Color> Color_SideBar_Background, Color_SideBar_Interface, Color_SideBar_Highlight, Color_MarkerLabelText, Color_MarkerLabelBG, Color_FoundIcon;
	public readonly ConfigEntryT<Rect> Rect_SaveValuesWindow, Rect_SearchWindow;
	public readonly DynamicEnumConfig IconSet;
	internal readonly ConfigEntryT<HornetIconAnimators.HornetHighlightTypes> HornetHighlights;
	public readonly DynamicEnumConfig Language;
	public readonly Translations Tr;

	private static ConfigDescription AVR<T>(T min, T max, string Description=DevStrings.Empty, ConfigurationManagerAttributes? CMA=null) where T : System.IComparable => new(Description, new AcceptableValueRange<T>(min, max), CMA);
	private static ConfigurationManagerAttributes NonBrowsable => new() { Browsable=false };
	private static ConfigurationManagerAttributes IsAdvanced => new() { IsAdvanced=true };

	internal Config(ConfigFile PConfig)
	{
		Misc.InitSingleton(this, ref _C);
		using TypedDisposer<TranslatedConfig> TCon=new(
			new(PConfig, Tr=Translations.StandardCreate("PharloomAtlas")),
			static LCon => LCon.Complete()
		);
		TranslatedConfig Con=TCon.Target;

		string Title="Map Features";
		AutoMap					=Con.Bind(Title, "Auto map",								false, "Areas that you have the map for will automatically fill in without needing to rest at a bench or have the quill");
		UnlockMap				=Con.Bind(Title, "Unlock map",								false, "Unlocks The Moss Grotto map for you, so you have access to the inventory map and sidebar");
		MapInAbyss				=Con.Bind(Title, "Show map in abyss",						false, "You got there!");
		MapInAbyssUnspoiled		=Con.Bind(Title, "Show map in *****",						false, "You’ll know once you get there ;-)");

		Title="Markers";
		MoreMarkers				=Con.Bind(Title, "More markers",							false, "Gives 99 of every kind of marker");
		ForceDisplayCompass		=Con.Bind(Title, "Force display Hornet pin (compass)",		false, "Force displaying Hornet’s location");
		HornetHighlights		=Con.Bind(Title, "Highlight Hornet marker",					HornetIconAnimators.HornetHighlightTypes.Revolve, "Shiny");
		IconSet=new				(Con,	  Title, "Icon set",								GetIconFiles(), "Pick your favorite icon set or create your own!", "Icons-FromGame.png");
		HornetHighlightSpeed	=Con.Bind(Title, "Hornet marker highlight speed",			1f, AVR(0.2f, 5f, "Applies to all highlight types", IsAdvanced));
		AlwaysShowMarkerLabels	=Con.Bind(Title, "Always show marker labels",				true, "Normally marker labels only show when you are over the marker. This will make all labels show all the time.");
		IconSize				=Con.Bind(Title, "Icon/Marker size",						0.75f, AVR(0.3f, 2.5f, "The size of the icons on the map"));
		IconSizeScalesWithZoom	=Con.Bind(Title, "Icon/Marker size scales with zoom",		true, "If true icons will always stay the same size at any zoom.", IsAdvanced);
		Color_FoundIcon			=Con.Bind(Title, "Found icon color*",						new Color(.5f, 0, .5f, .75f), "When in “All” mode for a category, found icons are tinted this color.\nRGB is actually HSV [Hue Saturation Lightness], unless the shader fails to load, in which case, it really is RGB.", IsAdvanced);
		Shortcut_EditMarkerLabel=Con.Bind(Title, "Shortcut: Edit Marker Label",				new KeyboardShortcut(KeyCode.KeypadPeriod));
		AutoPanEase				=Con.Bind(Title, "Autopan ease",							2.5f, AVR(1f, 5f, "The autopan ease formula multiplier. This is used when the map is animated panning to an icon.", IsAdvanced));
		AutoPanTime				=Con.Bind(Title, "Autopan time",							1.75f, AVR(0.1f, 3f, "The time it takes the autopan to move between points. This is used when the map is animated panning to an icon.", IsAdvanced));
		HornetRevolvingDist		=Con.Bind(Title, "Hornet marker: Revolving: Distance",		.1f, AVR(.02f, 1f, "The distance of the revolutions", IsAdvanced));
		HornetRevolvingClockwise=Con.Bind(Title, "Hornet marker: Revolving: Is Clockwise",	true, "The direction of the revolving", IsAdvanced);
		HornetGrowingMax		=Con.Bind(Title, "Hornet marker: Growing: Max Size",		0.5f, AVR(0.2f, 5f, "The largest the growing effect gets", IsAdvanced));
		HornetSpinningClockwise	=Con.Bind(Title, "Hornet marker: Spinning: Is Clockwise",	true, "The direction of the spinning", IsAdvanced);
		HornetRainbow1Scale		=Con.Bind(Title, "Hornet marker: Rainbow 1: Scale",			1f, AVR(.1f, 1f, "The zoom-scale of the 256 color gradient texture. This basically dictates how many color bands you can see at a time (higher=more).", IsAdvanced));
		HornetRainbow1Angle		=Con.Bind(Title, "Hornet marker: Rainbow 1: Angle",			-40f, AVR(-100f, 100f, "How many pixels to offset each successive line", IsAdvanced));
		HornetRainbow2WaitTime	=Con.Bind(Title, "Hornet marker: Rainbow 2: Wait time",		6f, AVR(0, 10f, "The time in between rainbow 2 effects", IsAdvanced));
		HornetRainbow2RunTime	=Con.Bind(Title, "Hornet marker: Rainbow 2: Run time",		1.2f, AVR(0.5f, 5f, "The time the rainbow 2 effects run", IsAdvanced));
		Shortcut_SelStack_Next	=Con.Bind(Title, "Next item in selection stack",			new KeyboardShortcut(KeyCode.Keypad7));
		Shortcut_SelStack_Prev	=Con.Bind(Title, "Previous item in selection stack",		new KeyboardShortcut(KeyCode.Keypad1));

		Title="Map Controls";
		ZoomSpeed				=Con.Bind(Title, "Zoom Speed",								1.03f, AVR(1.01f, 1.5f));
		PanSpeed				=Con.Bind(Title, "Pan Speed",								12f, AVR(1f, 40f));
		MarkerPanSpeed			=Con.Bind(Title, "Marker Pan Speed",						7f*2, AVR(1f, 20f, "Normally, the left stick pan speed is 7, with the right stick doubled. However, since the right stick is taken by other functions now, I put this at 14."));
		UnlockMapBounds			=Con.Bind(Title, "Unlock map bounds",						true, "This is needed due to zooming issues and the sidebar covering the map", IsAdvanced);
		MarkerZoomDoesntMove	=Con.Bind(Title, "Marker Zoom No Move-Reset",				true, "Normally, when you go in and out of marker mode, it resets the map position to the middle of the map you are actually on. With this turned on, it doesn’t reset the map position.", IsAdvanced);
		Shortcut_ZoomIn			=Con.Bind(Title, "Shortcut Key: Zoom In",					new KeyboardShortcut(KeyCode.KeypadPlus));
		Shortcut_ZoomOut		=Con.Bind(Title, "Shortcut Key: Zoom out",					new KeyboardShortcut(KeyCode.KeypadMinus));
		Shortcut_CenterOverChar	=Con.Bind(Title, "Shortcut Key: Center map over character",	new KeyboardShortcut(KeyCode.Keypad0));

		Title="Interface customization";
		Language				=Con.BindLanguage(Title);
		Color_SideBar_Background=Con.Bind(Title, "Sidebar background color",				new Color(0, 0, 0, .9f));
		Color_SideBar_Interface	=Con.Bind(Title, "Sidebar interface items color",			new Color(0, 0, 1, 1));
		Color_SideBar_Highlight	=Con.Bind(Title, "Sidebar highlight color",					new Color(1, 1, 0, 0.5f));
		SideBarWidth			=Con.Bind(Title, "Sidebar width (also resizable w/ mouse)",	480, AVR(300, 800));
		Color_MarkerLabelText	=Con.Bind(Title, "Marker label text color",					Color.white);
		Color_MarkerLabelBG		=Con.Bind(Title, "Marker label background color",			new Color(0, 0, 0, .26f));

		Title="Map Sidebar";
		ShowSidebarOnGameLoad	=Con.Bind(Title, "Starts open on game load",				true);
		ShowSideBarPictures		=Con.Bind(Title, "Show downloaded icon pictures",			true, "When you select an icon on the map, show the images from mapgenie.io on the sidebar.");
		ShowMouseWhenSBVisible	=Con.Bind(Title, "Show mouse with sidebar/search",			false, "Shows the mouse when the sidebar or search window are visible");
		Shortcut_ToggleSideBar	=Con.Bind(Title, "Shortcut key: Show/hide the sidebar",		new KeyboardShortcut(KeyCode.KeypadDivide));
		Shortcut_SB_Up			=Con.Bind(Title, "Sidebar navigation: Up",					new KeyboardShortcut(KeyCode.Keypad8));
		Shortcut_SB_Down		=Con.Bind(Title, "Sidebar navigation: Down",				new KeyboardShortcut(KeyCode.Keypad2));
		Shortcut_SB_Left		=Con.Bind(Title, "Sidebar navigation: Left",				new KeyboardShortcut(KeyCode.Keypad4));
		Shortcut_SB_Right		=Con.Bind(Title, "Sidebar navigation: Right",				new KeyboardShortcut(KeyCode.Keypad6));
		Shortcut_SB_ToggleItem	=Con.Bind(Title, "Shortcut Key: Execute selected item",		new KeyboardShortcut(KeyCode.Keypad5), "Executes the selected button/group/category on the sidebar.");
		Shortcut_SB_SelectIcon	=Con.Bind(Title, "Shortcut Key: Select icon",				new KeyboardShortcut(KeyCode.KeypadEnter), "When the sidebar and marker modes are active, pressing this will ‘select’ the icon you are over");

		Title="Saved Value Window";
		SaveValues_Monitor		=Con.Bind(Title, "Enable window and monitor all updates",	false, "Enable entering the Save Value Window through the shortcut key or side bar. When turned off, non-linked PlayerData save-values will not be recorded.");
		AnonymousSubmissions	=Con.Bind(Title, "Make submissions anonymously",			false, "Normally your Steam username is included with submissions for both credit and to help defeat spam, but it can be done anonymously.");
		Shortcut_SaveValueWindow=Con.Bind(Title, "Shortcut Key: Show/hide the window",		new KeyboardShortcut(KeyCode.KeypadMultiply), "Toggle the value window. See help or mod page for more information.");
		Shortcut_Val_ScrollUp	=Con.Bind(Title, "Scroll Up",								new KeyboardShortcut(KeyCode.Keypad9));
		Shortcut_Val_ScrollDown	=Con.Bind(Title, "Scroll Down",								new KeyboardShortcut(KeyCode.Keypad3));

		Title="Internal";
		UseErrorLog				=Con.Bind(Title, "Write Atlas errors to error file",		true, "Saves errors generated by the Atlas to “{0}”", IsAdvanced);
		UseErrorLogSend			=Con.Bind(Title, "Check this to send Atlas errors to dev",	true, "Errors generated and saved to “{0}” will be sent to the developer", IsAdvanced);
		QueryTime_PersistentObj	=Con.Bind(Title, "Persistent Object Query Time",			.25f, AVR(.1f, 5f, "How often in seconds to query persistent data value changes (separate thread). There are never more than ~50 objects per scene (more usually like 10), and current value queries are usually really simple, so this doesn’t take a lot of processing. Honestly, these could probably run every frame just fine."));
		QueryTime_PlayerData	=Con.Bind(Title, "Player Data Query Time",					1.0f, AVR(.1f, 5f, "How often in seconds to query player data value changes (separate thread). There are over 1200 reflection lookups for this so query time is less often. Honestly, these could probably run every frame just fine."));
		Rect_SaveValuesWindow	=Con.Bind(Title, "Window Position: Save Values",			Rect.zero, null, NonBrowsable);
		Rect_SearchWindow		=Con.Bind(Title, "Window Position: Search",					Rect.zero, null, NonBrowsable);
		CategoryToggleStates	=Con.Bind(Title, "Category States",							string.Empty, "DO NOT EDIT THIS. List of categories and their toggle states.", NonBrowsable);
		MarkerLabels			=Con.Bind(Title, "Marker Labels",							"{}", "DO NOT EDIT THIS. List of marker labels", NonBrowsable);
		Tr.AddFormatParameters("Write Atlas errors to error file", "SettingDescriptions", Plugin.CErrorLog.PAtlasErrorLogName);
		Tr.AddFormatParameters("Check this to send Atlas errors to dev", "SettingDescriptions", Plugin.CErrorLog.PAtlasErrorLogName);

		//When player checks to unlock the map, flip it back off after 1 second (Can’t put this in MapControl since it doesn’t exist yet)
		UnlockMap.SettingChanged += (_, _) => {
			if(!UnlockMap)
				return;
			PlayerData.instance.HasMossGrottoMap=true;
			System.Collections.IEnumerator FlipUnlockMapBackOff()
			{
				yield return new WaitForSecondsRealtime(1);
				UnlockMap.V=false;
				MapControl.CloseInventory();
			}
			_=Catcher.ExecCoroutine("Flip unlock map back off", FlipUnlockMapBackOff());
		};

		//When settings are reloaded we have to fix a few configs that aren’t normally watched
		PSC=new PerSaveConfig(PConfig, FileOps.GetPluginPath) {
			NumBackupsToKeep=2,
			ConfigChangedOnLoad=ConfigEntry => {
				 	 if(ConfigEntry==CategoryToggleStates	)   MapControl.Self?.DS.LoadCategoryToggleStates(true);
				else if(ConfigEntry==Rect_SaveValuesWindow	) _=SaveValuesWindow.Self?.WindowRect=Rect_SaveValuesWindow;
				else if(ConfigEntry==Rect_SearchWindow		) _=SearchWindow.Self?.WindowRect=Rect_SearchWindow;
			}
		};

		//Set up a spoiler pair for MapInAbyss
		SpoilerPair<bool> AbyssSP=new(MapInAbyssUnspoiled, MapInAbyss);
		SilkDev.Events.GameEvents.OnGameLoaded += _ => AbyssSP.CanSpoil(PlayerData.instance.visitedAbyss);
		SilkDev.Windows.Window.OnNextFrame(() =>
			MonitorSaveValues.Self.RegisterValueChanged.Add(
				new MonitorSaveValues.FromNamePair(nameof(PlayerData), nameof(PlayerData.visitedAbyss)),
				SI => AbyssSP.CanSpoil((bool)SI.NewValue)
			)
		);
	}

	//Get the icon files — Enumerate all resources or files matching “Icons-*.png”.
	//Dictionary keys are the filenames, and values are derived from the wildcard portion, with spaces added before capital letters that are preceded by lowercase letters or numbers.
	private const string IconStrStart="Icons-", IconStrEnd=".png";
	private Dictionary<string, string> GetIconFiles() =>
		((HashSet<string>)[
			.. FileOps.GetDirFiles(FileOps.GetPluginPath, $"{IconStrStart}*{IconStrEnd}").Select(FileOps.GetFileName),
			.. FileOps.GetResources().Where(static FileName => FileName.StartsWith(IconStrStart) && FileName.EndsWith(IconStrEnd))
		]).ToDictionary(
			Str => Str,
			Str => System.Text.RegularExpressions.Regex.Replace(
				Str[IconStrStart.Length .. ^IconStrEnd.Length],
				@"(?<=[a-z0-9])([A-Z])", " $1"
			)
		);
}