using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace SilkDev.Configs;

//Create a config entry that is a string->string key value pair with KVP.Key being the stored/returned value and the KVP.Value as the display string
public class KeyPairConfig
{
	//Custom struct for key-value
	public readonly struct Option(string Key, string Display) : IEquatable<Option>
	{
		public readonly string Key				=Key;
		public readonly string Display			=Display;
		public override string ToString()		=> Display;
		public override bool Equals(object Obj)	=> Obj is Option Other && Equals(Other);
		public bool Equals(Option Other)		=> Key==Other.Key;
		public override int GetHashCode()		=> Key?.GetHashCode() ?? 0;
	}

	//Custom converter
	public class CustomConverter(Dictionary<string, Option> Options) : System.ComponentModel.TypeConverter
	{
		//Convert to string: Use Key
		public override object? ConvertTo(ITypeDescriptorContext _, CultureInfo _2, object Value, Type DestinationType) =>
			DestinationType==typeof(string) ? ((Option)Value).Key : null;

		//Convert from string: Find Option by Key
		public override object ConvertFrom(ITypeDescriptorContext _, CultureInfo _2, object Key) =>
			Key is string KeyStr && Options.TryGetValue(KeyStr, out Option Result) ? Result : Options.First().Value;
	}

	//The created information
	public readonly ConfigDescription ConfigDesc;
	public readonly Dictionary<string, Option> Options;
	public KeyPairConfig(Dictionary<string, string> Entries, string Description="")
	{
		if(Entries?.Count is 0 or null)
			throw new ArgumentException("Entries cannot be empty");

		//Create a new dictionary for lookup
		Options=Entries.ToDictionary(
			KVP => KVP.Key,
			KVP => new Option(KVP.Key, KVP.Value)
		);

		ConfigDesc=new ConfigDescription(
			Description,
			new AcceptableValueList<Option>([.. Options.Values]),
			new CustomConverter(Options)
		);
	}
}