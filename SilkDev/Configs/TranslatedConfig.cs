using BepInEx.Configuration;
using SilkDev.Events;
using SilkDev.Hooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static SilkDev.Configs.TranslatedConfig;

namespace SilkDev.Configs;

//This is a drop in wrapper for the ConfigFile class with the .Bind() functions.
//It preserves original ordering for sections and configs. Also supports translations.
//The 3 translation sections for config names, sections, and descriptions are: SettingNames, SettingSections, SettingDescriptions.
//Sections are ordered by adding sorting numbers to their front. Warning: “Zero Width Space” unicode characters can be prepended on section names to force proper numeric sorting.
//Translations and ordering are only supported by the official BepInEx ConfigurationManager (Not BepInExConfigManager.Mono.dll).
public class TranslatedConfig(ConfigFile CF, Translations? Tr=null)
{
	public static bool FixBlankDescriptions=true; //If true, when ConfigEntries are created, blank descriptions will contain a space (to mitigate a ConfigurationManager bug)

	public readonly ConfigFile CF=CF;
	private readonly Translations Tr=Tr ?? new Translations();
	public IReadOnlyList<ConfigEntryBase> Configs => _Configs.AsReadOnly();
	private readonly Dictionary<string, (int SectionID, int CurrentItemID)> ConfigSections=[];
	private readonly List<ConfigEntryBase> _Configs=[];
	public DynamicEnumConfig? LanguageField { get; private set; }

	private const char ZeroWidthSpace='\u200B';
	private const int LargeConfigAmount=100000; //There shouldn’t be more configuration entries in a section than this

	public ConfigEntry<T> Bind<T>(ConfigDefinition ConfigDefinition, T DefaultValue, ConfigDescription? ConfigDescription=null) =>
		InternalBind(ConfigDefinition.Section, ConfigDefinition.Key, DefaultValue, ConfigDescription);
	public ConfigEntry<T> Bind<T>(string SectionName, string Key, T DefaultValue, ConfigDescription? ConfigDescription=null) =>
		InternalBind(SectionName, Key, DefaultValue, ConfigDescription);
	public ConfigEntry<T> Bind<T>(string SectionName, string Key, T DefaultValue, string Description) =>
		InternalBind(SectionName, Key, DefaultValue, new ConfigDescription(Description));
	public ConfigEntry<T> Bind<T>(string SectionName, string Key, T DefaultValue, string? Description, ConfigurationManagerAttributes Attr) => //Extra overload
		InternalBind(SectionName, Key, DefaultValue, new ConfigDescription(Description ?? string.Empty, null, Attr));

	private ConfigEntry<T> InternalBind<T>(string SectionName, string Key, T DefaultValue, ConfigDescription? ConfigDescription)
	{
		//Get and store the indexes for the section and its items
		int SectionID=ConfigSections.Count+1, CurrentItemID=0;
		if(ConfigSections.TryGetValue(SectionName, out var Sections))
			(SectionID, CurrentItemID)=(Sections.SectionID, Sections.CurrentItemID);
		ConfigSections[SectionName]=(SectionID, ++CurrentItemID);

		//Make sure ConfigDescription exists and has a ConfigurationManagerAttributes
		static string FixEmpty(string? Str) => FixBlankDescriptions && string.IsNullOrEmpty(Str) ? "\u00A0" : Str!;
		ConfigurationManagerAttributes CMA=ConfigDescription?.Tags?.OfType<ConfigurationManagerAttributes>().FirstOrDefault()!;
		if(CMA==null)
			ConfigDescription=new ConfigDescription(
				FixEmpty(ConfigDescription?.Description),
				ConfigDescription?.AcceptableValues,
				[.. ConfigDescription?.Tags ?? [], CMA=new ConfigurationManagerAttributes()]
			);

		//Fix the Order and DispName
		CMA.Order=LargeConfigAmount-_Configs.Count;
		CMA.DispName=Key;

		//Create the new binding
		ConfigEntry<T> CE=CF.Bind(new ConfigDefinition(SectionName, Key), DefaultValue, ConfigDescription);
		_Configs.Add(CE);
		return CE;
	}

