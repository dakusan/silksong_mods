import './Style.scss';
import $							from 'jquery';
import { FriendClass, InitFuncs, Log, PopupMessage, Util, Vector2, WillBeSet,
								}	from './Util/SharedClasses';
import { WM, Window }				from './Util/WindowManager';
import Translations, { DefaultTr }	from './Util/Translations';
import { Share }					from './Share';
import MonitorSaveValues			from './MonitorSaveValues';
import MapCanvas					from './MapCanvas';
import MapControl					from './MapControl';
import DataStorage					from './DataStorage';
import { CreateCustomItem }			from './CustomItem';

//Mimic C++ friend / C# internal
abstract class DataStorage_Friend extends DataStorage implements FriendClass
{
	public override async Load(_CategoriesPath:string, _ItemsPath:string, _MiscPath:string, _IconSetPath:string): Promise<void> { this.Stub(); }
	public override CompleteInit(): void { this.Stub(); }
	//Ignore these
	protected constructor() { super(); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}

//Set up translations
Translations.DefaultDOMModule='Atlas';
DefaultTr.HasFallbacks=false;
Util.GetMutable(Share).Tr=Translations.StandardCreate('Atlas');
Share.LC.Language.SetTranslations(Share.Tr);

//Independent libraries that can or have loaded early
Util.GetMutable(Share).WM=WM;
try { SetupOneTimeMessage(); } catch { }

async function Main(): Promise<void>
{
	let MCanvas:MapCanvas=WillBeSet;
	try {
		//Primary map and icon functionality
		MCanvas=Util.GetMutable(Share).MCanvas=new MapCanvas(100.0825, -100.0825, 2370, 1634);
		Util.GetMutable(Share).MSV=new MonitorSaveValues();
		Share.MSV.Load().then();
		const DS=new DataStorage();
		const MapImages={Default:'Assets/PAtlasMap.png', HighQuality:'Assets/PAtlasMap.FullRGBA.png'};
		await MCanvas.Init(Share.LC.UseHighQualityMap.V ? MapImages.HighQuality : MapImages.Default);
		await (DS as DataStorage_Friend).Load(
			'Assets/Categories.json',
			'Assets/Items.json',
			'Assets/Misc.json',
			Share.LC.IconSet.V,
		);
		Util.GetMutable(Share).DS=DS; //Setting this now flags some locations that DataStorage has now completed loading so they can start their tasks
		(DS as DataStorage_Friend).CompleteInit();

		//Finish initializing
		if(localStorage.getItem('SaveData'))
			try { Share.SaveData=Share.SaveData.ctor.CreateFrom_JSONString(localStorage.getItem('SaveData')!); }
			catch(e) { HandleLoadSaveFileError(e, localStorage.getItem('SaveDataFileName') ?? "Unknown filename"); }
		Util.GetMutable(Share).MC=new MapControl(MapImages.Default, MapImages.HighQuality);
		for(const Fn of InitFuncs)
			Fn();
		InitFuncs.length=0;
		Log.MaxStoredLogLines=1000;
		MCanvas.ExtraMessage=undefined;
		MCanvas.Refresh();
		CreateMainMenu();
		CreateContextMenu();
		if(import.meta.env.DEV)
			import('./Debug');
		Log.Info('Load complete');
	} catch(e) {
		const Message=Util.GetErrorMessage(e);
		if(MCanvas?.CanRender)
			MCanvas.ErrorMessage=Message;
		else
			$('#map').empty().append($('<div>').text(Message));
	} finally {
		document.body.classList.remove('Loading');
	}
}

function SetupOneTimeMessage(): void
{
	//If the popup message has changed, then show it
	function HashString(Str:string): string
	{
		let Hash=0;
		for(let i=0; i<Str.length; i++)
			Hash=(Hash*31+Str.charCodeAt(i))|0;
		return Hash.toString(16);
	}
	function ShowNewMessage(): void
	{
		const NewMessage=Share.Tr.TDef('OneTimeMessage', undefined, 'Failed to load one time message');
		const Hash=HashString(NewMessage);
		if(Hash===localStorage.getItem('LastPopupMessageSeen') || !NewMessage)
			return;
		localStorage.setItem('LastPopupMessageSeen', Hash);
		new PopupMessage(NewMessage, true);
	}

	//Wait for the current language to load to see if the popup message has changed
	Share.Tr.LanguageListLoaded.finally(() => setTimeout(
		() => Share.Tr.OnLanguageLoadedOnce(ShowNewMessage),
		10 //Delay until after Share.Tr.LanguageListLoaded has fired the ConfigItem_Languages load
	));
}

class SingleInstanceWindow<TWin extends Window>
{
	private OriginalOnClosing?:() => boolean=WillBeSet;
	public MyWin:TWin|undefined|null; //Null while loading

