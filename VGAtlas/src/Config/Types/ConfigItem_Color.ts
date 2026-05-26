import $ from 'jquery';
import { ColorRGBA, Util } from '../../Util/SharedClasses';
import ConfigItem, { type Options } from '../Abstract/ConfigItem';

const RGBList=['r', 'g', 'b'] as const;
const RGBAList=[...RGBList, 'a'] as const;
type ColorChannel=typeof RGBAList[number];
const CCMax=255;

export default class ConfigItem_Color extends ConfigItem<ColorRGBA>
{
	protected readonly $Picker?:JQuery;
	protected readonly $SlidersContainer?:JQuery;
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
		if(this.ShowColorSliders || this.ShowAlpha)
			this.$SlidersContainer=$('<div class=SliderContainer>').appendTo(this.$DOMHolder);
		if(this.ShowColorSliders)
			for(const CC of RGBList)
				this.CreateSlider(CC);
		if(this.ShowAlpha)
			this.CreateSlider('a');
	}
	private CreateSlider(CC:ColorChannel): void
	{
		const Me=this.Sliders[CC]=
			$(`<input type=range min=0 max=${CCMax} step=1 value=${CCMax} class='Color ${CC}'>`)
				.appendTo(this.$SlidersContainer!)
				.on('input', () => this.UpdateSaveValue({[CC]:Util.Clamp(Util.GetNumber(Me.val() as string, true) ?? 0, 0, CCMax)/CCMax}));
	}
	private UpdateSaveValue(NewVals:Partial<Record<ColorChannel, number>>): void
	{
		const NewColorObj:Record<ColorChannel, number>={...(({r, g, b, a}) => ({r, g, b, a}))(this.V), ...NewVals};
		const NewColor=new ColorRGBA(NewColorObj.r, NewColorObj.g, NewColorObj.b, NewColorObj.a);
		if(NewColor.Equals(this.V))
			return;
		this.SetVal(NewColor, true);
		this.ValueSet();
	}

	protected override ValueSet(): void
	{
		this.$Picker?.val('#'+this.V.ConfigSerialize().substring(0, 6));
		for(const CC of RGBAList)
			this.Sliders[CC]?.val(Math.round(this.V[CC]*CCMax));
	}
}