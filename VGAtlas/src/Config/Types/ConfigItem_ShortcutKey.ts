import { Iter, Util } from '../../Util/SharedClasses';
import ConfigItem, { Options, SaveAsString } from '../Abstract/ConfigItem';

class ToggleKeys
{
	public Ctrl	=false;
	public Alt	=false;
	public Shift=false;
	public Meta	=false;
}
type ToggleKeyID=keyof ToggleKeys;

class ToggleKeyInfo<K extends ToggleKeyID=ToggleKeyID>
{
	constructor(
		public readonly ID:K,
	) { }
}
const ToggleKeyInfos=new Map<ToggleKeyID, ToggleKeyInfo>([
	['Ctrl'	, new ToggleKeyInfo('Ctrl'	)],
	['Alt'	, new ToggleKeyInfo('Alt'	)],
	['Shift', new ToggleKeyInfo('Shift'	)],
	['Meta'	, new ToggleKeyInfo('Meta'	)],
]);

export class ShortcutKey implements SaveAsString<ShortcutKey>
{
	//Members
	constructor(
		public readonly KeyCode:string,
		public readonly KeyName:string,
		TheToggleKeys?:Partial<ToggleKeys>,
	) {
		this.ToggleKeys=Util.AssignProps(new ToggleKeys(), TheToggleKeys ?? {});
	}
	public ToggleKeys:Readonly<ToggleKeys>;

	//Convert to/from string for ConfigItem saving
	public ToString()
	{
		return [
			...new Iter(ToggleKeyInfos.values()).filter(TKI => this.ToggleKeys[TKI.ID]).map(TKI => TKI.ID),
			this.KeyCode,
		].join('+')+','+this.KeyName;
	}
	private static ParseShortcutKey=/^(?:(?<ToggleKeys>[^,]+)\+)?(?<KeyCode>[^,+]+),(?<KeyName>.*)$/;
	public FromString(Str:string)
	{
		const Match=ShortcutKey.ParseShortcutKey.exec(Str);
		if(!Match?.groups)
			throw new Error("Invalid shortcut key save value");

		const NewToggleKeys=new ToggleKeys();
		for(const ToggleKey of Match.groups.ToggleKeys?.split('+') ?? [])
			if(ToggleKeyInfos.has(ToggleKey as ToggleKeyID))
				NewToggleKeys[ToggleKey as ToggleKeyID]=true;

		return new ShortcutKey(Match.groups.KeyCode, Match.groups.KeyName, NewToggleKeys);
	}
}

export default class ConfigItem_ShortcutKey extends ConfigItem<ShortcutKey>
{
	//eslint-disable-next-line @typescript-eslint/no-useless-constructor
	constructor(Section:string, Key:string, Default:ShortcutKey, Opts?:Partial<Options>)
	{
		super(Section, Key, Default, Opts);
	}
}