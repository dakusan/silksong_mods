import { Config } from "./Config"
import { ColorRGBA } from "./SharedClasses"

class LocalConfig extends Config {
	constructor() { super("Atlas_"); }
	public readonly Color_FoundIcon			=this.Item("Color_FoundIcon",		new ColorRGBA(0.5, 0, 0.5, 0.75));
	public readonly IconSize				=this.Item("IconSize",				.75);
	public readonly IconSet					=this.Item("IconSet",				"Assets/Icons-FromGame.png");
	public readonly PanSpeed				=this.Item("PanSpeed",				300);
	public readonly ZoomSpeed				=this.Item("ZoomSpeed",				1.03);
	public readonly IconSizeScalesWithZoom	=this.Item("IconSizeScalesWithZoom",true);
	public readonly Debug					=this.Item("Language",				false);
}
export const LC=new LocalConfig();