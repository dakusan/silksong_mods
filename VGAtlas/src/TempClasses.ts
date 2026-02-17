/* eslint-disable @typescript-eslint/naming-convention */
import { DevStrings, StatStr, Util } from "./SharedClasses"

// noinspection JSUnusedGlobalSymbols
class SavePlayerData
{
	[Name:string]:number|boolean;
	public geo=0;
	public ShellShards=0;
	public act2Started=false;
	public act3_wokeUp=false;
	public permadeathMode=false;
}
class PlayerDataExtended
{
	public ToolEquips:Record<string, {Slots:Record<string, {IsUnlocked:boolean}>[]}>={};
}
export const PlayerData={instance:Object.assign(new SavePlayerData(), new PlayerDataExtended())};

export class Translate
{
		public TDef(_Key:string, _Section:string|null=null, Default:string=StatStr.Empty, SafeRich:boolean=false, ...FormatList:Util.Primitive[])
		{
			let Ret=Default;
			if(Ret===undefined || Ret===null)
				return Ret;
			for(const [Index, Value] of FormatList.entries())
				Ret=Ret.replaceAll(`{${Index}}`, Value?.toString() ?? StatStr.Empty);
			return SafeRich ? DevStrings.SafeRich(Ret) : Ret;
		}
		public Translate(Key:string, Section:string|undefined=undefined, SafeRich:boolean=false, ...FormatList:Util.Primitive[]) { return this.TDef(Key, Section, Key, SafeRich, ...FormatList); }
}