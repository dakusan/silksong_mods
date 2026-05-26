import { FriendClass, Log, StatStr } from '../Util/SharedClasses';
import ConfigItemBase from './Abstract/ConfigItemBase';

export * from './Abstract/ConfigItemTypes';

export default abstract class Config
{
	public static readonly IgnoreSection='**IGNORE**';

	private ConfigEntries=new Map<string, ConfigItemBase[]>;
	public get Sections(): ReadonlyMap<string, ConfigItemBase[]> { return this.ConfigEntries; }
	private HasFinishedInit=false;

	protected constructor(
		public readonly Prefix:string=StatStr.Empty,
		public readonly Storage=localStorage,
	) {
		setTimeout(() => {
			if(!this.HasFinishedInit)
				Log.Error(StatStr.NeedsTranslate+`Config “${Prefix}” not initialized!`);
		}, 0);
	}
	protected Init(): void
	{
		if(this.HasFinishedInit)
			return;
		this.HasFinishedInit=true;
		for(const Entry of Object.values(this))
			if(Entry instanceof ConfigItemBase)
				this.AddConfig(Entry)
	}
	protected AddConfig(Item:ConfigItemBase): void
	{
		const SectionArr=this.ConfigEntries.get(Item.Section) ?? [];
		if(!SectionArr.length)
			this.ConfigEntries.set(Item.Section, SectionArr);
		SectionArr.push(Item);
		(Item as ConfigItemBase_Friend).Init(this);
	}
}

abstract class ConfigItemBase_Friend extends ConfigItemBase implements FriendClass
{
	public override Init(_Parent:Config): void { this.Stub(); }
	//Ignore these
	protected constructor() { super(null!, null!, null!); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}