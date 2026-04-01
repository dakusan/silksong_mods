import './Style.scss';
import $							from 'jquery';
import { FriendClass, InitFuncs, Log,
	PopupMessage, Util, WillBeSet }	from './Util/SharedClasses';
import { WM }						from './Util/WindowManager';
import Translations					from './Util/Translations';
import { type Window }				from './Util/WindowManager';
import { Share }					from './Share';
import { MonitorSaveValues }		from './TempClasses';
import MapCanvas					from './MapCanvas';
import MapControl					from './MapControl';
import DataStorage					from './DataStorage';
import type ConfigWindow			from './Config/ConfigWindow';

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
Translations.DefaultDOMModule='Atlas';
Share.Tr=Translations.StandardCreate('Atlas');
Share.WM=WM;
Share.LC.Language.SetTranslations(Share.Tr);

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
		CreateMainMenu();
		if((import.meta as unknown as {env:{DEV:boolean}}).env.DEV)
			import('./Debug');
		Log.Info('Load complete');
	} catch(e) {
		const Message=Util.GetErrorMessage(e);
		if(MCanvas?.CanRender)
			MCanvas.ErrorMessage=Message;
		else
			$('#map').empty().append($('<div>').text(Message));
	}
}

function CreateMainMenu()
{
	//Popup button
	$('#MainMenu .PopupButton').on('click', () => {
		setTimeout(() => $('#MainMenu .Popup').show(), 0); //Timeout so that if the menu is already open it will reopen after being closed
		const ClosePopup=() => {
			$('#MainMenu .Popup').hide();
			$(window).off('click', ClosePopup);
		};
		setTimeout(() => $(window).on('click', ClosePopup), 0); //Timeout so that this current click doesn’t fire this event
	});

	//Menu items
	$('#MenuOpenCategories').on('click', async () => {
		const CategoryGroupsWindow=(await import('./DockableWindows/CategoryGroupsWindow')).default;
		CategoryGroupsWindow.Self.Visible=true;
		CategoryGroupsWindow.Self.Focus();
	});

	let MyConfigWindow:ConfigWindow|undefined|null; //Null while loading
	$('#MenuOpenConfig').on('click', async () => {
		if(MyConfigWindow)
			return MyConfigWindow.Focus();
		if(MyConfigWindow===null)
			return;
		MyConfigWindow=null;

		MyConfigWindow=new (await import('./Config/ConfigWindow')).default(Share.LC, Share.Tr);
		const OriginalOnClosing=MyConfigWindow.OnClosing;
		MyConfigWindow.OnClosing=() => { OriginalOnClosing.call(MyConfigWindow); MyConfigWindow=undefined; return false; }
	});

	//Add log lines to the log window
	Log.MaxStoredLogLines=1000;
	let MyLogWindow:Window|undefined|null; //Null while loading
	function AddLogLine(LL:(typeof Log.AllLogLines)[number])
	{
		if(!MyLogWindow)
			return;
		MyLogWindow.$Content.children().slice(Log.MaxStoredLogLines-1).remove();
		$('<div class=LogLine>').append([
			$('<span class=Time>').text(new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit', second:'2-digit'})),
			$('<span class=Contents>').text(String(LL.LogInfo[0])),
		])
			.toggleClass('IsError', LL.IsError)
			.prependTo(MyLogWindow.$Content);
	}
	Log.OnLog.Add('LogWindow', AddLogLine);

	//Open the log window
	$('#MenuOpenLogs').on('click', async () => {
		if(MyLogWindow)
			return MyLogWindow.Focus();
		if(MyLogWindow===null)
			return;
		MyLogWindow=null;
		MyLogWindow=new (await import('./Util/WindowManager')).Window({
			OnClosing:() => { MyLogWindow=undefined; return false; },
			LanguageChanged:() => Share.Tr.OnLanguageLoadedOnce(() => MyLogWindow!.Title=Share.Tr.T('Logs')),
			SaveID:'Logs',
		});
		MyLogWindow.LanguageChanged!(undefined!);
		for(const LL of Log.AllLogLines)
			AddLogLine(LL);
	});
}

$(Main);