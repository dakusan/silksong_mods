import { StatStr } from "./SharedClasses";

export class ConfigItem<T>
{
	public SettingChanged:((Value:T, Item:ConfigItem<T>) => void)[]=[];
	private Val:T;

	constructor(
		public  readonly Key:string,
		private readonly Default:T,
		private readonly Storage:Storage=localStorage
	) {
		const Raw=this.Storage.getItem(this.Key);
		this.Val=(Raw===null ? this.Default : (JSON.parse(Raw) as T));
		if(Raw===null)
			this.Storage.setItem(this.Key, JSON.stringify(this.Val));
	}

	public get V() { return this.Val; }
	public set V(NewVal: T) {
		this.Val=NewVal;
		this.Storage.setItem(this.Key, JSON.stringify(NewVal));
		for(const CB of this.SettingChanged)
			CB(NewVal, this);
	}
	public ResetToDefault() { this.V=this.Default; }
}

export abstract class Config
{
	protected constructor(
		protected readonly Prefix:string=StatStr.Empty,
		protected readonly Storage:Storage=localStorage,
	) { }
	protected Item<T>(Name:string, Def:T) { return new ConfigItem<T>(this.Prefix+Name, Def, this.Storage); }
}