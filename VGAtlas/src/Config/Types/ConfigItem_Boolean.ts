import $ from 'jquery';
import ConfigItem, { type Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Boolean extends ConfigItem<boolean>
{
	protected $Checkbox=$(document.createElement('input')).attr('type', 'checkbox').addClass('Boolean').appendTo(this.$DOMHolder);
	constructor(Section:string, Key:string, Default:boolean, Opts?:Partial<Options>)
	{
		super(Section, Key, Default, Opts);
		this.$Checkbox.on('change', () =>
			this.SetVal(this.$Checkbox.prop('checked'), true)
		);
	}
	protected override ValueSet() { this.$Checkbox.prop('checked', this.V); }
}