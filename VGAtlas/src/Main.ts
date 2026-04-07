import './Style.scss';
import $							from 'jquery';
import { FriendClass, InitFuncs, Log,
	PopupMessage, Util, WillBeSet }	from './Util/SharedClasses';
import { WM }						from './Util/WindowManager';
import Translations, { DefaultTr }	from './Util/Translations';
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

//Set up translations
Translations.DefaultDOMModule='Atlas';
DefaultTr.HasFallbacks=false;
Share.Tr=Translations.StandardCreate('Atlas');
Share.LC.Language.SetTranslations(Share.Tr);

//Independent libraries that can or have loaded early
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

class SingleInstanceWindow<TWin extends Window>
{
	private OriginalOnClosing?:() => boolean=WillBeSet;
	public MyWin:TWin|undefined|null; //Null while loading

	constructor(
		protected readonly CreateWin:() => Promise<TWin>,
		protected readonly OnClosing?:() => boolean,
	) { }
	public async FocusWin()
	{
		//Only open once
		if(this.MyWin)
			return this.MyWin.Focus();
		if(this.MyWin===null)
			return;
		this.MyWin=null;

		this.MyWin=await this.CreateWin();
		this.OriginalOnClosing=this.MyWin.OnClosing;
		this.MyWin.OnClosing=() => this.RunOnClosing();
	}
	private RunOnClosing()
	{
		//Stop if callback says to stop
		if(this.OnClosing?.call(this.MyWin))
			return true;

		this.OriginalOnClosing?.call(this.MyWin);
		this.MyWin=undefined;
		return false;
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

	//-----------Menu items-----------
	//Categories window
	$('#MenuOpenCategories').on('click', async () => {
		const CategoryGroupsWindow=(await import('./DockableWindows/CategoryGroupsWindow')).default;
		CategoryGroupsWindow.Self.Visible=true;
		CategoryGroupsWindow.Self.Focus();
	});

	//Config window
	const MyConfigWindow=new SingleInstanceWindow<ConfigWindow>(
		async () => new (await import('./Config/ConfigWindow')).default(Share.LC, Share.Tr),
	);
	$('#MenuOpenConfig').on('click', async () => MyConfigWindow.FocusWin());

	//Search window
	const MySearchWindow=new SingleInstanceWindow(
		async () => new (await import('./SearchWindow')).default(),
	);
	$('#MenuOpenSearch').on('click', async () => MySearchWindow.FocusWin());

	//Log window
	const MyLogWindow=new SingleInstanceWindow(
		async () => {
			const NewWin=new (await import('./Util/WindowManager')).Window({
				LanguageChanged:() => Share.Tr.OnLanguageLoadedOnce(() => MyLogWindow.MyWin!.Title=Share.Tr.T('Logs')),
				SaveID:'Logs',
			});

			setTimeout(() => {
				MyLogWindow.MyWin!.LanguageChanged!(undefined!);
				for(const LL of Log.AllLogLines)
					AddLogLine(LL);
			}, 0);

			return NewWin;
		},
	);
	$('#MenuOpenLogs').on('click', async () => MyLogWindow.FocusWin());

	//Add log lines to the log window
	Log.MaxStoredLogLines=1000;
	function AddLogLine(LL:(typeof Log.AllLogLines)[number])
	{
		if(!MyLogWindow.MyWin)
			return;
		MyLogWindow.MyWin.$Content.children().slice(Log.MaxStoredLogLines-1).remove();
		$('<div class=LogLine>').append([
			$('<span class=Time>').text(new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit', second:'2-digit'})),
			$('<span class=Contents>').text(String(LL.LogInfo[0])),
		])
			.toggleClass('IsError', LL.IsError)
			.prependTo(MyLogWindow.MyWin.$Content);
	}
	Log.OnLog.Add('LogWindow', AddLogLine);
}

$(Main);