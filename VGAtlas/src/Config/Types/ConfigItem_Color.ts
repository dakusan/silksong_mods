import $ from 'jquery';
import { ColorRGBA, Util } from '../../Util/SharedClasses';
import ConfigItem, { Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Color extends ConfigItem<ColorRGBA>
{
	protected readonly $Color=$(document.createElement('input')).attr('type', 'color').appendTo(this.$DOMHolder).addClass('Color Picker');
	protected readonly $Alpha=$('<input type=range min=0 max=255 step=1 value=255>').addClass('Color Alpha');

	constructor(
		Section:string, Key:string, Default:ColorRGBA,
		public readonly ShowAlpha:boolean=false,
		Opts?:Partial<Options>
	) {
		super(Section, Key, Default, Opts);

		this.$Color.on('input', () => this.UpdateSaveValue());
		if(this.ShowAlpha)
			this.$Alpha
				.appendTo(this.$DOMHolder)
				.on('input', () => this.UpdateSaveValue());
	}
	private UpdateSaveValue()
	{
		this.SetVal(
			this.Default.FromString(
				String(this.$Color.val()).substring(1)+(
					  !this.ShowAlpha ? 'FF'
					: Util.Clamp(Number.parseInt(this.$Alpha.val() as string) ?? 255, 0, 255).toString(16).toUpperCase().padStart(2, '0')
				)
			),
			true
		);
	}

	protected override ValueSet()
	{
		this.$Color.val('#'+this.V.ToString().substring(0, 6));
		this.$Alpha.val(Math.round(this.V.a*255));
	}
}