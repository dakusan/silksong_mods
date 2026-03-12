import { DevStrings, StatStr, Util } from './SharedClasses';

export class Translate
{
		public TDef(_Key:string, _Section:string|null=null, Default:string|null=StatStr.Empty, SafeRich:boolean=false, ...FormatList:Util.Primitive[]): string|null
		{
			let Ret=Default;
			if(Ret===undefined || Ret===null)
				return Ret;
			for(const [Index, Value] of FormatList.entries())
				Ret=Ret.replaceAll(`{${Index}}`, Value?.toString() ?? StatStr.Empty);
			return SafeRich ? DevStrings.SafeRich(Ret) : Ret;
		}
		public Translate(Key:string, Section:string|null=null, SafeRich:boolean=false, ...FormatList:Util.Primitive[]) { return this.TDef(Key, Section, Key, SafeRich, ...FormatList); }
}

export class MonitorSaveValues
{
	public UpdateAllUsedValuesOnLoad(): void { } //Should be internal
	public get GetMatchedIcons(): ReadonlyMap<number, string> { return new Map(); } //Key is ItemIDHash
	public static GetItemIDHash(ItemID:number, ForStarting:boolean) : number { return !ForStarting ? ItemID : ItemID*-1; }
}