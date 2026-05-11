import $ from 'jquery';
import { DevStrings, Log, PopupMessage, StatStr, Util } from './Util/SharedClasses';
import { Window } from './Util/WindowManager';
import { Share } from './Share';
import Html from './HTML/SaveFileWindow.html?raw';

export default class SaveFileWindow extends Window
{
	constructor(
		private HandleLoadSaveFileError:(e:unknown, FileName:string) => void
	) {
		super({SaveID:'LoadSaveFile', Type:'LoadSaveFile'});
		this.LanguageChanged();
		this.Init();
	}

	private Init()
	{
		//Create the DOM content
		this.$Content.append(Html);
		Share.Tr.UpdateDOMSubElements(this.$Content[0]);

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
					UpdateContentState(this);
					Share.MSV.UpdateAllUsedValuesOnLoad();
					Log.Info("Save file loaded: "+File.name);
				}
				catch(e) {
					this.HandleLoadSaveFileError(e, File.name);
				}

				UploadButton.val(null!);
			})
			.appendTo(this.$Content);

		$('#SaveFileContents').children('.CopyButton').on('click', () => void(
			navigator.clipboard.writeText(JSON.stringify(Share.SaveData, null, '    '))
				.catch(e => new PopupMessage("Clipboard copy failed: "+Util.GetErrorMessage(e)))
		));

		$('#UnloadSaveFileButton').on('click', () => {
			Share.SaveData=Share.SaveData.ctor.CreateEmptySave();
			localStorage.removeItem('SaveData');
			localStorage.removeItem('SaveDataFileName');
			UpdateContentState(this);
			Share.MSV.UpdateAllUsedValuesOnLoad();
			Log.Info("Save file cleared");
		});

		UpdateContentState(this);
	}

	public override LanguageChanged()
	{
		Share.Tr.OnLanguageLoadedOnce(() => this.Title=Share.Tr.TDef('WINDOW_TITLE', 'LoadSaveFile', "Load Save"));
	}
}

function UpdateContentState(Win:SaveFileWindow)
{
	const SaveFileContents=$('#SaveFileContents');
	const FileName=localStorage.getItem('SaveDataFileName');
	const HasContents=(FileName!==null);
	const TextEl=SaveFileContents.children('.Text');
	$('#CurrentlySelectedFile').text(FileName ?? Share.Tr.TDef("NO_FILE_LOADED", 'LoadSaveFile', "None"));
	$('#UnloadSaveFileButton')
		.toggleClass('Disabled', !HasContents)
		.prop('disabled', !HasContents);
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
		Win.$Content[0].scrollTop=NewEl.offsetTop; //While this offset is not strictly correct, it leaves a good amount of padding above the selected element
	});
}