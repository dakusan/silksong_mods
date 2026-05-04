import './Style.scss';
import $							from 'jquery';
import { DevStrings, FriendClass, InitFuncs, Log, PopupMessage,
	StatStr, Util, WillBeSet }	from './Util/SharedClasses';
import { WM, Window }				from './Util/WindowManager';
import Translations, { DefaultTr }	from './Util/Translations';
import { Share }					from './Share';
import MonitorSaveValues			from './MonitorSaveValues';
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

//Update Share readonly members during initialization
interface SettableShare
{
	MCanvas	:MapCanvas;
	DS		:DataStorage;
	MC		:MapControl;
	WM		:typeof WM;
	MSV		:MonitorSaveValues;
	Tr		:Translations;
}

//Set up translations
Translations.DefaultDOMModule='Atlas';
DefaultTr.HasFallbacks=false;
(Share as SettableShare).Tr=Translations.StandardCreate('Atlas');
Share.LC.Language.SetTranslations(Share.Tr);

//Independent libraries that can or have loaded early
(Share as SettableShare).WM=WM;
try { SetupOneTimeMessage(); } catch { }

async function Main()
{
	let MCanvas:MapCanvas=WillBeSet;
	try {
		//Primary map and icon functionality
		MCanvas=(Share as SettableShare).MCanvas=new MapCanvas(87.7487, -87.5855, 2090, 1569);
		(Share as SettableShare).MSV=new MonitorSaveValues();
		Share.MSV.Load().then();
		const DS=new DataStorage();
		await MCanvas.Init('Assets/PAtlasMap.png');
		await (DS as DataStorage_Friend).Load(
			'Assets/Categories.json',
			'Assets/Items.json',
			'Assets/Misc.json',
			Share.LC.IconSet.V,
		);
		(Share as SettableShare).DS=DS; //Setting this now flags some locations that DataStorage has now completed loading so they can start their tasks
		(DS as DataStorage_Friend).CompleteInit();

		//Finish initializing
		if(localStorage.getItem('SaveData'))
			try { Share.SaveData=Share.SaveData.ctor.CreateFrom_JSONString(localStorage.getItem('SaveData')!); }
			catch(e) { HandleLoadSaveFileError(e, localStorage.getItem('SaveDataFileName') ?? "Unknown filename"); }
		(Share as SettableShare).MC=new MapControl();
		for(const Fn of InitFuncs)
			Fn();
		InitFuncs.length=0;
		MCanvas.ExtraMessage=undefined;
		MCanvas.Refresh();
		CreateMainMenu();
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

function SetupOneTimeMessage()
{
	//If the popup message has changed, then show it
	function HashString(Str:string)
	{
		let Hash=0;
		for(let i=0; i<Str.length; i++)
			Hash=(Hash*31+Str.charCodeAt(i))|0;
		return Hash.toString(16);
	}
	function ShowNewMessage()
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
		setTimeout(() => $('#MainMenu .Popup').show(), 0); //Timeout so that if the menu is already opened, it will reopen after being closed
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
	$('#MenuOpenConfig').on('click', () => void(MyConfigWindow.FocusWin()));

	//Search window
	const MySearchWindow=new SingleInstanceWindow(
		async () => new (await import('./SearchWindow')).default(),
	);
	$('#MenuOpenSearch').on('click', () => void(MySearchWindow.FocusWin()));

	//Debug window
	const MyDebugWindow=new SingleInstanceWindow(
		async () => new (await import('./DebugWindow')).default(),
	);
	$('#MenuDebug').on('click', () => void(MyDebugWindow.FocusWin()));

	//Log window
	const MyLogWindow=new SingleInstanceWindow(
		() => {
			const NewWin=new Window({
				LanguageChanged:() => Share.Tr.OnLanguageLoadedOnce(() => MyLogWindow.MyWin!.Title=Share.Tr.T('Logs')),
				SaveID:'Logs', Type:'Logs',
			});

			setTimeout(() => {
				MyLogWindow.MyWin!.LanguageChanged!(undefined!);
				for(const LL of Log.AllLogLines)
					AddLogLine(LL);
			}, 0);

			return NewWin;
		},
	);
	$('#MenuOpenLogs').on('click', () => void(MyLogWindow.FocusWin()));

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

	const LoadSaveWindow=new SingleInstanceWindow(SetupLoadSaveFileWindow);
	$('#MenuLoadSave').on('click', () => void(LoadSaveWindow.FocusWin()));
}

function HandleLoadSaveFileError(e:unknown, FileName:string)
{
	const Err=Share.Tr.TDef("ERROR_LOADING", 'LoadSaveFile', "Error loading save data from “{0}”: {1}", false, FileName, Share.Tr.TranslatePassthroughError(e));
	Log.Error(Err);
	new PopupMessage(Err);
}

function SetupLoadSaveFileWindow()
{
	const NewWin=new Window({
		LanguageChanged:() => Share.Tr.OnLanguageLoadedOnce(() => NewWin.Title=Share.Tr.TDef('WINDOW_TITLE', 'LoadSaveFile', "Load Save")),
		SaveID:'LoadSaveFile', Type:'LoadSaveFile',
	});
	NewWin.LanguageChanged!(undefined!);

	//Create the DOM content
	NewWin.$Content.append(`
<input type=file class=SafeHide id=LoadSaveFileButton>
<div>
	<label for=LoadSaveFileButton class='TranslationEl WinButton' data-translation-key="SELECT_FILE_LABEL" data-translation-section=LoadSaveFile data-translation-default="Select your save file"></label>
	<button id=UnloadSaveFileButton class='TranslationEl WinButton' data-translation-key="CLEAR_SELECTED_FILE" data-translation-section=LoadSaveFile data-translation-default="Unload save file"></button>
</div>
<div>
	<span class=TranslationEl data-translation-key="CURRENT_SELECTED_FILE" data-translation-section=LoadSaveFile data-translation-default="Currently selected file: "></span>
	<span id=CurrentlySelectedFile></span>
</div>
<div id=SaveFileContents>
	<button class='CopyButton WinButton'></button>
	<div class=Text></div>
</div>
	`);
	Share.Tr.UpdateDOMSubElements(NewWin.$Content[0]);

	//Setup CurrentlySelectedFile and SaveFileContents
	const CurrentlySelectedFile=$('#CurrentlySelectedFile');
	const SaveFileContents=$('#SaveFileContents');
	const Get_NO_FILE_LOADED=() => Share.Tr.TDef("NO_FILE_LOADED", 'LoadSaveFile', "None");
	function UpdateContentState()
	{
		const FileName=localStorage.getItem('SaveDataFileName');
		const HasContents=(FileName!==null);
		const TextEl=SaveFileContents.children('.Text');
		CurrentlySelectedFile.text(FileName ?? Get_NO_FILE_LOADED());
		ClearButton.toggleClass('Disabled', !HasContents);
		ClearButton.prop('disabled', !HasContents);
		SaveFileContents.toggleClass('HasContents', HasContents);
		TextEl.empty();
		if(!HasContents)
			return;

		//Add highlighted lines
		TextEl.html(
			DevStrings.SafeRich(JSON.stringify(Share.SaveData, null, '    '))
				.replaceAll('<br>', StatStr.NewLine)
				.replace(/^(?: {4}"(?:playerData|sceneData)"| {8}"(?:persistentBools|persistentInts|EnemyJournalKillData|MateriumCollected|ToolEquips|Collectables)").*$\n/gm, '<div class=HLLine>$&<span class=Buttons><button class=Prev></button><button class=Next></button></span></div>')
				.replaceAll(StatStr.NewLine, '<br>')
			);

		//Scroll to next/previous highlighted line
		TextEl.find('.Prev,.Next').on('click', e => {
			const $El=$(e.currentTarget);
			const IsNext=$El.hasClass('Next');
			const Parent=$El.parents('.HLLine').eq(0);
			const List=TextEl.find('.HLLine');
			const NewEl=List[List.index(Parent)+(IsNext ? 1 : -1)];
			NewWin.$Content[0].scrollTop=NewEl.offsetTop; //While this offset is not strictly correct, it leaves a good amount of padding above the selected element
		});
	}

	//Set up other buttons
	const UploadButton=<JQuery<HTMLInputElement>>
		$('#LoadSaveFileButton')
		.on('change', async () => {
			const File=UploadButton[0].files?.[0];
			if (!File)
				return;

			try {
				Share.SaveData=await Share.SaveData.ctor.CreateFrom_File(File);
				localStorage.setItem('SaveData', JSON.stringify(Share.SaveData));
				localStorage.setItem('SaveDataFileName', File.name);
				UpdateContentState();
				Share.MSV.UpdateAllUsedValuesOnLoad();
				Log.Info("Save file loaded: "+File.name);
			}
			catch(e) {
				HandleLoadSaveFileError(e, File.name);
			}

			UploadButton.val(null!);
		})
		.appendTo(NewWin.$Content);

	SaveFileContents.children('.CopyButton').on('click', () => void(
		navigator.clipboard.writeText(JSON.stringify(Share.SaveData, null, '    '))
			.catch(e => new PopupMessage("Clipboard copy failed: "+Util.GetErrorMessage(e)))
	));

	const ClearButton=$('#UnloadSaveFileButton').on('click', () => {
		Share.SaveData=Share.SaveData.ctor.CreateEmptySave();
		localStorage.removeItem('SaveData');
		localStorage.removeItem('SaveDataFileName');
		UpdateContentState();
		Share.MSV.UpdateAllUsedValuesOnLoad();
		Log.Info("Save file cleared");
	});

	UpdateContentState();

	return NewWin;
}

$(Main);