	constructor(
		protected readonly CreateWin:() => TWin|Promise<TWin>,
		protected readonly OnClosing?:() => boolean,
	) { }
	public async FocusWin(): Promise<void>
	{
		//Only open once
		if(this.MyWin)
			return this.MyWin.Focus();
		if(this.MyWin===null)
			return;
		this.MyWin=null;

		try {
			this.MyWin=await this.CreateWin();
		} catch(e) {
			this.MyWin=undefined;
			const Err="Failed to open window: "+Util.GetErrorMessage(e);
			Log.Error(Err);
			new PopupMessage(Err);
			return;
		}

		this.OriginalOnClosing=this.MyWin.OnClosing;
		this.MyWin.OnClosing=() => this.RunOnClosing();
	}
	private RunOnClosing(): boolean
	{
		//Stop if callback says to stop
		if(
			   true===this.OnClosing?.call(this.MyWin)
			|| true===this.OriginalOnClosing?.call(this.MyWin)
		)
			return true;

		this.MyWin=undefined;
		return false;
	}
}

function ExecMenuPopup(El:JQuery): void
{
	const EventNS='.ExecMenuPopup';
	const ClosePopup=() => {
		El.hide();
		$(window).off(EventNS);
	};

	setTimeout(() => { //Timeout so that if the menu is already opened, it will reopen after being closed
		El.show();
		$(window)
			.off(EventNS)
			.on(`pointerdown${EventNS}`, e => $(e.target).closest(El).length ? null : ClosePopup())
			.on(`click${EventNS} auxclick${EventNS} contextmenu${EventNS}`, () => setTimeout(ClosePopup, 0));
	}, 0);
}

function CreateMainMenu(): void
{
	//Popup button
	$('#MainMenu .PopupButton').on('click', () => ExecMenuPopup($('#MainMenu .Popup')));

	//-----------Menu items-----------
	//Categories window
	//TODO: This could probably also be made into a single instance window
	$('#MenuOpenCategories').on('click', async () => {
		const CategoryGroupsWindow=(await import('./Windows/CategoryGroupsWindow/CategoryGroupsWindow')).default;
		CategoryGroupsWindow.Self.Visible=true;
		CategoryGroupsWindow.Self.Focus();
	});

	//Menu item create single instance windows
	function SingleInstanceWindowFromClick<T extends Window>(Selector:string, CreateWin:() => Promise<T>): SingleInstanceWindow<T>
	{
		const NewWin=new SingleInstanceWindow(CreateWin);
		$(Selector).on('click', () => void(NewWin.FocusWin()));
		return NewWin;
	}
	SingleInstanceWindowFromClick('#MenuOpenConfig'	, async () => new (await import('./Windows/ConfigWindow/ConfigWindow'		)).default(Share.LC, Share.Tr		));
	SingleInstanceWindowFromClick('#MenuOpenSearch'	, async () => new (await import('./Windows/SearchWindow/SearchWindow'		)).default(							));
	SingleInstanceWindowFromClick('#MenuDebug'		, async () => new (await import('./Windows/DebugWindow/DebugWindow'			)).default(							));
	SingleInstanceWindowFromClick('#MenuOpenLogs'	, async () => new (await import('./Windows/LogsWindow/LogsWindow'			)).default(							));
	SingleInstanceWindowFromClick('#MenuLoadSave'	, async () => new (await import('./Windows/SaveFileWindow/SaveFileWindow'	)).default(HandleLoadSaveFileError	));
}

function HandleLoadSaveFileError(e:unknown, FileName:string): void
{
	const Err=Share.Tr.TDef("ERROR_LOADING", 'LoadSaveFile', "Error loading save data from “{0}”: {1}", false, FileName, Share.Tr.TranslatePassthroughError(e));
	Log.Error(Err);
	new PopupMessage(Err);
}

function CreateContextMenu(): void
{
	let LastMapPos:Vector2;
	Share.MCanvas.Events.Click.Add('Main.CreatePopupMenu', Ev => {
		if(!(Ev.Button===Ev.Buttons.Right || (Ev.Button===Ev.Buttons.Pointer && Ev.ClickInterval>=750))) //Right-click or 3/4-second pointer-click
			return;
		ExecMenuPopup($('#MapContextMenu').css('left', Ev.Event.clientX!).css('top', Ev.Event.clientY!));
		LastMapPos=Share.MCanvas.CanvasToMap(new Vector2(Ev.Pos.X, Ev.Pos.Y));
	});
	$('#AddCustomItem').on('click', async () =>
		new (await import('./Windows/CustomItemWindow/CustomItemWindow')).default(LastMapPos.X, LastMapPos.Y, CreateCustomItem)
	);
}

$(Main);