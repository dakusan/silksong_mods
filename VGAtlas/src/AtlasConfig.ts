import { Config } from './Config';
import { ColorRGBA } from './SharedClasses';

export enum Languages { English }
class LocalConfig extends Config {
	constructor() { super('Atlas_'); }
	public readonly CategoryToggleStates	=this.Item('CategoryToggleStates',	[[], [], []] as number[][]);
	public readonly Color_FoundIcon			=this.Item('Color_FoundIcon',		new ColorRGBA(0.5, 0, 0.5, 0.75));
	public readonly IconSize				=this.Item('IconSize',				.75);
	public readonly IconSet					=this.Item('IconSet',				'Assets/Icons-FromGame.png');
	public readonly PanSpeed				=this.Item('PanSpeed',				300);
	public readonly ZoomSpeed				=this.Item('ZoomSpeed',				1.03);
	public readonly IconCenterTime			=this.Item('IconCenterTime',		1.75);
	public readonly IconCenterEase			=this.Item('IconCenterEase',		2.5);
	public readonly IconSizeScalesWithZoom	=this.Item('IconSizeScalesWithZoom',true);
	public readonly Language				=this.Item('Language',				Languages.English);
}
export const LC=new LocalConfig();