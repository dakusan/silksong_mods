/* eslint-disable @typescript-eslint/naming-convention */
//noinspection JSUnusedGlobalSymbols,SpellCheckingInspection

import { StatStr, Util, WillBeSet } from './SharedClasses';
import { LoadJson } from './JSON';

class SaveDataClass
{
	private playerData=new PlayerData();
	private sceneData =new SceneData();
	public get PlayerData() { return this.playerData; }
	public get SceneData () { return this.sceneData ; }
}

class PlayerData
{
	public Get(Name:string): number|boolean|null
	{
		return	!this.hasOwnProperty(Name) ? null
			:	this[Name as keyof this] as number|boolean;
	}
	public Has(Name:string): boolean { return this.hasOwnProperty(Name); }
	public geo=0;
	public ShellShards=0;
	public act2Started=false;
	public act3_wokeUp=false;
	public permadeathMode=false;
	//eslint-disable-next-line @typescript-eslint/array-type
	public ToolEquips:{savedData:Array<{Name:string, Data:{IsUnlocked:boolean, Slots:Array<{EquippedTool:string, IsUnlocked:boolean}>}}>}={savedData:[]};
}

class SceneData
{
	private persistentBools=new SerializedList<boolean>();
	private persistentInts =new SerializedList<number >();
	//public geoRocks=new SerializedList<number>(); //Not needed
	public get PersistentBools(): SerializedList<boolean> { return this.persistentBools; }
	public get PersistentInts (): SerializedList<number > { return this.persistentInts ; }
}
class SerializedList<T extends boolean|number>
{
	private serializedList:SceneDataItem<T>[]=[];
	public get SerializedList() { return this.serializedList; }
}
class SceneDataItem <T extends boolean|number>
{
	public	 SceneName	:string	=WillBeSet;
	public	 ID			:string	=WillBeSet;
	public	 Value		:T		=WillBeSet;
	//public Mutator	:0|1	=WillBeSet; //Not needed: this is only used within the game engine to tell it to periodically reset the value
}

export let SaveData=new SaveDataClass();
await ImportSaveData('Assets/SaveData.json'); //For debugging, if this file exists, use it

//Importing save data
export async function ImportSaveData(FileURL:string): Promise<string|null>
{
	try {
		const NewSaveData=await LoadJson.FromURL(FileURL) as SaveDataClass;
		if(NewSaveData)
			SaveData=CreateSaveData(NewSaveData);
		return null;
	} catch(e) {
		return StatStr.NeedsTranslate+`Error loading save data from “${FileURL}”: ${Util.GetErrorMessage(e)}`;
	}
}
function CreateSaveData(NewSaveData:SaveDataClass): SaveDataClass
{
	Object.setPrototypeOf(NewSaveData, SaveDataClass.prototype);
	Object.setPrototypeOf(NewSaveData.PlayerData, PlayerData.prototype);
	Object.setPrototypeOf(NewSaveData.SceneData, SceneData.prototype);
	Object.setPrototypeOf(NewSaveData.SceneData.PersistentBools, SerializedList.prototype);
	Object.setPrototypeOf(NewSaveData.SceneData.PersistentInts, SerializedList.prototype);
	return NewSaveData;
}