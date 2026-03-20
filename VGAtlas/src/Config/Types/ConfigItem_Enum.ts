import ConfigItem, { Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Enum extends ConfigItem<string>
{
	protected EnumValues=new Map<string, string>();
	constructor(Section:string, Key:string, Default:string, List:Map<string, string>|Record<string, string>, Opts?:Partial<Options>)
	{
		super(Section, Key, Default, Opts);
		this.AddList(List);
	}
	public AddList(List:Map<string, string>|Record<string, string>)
	{
		for(const [Key, Value] of (List instanceof Map ? List : Object.entries(List)))
			this.Add(Key, Value);
	}
	public Add(Key:string, Value:string)
	{
		if(this.EnumValues.has(Key))
			throw new Error("Key already used: "+Key);
		this.EnumValues.set(Key, Value);
	}
}