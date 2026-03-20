import { Util } from '../../Util/SharedClasses';
import ConfigItem, { Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Number extends ConfigItem<number>
{
	constructor(
		Section:string, Key:string, Default:number,
		public readonly Min:number,
		public readonly Max:number,
		public readonly Digits:number,
		Opts?:Partial<Options>,
	) {
		super(Section, Key, Default, Opts);
		this.Digits=Util.Clamp(Math.round(this.Digits), 0, 8);
	}
}