import { CallbackList, DevStrings, Log, StatStr, Util, WillBeSet } from './SharedClasses';
import { Share } from './Share';
import { LoadJson } from './JSON';
import { ConfigEnum } from './Config';

class LangNames { constructor(
	public readonly ISO:string,
	public readonly Eng:string,
	public readonly Native:string,
	public readonly LanguageAsString:string,
	public readonly PickLanguageAsString:string,
) { } }

//Loads translations from $TranslationsPath/$LangIsoName$TranslationFileExtension.
export default class Translations
{
	public static readonly ROOT:string				=StatStr.Empty; //The default section all translations are under
	public static readonly LanguageAsStr			="Language";
	public static readonly PickLanguageAsStr		="Pick your language";
	public static readonly TranslationFileExtension	='.tr.json';
	public static readonly DefaultLang				="en";
	public static readonly DefaultLangName			="English";

	//The list of languages
	public readonly Languages:Record<string, LangNames>={};
	public get GetEnum() { return Object.values(this.Languages).map(L => new ConfigEnum(L.ISO, L.Native)); }

	//Init
	private constructor(public readonly TranslationsPath:string) { }
	public static StandardCreate(TranslationsPathShort:string) //Path="Assets/Translations/$TranslationsPathShort/"; Languages loaded from “Languages.json”
	{
		const NewTr=new Translations(`Assets/Translations/${TranslationsPathShort}`);
		NewTr.LanguageListLoad().then();
		return NewTr;
	}
	private async LanguageListLoad()
	{
		let LList:Record<string, string[]>={en:["English", "English", "Language", "Pick your language"]}; //LList={ISO:[Eng, Native, LanguageAsString, PickLanguageAsString], ...}
		try {
			 LList=await LoadJson.FromURL(this.TranslationsPath+'/Languages.json') as Record<string, string[]>;
		} catch (e) {
			Log.Error("Error loading languages list: "+Util.GetErrorMessage(e));
		}

		for(const [Key, Value] of Object.entries(LList))
			this.Languages[Key]=new LangNames(
				Key,
				Value[0] ?? `${Key} missing English Name`,
				Value[1] ?? `${Key} missing Name`,
				Value[2] ?? `${Key} ${Translations.LanguageAsStr}`,
				Value[3] ?? `${Key} ${Translations.PickLanguageAsStr}`,
			);

		await this.LoadLanguage(Share.LC.Language.V.Key);
		Share.LC.Language.SettingChanged.Add('Translations.LoadLanguage', LangName => void(this.LoadLanguage(LangName.Key)));
	}

	//Load current language
	public Sections?:Record<string, Record<string, string> >=WillBeSet;
	public LanguageChanged=new CallbackList<[string]>('LanguageChanged');
	private _Language:string=StatStr.Empty; public get Language() { return this._Language; }
	public set Language(Value:string)
	{
		if(Value && this._Language!==Value)
			this.LoadLanguage(Value).then();
	}
	private async LoadLanguage(ISO:string)
	{
		this._Language=ISO;
		try {
			this.Sections=await LoadJson.FromURL(`${this.TranslationsPath}/${ISO}${Translations.TranslationFileExtension}`) as Record<string, Record<string, string>>;
		} catch (e) {
			this.Sections=undefined;
			Log.Error("Could not load language file: "+Util.GetErrorMessage(e));
		}
		this.LanguageChanged.Execute(ISO); //Callbacks for after the language changes
	}

	//Translation functions
	public T			(Key:string, Section?:string, SafeRich=false, ...FormatList:Util.Primitive[]): string		{ return this.TranslateDef(Key, Section, Key,		SafeRich, ...FormatList); } //If not found, return Key
	public Translate	(Key:string, Section?:string, SafeRich=false, ...FormatList:Util.Primitive[]): string		{ return this.TranslateDef(Key, Section, Key,		SafeRich, ...FormatList); } //If not found, return Key
	public TranslateNull(Key:string, Section?:string, SafeRich=false, ...FormatList:Util.Primitive[]): string|null	{ return this.TranslateDef(Key, Section, null!,		SafeRich, ...FormatList); } //If not found, return null
	public TDef			(Key:string, Section?:string, Default:string=StatStr.Empty, //Same as TranslateDef
		 											  SafeRich=false, ...FormatList:Util.Primitive[]): string		{ return this.TranslateDef(Key, Section, Default,	SafeRich, ...FormatList); } //If not found, return Default

	//Replacement arguments for parameterized translation strings (e.g., {0} placeholders).
	public FormatParameters:Record<string, Util.Primitive[]>={}; //If SectionName is not ROOT or undefined, Expects “SectionName/” before the Key
	public TranslateDef(Key:string, Section?:string, Default:string|null=StatStr.Empty, SafeRich=false, ...FormatList:Util.Primitive[]): string //Return Default if not found. Section is ROOT if null.
	{
		Section ??= Translations.ROOT;
		const Text=this.Sections?.[Section]?.[Key] ?? Default;
		let FPs:Util.Primitive[]|undefined;
		const Ret=
			  Text===null ? null //Only TranslateNull should return null as all other functions are set to use a default non-null string
			: FormatList.length>0 ? Translations.FormatString(Text, ...FormatList)
			: (FPs=this.FormatParameters[Section===Translations.ROOT ? Key : `${Section}/${Key}`])!==undefined ? Translations.FormatString(Text, ...FPs)
			: Text;
		return Ret!==null && SafeRich ? DevStrings.SafeRich(Ret) : Ret!;
	}
	public AddFormatParameters(Key:string, Section?:string, ...List:Util.Primitive[])
	{
		this.FormatParameters[Section===undefined || Section===Translations.ROOT ? Key : `${Section}/${Key}`]=List;
	}

	//Translate the string with the default values
	public GetDefault(Key:string, Section?:string): string
	{
		const FPs=this.FormatParameters[Section===undefined || Section===Translations.ROOT ? Key : `${Section}/${Key}`];
		return FPs!==undefined ? Translations.FormatString(Key, ...FPs) : Key;
	}

	//Replaces “{ARG_INDEX}” with “Args[ARG_INDEX]” in Str for each Arg
	public static FormatString(Str:string, ...Args:Util.Primitive[]): string
	{
		for(const [Index, Value] of Args.entries())
			Str=Str.replaceAll(`{${Index}}`, Value?.toString() ?? StatStr.Empty);
		return Str;
	}
}