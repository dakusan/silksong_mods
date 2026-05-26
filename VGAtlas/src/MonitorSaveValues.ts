import { Log, PopupMessage, StatStr, Util } from './Util/SharedClasses';
import GetExtraAssets from './Util/GetExtraAssets';
import { Share } from './Share';
import type SaveDataClass from './SaveData';

type IIDHashType=number; //ItemIDHash

//This class from the ported mod actually did monitoring of save values, but now it just handles save loads via UpdateAllUsedValuesOnLoad()
export default class MonitorSaveValues
{
	//Static stuff
	public static GetItemIDHash(ItemID:number, ForStarting:boolean): IIDHashType { return !ForStarting ? ItemID : ItemID*-1; }
	private static readonly JsonFileName='ItemFinder.json';
	private static readonly PlayerDataStr='PlayerData';
	public get ctor(): typeof MonitorSaveValues { return MonitorSaveValues; }

	//Members
	private readonly MatchedIcons=new Map<IIDHashType, string>();

	//Initialize the monitoring
	public async Load(): Promise<void>
	{
		let JSONData:object;
		try {
			JSONData=await GetExtraAssets.LoadJson('Assets/'+this.ctor.JsonFileName);
		} catch(e) {
			const FailedKey="ItemFinderLoadFailed", FailedSection='MonitorSaveValues';
			const DefaultMessage=`Loading item finder json failed! Everything will be marked as not found and you’ll be getting excessive save value finds.\n{0}`;
			Share.Tr.AddFormatParameters(FailedKey, FailedSection, Util.GetErrorMessage(e));
			Log.Error(Share.Tr.ctor.FormatString(DefaultMessage, Util.GetErrorMessage(e)));
			await Share.Tr.LanguageListLoaded;
			Share.Tr.OnLanguageLoadedOnce(() =>
				new PopupMessage(Share.Tr.TDef(FailedKey, FailedSection, DefaultMessage, false))
			);
			return;
		}

		for(const [ItemIDStr, SaveInfo] of Object.entries((JSONData as {MatchedIcons:Record<string, string>}).MatchedIcons)) {
			const IsForStarting=(ItemIDStr[0]==='~');
			const ItemID=Util.GetInt(ItemIDStr.slice(IsForStarting ? 1 : 0));
			if(ItemID===null)
				Log.Error(StatStr.NeedsTranslate+`Invalid key found in ${this.ctor.JsonFileName}: ${ItemIDStr}`);
			else
				this.MatchedIcons.set(this.ctor.GetItemIDHash(ItemID, IsForStarting), SaveInfo);
		}

		this.UpdateAllUsedValuesOnLoad();
	}

	//When a game is loaded: Update all icon IsFound states from the player and scene data; Reload window data that shows item information
	public UpdateAllUsedValuesOnLoad(): void
	{
		for(const I of Share.DS?.Items.values() ?? []) {
			let IsLinked=false;
			for(const ForStarting of [false, true]) {
				const GameName=this.MatchedIcons.get(this.ctor.GetItemIDHash(I.ID, ForStarting));
				const Parts=(GameName!==undefined ? GameName.split('.') : [StatStr.Empty, StatStr.Empty]);
				if(Parts.length!==2)
					Log.Error("Invalid name found: "+GameName);

				I.SetStatusFlag(ForStarting,
					   !!GameName											//Match was found
					&& Parts.length===2										//Match value is in proper format (dot separated)
					&& this.ctor.GetLiveCompletedValue(Parts[0], Parts[1])	//If value is completed
				);
				IsLinked||=!ForStarting && !!GameName;
			}
			I.IsLinked=IsLinked;
		}

		//Run icon and window updates that contain item infos
		for(const Win of Share.WM.AllWindows)
			if(['Item', 'Search'].includes(Win.Type!))
				Win.Refresh();
	}

	private static IsValueCompleted(a:boolean|number|string|null): boolean
	{
		return (
			//a==null ? false
			  typeof(a)==='boolean' ? a
			: typeof(a)==='number' ? a===0
			: typeof(a)==='string' && a!==StatStr.Empty
			//: false;
		);
	}

	//Get if a named value is completed
	public static GetLiveCompletedValue(From:string, Name:string): boolean
	{
		return (
			  From===this.PlayerDataStr
			? this.GetLiveCompletedValue_PlayerData(Name, Share.SaveData.PlayerData)
			: this.GetLiveCompletedValue_SceneData(From, Name, Share.SaveData.SceneData)
		);
	}

	private static GetLiveCompletedValue_PlayerData(Name:string, PD:SaveDataClass['PlayerData']): boolean
	{
		const P=Name.split("__", 2);
		return (
			P.length>1 ? //string[] (HashSet in-game) contains a __ between the variable name and the string lookup
				(PD.Get(P[0]) as string[])?.includes?.(P[1]) ?? false : //string[]
				this.IsValueCompleted((PD.Get(Name) as number|boolean) ?? null) //Scalars
		);
	}

	private static GetLiveCompletedValue_SceneData(From:string, Name:string, SD:SaveDataClass['SceneData']): boolean
	{
		return	SD.PersistentBools.GetValue(From, Name) ??
				SD.PersistentInts.GetValue(From, Name)===0;
	}
}