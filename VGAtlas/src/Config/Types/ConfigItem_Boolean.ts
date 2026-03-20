import ConfigItem, { Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Boolean extends ConfigItem<boolean>
{
	//eslint-disable-next-line @typescript-eslint/no-useless-constructor
	constructor(Section:string, Key:string, Default:boolean, Opts?:Partial<Options>)
	{
		super(Section, Key, Default, Opts);
	}
}