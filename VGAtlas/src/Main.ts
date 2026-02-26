import "./style.scss"
import $ from "jquery"
import MapCanvas from "./MapCanvas"
import DataStorage from "./DataStorage"
import MapControl from "./MapControl"
import { WM } from "./WindowManager"
import { FriendClass, Util, WillBeSet } from "./SharedClasses"
import { InitFuncs } from "./Misc"
import { Share } from "./Share"

//Mimic C++ friend / C# internal
abstract class DataStorage_Friend extends DataStorage implements FriendClass
{
	public override async Load(_CategoriesPath:string, _ItemsPath:string, _MiscPath:string, _IconSetPath:string): Promise<void> { this.Stub(); }
	public override CompleteInit(): void { this.Stub(); }
	//Ignore these
	protected constructor() { super(); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error("This function is a stub"); }
}

async function Main()
{
	let MCanvas:MapCanvas=WillBeSet;
	try {
		//Primary map and icon functionality
		MCanvas=Share.MCanvas=new MapCanvas(87.7487, -87.5855, 2090, 1569);
		const DS=new DataStorage();
		await MCanvas.Init("Assets/PAtlasMap.png");
		await (DS as DataStorage_Friend).Load(
			"Assets/Categories.json",
			"Assets/Items.json",
			"Assets/Misc.json",
			Share.LC.IconSet.V,
		);
		Share.DS=DS; //Setting this now flags some locations that DataStorage has now completed loading so they can start their tasks
		(DS as DataStorage_Friend).CompleteInit();

		//Finish initializing
		Share.WM=WM;
		Share.MC=new MapControl();
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