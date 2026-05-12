import $ from 'jquery';
import { Util } from '../../Util/SharedClasses';
import ConfigItem, { type Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Number extends ConfigItem<number>
{
	protected $NumberBox	=$(document.createElement('input'	)).attr('type', 'number').appendTo(this.$DOMHolder).addClass('Number Text');
	protected $Slider		=$(document.createElement('input'	)).attr('type', 'range'	).appendTo(this.$DOMHolder).addClass('Number Slider');
	constructor(
		Section:string, Key:string, Default:number,
		public readonly Min:number,
		public readonly Max:number,
		public readonly Digits:number,
		Opts?:Partial<Options>,
	) {
		super(Section, Key, Default, Opts);
		this.Digits=Util.Clamp(Math.round(this.Digits), 0, 8);
		this.$NumberBox.add(this.$Slider)
			.attr('min', this.Min)
			.attr('max', this.Max)
			.attr('step', 1/(10 ** this.Digits))
			.on('input', e => {
				let NewVal=Number.parseFloat($(e.currentTarget).val() as string);
				if(Number.isNaN(NewVal))
					return;
				const F=10**this.Digits;
				NewVal=Util.Clamp(Math.round(NewVal*F)/F, this.Min, this.Max);
				this.SetVal(NewVal, true);
				(e.currentTarget===this.$NumberBox[0] ? this.$Slider : this.$NumberBox).val(NewVal);
			});
	}
	protected override ValueSet() { this.$NumberBox.add(this.$Slider).val(this.V); }
}