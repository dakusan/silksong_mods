import $ from 'jquery';
import { ColorRGBA, Util } from '../../Util/SharedClasses';
import ConfigItem, { Options } from '../Abstract/ConfigItem';

const RGBList=['r', 'g', 'b'] as const;
const RGBAList=[...RGBList, 'a'] as const;
type ColorChannel=typeof RGBAList[number];
const CCMax=255;

export default class ConfigItem_Color extends ConfigItem<ColorRGBA>
{
	protected readonly $Picker?:JQuery;
	protected readonly Sliders:Partial<Record<ColorChannel, JQuery>> ={};

	constructor(
		Section:string, Key:string, Default:ColorRGBA,
		public readonly ShowPicker:boolean=false,
		public readonly ShowColorSliders:boolean=false,
		public readonly ShowAlpha:boolean=false,
		Opts?:Partial<Options>
	) {
		super(Section, Key, Default, Opts);

		if(this.ShowPicker)
			this.$Picker=
				$(document.createElement('input')).attr('type', 'color').appendTo(this.$DOMHolder).addClass('Color Picker')
				.on('input', () => {
					const C=this.Default.ConfigDeserialize(String(this.$Picker!.val()).substring(1));
					this.UpdateSaveValue({r:C.r, g:C.g, b:C.b});
				});
		if(this.ShowColorSliders)
			for(const CC of RGBList)
				this.CreateSlider(CC);
		if(this.ShowAlpha)
			this.CreateSlider('a');
	}
	private CreateSlider(CC:ColorChannel)
	{
		const Me=this.Sliders[CC]=
			$(`<input type=range min=0 max=${CCMax} step=1 value=${CCMax} class='Color ${CC}'>`)
				.appendTo(this.$DOMHolder)
				.on('input', () => this.UpdateSaveValue({[CC]:Util.Clamp(Number(Me.val()), 0, CCMax)/CCMax}));
	}
	private UpdateSaveValue(NewVals:Partial<Record<ColorChannel, number>>)
	{
		const NewColor:Record<ColorChannel, number>={...(({r, g, b, a}) => ({r, g, b, a}))(this.V), ...NewVals};
		this.SetVal(new ColorRGBA(NewColor.r, NewColor.g, NewColor.b, NewColor.a), true);
		this.ValueSet();
	}

	protected override ValueSet()
	{
		this.$Picker?.val('#'+this.V.ConfigSerialize().substring(0, 6));
		for(const CC of RGBAList)
			this.Sliders[CC]?.val(Math.round(this.V[CC]*CCMax));
	}
}