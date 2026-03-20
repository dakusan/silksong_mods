import { ColorRGBA } from '../../Util/SharedClasses';
import ConfigItem, { Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Color extends ConfigItem<ColorRGBA>
{
	constructor(
		Section:string, Key:string, Default:ColorRGBA,
		public readonly ShowAlpha:boolean=false,
		Opts?:Partial<Options>
	) {
		super(Section, Key, Default, Opts);
	}
}