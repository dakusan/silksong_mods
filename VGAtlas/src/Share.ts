//Primary share object
import type DataStorage from "./DataStorage";
import type MapCanvas from "./MapCanvas";
import type MapControl from "./MapControl";
import { LC } from "./AtlasConfig";
import { type WindowManager } from "./WindowManager";
import { WillBeSet } from "./SharedClasses";
class Shared
{
	public MCanvas	:MapCanvas		=WillBeSet;
	public DS		:DataStorage	=WillBeSet;
	public MC		:MapControl		=WillBeSet;
	public WM		:WindowManager	=WillBeSet;
	public LC						=LC;
}
export const Share=new Shared();