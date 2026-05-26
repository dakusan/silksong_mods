import './ConfigWindow.scss';
import $									  from 'jquery';
import { FriendClass, StatStr				} from '../../Util/SharedClasses';
import type Translations					  from '../../Util/Translations';
import { DefaultTr, TranslatePassthrough	} from '../../Util/Translations';
import { Window								} from '../../Util/WindowManager';
import Config								  from '../../Config/Config';
import ConfigItemBase						  from '../../Config/Abstract/ConfigItemBase';
import type ConfigItem						  from '../../Config/Abstract/ConfigItem';
import { type ConfigItemValueTypes			} from '../../Config/Abstract/ConfigItem';
import ConfigItem_Languages					  from '../../Config/Types/ConfigItem_Languages';

export default class ConfigWindow extends Window
{
	private static readonly IgnoreSection=Config.IgnoreSection;
	public readonly Sections:ConfigWindowSection[]=[];
	public readonly $ConfigTable=$(document.createElement('table')).addClass('ConfigTable');
	constructor(
		public readonly Config:Config,
		public readonly Tr?:Translations,
	) {
		super({
			SaveID:'Config'+Config.Prefix, Type:'Config', Width:750, Height:550,
			TitleTranslator:new TranslatePassthrough('Configure', 'ConfigWindow', "Settings", DefaultTr),
		});
		this.AddViewableToggles();
		this.$ConfigTable.appendTo(this.$Content);
		for(const [SectionName, Entries] of this.Config.Sections)
			if(SectionName!==ConfigWindow.IgnoreSection)
				this.Sections.push(new ConfigWindowSection(this, SectionName, Entries, this.$ConfigTable));

		this.LanguageChanged();
	}
	private AddViewableToggles(): void
	{
		const ToggleViews={Configure:"Settings", Shortcuts:"Keyboard Shortcuts"};
		let CurrentToggleView=GetValidToggleView(localStorage.Config_ViewSection);
		function GetValidToggleView(ToggleView:string): keyof typeof ToggleViews
		{
			const TV=ToggleView as keyof typeof ToggleViews;
			return ToggleViews[TV] ? TV : Object.keys(ToggleViews)[0] as typeof TV;
		}
		const CreateLabeledOption=(Key:string, Default:string|null, El:JQuery) =>
			$('<label>').append(
				El,
				$('<span class=TranslationEl>')
					.attr('data-translation-key', Key)
					.attr('data-translation-section', 'ConfigWindow')
					.attr('data-translation-module', 'Default')
					.attr('data-translation-default', Default ?? Key),
			);

		DefaultTr.UpdateDOMSubElements($('<div class=ConfigViewableToggles>').appendTo(this.$Content).append(
			...Object.entries(ToggleViews).map(([ToggleID, SettingDefault]) =>
				CreateLabeledOption(ToggleID, SettingDefault,
					$(`<input type=radio name=ConfigToggleView class=ConfigToggleView value=${ToggleID}>`)
						.prop('checked', ToggleID===CurrentToggleView)
						.on('change', e => {
							if($(e.currentTarget).is(':checked'))
								this.$ConfigTable
									.toggleClass('View_'+CurrentToggleView, false)
									.toggleClass('View_'+(localStorage.Config_ViewSection=CurrentToggleView=GetValidToggleView($(e.currentTarget).val() as string)), true);
						}).trigger('change'),
				),
			),
			CreateLabeledOption("Advanced", null,
				$('<input type=checkbox>')
					.prop('checked', localStorage.Config_ShowAdvanced==='true')
					.on('change', e => {
						const Checked=$(e.currentTarget).is(':checked');
						this.$ConfigTable.toggleClass('ShowAdvanced', Checked);
						localStorage.Config_ShowAdvanced=String(Checked);
					})
					.trigger('change'),
			),
		)[0]);
	}

	public TranslateFunc(Section:string, Key:string, ConfigItem?:ConfigItemBase): string
	{
		return this.Tr ? ConfigWindow.TranslateFuncReal(this.Tr, Section, Key, ConfigItem) : ConfigWindow.NoTranslation(Section, Key, ConfigItem);
	}
	private static TranslationSectionConversions:Record<string, string>={SECTION_TITLE:'SettingSections', CONFIG_KEY:'SettingNames', CONFIG_DESCRIPTION:'SettingDescriptions'};
	public static TranslateFuncReal(Tr:Translations, Section:string, Key:string, ConfigItem?:ConfigItemBase): string
	{
		if(ConfigItem instanceof ConfigItem_Languages)
			switch(Section) {
				case 'CONFIG_KEY': return (Tr.LanguagesList[Tr.Language!]?.LanguageAsString ?? Key)+(Tr.Language===Tr.ctor.DefaultLang ? '' : ` [${Tr.ctor.LanguageAsStr}]`);
				case 'CONFIG_DESCRIPTION': return Tr.LanguagesList[Tr.Language!]?.PickLanguageAsString ?? Tr.ctor.PickLanguageAsStr;
			}

		return Tr.TranslateNull(Key, this.TranslationSectionConversions[Section]) ?? ConfigWindow.NoTranslation(Section, Key, ConfigItem);
	}
	private static NoTranslation(Section:string, Key:string, ConfigItem?:ConfigItemBase): string
	{
		return Section==='CONFIG_DESCRIPTION' ? (ConfigItem!.Options?.Description ?? StatStr.Empty) : Key;
	}

