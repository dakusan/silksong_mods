import ConfigItem, { type Options, type ConfigSerializer } from '../Abstract/ConfigItem';

//These are readonly POJOs
export default class ConfigItem_Object<T extends object> extends ConfigItem<OtherObject<T>>
{
	public constructor(Section:string, Key:string, Default:OtherObject<T>, Opts?:Partial<Options>) { super(Section, Key, Default, {...Opts, Hide:true}); }
	protected override ValueSet() { }
}
export class OtherObject<T extends object> implements ConfigSerializer<OtherObject<T>>
{
	constructor(
		public readonly Obj:Readonly<T>
	) { }
	public ConfigSerialize() { return JSON.stringify(this.Obj); }
	public ConfigDeserialize(Str:string) { return new OtherObject<T>(JSON.parse(Str)); }
}