import $ from 'jquery';
import { DefaultTr } from '../../Util/Translations';
import ConfigItem, { type Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Enum extends ConfigItem<string>
{
	protected $SelectBox=$(document.createElement('select')).addClass('Enum').appendTo(this.$DOMHolder);
	protected EnumValues=new Map<string, string>();
	constructor(Section:string, Key:string, Default:string, List:ReadonlyMap<string, string>|Readonly<Record<string, string>>, Opts?:Partial<Options>)
	{
		super(Section, Key, Default, Opts);
		this.$SelectBox
			.append($('<option class="NoVal TranslationEl" selected disabled data-translation-key="Select A Value" data-translation-section=ConfigWindow data-translation-module=Default>'))
			.on('change', () =>
				this.SetVal(this.$SelectBox.val() as string, true)
			);
		this.AddList(List);
		this.LanguageChanged();
		DefaultTr.UpdateDOMSubElements(this.$SelectBox[0]);
	}
	public AddList(List:ReadonlyMap<string, string>|Readonly<Record<string, string>>): void
	{
		for(const [Key, Value] of (List instanceof Map ? List : Object.entries(List)))
			this.Add(Key, Value);
	}
	public Add(Key:string, Value:string): void
	{
		if(this.EnumValues.has(Key))
			throw new Error("Key already used: "+Key);
		this.$SelectBox.append($('<option>').val(Key).text(Value));
		this.EnumValues.set(Key, Value);
	}
	protected override ValueSet(): void { this.$SelectBox.val(this.V); }
	protected override LanguageChanged(): void
	{
		this.$SelectBox.children('option:not(.NoVal)').each((_, El) =>
			void($(El).text(
				   this.GetTranslation?.($(El).val() as string)
				?? this.EnumValues.get($(El).val() as string)!
			))
		);
	}
}