export class MonitorSaveValues
{
	public UpdateAllUsedValuesOnLoad(): void { } //Should be internal
	public get GetMatchedIcons(): ReadonlyMap<number, string> { return new Map(); } //Key is ItemIDHash
	public static GetItemIDHash(ItemID:number, ForStarting:boolean) : number { return !ForStarting ? ItemID : ItemID*-1; }
}