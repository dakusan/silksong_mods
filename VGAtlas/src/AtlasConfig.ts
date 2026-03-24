import { ColorRGBA } from './Util/SharedClasses';
import Config, { ConfigItem_Boolean, ConfigItem_Color, ConfigItem_Enum, ConfigItem_Languages, ConfigItem_Number, ConfigItem_Object, ConfigItem_ShortcutKey, OtherObject, ShortcutKey } from './Config/Config';

const PanSpeedMultiplier=25;

class LocalConfig extends Config {
	constructor() { super('Atlas_'); this.Init(); }

	public readonly IconSet					=new ConfigItem_Enum		("Markers", "Icon set",								Object.keys(GetIconFiles())[0], GetIconFiles(), {Description:"Pick your favorite icon set or create your own!"});
	public readonly IconSize				=new ConfigItem_Number		("Markers", "Icon/Marker size",						0.75, 0.3, 2.5, 2, {Description:"The size of the icons on the map"});
	public readonly IconSizeScalesWithZoom	=new ConfigItem_Boolean		("Markers", "Icon/Marker size scales with zoom",	true, {Description:"If true icons will always stay the same size at any zoom.", IsAdvanced:true});
	public readonly Color_FoundIcon			=new ConfigItem_Color		("Markers", "Found icon color*",					new ColorRGBA(0.5, 0, 0.5, 0.75), true, {Description:"When in “All” mode for a category, found icons are tinted this color.\nRGB is actually HSV [Hue Saturation Lightness], unless the shader fails to load, in which case, it really is RGB.", IsAdvanced:true});
	public readonly AutoPanEase				=new ConfigItem_Number		("Markers", "Autopan ease",							2.5, 1, 5, 1, {Description:"The autopan ease formula multiplier. This is used when the map is animated panning to an icon.", IsAdvanced:true});
	public readonly AutoPanTime				=new ConfigItem_Number		("Markers", "Autopan time",							1.75, 0.1, 3, 2, {Description:"The time it takes the autopan to move between points. This is used when the map is animated panning to an icon.", IsAdvanced:true});

	public readonly ZoomSpeed				=new ConfigItem_Number		("Map Controls", "Zoom Speed",								1.03, 1.01, 1.5, 2);
	public readonly PanSpeed				=new ConfigItem_Number		("Map Controls", "Pan Speed",								12*PanSpeedMultiplier, Math.max(1)*PanSpeedMultiplier, 40*PanSpeedMultiplier, 0);
	public readonly Shortcut_ZoomIn			=new ConfigItem_ShortcutKey	("Map Controls", "Shortcut Key: Zoom In",					new ShortcutKey('Equal', '='));
	public readonly Shortcut_ZoomOut		=new ConfigItem_ShortcutKey	("Map Controls", "Shortcut Key: Zoom out",					new ShortcutKey('Minus', '-'));

	public readonly Language				=new ConfigItem_Languages	("Interface customization");

	public readonly CategoryToggleStates	=new ConfigItem_Object		(Config.IgnoreSection, "Category States", new OtherObject<number[][]>([[], [], []]));
}
const LC=new LocalConfig();
export default LC;

function GetIconFiles()
{
	return {
		'Assets/Icons-FromGame.png':'From Game',
		'Assets/Icons-Circles.png':'Circle',
	};
}