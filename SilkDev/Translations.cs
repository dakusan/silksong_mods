using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SilkDev;

//Loads translations from $TranslationsPath/$LangIsoName$TranslationFileExtension.
public class Translations
{
	public const string ROOT="ROOT"; //The default section all translations are under
	public const string LanguageAsStr="Language";
	public const string PickLanguageAsStr="Pick your language";
	public const string TranslationFileExtension=".tr.json";

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
	public Translations() => TranslationsPath=FileOps.GetPluginPath; //No translations

	//Load current language
	public Dictionary<string, Dictionary<string, string>>? Sections;
	public string Language
	{
		get;
		set => Misc.IFF(value is not null && field!=value, () => LoadLanguage(field=value!));
	} = Misc.Empty;
	private void LoadLanguage(string ISO)
	{
		try {
			JSON.JsonUtils.Deserialize(FileOps.ReadFile(FileOps.PathCombine(TranslationsPath, ISO+TranslationFileExtension)), out Sections);
		} catch(System.Exception e) {
			Sections=null;
			Log.Error($"Could not load language file: {e.Message}");
		}
	}

	//Translation functions
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public string  T				(string Key, string Section=ROOT) => TranslateDef(Key, Section, Key  ); //Fallback to Key if not found
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public string  Translate		(string Key, string Section=ROOT) => TranslateDef(Key, Section, Key  ); //Fallback to Key if not found
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public string? TranslateNull	(string Key, string Section=ROOT) => TranslateDef(Key, Section, null!); //Fallback to null if not found

	//Replacement arguments for parameterized translation strings (e.g., {0} placeholders).
	public Dictionary<string, object[]> FormatParameters=[]; //If SectionName is not ROOT, Expects "SectionName/" before the Key
	public string TranslateDef(string Key, string Section=ROOT, string Default="") //Fallback to Default if not found
	{
		string? Text=Sections?.GetValueOrDefault(Section)?.GetValueOrDefault(Key) ?? Default;
		return
			  Text is not null && FormatParameters.TryGetValue(Section==ROOT ? Key : $"{Section}/{Key}", out object[] FPs)
			? string.Format(Text, FPs)
			: Text!; //Only TranslateNull should return null as all other functions are set to use a default non-null string
	}
}