	//Add a language field to the config
	public DynamicEnumConfig BindLanguage(string Title, string? Default=null) //If Default is not set, uses Translations.DefaultLang
	{
		//Set the default if not given
		Default ??= Tr.DefaultLang;

		if(LanguageField!=null)
			throw new InvalidOperationException($"{nameof(LanguageField)} already set");

		//Fill in the enum values
		Dictionary<string, string> LList=Tr.GetEnum ?? [];
		if(!LList.ContainsKey(Default))
			LList[Default]=Default==Tr.DefaultLang ? Tr.DefaultLangName : $"Unknown {Default}";

		//Create the field and handle changing the language
		LanguageField=new DynamicEnumConfig(this, Title, Translations.LanguageAsStr, LList, Translations.PickLanguageAsStr, Default);
		LanguageField.SettingChanged += (_, _) => LanguageChanged(LanguageField.Value);

		AddLangFieldToSync(LanguageField);
		return LanguageField;
	}

	//Keep language fields in-sync
	private static bool SyncRunning=false; //Make sure there is no SettingChanged recursion
	private static readonly List<DynamicEnumConfig> AllLanguageFields=[];
	private void RunLanguageSync(string NewLang)
	{
		if(SyncRunning)
			return;
		SyncRunning=true;
		AllLanguageFields.ForEach(LF => Misc.IFF(LF.Value!=NewLang && LF.HasKey(NewLang), () => LF.Value=NewLang));
		SyncRunning=false;
		RefreshConfigManager();
	}
	private void AddLangFieldToSync(DynamicEnumConfig LanguageField)
	{
		LanguageField.SettingChanged += (_, _) => RunLanguageSync(LanguageField.Value);
		AllLanguageFields.Add(LanguageField);
	}

	//Call this after all binding has been done
	public void Complete() =>
		LanguageChanged(LanguageField?.Value);

	//Update the displayed language
	private void LanguageChanged(string? NewLanguage)
	{
		Tr.Language=NewLanguage ?? string.Empty;

		//Create the section titles
		Dictionary<string, string> SectionTitles=new(ConfigSections.Count);
		int SectionsStrLen=ConfigSections.Count.ToString().Length;
		foreach(var (SectionName, SectionInfo) in ConfigSections)
			SectionTitles[SectionName]=string.Join(null, [
				SectionInfo.SectionID.ToString().PadLeft(SectionsStrLen, ZeroWidthSpace), ". ",
				Tr.T(SectionName, SettingTranslationSections.Sections.TranslationName()),
			]);

		//Set the translations for the language field
		void UpdateLangStr(SettingTranslationSections SectionName, string Default, Func<Translations.LangNames, string?> GetVal)
		{
			//Make sure the setting section exists in the translation dictionaries
			Tr.Sections ??= [];
			string SectionNameStr=SectionName.TranslationName();
			if(!Tr.Sections.TryGetValue(SectionNameStr, out Dictionary<string, string> LSec))
				LSec=Tr.Sections[SectionNameStr]=[];

			//Get the translation (Looked up in Tr.Languages [from Languages.json])
			string NewVal=GetVal(Tr.Languages!.GetValueOrDefault(Tr.Language)) ?? Default;

			//If is the name field and not set to the default, append the default in square brackets to help clarify where the language field is
			if(SectionName==SettingTranslationSections.Names && NewVal!=Default)
				NewVal+=$" [{Default}]";

			//Save the value
			LSec[Translations.LanguageAsStr]=NewVal;
		}
		UpdateLangStr(SettingTranslationSections.Names		 , Translations.LanguageAsStr	 , static LN => LN?.LanguageAsString	);
		UpdateLangStr(SettingTranslationSections.Descriptions, Translations.PickLanguageAsStr, static LN => LN?.PickLanguageAsString);

		//Update the config names
		_Configs.ForEach(CE => {
			//Get the ConfigurationManagerAttributes to modify
			ConfigurationManagerAttributes CMA=CE.Description.Tags.OfType<ConfigurationManagerAttributes>().FirstOrDefault();
			if(CMA==null) {
				Log.Error($"{nameof(ConfigurationManagerAttributes)} not found for: {CE.Definition.Section}.{CE.Definition.Key}");
				return;
			}

			//Update the strings
			CMA.Category=SectionTitles[CE.Definition.Section];
			CMA.DispName=Tr.T(CE.Definition.Key, SettingTranslationSections.Names.TranslationName());
			CMA.Description=Tr.TDef(CE.Definition.Key, SettingTranslationSections.Descriptions.TranslationName(), CE.Description.Description);
		});
	}

