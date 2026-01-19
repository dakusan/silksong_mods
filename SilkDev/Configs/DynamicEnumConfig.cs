using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace SilkDev.Configs;

//Creates a ConfigEntry<Enum> with dynamic values
//Dictionary values are only used as display text in the configuration interface. Everything else uses the dictionary keys.
public class DynamicEnumConfig
{
	//Members
	private readonly ConfigEntryBase MyDynamicEntry;
	private readonly List<string> Keys;
	public string[] GetKeys => [.. Keys];
	public bool HasKey(string Key) => Keys.Contains(Key);

	//Initialize from different config file types
	public DynamicEnumConfig(ConfigFile			CF, string SectionName, string KeyName, Dictionary<string, string> Options, string Description=DevStrings.Empty, string? Default=null) =>
		(MyDynamicEntry, Keys)=CreateConfig(CF, SectionName, KeyName, Description, Default, Options);
	public DynamicEnumConfig(TranslatedConfig	CF, string SectionName, string KeyName, Dictionary<string, string> Options, string Description=DevStrings.Empty, string? Default=null) =>
		(MyDynamicEntry, Keys)=CreateConfig(CF, SectionName, KeyName, Description, Default, Options);

	//The real initialization
	private static (ConfigEntryBase, List<string>) CreateConfig<T>(T CF, string SectionName, string KeyName, string Description, string? Default, Dictionary<string, string> Options)
	{
		//Create the dynamic bindings
		TypeInfo DynamicEnumType=BuildEnum(
			Options,
			System.Text.RegularExpressions.Regex.Replace($"Enum_{SectionName}_{KeyName}", @"\W", "_") //Replace invalid characters with underscore
		).CreateTypeInfo();
		MethodInfo BindMethod=GetMethodInfo<T>(DynamicEnumType);

		//Create the ConfigEntry
		List<string> MyKeys=[.. Options.Select(static KVP => KVP.Key)];
		object EntryObj=BindMethod.Invoke(CF, [
			new ConfigDefinition(SectionName, KeyName),
			Enum.ToObject(DynamicEnumType, MyKeys.IndexOf(Default ?? string.Empty) is int i && i!=-1 ? i : 0),
			new ConfigDescription(Description)
		]);

		//Return new data to constructors
		return ((ConfigEntryBase)EntryObj, MyKeys);
	}

	//Create the dynamic enum
	private static EnumBuilder BuildEnum(Dictionary<string, string> Options, string Name)
	{
		//Create the enum through reflection
		EnumBuilder EnumBuilder=AssemblyBuilder.DefineDynamicAssembly(
			new AssemblyName("DynamicEnumAssembly"), AssemblyBuilderAccess.Run)
			.DefineDynamicModule("DynamicEnumModule")
			.DefineEnum(Name, TypeAttributes.Public, typeof(int));

		//Fill in its values
		int Index=0;
		foreach((string Key, string Value) in Options)
			EnumBuilder.DefineLiteral(Key, Index++)
			.SetCustomAttribute(new CustomAttributeBuilder(
				typeof(System.ComponentModel.DescriptionAttribute).GetConstructor([typeof(string)]),
				[Value]
			));

		return EnumBuilder;
	}

	//Prepare the bind method
	private static MethodInfo GetMethodInfo<T>(TypeInfo BuildEnumType) =>
		(typeof(T)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.FirstOrDefault(static M =>
				   M.Name=="Bind"
				&& M.IsGenericMethodDefinition
				&& M.GetParameters().Length==3
				&& M.GetParameters()[0].ParameterType==typeof(ConfigDefinition)
				&& M.GetParameters()[2].ParameterType==typeof(ConfigDescription)
			) ?? throw new InvalidOperationException("Bind method not found."))
			.MakeGenericMethod(BuildEnumType);

	//Get and set the value
	public string Value {
		get => MyDynamicEntry.BoxedValue.ToString();
		set => MyDynamicEntry.BoxedValue=Enum.ToObject(
			MyDynamicEntry.SettingType,
			Keys.IndexOf(value) is int i && i!=-1 ? i : throw new ArgumentException("Key not found")
		);
	}

	public event EventHandler SettingChanged
	{
		add		=> MyDynamicEntry.GetType().GetEvent(nameof(SettingChanged)).AddMethod		.Invoke(MyDynamicEntry, [value]);
		remove	=> MyDynamicEntry.GetType().GetEvent(nameof(SettingChanged)).RemoveMethod	.Invoke(MyDynamicEntry, [value]);
	}
}