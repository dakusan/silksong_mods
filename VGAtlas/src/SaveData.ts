/* eslint-disable @typescript-eslint/naming-convention */
//noinspection JSUnusedGlobalSymbols,SpellCheckingInspection

import { WillBeSet } from './Util/SharedClasses';
import { TranslatePassthrough } from './Util/Translations';
type RO<T>=Readonly<T>;

//Encrypted file variables
const BeginningBytes=25;
const EndBytes=1;
const KeyString='UKu52ePUBwetZ9wNX88o54dnfKRu0T1l';

export default class SaveDataClass
{
	private playerData=new PlayerData();
	private sceneData =new SceneData();
	public get PlayerData(): PlayerData { return this.playerData; }
	public get SceneData (): SceneData  { return this.sceneData ; }

	private constructor() { }
	public get ctor(): typeof SaveDataClass { return SaveDataClass; }
	public static		CreateEmptySave			(					): SaveDataClass			{ return new SaveDataClass(); }
	public static async	CreateFrom_File			(File:File			): Promise<SaveDataClass>	{ return await this.CreateFrom_FileBytes(new Uint8Array(await File.arrayBuffer())); }
	public static async	CreateFrom_Base64String	(Base64String:string): Promise<SaveDataClass>	{ return CreateSaveData(JSON.parse(await DecryptSaveFile(Base64String)) as SaveDataClass); }
	public static		CreateFrom_JSONString	(JSONString:string	): SaveDataClass			{ return CreateSaveData(JSON.parse(JSONString) as SaveDataClass); }
	public static async	CreateFrom_FileBytes	(Bytes:RO<Uint8Array>):Promise<SaveDataClass>	{
		if(Bytes.length<BeginningBytes+EndBytes)
			throw new TranslatePassthrough("File is too small", 'LoadSaveFile').AsError();

		return await this.CreateFrom_Base64String(new TextDecoder('Latin1').decode(
			Bytes.subarray(BeginningBytes, Bytes.length-EndBytes)
		));
	}
}

class PlayerData
{
	public Get(Name:string): number|boolean|string[]|null
	{
		return	!this.hasOwnProperty(Name) ? null
			:	this[Name as keyof this] as number|boolean|string[];
	}
	public Has(Name:string): boolean { return this.hasOwnProperty(Name); }

	//Members used in Misc.json StaticLinks MUST be listed here
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
	public get SerializedList(): SceneDataItem<T>[] { return this.serializedList; }
	public GetValue(SceneName:string, ID:string): T|null
	{
		for(const SL of this.serializedList)
			if(SL.SceneName===SceneName && SL.ID===ID)
				return SL.Value;
		return null;
	}
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

async function DecryptSaveFile(Base64String:string): Promise<string>
{
	//Instead of using import('crypto-js'), this does the same thing but only pulls in the required modules, drastically reducing the module’s output chunk size
	const LoadParams={
		CryptoJS:	import('crypto-js/core'			),
		AES:		import('crypto-js/aes'			),
		Base64:		import('crypto-js/enc-base64'	),
		Utf8:		import('crypto-js/enc-utf8'		),
		ECB:		import('crypto-js/mode-ecb'		),
		Pkcs7:		import('crypto-js/pad-pkcs7'	),
	} as const;
	type LoadedModules={[K in keyof typeof LoadParams]: Awaited<(typeof LoadParams)[K]>['default']};
	const Mods=await Promise.all(
		Object.entries(LoadParams).map(async ([k, v]) =>
			[k, (await v).default] as const
		)
	).then(Entries => Object.fromEntries(Entries) as LoadedModules);
	const CryptoJS={
		...Mods.CryptoJS,
		AES:Mods.AES,
		enc:{Base64:Mods.Base64, Utf8:Mods.Utf8},
		mode:{ECB:Mods.ECB},
		pad:{Pkcs7:Mods.Pkcs7}
	};

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