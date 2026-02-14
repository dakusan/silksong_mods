import { Config } from "./Config"
import { ColorRGBA } from "./SharedClasses"

class LocalConfig extends Config {
	constructor() { super("Atlas_"); }
	public readonly Color_FoundIcon=this.Item<ColorRGBA>("Color_FoundIcon", new ColorRGBA(0.5, 0, 0.5, 0.75));
	public readonly IconSize=this.Item<number>("IconSize", .75);
}
export const LC=new LocalConfig();