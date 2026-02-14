import "./style.scss";
import $ from "jquery";
import { MapCanvas } from "./MapCanvas"
import { DataStorage } from "./DataStorage"
import { Util, WillBeSet } from "./SharedClasses";
import { InitFuncs } from "./Misc"

class Shared
{
	public MCanvas	:MapCanvas	=WillBeSet;
	public DS		:DataStorage=WillBeSet;
}
export const Share=new Shared();

//Mimic C++ friend / C# internal
class DataStorage_Friend extends DataStorage
{
	public override async Load(_CategoriesPath:string, _ItemsPath:string, _MiscPath:string, _IconSetPath:string) { }
	public override CompleteInit(): void { }
}

async function Main()
{
	let MCanvas:MapCanvas=WillBeSet;
	try {
		MCanvas=Share.MCanvas=new MapCanvas();
		const DS=new DataStorage();
		await MCanvas.Init("Assets/PAtlasMap.png");
		await (DS as DataStorage_Friend).Load(
			"Assets/categories.json",
			"Assets/items.json",
			"Assets/Misc.json",
			"Assets/Icons-FromGame.png",
		);
		Share.DS=DS; //Setting this now flags some locations that DataStorage has now completed loading so they can start their tasks
		(DS as DataStorage_Friend).CompleteInit();
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