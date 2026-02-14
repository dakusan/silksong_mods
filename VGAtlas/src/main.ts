import "./style.scss";
import $ from "jquery";
import { MapCanvas } from "./MapCanvas"
import { Util, WillBeSet } from "./SharedClasses";
import { InitFuncs } from "./Misc"

class Shared
{
	public MCanvas:MapCanvas=WillBeSet;
}
export const Share=new Shared();

async function Main()
{
	let MCanvas:MapCanvas=WillBeSet;
	try {
		MCanvas=Share.MCanvas=new MapCanvas();
		await MCanvas.Init("Assets/PAtlasMap.png");
		for(const Fn of InitFuncs)
			Fn();
		MCanvas.ExtraMessage=undefined;
		MCanvas.Refresh();
	} catch(e) {
		const Message=Util.GetErrorMessage(e);
		if(MCanvas?.CanRender)
			MCanvas.ErrorMessage=Message;
		else
			$("#map").empty().append($("<div>").text(Message));
	}
}

$(Main);