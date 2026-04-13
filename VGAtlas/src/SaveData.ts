/* eslint-disable @typescript-eslint/naming-convention */
//noinspection JSUnusedGlobalSymbols,SpellCheckingInspection

import CryptoJS from 'crypto-js';
import { WillBeSet } from './Util/SharedClasses';
import { TranslatePassthrough } from './Util/Translations';

//Encrypted file variables
const BeginningBytes=25;
const EndBytes=1;
const KeyString='UKu52ePUBwetZ9wNX88o54dnfKRu0T1l';

export default class SaveDataClass
{
	private playerData=new PlayerData();
	private sceneData =new SceneData();
	public get PlayerData() { return this.playerData; }
	public get SceneData () { return this.sceneData ; }

	private constructor() { }
	public get ctor() { return SaveDataClass; }
	public static		CreateEmptySave			(					): SaveDataClass			{ return new SaveDataClass(); }
	public static async	CreateFrom_File			(File:File			): Promise<SaveDataClass>	{ return this.CreateFrom_FileBytes(new Uint8Array(await File.arrayBuffer())); }
	public static		CreateFrom_Base64String	(Base64String:string): SaveDataClass			{ return CreateSaveData(JSON.parse(DecryptSaveFile(Base64String)) as SaveDataClass); }
	public static		CreateFrom_JSONString	(JSONString:string	): SaveDataClass			{ return CreateSaveData(JSON.parse(JSONString) as SaveDataClass); }
	public static		CreateFrom_FileBytes	(Bytes:Uint8Array	): SaveDataClass			{
		if(Bytes.length<BeginningBytes+EndBytes)
			throw new TranslatePassthrough("File is too small", 'LoadSaveFile').AsError();

		return this.CreateFrom_Base64String(new TextDecoder('latin1').decode(
			Bytes.subarray(BeginningBytes, Bytes.length-EndBytes)
		));
	}
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

//Importing save data
function CreateSaveData(NewSaveData:SaveDataClass): SaveDataClass
{
	Object.setPrototypeOf(NewSaveData, SaveDataClass.prototype);
	Object.setPrototypeOf(NewSaveData.PlayerData, PlayerData.prototype);
	Object.setPrototypeOf(NewSaveData.SceneData, SceneData.prototype);
	Object.setPrototypeOf(NewSaveData.SceneData.PersistentBools, SerializedList.prototype);
	Object.setPrototypeOf(NewSaveData.SceneData.PersistentInts, SerializedList.prototype);
	return NewSaveData;
}

function DecryptSaveFile(Base64String:string): string
{
	//Decrypt AES-256-ECB with PKCS7 padding
	const Decrypted=CryptoJS.AES.decrypt(
		CryptoJS.lib.CipherParams.create({ ciphertext:CryptoJS.enc.Base64.parse(Base64String) }),
		CryptoJS.enc.Utf8.parse(KeyString),
		{ mode:CryptoJS.mode.ECB, padding:CryptoJS.pad.Pkcs7 }
	);

	return CryptoJS.enc.Utf8.stringify(Decrypted);

/*C# code
	string Base64String=File.ReadAllText(FileName);
	byte[] FileBytes=Convert.FromBase64String(Base64String.Substring(BeginningBytes, Base64String.Length-BeginningBytes-EndBytes));
	return
		Encoding.UTF8.GetString(new RijndaelManaged {
			Key=Encoding.UTF8.GetBytes("UKu52ePUBwetZ9wNX88o54dnfKRu0T1l"),
			Mode=CipherMode.ECB,
			Padding=PaddingMode.PKCS7
		}.CreateDecryptor().TransformFinalBlock(FileBytes, 0, FileBytes.Length));
 */
}