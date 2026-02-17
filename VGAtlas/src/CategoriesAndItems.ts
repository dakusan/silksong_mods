import { StatStr, Util, Vector2, WillBeSet } from "./SharedClasses"
import { JsonClass, JsonPropsDec, LoadJson } from "./LoadJSON"
import { MapIcon, Sprite } from "./MapIcon"
import { Share } from "./Main"

export enum CategoryToggleState
{
	All=0, Incomplete, None, Unknown //Unknown must be last
}

//Category groups (title and list of categories)
export class CategoryGroup extends Map<number, Category>
{
	constructor(public readonly Title:string, public readonly Order:number) { super(); }
}

//Categories (All Items have a category)
export class Category extends JsonClass
{
	constructor(
		public readonly ID:number
	) { super(); }

	@JsonPropsDec(true) public readonly Order:number=-1;
	@JsonPropsDec(true) public readonly IconID:number=-1;
	@JsonPropsDec(true, StatStr.Empty) public Title:string=WillBeSet;

	protected _TotalCount  :number=0	; public get TotalCount  () { return this._TotalCount	; } protected set TotalCount	(Value) { this._TotalCount	=Value; } //Set by friends
	protected _CurrentCount:number=0	; public get CurrentCount() { return this._CurrentCount	; } protected set CurrentCount	(Value) { this._CurrentCount=Value; } //Set by friends
	protected _Sprite:Sprite=WillBeSet	; public get Sprite		 () { return this._Sprite		; } protected set Sprite		(Value) { this._Sprite		=Value; } //Set by friends
}
class Category_Friend extends Category
{
	public override set CurrentCount(_Value:number){ }
}

//Items (icons)
export class Item extends JsonClass
{
	constructor(
		public readonly ID:number
	) { super(); }

	@JsonPropsDec(true) public readonly CategoryID:number=-1; //Locking down CategoryID to make sure only registered categories are used
	@JsonPropsDec(true, StatStr.Empty) public Title:string=WillBeSet;
	@JsonPropsDec(true) public readonly x:number=-1;
	@JsonPropsDec(true) public readonly y:number=-1;

	public IconID:number=-1;

	public get Pos() { return new Vector2(this.x, this.y); }

	private _CurrentToggleState=CategoryToggleState.Unknown;
	public get CurrentToggleState() { return this._CurrentToggleState; }
	public set CurrentToggleState(Value:CategoryToggleState)
	{
		if(Value===CategoryToggleState.Unknown)
			return;
		this._CurrentToggleState=Value;
		Util.SetNullable(this.MapIcon, "CTS", Value);
	}
	public SetStatusFlag(ForStarted:boolean, Value:boolean)
	{
		if(!ForStarted)
			this.IsFound=Value;
		else
			this.IsStarted=Value;
	}

	public IsStarted=false;
	private _IsFound=false;
	public get IsFound() { return this._IsFound; }
	public set IsFound(Value:boolean)
	{
		if(this._IsFound===Value)
			return;
		(Share.DS.Categories.get(this.CategoryID)! as Category_Friend).CurrentCount+=(Value ? 1 : -1);
		this._IsFound=Value;
		Util.SetNullable(this.MapIcon, "IsFound", Value);
	}

	private _IsLinked=false;
	public get IsLinked() { return this._IsLinked; }
	public set IsLinked(Value:boolean)
	{
		if(this._IsLinked===Value)
			return;
		this._IsLinked=Value;
		Util.SetNullable(this.MapIcon, "IsLinked", Value);
	}

	private _MapIcon?:MapIcon=undefined;
	public get MapIcon(): MapIcon|undefined { return this._MapIcon; }
	public set MapIcon(Value:MapIcon)
	{
		this._MapIcon=Value;
		this._MapIcon.IsFound=this.IsFound;
		this._MapIcon.IsLinked=this.IsLinked;
		this._MapIcon.CTS=this.CurrentToggleState;
	}

	public get Visible() {
		return	this.CurrentToggleState===CategoryToggleState.All
			|| (this.CurrentToggleState===CategoryToggleState.Incomplete && !this.IsFound);
	}
}
export namespace CreateItem
{
	export function Process(ID:number, Obj:object) {
		return LoadJson.ClassFromObj<Item>(new Item(ID), Obj);
	}
}