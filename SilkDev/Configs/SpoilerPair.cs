using BepInEx.Configuration;
using System.Linq;

namespace SilkDev.Configs;

//Toggles the visibility of a pair of settings based upon if the spoiler has been reached yet
public class SpoilerPair<T> where T : System.IEquatable<T>
{
	public readonly ConfigEntryT<T> Unspoiled, IsSpoiled;
	public SpoilerPair(ConfigEntryT<T> Unspoiled, ConfigEntryT<T> IsSpoiled)
	{
		(this.Unspoiled, this.IsSpoiled)=(Unspoiled, IsSpoiled);

		//These 2 settings must stay in sync
		IsSpoiled.SettingChanged += (_, _) => Misc.IFF(!Unspoiled.V.Equals(IsSpoiled.V), () => Unspoiled.V=IsSpoiled);
		Unspoiled.SettingChanged += (_, _) => Misc.IFF(!IsSpoiled.V.Equals(Unspoiled.V), () => IsSpoiled.V=Unspoiled);

		//Force the spoiled entry to not be visible
		CanSpoil(false);
	}

	//Update the visibility depending on if it can be spoiled or not
	public void CanSpoil(bool CanSpoil)
	{
		static ConfigurationManagerAttributes? GetCMA(ConfigEntryBase CE) =>
		(ConfigurationManagerAttributes?)CE.Description.Tags.FirstOrDefault(T => T is ConfigurationManagerAttributes);
		_=GetCMA(IsSpoiled.CE)?.Browsable=CanSpoil;
		_=GetCMA(Unspoiled.CE)?.Browsable=!CanSpoil;
	}
}