//Primary share object
import DataStorage from "./DataStorage";
import MapCanvas from "./MapCanvas";
import MapControl from "./MapControl";
import { LC } from "./AtlasConfig";
import { WillBeSet } from "./SharedClasses";
class Shared
{
	public MCanvas	:MapCanvas	=WillBeSet;
	public DS		:DataStorage=WillBeSet;
	public MC		:MapControl	=WillBeSet;
	public LC					=LC;
}
export const Share=new Shared();