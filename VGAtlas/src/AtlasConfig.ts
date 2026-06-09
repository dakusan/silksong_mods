import { ColorRGBA, InitFuncs, Util } from './Util/SharedClasses';
import Config, { ConfigItem_Boolean, ConfigItem_Color, ConfigItem_Enum, ConfigItem_Languages, ConfigItem_Number, ConfigItem_Object, ConfigItem_ShortcutKey, OtherObject, ShortcutKey } from './Config/Config';
import type Translations from './Util/Translations';

const PanSpeedMultiplier=25;

class LocalConfig extends Config {
	constructor() { super('Atlas_'); this.Init(); }

	public readonly IconSet					=new ConfigItem_Enum		("Markers", "Icon set",								Object.keys(GetIconFiles())[0], GetIconFiles(), {Description:"Pick your favorite icon set or create your own!"});
	public readonly IconSize				=new ConfigItem_Number		("Markers", "Icon/Marker size",						0.75, 0.3, 2.5, 2, {Description:"The size of the icons on the map"});
	public readonly IconSizeScalesWithZoom	=new ConfigItem_Boolean		("Markers", "Icon/Marker size scales with zoom",	true, {Description:"If true icons will always stay the same size at any zoom.", IsAdvanced:true});
	public readonly Color_FoundIcon			=new ConfigItem_Color		("Markers", "Found icon color*",					new ColorRGBA(0.5, 0, 0.5, 0.75), false, true, true, {Description:"When in “All” mode for a category, found icons are tinted this color.\nRGB is actually HSV [Hue Saturation Lightness], unless the shader fails to load, in which case, it really is RGB.", IsAdvanced:true});
	public readonly AutoPanEase				=new ConfigItem_Number		("Markers", "Autopan ease",							2.5, 1, 5, 1, {Description:"The autopan ease formula multiplier. This is used when the map is animated panning to an icon.", IsAdvanced:true});
	public readonly AutoPanTime				=new ConfigItem_Number		("Markers", "Autopan time",							1.75, 0.1, 3, 2, {Description:"The time it takes the autopan to move between points. This is used when the map is animated panning to an icon.", IsAdvanced:true});
	public readonly Shortcut_SelStack_Next	=new ConfigItem_ShortcutKey	("Markers", "Next item in selection stack",			new ShortcutKey('KeyN', 'n'));
	public readonly Shortcut_SelStack_Prev	=new ConfigItem_ShortcutKey	("Markers", "Previous item in selection stack",		new ShortcutKey('KeyP', 'p'));

	public readonly ZoomSpeed				=new ConfigItem_Number		("Map Controls", "Zoom Speed",								1.03, 1.01, 1.5, 2);
	public readonly PanSpeed				=new ConfigItem_Number		("Map Controls", "Pan Speed",								12*PanSpeedMultiplier, Math.max(1)*PanSpeedMultiplier, 40*PanSpeedMultiplier, 0);
	public readonly Shortcut_ZoomIn			=new ConfigItem_ShortcutKey	("Map Controls", "Shortcut Key: Zoom In",					new ShortcutKey('Equal', '='));
	public readonly Shortcut_ZoomOut		=new ConfigItem_ShortcutKey	("Map Controls", "Shortcut Key: Zoom out",					new ShortcutKey('Minus', '-'));
	public readonly UseHighQualityMap		=new ConfigItem_Boolean		("Map Controls", "Use High-Quality (32bit RGBA) Map",		false,  {Description:"The default map is 256 color indexed at 451K.\nIf enabled, the new map is full 32 bit RGBA at 2.9MB"});

	public readonly Language				=new ConfigItem_Languages	("Interface customization");
	public readonly Theme					=new ConfigItem_Enum		("Interface customization", "Theme",						Object.keys(GetThemes())[0], GetThemes());
	public readonly ShowDebugMenu			=new ConfigItem_Boolean		("Interface customization", "Show Debug In Menu",			false, {IsAdvanced:true});

	public readonly CategoryToggleStates	=new ConfigItem_Object		(Config.IgnoreSection, "Category States", new OtherObject<number[][]>([[], [], []]));
}
const LC=Util.OneTimeInit('LocalConfig', () => new LocalConfig());
export default LC;

function GetIconFiles(): Readonly<Record<string, string>>
{
	return {
		'Assets/Icons-FromGame.png':'From Game',
		'Assets/Icons-Circles.png':'Circle',
	};
}

function GetThemes(): Readonly<Record<string, string>>
{
	return {
		'Base':'Default',
		'Forest':'Forest',
	};
}

let CurrentTheme='Base';
function SetupThemeSwap(): void
{
	function SwapTheme(NewTheme:string): void
	{
		document.body.classList.remove('Theme'+CurrentTheme);
		document.body.classList.add('Theme'+NewTheme);
		import(`../Assets/Themes/_${CurrentTheme=NewTheme}.scss`);
	}

	LC.Theme.SettingChanged.Add('ThemeSwap', NewTheme => SwapTheme(NewTheme));
	SwapTheme(LC.Theme.V);
}

function SetupEnumTranslations(ShareTr:Translations): void
{
	for(const ConfItem of [LC.IconSet, LC.Theme])
		ConfItem.GetTranslation=(TKey => ShareTr.TranslateNull(TKey, 'SettingEnums')!);
}

function Init_Color_FoundIcon_Demo(): void
{
	const DemoIconContainer=document.createElement('div');
	const DemoIcon=document.createElement('div');
	DemoIconContainer.classList.add('DemoIcon');
	DemoIcon.classList.add('ItemIcon', 'I14');
	DemoIconContainer.append(DemoIcon);
	LC.Color_FoundIcon.$DOMHolder.append(DemoIconContainer);

	function UpdateDemoIcon(): void
	{
		const RGBA=LC.Color_FoundIcon.V;
		DemoIcon.style=[
			`filter: hue-rotate(${(RGBA.r*360+180)%360}deg)`,
			`saturate(${RGBA.g*200}%)`,
			`brightness(${RGBA.b*200}%)`,
			`opacity(${RGBA.a})`,
		].join(' ');
	}
	UpdateDemoIcon();
	LC.Color_FoundIcon.SettingChanged.Add('UpdateDemoIcon', UpdateDemoIcon);
}

function SetupDebugMenu(): void
{
	function DoShowMenu(): void { document.getElementById('MenuDebug')!.classList.toggle('Show', LC.ShowDebugMenu.V); }
	DoShowMenu();
	LC.ShowDebugMenu.SettingChanged.Add('DoShowMenu', DoShowMenu);
}

InitFuncs.push(async () => {
	const ShareObj=(await import('./Share')).Share;
	SetupThemeSwap();
	SetupEnumTranslations(ShareObj.Tr);
	Init_Color_FoundIcon_Demo();
	SetupDebugMenu();
});