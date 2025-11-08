using BepInEx.Configuration;
using System.Collections.Generic;

namespace SilkDev.Configs;

//This is a drop in wrapper for the ConfigFile class with the .Bind() functions.
//It orders the config file sections by adding numbers to their front. The options are configured using the Order parameter, but can optionally also have numbers added.
//This will recover settings from section/key names that change due to updated index numbers
//Warning: This prepends a “Zero Width Space” unicode character on key names to force numeric sorting
public class OrderedConfig
{
	public readonly ConfigFile CF;
	public IReadOnlyList<ConfigEntryBase> Configs => _Configs.AsReadOnly();
	private readonly Dictionary<string, (int SectionID, int CurrentItemID)> ConfigSections=[];
	private readonly Dictionary<ConfigDefinition, string> Orphaned;
	private readonly List<ConfigEntryBase> _Configs=[];
	private const char ZeroWidthSpace='\u200B';
	private const bool AddSettingNumbers=false;
	public OrderedConfig(ConfigFile CF)
	{
		this.CF=CF;
		Orphaned=new Reflectors.RProp<ConfigFile, Dictionary<ConfigDefinition, string>>(CF, "OrphanedEntries")?.Get()!;
		if(Orphaned==null) {
			Orphaned=[];
			Log.Error("Couldn’t get OrphanedEntries. Values for names with changed indexes will be lost");
		}
	}

	public ConfigEntry<T> Bind<T>(ConfigDefinition ConfigDefinition, T DefaultValue, ConfigDescription? ConfigDescription=null) =>
		InternalBind(ConfigDefinition.Section, ConfigDefinition.Key, DefaultValue, ConfigDescription);
	public ConfigEntry<T> Bind<T>(string SectionName, string Key, T DefaultValue, ConfigDescription? ConfigDescription=null) =>
		InternalBind(SectionName, Key, DefaultValue, ConfigDescription);
	public ConfigEntry<T> Bind<T>(string SectionName, string Key, T DefaultValue, string Description) =>
		InternalBind(SectionName, Key, DefaultValue, new ConfigDescription(Description));
	public ConfigEntry<T> Bind<T>(string SectionName, string Key, T DefaultValue, string? Description, ConfigurationManagerAttributes Attr) => //Extra overload
		InternalBind(SectionName, Key, DefaultValue, new ConfigDescription(Description ?? Misc.Empty, null, Attr));

	private ConfigEntry<T> InternalBind<T>(string SectionName, string Key, T DefaultValue, ConfigDescription? ConfigDescription)
	{
		//Get and store the indexes for the section and its items
		int SectionID=ConfigSections.Count+1, CurrentItemID=0;
		if(ConfigSections.TryGetValue(SectionName, out var Sections))
			(SectionID, CurrentItemID)=(Sections.SectionID, Sections.CurrentItemID);
		ConfigSections[SectionName]=(SectionID, ++CurrentItemID);

		//See if the item is in the Orphaned list, and if not, try to match it with old settings
		ConfigDefinition CD=new(
			$"{SectionID}. {SectionName}",
				  !AddSettingNumbers ? Key
				: $"{CurrentItemID.ToString().PadLeft(2, ZeroWidthSpace)}. {Key}"
		);
		if(!Orphaned.ContainsKey(CD)) {
			//Search for a matching orphan and set to new key if found
			ConfigDefinition? Found=null;
			foreach((ConfigDefinition ConfName, string ConfValue) in Orphaned)
				if(
					(ConfName.Section.EndsWith($". {SectionName}") || ConfName.Section==SectionName) &&
					(ConfName.Key.EndsWith($". {Key}") || ConfName.Key==Key)
				) {
					Log.Info($"Config: Recovered ‘{SectionName}.{Key}’ from ‘{ConfName}’");
					Orphaned[CD]=ConfValue;
					Found=ConfName;
					break;
				}

			//If a matching orphan was found, remove the old one
			if(Found!=null)
				_=Orphaned.Remove(Found);
			else
				Log.Info($"Config: Couldn’t recover {CD}");
		}

		//Force the order
		if(ConfigDescription?.Tags.Length>0 && ConfigDescription.Tags[0] is ConfigurationManagerAttributes CMA)
			CMA.Order=10000-_Configs.Count;
		else
			ConfigDescription=new ConfigDescription(
				ConfigDescription?.Description ?? Misc.Empty,
				ConfigDescription?.AcceptableValues ?? null,
				[.. ConfigDescription?.Tags ?? [], new ConfigurationManagerAttributes() { Order=10000-_Configs.Count }]
			);

		//Create the new binding
		ConfigEntry<T> CE=CF.Bind(CD, DefaultValue, ConfigDescription);
		_Configs.Add(CE);
		return CE;
	}
}

//From ConfigurationManagerAttributes.cs
#pragma warning disable 0169, 0414, 0649, CS8618
public sealed class ConfigurationManagerAttributes
{
	public bool? ShowRangeAsPercent;
	public System.Action<ConfigEntryBase> CustomDrawer;
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
	public bool? ReadOnly;
	public bool? IsAdvanced;
	public System.Func<object, string> ObjToStr;
	public System.Func<string, object> StrToObj;
}