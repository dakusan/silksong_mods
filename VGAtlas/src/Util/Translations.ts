import { CallbackList, DevStrings, Log, StatStr, Util } from './SharedClasses';
import { LoadJson } from './JSON';

const DefaultModuleName='Default';

class LangNames { constructor(
	public readonly ISO:string,
	public readonly Eng:string,
	public readonly Native:string,
	public readonly LanguageAsString:string,
	public readonly PickLanguageAsString:string,
) { } }
type SectionListType=Record<string, Record<string, string>>;

//Immediately call the callback on adding in case the language has already loaded
type Callback<Args extends unknown[]=unknown[]> = (...args: Args) => void;
class LanguagesChangedCallbackList extends CallbackList<[string]>
{
	constructor(private readonly Tr:Translations) { super('LanguageChanged') }
	public override Add(Name: string, CB:Callback<[string]>)
	{
		super.Add(Name, CB);
		if(this.Tr.Language)
			this.Tr.OnLanguageLoadedOnce(() => CB(this.Tr.Language!));
	}
}

//Loads translations from $TranslationsPath/$LangIsoName$TranslationFileExtension.
export default class Translations
{
	//Defaults
	public static readonly ROOT:string				=StatStr.Empty; //The default section all translations are under
	public static readonly LanguageAsStr			="Language";
	public static readonly PickLanguageAsStr		="Pick your language";
	public static readonly TranslationFileExtension	='.tr.json';
	public static readonly DefaultLang				="en";
	public static readonly DefaultLangName			="English";

	//Since a lot of the functions are ran on the static class, give easy access to it
	public get ctor() { return Translations; }

	//Store modules so their language can be synced
	private static Modules=new Map<string, Translations>;
	public static get ModulesList(): ReadonlyMap<string, Translations> { return this.Modules; }

	//The list of languages
	private readonly Languages:Record<string, LangNames>={};
	public get LanguagesList():Readonly<typeof this.Languages> { return this.Languages; }
	public readonly LanguageListLoaded:Promise<void>; //Languages loaded from “Languages.json”

	//Init
	constructor(
		public readonly ModuleName:string,
		public readonly TranslationsPath:string,
		public HasFallbacks=true,
	) {
		Translations.Modules.set(this.ModuleName, this);
		this.LanguageListLoaded=this.LanguageListLoad();
	}
	public static StandardCreate(ModuleName:string) //Path="Assets/Translations/$ModuleName/"
	{
		return new Translations(ModuleName, `Assets/Translations/${ModuleName}`);
	}
	private async LanguageListLoad()
	{
		let LList:Record<string, string[]>={[this.ctor.DefaultLang]:[this.ctor.DefaultLangName, this.ctor.DefaultLangName, this.ctor.LanguageAsStr, this.ctor.PickLanguageAsStr]}; //LList={ISO:[Eng, Native, LanguageAsString, PickLanguageAsString], ...}
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
	}

	//Load current language
	public Sections?:SectionListType;
	private _Language?:string; public get Language(): string|undefined { return this._Language; }
	public set Language(Value:string)
	{
		if(Value && this._Language!==Value)
			this.LanguageLoaded=this.LoadLanguage(this._Language=Value).then();
	}
	private async LoadLanguage(ISO:string)
	{
		//Sync the languages of other translations
		for(const M of this.ctor.Modules.values())
			if(M.Language!==this.Language)
				M.Language=this.Language!;

		const NewSections:[SectionListType|undefined, SectionListType|undefined]=[undefined, undefined];
		await Promise.all([
			LoadJson.FromURL(`${this.TranslationsPath}/${ISO}${Translations.TranslationFileExtension}`)
				.then(RetObj => NewSections[0]=RetObj as SectionListType)
				.catch(e => Log.Error(StatStr.NeedsTranslate+`Could not load language file “${ISO}” for module “${this.ModuleName}”: `+Util.GetErrorMessage(e))),
			  !this.HasFallbacks ? null
			: LoadJson.FromURL(`${this.TranslationsPath}/Merge/${ISO}${Translations.TranslationFileExtension}`)
				.then(RetObj => NewSections[1]=RetObj as SectionListType)
				.catch(() => {}),
		]);

		this.Sections=NewSections[0] ?? NewSections[1];
		if(NewSections[0] && NewSections[1])
			for(const [SectionName, NewTranslations] of Object.entries(NewSections[1]))
				this.Sections![SectionName]={...(this.Sections![SectionName] ?? {}), ...NewTranslations};

		this.LanguageLoaded=undefined;
		this.OnLanguageChanged.Execute(ISO); //Callbacks for after the language changes
		this.UpdateDOMSubElements();
	}

