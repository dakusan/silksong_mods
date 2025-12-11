using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SilkDev;

//Loads translations from $TranslationsPath/$LangIsoName$TranslationFileExtension.
public class Translations
{
	public const string ROOT=Misc.Empty; //The default section all translations are under
	public const string LanguageAsStr="Language";
	public const string PickLanguageAsStr="Pick your language";
	public const string TranslationFileExtension=".tr.json";
	public string DefaultLang="en", DefaultLangName="English";
	public bool DoNotLoadDefaultLanguage=true; //If true, skips loading translations for default language, using hardcoded defaults instead. This reduces non-negligable load time when no default language files exist.

	//The list of languages
	public record class LangNames(string ISO, string Eng, string Native, string LanguageAsString, string PickLanguageAsString);
	public readonly Dictionary<string, LangNames> Languages=[];
	public Dictionary<string, string> GetEnum => Languages.Values.ToDictionary(static L => L.ISO, static L => L.Native);

	//Init
	public readonly string TranslationsPath;
	public Translations(string TranslationsPath, Dictionary<string, string[]> LList) //LList={ISO:[Eng, Native, LanguageAsString, PickLanguageAsString], ...}
	{
		this.TranslationsPath=TranslationsPath;
		LList.ForEach(InData => Languages[InData.Key]=new LangNames(
			InData.Key,
			InData.Value.FirstOrDefault()		?? $"{InData.Key} missing English Name",
			InData.Value.ElementAtOrDefault(1)	?? $"{InData.Key} missing Name",
			InData.Value.ElementAtOrDefault(2)	?? $"{InData.Key} {LanguageAsStr}",
			InData.Value.ElementAtOrDefault(3)	?? $"{InData.Key} {PickLanguageAsStr}"
		));
	}
	public Translations(string TranslationsPath, string LanguageJSON) :
		this(TranslationsPath, JSON.JsonUtils.Deserialize<Dictionary<string, string[]>>(LanguageJSON) ?? []) { }
	public Translations(string TranslationsPath, System.IO.Stream LanguageJSON) :
		this(TranslationsPath, LanguageJSON?.ReadAllAndCloseS() ?? "{}") { }
	public static Translations StandardCreate(string TranslationsPathShort) //Path="Translations/$TranslationsPathShort/"; Languages loaded from “Languages.json” from calling assembly
	{
		try {
			return new Translations(
				FileOps.PathCombine(FileOps.GetPluginPath, "Translations", TranslationsPathShort),
				FileOps.LoadEmbeddedResource("Languages.json", System.Reflection.Assembly.GetCallingAssembly())
			);
		} catch(Exception e) {
			Log.Info($"Error loading languages: {e}");
			return new Translations();
		}
	}

	public Translations() => TranslationsPath=FileOps.GetPluginPath; //No translations

	//Load current language
	public Dictionary<string, Dictionary<string, string>>? Sections;
	public event Action LanguageChanged=delegate { }; //Callbacks for after the language changes
	public string Language
	{
		get;
		set => Misc.IFF(value is not null && field!=value, () => LoadLanguage(field=value!));
	} = Misc.Empty;
	private void LoadLanguage(string ISO)
	{
		try {
			if(ISO==DefaultLang && DoNotLoadDefaultLanguage)
				Sections=null;
			else
				JSON.JsonUtils.Deserialize(FileOps.ReadFile(FileOps.PathCombine(TranslationsPath, ISO+TranslationFileExtension)), out Sections);
		} catch(Exception e) {
			Sections=null;
			Log.Error($"Could not load language file: {e.Message}");
		}
		LanguageChanged(); //Callbacks for after the language changes
	}

	//Translation functions
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public string  T				(string Key, string? Section=null, bool RichSanitize=false, params object[] FormatList) => TranslateDef(Key, Section, Key  , RichSanitize, FormatList); //If not found, return Key
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public string  Translate		(string Key, string? Section=null, bool RichSanitize=false, params object[] FormatList) => TranslateDef(Key, Section, Key  , RichSanitize, FormatList); //If not found, return Key
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public string? TranslateNull	(string Key, string? Section=null, bool RichSanitize=false, params object[] FormatList) => TranslateDef(Key, Section, null!, RichSanitize, FormatList); //If not found, return null
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public string  TDef			(string Key, string? Section=null, string Default=Misc.Empty, //Same as TranslateDef
																													   bool RichSanitize=false, params object[] FormatList) => TranslateDef(Key, Section,Default,RichSanitize, FormatList); //If not found, return Default

	//Replacement arguments for parameterized translation strings (e.g., {0} placeholders).
	public Dictionary<string, object[]> FormatParameters=[]; //If SectionName is not ROOT or null, Expects "SectionName/" before the Key
	public string TranslateDef(string Key, string? Section=null, string Default=Misc.Empty, bool RichSanitize=false, params object[] FormatList) //Return Default if not found. Section is ROOT if null.
	{
		Section ??= ROOT;
		string? Text=Sections?.GetValueOrDefault(Section)?.GetValueOrDefault(Key) ?? Default;
		string? Ret=
			  Text is null ? null //Only TranslateNull should return null as all other functions are set to use a default non-null string
			: FormatList.Length>0 ? string.Format(Text, FormatList)
			: FormatParameters.TryGetValue(Section==ROOT ? Key : $"{Section}/{Key}", out object[] FPs) ? string.Format(Text, FPs)
			: Text;

		return Text!=null && RichSanitize ? Misc.SanitizeRichString(Ret!) : Ret!;
	}
	public void AddFormatParameters(string Key, string? Section=null, params object[] List) =>
		FormatParameters[Section is null or ROOT ? Key : $"{Section}/{Key}"]=List;

	//Translate the string with the default values
	public string GetDefault(string Key, string? Section=null) =>
		FormatParameters.TryGetValue(Section is null or ROOT ? Key : $"{Section}/{Key}", out object[] FPs)
			? string.Format(Key, FPs) : Key;
}