import { type StoreRef } from '../../Util/SharedClasses';
import ConfigItem, { type Options, type ConfigSerializer } from '../Abstract/ConfigItem';

//These are readonly POJOs
export default class ConfigItem_Object<T extends object> extends ConfigItem<OtherObject<T>>
{
	public constructor(Section:string, Key:string, Default:StoreRef<OtherObject<T>>, Opts?:Partial<Options>) { super(Section, Key, Default, {...Opts, Hide:true}); }
	protected override ValueSet(): void { }
}
export class OtherObject<T extends object> implements ConfigSerializer<OtherObject<T>>
{
	constructor(
		public readonly Obj:Readonly<T>
	) { }
	public ConfigSerialize(): string { return JSON.stringify(this.Obj); }
	public ConfigDeserialize(Str:string): OtherObject<T> { return new OtherObject<T>(JSON.parse(Str)); }
}