import "./style.scss";
import $ from "jquery";
import { MapControl } from "./MapControl"
import { Util } from "./SharedClasses";
import { InitFuncs } from "./Misc"

class Shared
{
	public MC!:MapControl;
}
export const Share=new Shared();

async function Main()
{
	try {
		Share.MC=new MapControl();
		for(const Fn of InitFuncs)
			Fn();
		await Share.MC.Init("Assets/PAtlasMap.png");
	} catch(e) {
		const Message=Util.GetErrorMessage(e);
		if(Share.MC?.CanRender)
			Share.MC.ErrorMessage=Message;
		else
			$("#map").empty().append($("<div>").text(Message));
	}
}

$(Main);