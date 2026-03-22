import $ from 'jquery';
import { Iter, Util } from '../../Util/SharedClasses';
import { DefaultTr } from '../../Util/Translations';
import ConfigItem, { Options, SaveAsString } from '../Abstract/ConfigItem';

class ToggleKeys
{
	public Ctrl	=false;
	public Alt	=false;
	public Shift=false;
	public Meta	=false;
}
type ToggleKeyID=keyof ToggleKeys;
type EventToggleNames='ctrlKey'|'altKey'|'shiftKey'|'metaKey';

class ToggleKeyInfo<K extends ToggleKeyID=ToggleKeyID>
{
	constructor(
		public readonly ID:K,
		public readonly Name:string,
		public readonly EventToggleName:EventToggleNames,
	) { }
}
const ToggleKeyInfos=new Map<ToggleKeyID, ToggleKeyInfo>([
	['Ctrl'	, new ToggleKeyInfo('Ctrl'	, 'Control'	, 'ctrlKey'	)],
	['Alt'	, new ToggleKeyInfo('Alt'	, 'Alt'		, 'altKey'	)],
	['Shift', new ToggleKeyInfo('Shift'	, 'Shift'	, 'shiftKey')],
	['Meta'	, new ToggleKeyInfo('Meta'	, 'Meta'	, 'metaKey'	)],
]);

const SkipKeyNames=new Set([...new Iter(ToggleKeyInfos.values()).map(TKI => TKI.Name), 'Dead', 'Unidentified']);
const NoKeySet='-NO_KEY-';

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

	//Convert to visible string for display
	public DisplayString()
	{
		if(this.KeyCode===NoKeySet)
			return DefaultTr.TDef('NoKeySet', 'ConfigWindow', "None");

		return [
			...new Iter(ToggleKeyInfos.values()).filter(TKI => this.ToggleKeys[TKI.ID]).map(TKI => TKI.Name),
			this.KeyName,
		].join('+')
	}
}

export default class ConfigItem_ShortcutKey extends ConfigItem<ShortcutKey>
{
	private static AnyCapturing=false;
	protected readonly $Button=$(document.createElement('button')).addClass('ShortcutKey').appendTo(this.$DOMHolder);
	private readonly BoundListenForKey=(e:KeyboardEvent) => this.ListenForKey(e);
	private readonly BoundEndKeyListen=() => this.EndKeyListen();

	constructor(Section:string, Key:string, Default:ShortcutKey, Opts?:Partial<Options>)
	{
		super(Section, Key, Default, Opts);

		this.$Button.on('click', () =>
		{
			if(ConfigItem_ShortcutKey.AnyCapturing)
				return;
			ConfigItem_ShortcutKey.AnyCapturing=true;
			this.$Button.text(DefaultTr.T("Press shortcut", 'ConfigWindow'));
			window.addEventListener('keydown', this.BoundListenForKey, true);
			window.addEventListener('click', this.BoundEndKeyListen, true);
		});
	}

	private ListenForKey(e:KeyboardEvent)
	{
		e.preventDefault();
		e.stopPropagation();

		//Ignore pure modifier presses or unknown keys
		if(SkipKeyNames.has(e.key))
			return;

		const NewToggleKeys=new ToggleKeys();
		for(const TKI of ToggleKeyInfos.values())
			if(e[TKI.EventToggleName])
				NewToggleKeys[TKI.ID]=true;
		const SK=new ShortcutKey(e.code, e.key, NewToggleKeys);

		this.SetVal(
			  SK.KeyName!=='Escape' ? SK
			: new ShortcutKey(NoKeySet, '-', {})
		, true);
		this.EndKeyListen();
	}
	private EndKeyListen()
	{
		window.removeEventListener('keydown', this.BoundListenForKey, true);
		window.removeEventListener('click', this.BoundEndKeyListen, true);
		ConfigItem_ShortcutKey.AnyCapturing=false;
		this.ValueSet();
	}

	protected override ValueSet() { this.$Button.text(this.V.DisplayString()); }
	protected override LanguageChanged() { if(this.V.KeyCode===NoKeySet) this.ValueSet(); }
}