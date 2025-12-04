using BepInEx;
using BepInEx.Configuration;
using System.Linq;
using UnityEngine;

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

//A ConfigEntryT<KeyboardShortcut> with special handling for Is(Up/Down/Pressed)
public class ConfigEntryTKeyboardShortcut(ConfigEntry<KeyboardShortcut> CE) : ConfigEntryT<KeyboardShortcut>(CE)
{
	//ShortcutKey handling
	public static bool DEFAULT_IgnoreExtraKeysOnShortcut=true; //When object is created, IgnoreExtraKeysOnShortcut is set to what this currently is. This enables setting the value for entire configuration sections at once.
	public bool IgnoreExtraKeysOnShortcut=DEFAULT_IgnoreExtraKeysOnShortcut; //If this is true, the keyboard functions will ignore extra keys being down
	public bool IsDown		() => CheckShortcut(CheckShortcutType.Down		);
	public bool IsUp		() => CheckShortcut(CheckShortcutType.Up		);
	public bool IsPressed	() => CheckShortcut(CheckShortcutType.IsPressed	);
	private enum CheckShortcutType {Down, Up, IsPressed};
	private bool CheckShortcut(CheckShortcutType CheckType)
	{
		//If IgnoreExtraKeysOnShortcut is false, use the original functions
		KeyboardShortcut KBS=CE.Value;
		if(!IgnoreExtraKeysOnShortcut)
			return CheckType switch {
				CheckShortcutType.Down		=> KBS.IsDown	(),
				CheckShortcutType.Up		=> KBS.IsUp		(),
				CheckShortcutType.IsPressed	=> KBS.IsPressed(),
				_							=> false
			};

		//Check the main key
		KeyCode MainKey=KBS.MainKey;
		if(MainKey==KeyCode.None)
			return false;
		if(CheckType switch {
			CheckShortcutType.Down		=> UnityInput.Current.GetKeyDown(MainKey),
			CheckShortcutType.Up		=> UnityInput.Current.GetKeyUp	(MainKey),
			CheckShortcutType.IsPressed	=> UnityInput.Current.GetKey	(MainKey),
			_							=> false
		}==false)
			return false;

		//Check the modifier keys
		return KBS.Modifiers.All(UnityInput.Current.GetKey);
	}

	public static implicit operator ConfigEntryTKeyboardShortcut(ConfigEntry<KeyboardShortcut> CE) => new(CE);
}