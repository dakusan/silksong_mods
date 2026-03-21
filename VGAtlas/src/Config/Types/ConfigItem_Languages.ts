import { StatStr, WillBeSet } from '../../Util/SharedClasses';
import Translations, { DefaultTr } from '../../Util/Translations';
import ConfigItem_Enum from './ConfigItem_Enum';

export default class ConfigItem_Languages extends ConfigItem_Enum
{
	protected Tr:Translations=WillBeSet;
	constructor(Section:string, Tr?:Translations) //If this.Tr is not set here, call SetTranslations when it is available
	{
		super(Section, Translations.LanguageAsStr, DefaultTr.ctor.DefaultLang, {}, {Description:'-'});
		this.$SelectBox.addClass('Language');
		(this.Tr=Tr!)?.LanguageListLoaded.finally(() => this.FinishLoad());
	}
	public SetTranslations(Tr:Translations)
	{
		if(this.Tr)
			throw new Error(StatStr.NeedsTranslate+`Translations already set for Languages config ${this.Tr.ModuleName}.${this.Section}.${this.Key}`);
		(this.Tr=Tr).LanguageListLoaded.finally(() => this.FinishLoad());
	}
	private FinishLoad()
	{
		//Add the rest of the languages
		for(const [LangKey, LangInfo] of Object.entries(this.Tr.LanguagesList))
			this.Add(LangKey, LangInfo.Native);

		this.Tr.OnLanguageChanged.Add(StatStr.NeedsTranslate+`Config languages: ${this.Tr.ModuleName}.${this.Section}.${this.Key}`, NewLang => this.V=NewLang);
		this.$SelectBox.val(this.Tr.Language=this.V);
		this.SettingChanged.Add('ConfigItem_Languages', V => this.Tr.Language=V);
	}
}