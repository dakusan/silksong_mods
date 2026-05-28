import { CallbackList, type StoreRef, Util, WillBeSet } from '../../Util/SharedClasses';
import ConfigItemBase, { type Options } from './ConfigItemBase';
import type Config from '../Config';

export { Options };

export interface ConfigSerializer<T>
{
	ConfigSerialize(): string;
	ConfigDeserialize(Str:string): T;
}
export type ConfigItemValueTypes=string|number|boolean|ConfigSerializer<unknown>;

export default abstract class ConfigItem<T extends ConfigItemValueTypes> extends ConfigItemBase
{
	public SettingChanged;
	private Val:T=WillBeSet;
	private readonly IsSaveAsString:boolean;

	protected constructor(Section:string, Key:string, public readonly Default:T, Opts?:Partial<Options>)
	{
		super(Section, Key, Opts);
		this.SettingChanged=new CallbackList<[Value:T, Item:ConfigItem<T>]>(`Config setting “${this.Section}.${this.Key}”`);
		this.IsSaveAsString=typeof((this.Default as Partial<ConfigSerializer<T>>).ConfigDeserialize)==='function';
	}
	protected override Init(Parent:StoreRef<Config>): void
	{
		this.Parent=Parent;
		const Raw=this.Parent.Storage.getItem(this.Parent.Prefix+this.Key);
		this.Val=this.Default;
		if(Raw!==null)
			try {
				const Parsed=JSON.parse(Raw);
				if(this.IsSaveAsString) {
					if(typeof(Parsed)!=='string')
						throw new Error("Saved value is not a string");
					this.Val=(this.Default as ConfigSerializer<T>).ConfigDeserialize(Parsed);
				} else if(Util.SameType(this.Default, Parsed))
					this.Val=Parsed as T;
			} catch { }
		this.ValueSet();
		if(Raw!==this.GetStorageValue())
			this.SaveToStorage();
	}

	public get V(): T { return this.Val; }
	public set V(NewVal:StoreRef<T>) { this.SetVal(NewVal); }
	protected SetVal(NewVal:StoreRef<T>, FromDOM=false): void
	{
		if(NewVal===this.Val)
			return;
		this.Val=NewVal;
		this.SaveToStorage();
		this.SettingChanged.Execute(NewVal, this);
		if(!FromDOM)
			this.ValueSet();
	}
	private SaveToStorage(	): void		{ this.Parent.Storage.setItem(this.Parent.Prefix+this.Key, this.GetStorageValue()); }
	private GetStorageValue(): string	{ return JSON.stringify(this.IsSaveAsString ? (this.Val as ConfigSerializer<T>).ConfigSerialize() : this.Val); }
	public ResetToDefault(	): void		{ this.V=this.Default; }
}