	//Add translation format parameters
	public enum SettingTranslationSections { Names, Sections, Descriptions };
	public void AddTranslationParameters(SettingTranslationSections Section, ConfigEntryBase CE, params object[] Params) =>
		Tr.AddFormatParameters(Section switch {
			SettingTranslationSections.Names		=> CE.Definition.Key,
			SettingTranslationSections.Sections		=> CE.Definition.Section,
			SettingTranslationSections.Descriptions => CE.Definition.Key,
			_ => "!INVALID!"
		}, Section.TranslationName(), Params);

	//If ConfigurationManager is open then close and reopen it to refresh language
	private static readonly HookCMWindow CM=CreateHook();
	private static HookCMWindow CreateHook()
	{
		static void PatchOnce()
		{
			GameEvents.OnUpdate -= PatchOnce;
			CM.IsEnabled=true;
		}
		GameEvents.OnUpdate += PatchOnce;
		return HookCMWindow.Self=new HookCMWindow();
	}
	private void RefreshConfigManager()
	{
		if(CM.FailedHook)
			_=new Windows.PopupMessage(Tr.T("Refreshing config manager failed. Close and reopen it for translation changes", "Errors", true));

		if(!CM.DisplayingWindow)
			return;

		//For the config Window: Close it, open it, restore its position
		PropertyInfo SettingWindowRect=CM.CM.GetType()?.GetProperty("SettingWindowRect", BindingFlags.Instance|BindingFlags.NonPublic)!;
		var CurRect=(UnityEngine.Rect)SettingWindowRect.GetValue(CM.CM);
		CM.DisplayingWindow=false;
		CM.DisplayingWindow=true;
		SettingWindowRect.SetValue(CM.CM, CurRect);
	}
	private class HookCMWindow() : LiveHook(
		new("temp.patcher.dakusan.ConfigManager"),
		DynamicHook.FindType("ConfigurationManager.ConfigurationManager")?.GetMethod("BuildSettingList", BindingFlags.Instance|BindingFlags.Public)!,
		PostfixMethod: typeof(HookCMWindow).GetMethod(nameof(GetCM), BindingFlags.Static|BindingFlags.NonPublic)
	) {
		public static HookCMWindow Self=null!;
		public object CM=null!;
		public PropertyInfo PI=null!;
		protected static void GetCM(object __instance)
		{
			Self.CM=__instance;
			Self.PI=Self.CM.GetType()?.GetProperty("DisplayingWindow", BindingFlags.Instance|BindingFlags.Public)!;
			Self.IsEnabled=false;
		}
		public bool DisplayingWindow
		{
			get => CM!=null && (bool)(PI?.GetValue(CM) ?? false);
			set => Misc.IFF(CM!=null, () => PI?.SetValue(CM, value));
		}
	}
}

//Get the translation section name for a SettingTranslationSections
public static class Extension {
	public static string TranslationName(this SettingTranslationSections Section) => $"Setting{Section}";
}

//From ConfigurationManager.dll/ConfigurationManager.SettingEntryBase
#pragma warning disable CS8618 //Non-nullable field must contain a non-null value when exiting constructor
public sealed class ConfigurationManagerAttributes
{
//	public object[] AcceptableValues;
//	public KeyValuePair<object, object> AcceptableValueRange;
	public bool? ShowRangeAsPercent;
	public Action<ConfigEntryBase> CustomDrawer;
	public CustomHotkeyDrawerFunc CustomHotkeyDrawer;
	public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);
	public bool? Browsable;
	public string Category;
	public object DefaultValue;
	public bool? HideDefaultButton;
	public bool? HideSettingName;
	public string Description;
	public string DispName;
	public int? Order;
//	public BepInPlugin PluginInfo;
	public bool? ReadOnly;
//	public abstract Type SettingType;
//	public BaseUnityPlugin PluginInstance;
	public bool? IsAdvanced;
	public Func<object, string> ObjToStr;
	public Func<string, object> StrToObj;
}