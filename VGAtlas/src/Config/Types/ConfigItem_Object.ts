import ConfigItem, { Options, SaveAsString } from '../Abstract/ConfigItem';

//These are readonly POJOs
export default class ConfigItem_Object<T extends object> extends ConfigItem<OtherObject<T>>
{
	public constructor(Section:string, Key:string, Default:OtherObject<T>, Opts?:Partial<Options>) { super(Section, Key, Default, {...Opts, Hide:true}); }
}
export class OtherObject<T extends object> implements SaveAsString<OtherObject<T>>
{
	constructor(
		public readonly Obj:Readonly<T>
	) { }
	public ToString() { return JSON.stringify(this.Obj); }
	public FromString(Str:string) { return new OtherObject<T>(JSON.parse(Str)); }
}