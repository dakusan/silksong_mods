//Primary share object
import { WillBeSet }				from './Util/SharedClasses';
import { type WindowManager }		from './Util/WindowManager';
import type Translations			from './Util/Translations';
import LC							from './AtlasConfig';
import { type MonitorSaveValues }	from './TempClasses';
import type MapCanvas				from './MapCanvas';
import type MapControl				from './MapControl';
import type DataStorage				from './DataStorage';
class Shared
{
	public MCanvas	:MapCanvas			=WillBeSet;
	public DS		:DataStorage		=WillBeSet;
	public MC		:MapControl			=WillBeSet;
	public WM		:WindowManager		=WillBeSet;
	public MSV		:MonitorSaveValues	=WillBeSet;
	public Tr		:Translations		=WillBeSet;
	public LC							=LC;
}
export const Share=new Shared();