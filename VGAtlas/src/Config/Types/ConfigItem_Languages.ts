import { StatStr, WillBeSet } from '../../Util/SharedClasses';
import Translations, { DefaultTr } from '../../Util/Translations';
import ConfigItem_Enum from './ConfigItem_Enum';

export default class ConfigItem_Languages extends ConfigItem_Enum
{
	protected Tr:Translations=WillBeSet;
	constructor(Section:string, Tr?:Translations) //If this.Tr is not set here, call SetTranslations when it is available
	{
		super(Section, Translations.LanguageAsStr, '*UNSET*', {}, {Description:'-'});
		this.$SelectBox.addClass('Language');
		(this.Tr=Tr!)?.LanguageListLoaded.finally(() => this.FinishLoad());
	}
	public SetTranslations(Tr:Translations): void
	{
		if(this.Tr)
			throw new Error(StatStr.NeedsTranslate+`Translations already set for Languages config ${this.Tr.ModuleName}.${this.Section}.${this.Key}`);
		(this.Tr=Tr).LanguageListLoaded.finally(() => this.FinishLoad());
	}
	private FinishLoad(): void
	{
		//Add the rest of the languages
		for(const [LangKey, LangInfo] of Object.entries(this.Tr.LanguagesList))
			this.Add(LangKey, LangInfo.Native);

		//If the language is not yet set or is invalid, get it from the browser or use the default
		const OverrideLang=(): string => (navigator.languages?.[0] || navigator.language)?.slice(0, 2).toLowerCase() ?? StatStr.Empty; //TODO: May need to update this depending on languages that require 3 letters
		if(!this.Tr.LanguagesList.hasOwnProperty(this.V))
			this.V=
				this.Tr.LanguagesList.hasOwnProperty(OverrideLang()) ? OverrideLang()
				: DefaultTr.ctor.DefaultLang;

		//TODO: Rapidly changing languages can cause an infinite callback loop. Commenting this out was the easiest way of taking care of it.
		//Though if the Translations language was changed through other means, this wouldn’t catch it. The proper fix would probably involve a timeout.
		//Also, language loads are a race condition on which language loads first, if rapidly changing them.
		//this.Tr.OnLanguageChanged.Add(StatStr.NeedsTranslate+`Config languages: ${this.Tr.ModuleName}.${this.Section}.${this.Key}`, NewLang => this.V=NewLang);

		this.$SelectBox.val(this.Tr.Language=this.V);
		this.SettingChanged.Add('ConfigItem_Languages', V => this.Tr.Language=V);
	}
	protected override LanguageChanged(): void { } //Text is not changed on language change
}