	//Sending events when language has changed
	public OnLanguageChanged=new LanguagesChangedCallbackList(this); //Called when changed language load completes
	public LanguageLoaded?:Promise<void>; //Undefined if language load has complete, otherwise returns the promise for the language loading routine
	public OnLanguageLoadedOnce(CB:(NewLang:string) => void)
	{
		if(this.LanguageLoaded===undefined)
			CB(this.Language!);
		else
			this.LanguageLoaded.finally(() => CB(this.Language!));
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

	public TranslatePassthrough(PT:TranslatePassthrough)
	{
		return this.TranslateDef(PT.Key, PT.Section, PT.Default ?? PT.Key, PT.SafeRich, ...PT.FormatList);
	}

	//Translates a TranslatePassthrough.AsError(), or otherwise uses Util.GetErrorMessage
	public TranslatePassthroughError(Err:Error|unknown)
	{
		const PT=(Err as Error)?.cause;
		return PT instanceof TranslatePassthrough ? this.TranslatePassthrough(PT) : Util.GetErrorMessage(Err);
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

	//Update translation DOM elements via their dataset attributes
	public static DefaultDOMModule=DefaultModuleName;
	public UpdateDOMSubElements(RootSearchDOMElement:HTMLElement=document.body)
	{
		for(const El of RootSearchDOMElement.getElementsByClassName('TranslationEl'))
			if(El instanceof HTMLElement && (El.dataset.translationModule ?? this.ctor.DefaultDOMModule)===this.ModuleName)
				this.UpdateDOMElement(El);
	}
	public UpdateDOMElement(El:HTMLElement)
	{
		const FinalText=this.TranslateDef(El.dataset.translationKey!, El.dataset.translationSection, El.dataset.translationDefault ?? El.dataset.translationKey ?? null);
		if(FinalText!==null)
			El[El.dataset.translationHtml ? 'innerHTML' : 'innerText']=FinalText;
		return FinalText!==null;
	}
	public static CreateTranslationElement(Element:HTMLElement, Key:string, Section?:string, DefaultTextOverride?:string, AllowHTML=false, Module?:string)
	{
		Element.classList.add('TranslationEl');
		Element.innerText=(DefaultTextOverride ?? Key);
		Object.entries({Key, Section, Module, Default:DefaultTextOverride, Html:AllowHTML ? "true" : null})
			.filter(([_, Value]) => Value!==null && Value!==undefined)
			.forEach(([Name, Value]) => Element.setAttribute('data-translation-'+Name.toLowerCase(), Value!));
	}
	public CreateTranslationElement(Element:HTMLElement, Key:string, Section?:string, DefaultTextOverride?:string, AllowHTML=false, OverrideModule?:string)
	{
		return Translations.CreateTranslationElement(Element, Key, Section, DefaultTextOverride, AllowHTML, OverrideModule ?? this.ModuleName);
	}
}

//Create a passthrough object that can be translated later
export class TranslatePassthrough
{
	public readonly FormatList:Util.Primitive[];
	constructor(
		public readonly Key:string,
		public readonly Section?:string,
		public readonly Default:string|null=null, //If null then the Key will be used
		public readonly SafeRich=false,
		...FormatList:Util.Primitive[]
	) { this.FormatList=FormatList; }

	public AsError()
	{
		return new Error(this.Default ?? this.Key, {cause:this});
	}
}

//Used by utilities
export const DefaultTr=Util.OneTimeInit('DefaultTr', () => Translations.StandardCreate(DefaultModuleName));