	public override LanguageChanged(): void
	{
		const UpdateSections=() => this.Sections.forEach(Section => (Section as ConfigWindowSection_Friend).LanguageChanged());
		if(this.Tr)
			this.Tr.OnLanguageLoadedOnce(UpdateSections);
		else
			UpdateSections();
	}

	public override OnClosing(): false
	{
		for(const Section of this.Sections)
			for(const Row of Section.Rows)
				Row.ConfigItem.$DOMHolder.detach();
		return false;
	}
}

class ConfigWindowSection
{
	public readonly Rows:ConfigWindowRow[]=[];
	public readonly $Title=$(document.createElement('td')).attr('colspan', 3).addClass('SectionTitle');
	constructor(
		public readonly Parent:ConfigWindow,
		public readonly SectionName:string,
		ConfigItems:ConfigItemBase[],
		$Table:JQuery,
	) {
		$Table.append($('<tr class=TitleRow>').append(this.$Title));
		for(const ConfigItem of ConfigItems)
			this.Rows.push(new ConfigWindowRow(this, ConfigItem, $Table));
	}

	protected LanguageChanged(): void
	{
		this.$Title.text(this.Parent.TranslateFunc('SECTION_TITLE', this.SectionName));
		for(const Row of this.Rows)
			(Row as ConfigWindowRow_Friend).LanguageChanged();
	}
}
class ConfigWindowRow
{
	public readonly $Row=$(document.createElement('tr')).addClass('ItemRow');
	public readonly $TitleCol=$(document.createElement('td')).appendTo(this.$Row).addClass('TitleCol');
	public readonly $TitleText=$(document.createElement('div')).appendTo(this.$TitleCol).addClass('TitleText');
	public readonly $DescriptionEl?:JQuery;
	public readonly $ConfigElCol=$(document.createElement('td')).appendTo(this.$Row).addClass('ConfigElCol');
	public readonly $ConfigResetButton=$(document.createElement('button')).addClass('ConfigResetButton')
		.appendTo($(document.createElement('td')).appendTo(this.$Row).addClass('ConfigResetCol'));
	constructor(
		public readonly Parent:ConfigWindowSection,
		public readonly ConfigItem:ConfigItemBase,
		$Table:JQuery,
	) {
		$Table.append(
			this.$Row
				.toggleClass('IsAdvanced', this.ConfigItem.Options.IsAdvanced)
				.toggleClass('IsHidden', this.ConfigItem.Options.Hide)
		);
		if(this.ConfigItem.Options.Description)
			this.$TitleCol.append(this.$DescriptionEl=$('<div class=Description />'));

		this.ConfigItem.$DOMHolder.appendTo(this.$ConfigElCol);
		this.$ConfigResetButton.on('click', () => (this.ConfigItem as ConfigItem<ConfigItemValueTypes>).ResetToDefault());
	}

	protected LanguageChanged(): void
	{
		this.$TitleText.text(this.Parent.Parent.TranslateFunc('CONFIG_KEY', this.ConfigItem.Key, this.ConfigItem));
		this.$DescriptionEl?.text(this.Parent.Parent.TranslateFunc('CONFIG_DESCRIPTION', this.ConfigItem.Key, this.ConfigItem));
		this.$ConfigResetButton.text(DefaultTr.T("Reset", 'ConfigWindow'));
		(this.ConfigItem as ConfigItemBase_Friend).LanguageChanged?.();
	}
}

abstract class ConfigItemBase_Friend extends ConfigItemBase implements FriendClass
{
	public override LanguageChanged?(): void;
	//Ignore these
	protected constructor() { super(null!, null!, null!); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}
abstract class ConfigWindowSection_Friend extends ConfigWindowSection implements FriendClass
{
	public override LanguageChanged(): void { this.Stub(); }
	//Ignore these
	protected constructor() { super(null!, null!, null!, null!); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}
abstract class ConfigWindowRow_Friend extends ConfigWindowRow implements FriendClass
{
	public override LanguageChanged(): void { this.Stub(); }
	//Ignore these
	protected constructor() { super(null!, null!, null!); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}