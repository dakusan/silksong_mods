//Primary share object
import { WillBeSet }				from './Util/SharedClasses';
import { type WindowManager }		from './Util/WindowManager';
import type Translations			from './Util/Translations';
import LC							from './AtlasConfig';
import type MonitorSaveValues		from './MonitorSaveValues';
import type MapCanvas				from './MapCanvas';
import type MapControl				from './MapControl';
import type DataStorage				from './DataStorage';
import SaveDataClass				from './SaveData';
class Shared
{
	public readonly	MCanvas	:MapCanvas			=WillBeSet;
	public readonly	DS		:DataStorage		=WillBeSet;
	public readonly	MC		:MapControl			=WillBeSet;
	public readonly	WM		:WindowManager		=WillBeSet;
	public readonly	MSV		:MonitorSaveValues	=WillBeSet;
	public readonly	Tr		:Translations		=WillBeSet;
	public readonly	LC							=LC;
	public			SaveData:SaveDataClass		=SaveDataClass.CreateEmptySave();
}
export const Share=new Shared();