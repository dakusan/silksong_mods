import "./style.scss";
import $ from "jquery";
import { MapControl } from "./MapControl"
import { Util } from "./SharedClasses";

export class Shared
{
	public MC!:MapControl;
}
export const Share=new Shared();
(window as any).Atlas=Share;

async function Main()
{
	try {
		Share.MC=new MapControl();
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