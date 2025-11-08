using BepInEx.Configuration;

namespace SilkDev.Configs;

//A ConfigEntry wrapper class that allows getting/setting the value without using .Value
public class ConfigEntryT<T>(ConfigEntry<T> CE)
{
	//The reason for this class
	public static implicit operator T(ConfigEntryT<T> Entry) => Entry.CE.Value;
	public T V		{ get => CE.Value; set => CE.Value=value; }

	//Member forwarding
	public readonly ConfigEntry<T> CE=CE;
	public T Value	{ get => CE.Value; set => CE.Value=value; }
	public object BoxedValue => CE.BoxedValue;
	public event System.EventHandler SettingChanged
	{
		add => CE.SettingChanged += value;
		remove => CE.SettingChanged -= value;
	}
	public bool IsDown		() => CE is ConfigEntry<KeyboardShortcut> CEKB && CEKB.Value.IsDown		();
	public bool IsUp		() => CE is ConfigEntry<KeyboardShortcut> CEKB && CEKB.Value.IsUp		();
	public bool IsPressed	() => CE is ConfigEntry<KeyboardShortcut> CEKB && CEKB.Value.IsPressed	();

	//Automatic conversion between wrapper and base types
	public static implicit operator ConfigEntry<T>(ConfigEntryT<T> Entry) => Entry.CE;
	public static implicit operator ConfigEntryT<T>(ConfigEntry<T> CE) => new(CE);

	//Equality
	public override bool Equals(object Obj) =>
		Obj switch {
			ConfigEntryT<T> Other  => Equals(Other),
			ConfigEntry<T>  CEObj  => Equals(CEObj),
			ConfigEntryBase CEBase => Equals(CEBase),
			_					   => false
		};
	public bool Equals(ConfigEntryT<T>?  Other)									=>				  ReferenceEquals(CE, Other?.CE);
	public bool Equals(ConfigEntry<T>?   Other)									=> Other!=null && ReferenceEquals(CE, Other);
	public bool Equals(ConfigEntryBase?  Other)									=> Other!=null && ReferenceEquals(CE, Other);
	public override int GetHashCode()											=> CE?.GetHashCode() ?? 0;
	public static bool operator ==(ConfigEntryT<T> Left, ConfigEntryBase Right)	=> Left?.Equals(Right) ?? Right is null;
	public static bool operator !=(ConfigEntryT<T> Left, ConfigEntryBase Right)	=> !(Left==Right);
	public static bool operator ==(ConfigEntryBase Left, ConfigEntryT<T> Right)	=> Right==Left;
	public static bool operator !=(ConfigEntryBase Left, ConfigEntryT<T> Right)	=> !(Right==Left);
}