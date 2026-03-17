import './Style.scss';
import $												from 'jquery';
import { FriendClass, PopupMessage, Util, WillBeSet }	from './Util/SharedClasses';
import { InitFuncs }									from './Debug';
import { WM }											from './Util/WindowManager';
import Translations										from './Util/Translations';
import { Share }										from './Share';
import { MonitorSaveValues }							from './TempClasses';
import MapCanvas										from './MapCanvas';
import MapControl										from './MapControl';
import DataStorage										from './DataStorage';
import CategoryGroupsWindow								from './DockableWindows/CategoryGroupsWindow';

//Mimic C++ friend / C# internal
abstract class DataStorage_Friend extends DataStorage implements FriendClass
{
	public override async Load(_CategoriesPath:string, _ItemsPath:string, _MiscPath:string, _IconSetPath:string): Promise<void> { this.Stub(); }
	public override CompleteInit(): void { this.Stub(); }
	//Ignore these
	protected constructor() { super(); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}

const CurPopupMessage="Welcome to the <a href='https://silksong.castledragmire.com/' style='color:cyan; text-decoration:none'>Pharloom Atlas</a>.<br>This web port is about 70% feature complete against the in-game version, and is still being worked on.<br><div style='font-size:15px'>Mobile version is a bit buggy at the moment.</div><br>To navigate your item selection history, use your browsers forward and back button feature.";
const CurPopupMessageVersion=1;

//Independent libraries that can load early
Share.Tr=Translations.StandardCreate('Atlas');
Share.WM=WM;

async function Main()
{
	let MCanvas:MapCanvas=WillBeSet;
	try {
		//1-time popup message (until version number changes)
		if(Number(localStorage.getItem('LastPopupMessageSeen') ?? 0)!==CurPopupMessageVersion) {
			localStorage.setItem('LastPopupMessageSeen', String(CurPopupMessageVersion));
			new PopupMessage(CurPopupMessage, true);
		}

		//Primary map and icon functionality
		MCanvas=Share.MCanvas=new MapCanvas(87.7487, -87.5855, 2090, 1569);
		Share.MSV=new MonitorSaveValues();
		const DS=new DataStorage();
		await MCanvas.Init('Assets/PAtlasMap.png');
		await (DS as DataStorage_Friend).Load(
			'Assets/Categories.json',
			'Assets/Items.json',
			'Assets/Misc.json',
			Share.LC.IconSet.V,
		);
		Share.DS=DS; //Setting this now flags some locations that DataStorage has now completed loading so they can start their tasks
		(DS as DataStorage_Friend).CompleteInit();

		//Finish initializing
		Share.MC=new MapControl();
		for(const Fn of InitFuncs)
			Fn();
		InitFuncs.length=0;
		MCanvas.ExtraMessage=undefined;
		MCanvas.Refresh();
		$('<div class=OpenCategoriesButton>').appendTo(document.body).on('click', () => CategoryGroupsWindow.Self.Visible=true);
	} catch(e) {
		const Message=Util.GetErrorMessage(e);
		if(MCanvas?.CanRender)
			MCanvas.ErrorMessage=Message;
		else
			$('#map').empty().append($('<div>').text(Message));
	}
}

$(Main);