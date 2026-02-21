import { Log, StatStr, Util, Vector2, WillBeSet } from "./SharedClasses"
import { ExpNo, ExpYes, JsonClass, JsonConverter, JsonConverter_Generic, JsonPropsDec, LoadJson, SaveJson } from "./JSON";
import { MapIcon, Sprite } from "./MapIcon"
import { Share } from "./Main"
import { LC, Languages } from "./AtlasConfig"
import { LoadMisc_StaticLink } from "./DataStorage"
import { SaveData } from "./SaveData";
import { Translate } from "./TempClasses"

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

	@ExpNo() protected _TotalCount  :number=0	;			public get TotalCount  	() { return this._TotalCount	; } protected set TotalCount	(Value) { this._TotalCount	=Value; } //Set by friends
	@ExpNo() protected _CurrentCount:number=0	; @ExpNo()	public get CurrentCount	() { return this._CurrentCount	; } protected set CurrentCount	(Value) { this._CurrentCount=Value; } //Set by friends
	@ExpNo() protected _Sprite:Sprite=WillBeSet	; @ExpNo()	public get Sprite		() { return this._Sprite		; } protected set Sprite		(Value) { this._Sprite		=Value; } //Set by friends

	public static readonly MinID=101;
	public static readonly MaxID=499;
	public static IDInRange(ID:number) { return ID>=Category.MinID && ID<=Category.MaxID; }
}
class Category_Friend extends Category
{
	public override set CurrentCount(_Value:number){ }
}

//Characters used for string manipulation stand-ins
const enum LStatStr {
	ChainItem_AmountChar="\uE002",
	TrVarChar="\uE003", //Translation variable character - This is placed around any translation names in strings for quick variable fill-in
}

//Translation functions
const Tr=new Translate();
function TSan(Message:string)						: string { return Tr.TDef(Message, "ItemFields", Message, true)!; }
function TDef(Message:string, Default:string|null)	: string { return Tr.TDef(Message, "ItemFields", Default, true)!; }
function TrVar(Name:string)							: string { return LStatStr.TrVarChar+Name+LStatStr.TrVarChar; }
const VarDefaults:Record<string, string>={
	SEP_AND			: ", ",
	SEP_OR			: "OR",
	FLAG_NOT		: "NOT",
	FLAG_STARTED	: "STARTED",
	FLAG_RECOMMENDED: "RECOMMENDED",
};

//Get the title from the item ID (cannot be run until after all Items are loaded, which is why below objects have delayed string rendering)
function GetItemTitleFromID(ID:string): string|undefined
{
	const i=Number.parseInt(ID, 10);
	return	!Number.isFinite(i)		? undefined
		:	StaticLink.IDInRange(i)	? Share.DS.StaticLinks.get(i)?.Name
		:							  Share.DS.Items.get(i)?.Title;
}

//Items (icons)
enum ChainType { Reqs, Needs, Rewards }
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

	//These are all set from Converters. Anything ran through a converter has a confirmed type so JsonPropsDec is not needed.
	public WhereAt	?:RenderedField	=undefined;
	public Notes	?:RenderedField	=undefined;
	public Effect	?:RenderedField	=undefined;
	public Tip		?:RenderedField	=undefined;
	public Reqs		?:ChainList		=undefined;
	public Needs	?:ChainList		=undefined;
	public Rewards	?:ChainList		=undefined;
	public Store	?:StoreItems	=undefined;
	public ImageURLs?:string[]		=undefined;
	public OtherLinks?:string[]		=undefined;

	public Unlocks?=new ItemSet(this);
	public AQFrom ?=new ItemSet(this); //AQFrom=Acquired From

	@ExpNo() public get Pos() { return new Vector2(this.x, this.y); }

	@ExpNo() private UniqueLinkIndex=0;
	@ExpNo() protected get GetLinkID() { return `${this.ID}.${this.UniqueLinkIndex++}`; }

	public static readonly MinID=100_001;
	public static readonly MaxID=Util.MaxInt;
	public static IDInRange(ID:number) { return ID>=Item.MinID && ID<=Item.MaxID; }

	//Render the description
	public get Description() { return this.toString(); }
	public override toString()
	{
		return [
			this.WhereAt	?.Render("Where"		),
			this.Notes		?.Render("Notes"		),
			this.Effect		?.Render("Effect"		),
			this.Tip		?.Render("Tips"			),
			this.Reqs		?.Render("Requirements"	),
			this.Needs		?.Render("Needs"		),
			this.Rewards	?.Render("Rewards"		),
			this.Unlocks	?.Render("Unlocks"		),
			this.AQFrom		?.Render("Acq. From"	),
			this.Store		?.Render("Store"		),
		].filter(V => V!==undefined).join(StatStr.NewLine);
	}

	public AddStoreChainList(ChainListToCopy:ChainList): string|null //Returns error or null on success
	{
		const CType=ChainListToCopy.Type;
		if(CType!==ChainType.Reqs && CType!==ChainType.Needs)
			return "Combined chain lists must be for either Reqs or Needs: "+CType;

		//Clone the new list
		if((ChainListToCopy.Items?.length ?? 0)<=0)
			return "List cannot be empty";
		const ClonedChainList=new ChainList(this, ChainListToCopy.StartString, CType);

		//Create the new ChainItem list
		const ChainListToStartWith=(CType===ChainType.Reqs ? this.Reqs : this.Needs);
		const ListToStartWith=ChainListToStartWith?.Items ?? [];
		const NewList:ChainItem[][]=[...ListToStartWith, ...ClonedChainList.Items!];

		//Create the combined chain list
		const CombinedExtraString=[
			ChainListToStartWith?.ExtraStr?.StartString,
			ChainListToCopy.ExtraStr?.StartString,
		].filter(S => S!==undefined && S!==StatStr.Empty).join("; ");

		//Set the new chain list and return success
		this[CType===ChainType.Reqs ? "Reqs" : "Needs"]=new ChainList(
			this, (ListToStartWith.length>0 ? ChainListToStartWith!.StartString+'|' : StatStr.Empty)+ChainListToCopy.StartString,
			CType, NewList, !CombinedExtraString ? undefined : new RenderedField(this, CombinedExtraString)
		);

		return null;
	}

	@ExpNo() private _CurrentToggleState=CategoryToggleState.Unknown;
	@ExpNo() public get CurrentToggleState() { return this._CurrentToggleState; }
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

	@ExpNo() public IsStarted=false;
	@ExpNo() private _IsFound=false;
	@ExpNo() public get IsFound() { return this._IsFound; }
	public set IsFound(Value:boolean)
	{
		if(this._IsFound===Value)
			return;
		(Share.DS.Categories.get(this.CategoryID)! as Category_Friend).CurrentCount+=(Value ? 1 : -1);
		this._IsFound=Value;
		Util.SetNullable(this.MapIcon, "IsFound", Value);
	}

	@ExpNo() private _IsLinked=false;
	@ExpNo() public get IsLinked() { return this._IsLinked; }
	public set IsLinked(Value:boolean)
	{
		if(this._IsLinked===Value)
			return;
		this._IsLinked=Value;
		Util.SetNullable(this.MapIcon, "IsLinked", Value);
	}

	@ExpNo() private _MapIcon?:MapIcon=undefined;
	@ExpNo() public get MapIcon(): MapIcon|undefined { return this._MapIcon; }
	public set MapIcon(Value:MapIcon)
	{
		this._MapIcon=Value;
		this._MapIcon.IsFound=this.IsFound;
		this._MapIcon.IsLinked=this.IsLinked;
		this._MapIcon.CTS=this.CurrentToggleState;
	}

	@ExpNo() public get Visible() {
		return	this.CurrentToggleState===CategoryToggleState.All
			|| (this.CurrentToggleState===CategoryToggleState.Incomplete && !this.IsFound);
	}
}

class Item_Friend extends Item
{
	public override get GetLinkID() { return super.GetLinkID; }
}

class StringCountPair { //Only last item in RenderParts will have SL=null
	constructor(public StrBeforeCount:string, public readonly SL?:StaticLink) { }
}

//A full chain list for a single field
export class ChainList extends JsonClass
{
	@ExpNo() public readonly Parent:Item;
	public readonly StartString:string;

	constructor(
		Parent:Item,
		ItemList:string,
		public readonly Type:ChainType,
		public readonly Items?:ChainItem[][],
		public readonly ExtraStr?:RenderedField
	) {
		super();
		[this.Parent, this.StartString]=[Parent, ItemList];
		if(this.Items!==undefined)
			return;

		//Get and remove the extra string part
		const ExtraStrPos=ItemList.indexOf("^");
		if(ExtraStrPos!==-1) {
			this.ExtraStr=new RenderedField(this.Parent, ItemList.slice(ExtraStrPos+1));
			ItemList=ItemList.slice(0, ExtraStrPos);
		}

		//Parse the list
		if(ItemList!==StatStr.Empty)
			this.Items=ItemList.split("|").map(OrStr =>
				OrStr.split("`").map(ItemStr =>
					new ChainItem(this, ItemStr)
				)
			);
	}

	//--------------------String rendering--------------------
	//StringCountPair are created such that we can essentially do a `strings.Join(RenderParts.Select(RP => RP.StrBeforeCount+RP.SL.NumCollected))`
	private static readonly ExtractItemCounts=new RegExp(`${LStatStr.ChainItem_AmountChar}\\d+${LStatStr.ChainItem_AmountChar}`, "g");
	private static readonly ReplaceLangVars=new RegExp(`${LStatStr.TrVarChar}([\\p{L}_]+)${LStatStr.TrVarChar}`, "gu");
	//@ts-expect-error Private function is used in json export
	@ExpYes() private get ExpRenderParts() { return this.RenderParts===undefined ? {_:this.RenderedString, RP:this.RenderParts}.RP : this.RenderParts; }
	@ExpNo() private RenderParts:StringCountPair[]=WillBeSet;
	@ExpNo() private RenderPartsAgnostic:string[]=WillBeSet; //Original RenderParts strings before replacing language variables
	@ExpNo() private CurrentLang:Languages=WillBeSet;
	public get RenderedString() { return this.CompileRenderString(); }
	private CompileRenderString()
	{
		//Fill in RenderParts on language change
		if(this.CurrentLang!==LC.Language.V) {
			this.CurrentLang=LC.Language.V;

			//Only need to render parts and fill in RenderPartsAgnostic once
			if(this.RenderPartsAgnostic===undefined) {
				this.RenderParts=this.GetRenderParts();
				this.RenderPartsAgnostic=new Array(this.RenderParts.length);
				for(const [Index, SCP] of this.RenderParts.entries())
					this.RenderPartsAgnostic[Index]=SCP.StrBeforeCount;
			}

			//Translate strings from agnostic back into RenderParts
			for(const [Index, AgStr] of this.RenderPartsAgnostic.entries())
				this.RenderParts[Index].StrBeforeCount=AgStr.replace(
					ChainList.ReplaceLangVars,
					M => TDef(M.slice(1, -1), null) ?? VarDefaults[M.slice(1, -1)]
				);
		}

		//Shortcut if no parts to fill in
		if(this.RenderParts.length===1)
			return this.RenderParts[0].StrBeforeCount;

		//Build string from RenderParts
		const Parts=new Array<string>(this.RenderParts.length*2-1);
		for(const [Index, Part] of this.RenderParts.entries()) {
			Parts[Index*2]=Part.StrBeforeCount;
			if(Part.SL!==undefined)
				Parts[Index*2+1]=Part.SL.NumCollected.toString();
		}
		return Parts.join(StatStr.Empty);
	}

	private GetRenderParts()
	{
		//If no list, just use the extra string
		if(this.Items===undefined)
			return [new StringCountPair(this.ExtraStr?.toString() ?? StatStr.Empty)];

		//Reformat the list
		const Ret=
			this.Items.map(ItemList =>
				ItemList.map(I => (I as Category_ChainItem).RenderedStringInternal).join(`<span style="color:${Share.DS.LinkColors.Sep_AND}">${TrVar("SEP_AND")}</span>`)
			).join(` <b><span style="color:${Share.DS.LinkColors.Sep_OR}">${TrVar("SEP_OR")}</span></b> `)
			+(this.ExtraStr===undefined ? StatStr.Empty : `; ${this.ExtraStr}`);

		//Extract ExtractItemCounts sections as StringCountPair. Only StaticLinks are used since items cannot have a count and are just set as “1”
		const Parts:StringCountPair[]=[];
		let PendingStr:string=StatStr.Empty;
		let CurPos=0;
		for(const m of Ret.matchAll(ChainList.ExtractItemCounts)) {
			//Only add when non-empty
			if(m.index>CurPos)
				PendingStr+=Ret.slice(CurPos, m.index);
			CurPos=m.index+m[0].length;

			//Add to pending string as Count=1 if not a static link
			const ID=Number.parseInt(m[0].slice(1, -1), 10);
			if(!StaticLink.IDInRange(ID)) {
				PendingStr+="1";
				continue;
			}

			//Create a new StringCountPair and reset for the next string part
			Parts.push(new StringCountPair(PendingStr, Share.DS.StaticLinks.get(ID)!));
			PendingStr=StatStr.Empty;
		}
		if(CurPos<Ret.length)
			PendingStr+=Ret.slice(CurPos);
		Parts.push(new StringCountPair(PendingStr));
		return Parts;
	}

	public Render(FieldTitle:string) { return `<b>${TSan(FieldTitle)}</b>: `+this.RenderedString; }
}

//A single item in a ChainList
export class ChainItem extends JsonClass
{
	@ExpNo() public readonly Parent:ChainList;
	public readonly StartString:string;
	@ExpNo() private _RenderedStringReal:string=WillBeSet; //Contains AmountChar where the live collected count will need to be inserted
	@ExpNo() private 	get RenderedStringReal		() { return this._RenderedStringReal ??= this.FinishInternalRender(); }
	@ExpNo() protected	get RenderedStringInternal	() { return this.GetProcessedRenderString(this.RenderedStringReal, `${LStatStr.ChainItem_AmountChar}${this.LinkID}${LStatStr.ChainItem_AmountChar}`); } //AmountChar becomes LinkID surround by AmountChar
			 public		get RenderedString			() { return this.GetProcessedRenderString(this.RenderedStringReal, "?"); } //Changes AmountChar to a question mark
	private GetProcessedRenderString(Str:string, Replacement:string) { return this.LinkID===-1 ? Str : Str.replaceAll(LStatStr.ChainItem_AmountChar, Replacement); }

	public readonly FlagNot			:boolean=false	;
	public readonly FlagStarted		:boolean=false	;
	public readonly FlagRecommend	:boolean=false	;
	public readonly FlagUnlinked	:boolean=false	;
	public readonly FlagAmount		:number	=1		;
	@ExpNo() protected _Name		:string	=StatStr.Empty	; public get Name	() { return this._Name	; } //Set after DataStorage load complete (usually during DataStorage.CompleteInit)
	@ExpNo() protected _LinkID		:number	=-1				; public get LinkID	() { return this._LinkID; } //Set after DataStorage load complete (usually during DataStorage.CompleteInit)
	public constructor(Parent:ChainList, Item:string)
	{
		super();
		this.Parent=Parent;
		this.StartString=Item;

		//Find flags
		let LoopDone=false;
		let CharIndex:number;
		for(CharIndex=0; CharIndex<Item.length && !LoopDone; CharIndex++)
			switch(Item[CharIndex]) {
				case '!': this.FlagNot					=true; break;
				case '~': this.FlagStarted				=true; break;
				case '@': this.FlagRecommend			=true; break;
				case '?': this.FlagUnlinked=LoopDone	=true; break;
				default : CharIndex--;		LoopDone	=true; break;
				case '*':
					this.FlagAmount=0;
					let CharDigit:number;
					while((CharDigit=Item.charCodeAt(++CharIndex)-'0'.charCodeAt(0))>=0 && CharDigit<=9)
						this.FlagAmount=this.FlagAmount*10+CharDigit;
					if(Item[CharIndex]==='*')
						LoopDone=true;
					else
						CharIndex--;
					break;
			}

		//Temporarily use name until SetIDAndName
		this._Name=Item.slice(CharIndex);
		if(Share.DS===undefined)
			ChainItem.NeedsIDAndName.push(this);
		else
			this.SetIDAndName();
	}
	private FinishInternalRender()
	{
		//Add flags back
		const Parts:string[]=[];
		if(this.FlagNot		 ) Parts.push(`<i>${TrVar("FLAG_NOT")			}</i> `);
		if(this.FlagStarted  ) Parts.push(`<i>${TrVar("FLAG_STARTED")		}</i> `);
		if(this.FlagRecommend) Parts.push(`<i>${TrVar("FLAG_RECOMMENDED")	}</i> `);
		const Amounts=
			  this.FlagAmount===1 ? undefined
			: `<span style="color:${Share.DS.LinkColors.CollectedCounts}">`+(this.Parent.Type!==ChainType.Rewards ? LStatStr.ChainItem_AmountChar : StatStr.Empty)
			+ `<b>${this.FlagAmount}</b>×</span>`;

		//If unlinked or linking failed do not make it a real link
		if(this.LinkID===-1)
			return [Amounts?.replace(LStatStr.ChainItem_AmountChar, StatStr.Empty) ?? StatStr.Empty, "<u>", ...Parts, this.Name, "</u>"].join(StatStr.Empty);

		//Prepare variables for rendered string
		const ExtraColor=
			  this.FlagNot		? Share.DS.LinkColors.Flag_NOT			.Value
			: this.FlagStarted	? Share.DS.LinkColors.Flag_STARTED		.Value
			: this.FlagRecommend? Share.DS.LinkColors.Flag_RECOMMENDED	.Value
			: undefined;

		//Render as a linked item
		return [
			`<a data-LinkID=${(this.Parent.Parent as Item_Friend).GetLinkID} data-ItemID=${this.LinkID} href="#${this.LinkID}"`,
			ExtraColor!==undefined ? ` style="color:${ExtraColor}"` : undefined,
			">",
			Amounts?.replace(LStatStr.ChainItem_AmountChar, `<b><size=-4>${LStatStr.ChainItem_AmountChar}</size></b><span style="color:white">/</span>`),
			"<u>",
			...Parts,
			this.Name,
			"</u></a>",
		].filter(Str => Str!==undefined).join(StatStr.Empty);
	}

	//Set LinkID and Name (Set after DataStorage loading is complete [immediate if that is already done])
	private static readonly NeedsIDAndName:ChainItem[]=[];
	protected static Process_NeedsIDAndName()
	{
		for(const CI of ChainItem.NeedsIDAndName)
			CI.SetIDAndName();
		ChainItem.NeedsIDAndName.length=0;
	}
	private SetIDAndName() //This is run on every ChainItem after all the Items and StaticLinks are loaded
	{
		const TestID=Number.parseInt(this.Name, 10);
		if(this.FlagUnlinked || !Number.isFinite(TestID))
			return;

		const RetErr=(Type:string) => Log.Error(`Invalid ${Type} ID Found in ${this.Parent.Parent.ID}.${this.Parent.Type}: ${TestID}`);
		let FoundItem:StaticLink|Item|undefined;
		if(StaticLink.IDInRange(TestID))
			if(!(FoundItem=Share.DS.StaticLinks.get(TestID)))	RetErr("Static Link");
			else												[this._LinkID, this._Name]=[TestID, FoundItem.Name];
		else if(!Item.IDInRange(TestID))						RetErr("Unranged");
		else if(!!(FoundItem=Share.DS.Items.get(TestID)))		[this._LinkID, this._Name]=[TestID, FoundItem.Title];
		else													RetErr("Item");
	}
}
class Category_ChainItem extends ChainItem
{
	public override get RenderedStringInternal() { return ""; }
}

//A string with item links inside square brackets rendered as actual links
class RenderedField extends JsonClass
{
	//Turn item links in a string into actual links
	private static readonly GetLinks=/\[(\d+)(~[^^|`\]]+)?]/g;

	@ExpNo() public readonly Parent:Item;
	public constructor(
		Parent:Item,
		public readonly StartString:string
	) { super(); this.Parent=Parent; }

	@ExpNo() private _RenderedString:string=WillBeSet;
	public get RenderedString() { return this._RenderedString ??= this.FinishInternalRender(); }
	private FinishInternalRender()
	{
		return this.StartString.replace(RenderedField.GetLinks, (_:string, ID:string, Text:string|undefined) => {
			Text=(Text ? Text.slice(1) : (GetItemTitleFromID(ID) ?? ID));
			return `<a data-LinkID=${(this.Parent as Item_Friend).GetLinkID} data-ItemID=${ID} href="#${ID}"><u>${Text}</u></a>`;
		});
	}
	public override toString() { return this.RenderedString; }
	public Render(FieldTitle:string) { return `<b>${TSan(FieldTitle)}</b>: `+this.RenderedString; }
}

class ItemSet extends JsonClass
{
	@ExpNo() public readonly Parent:Item;
	public constructor(Parent:Item) { super(); this.Parent=Parent; }
	@ExpNo() private readonly ItemList=new Set<Item>();
	@ExpNo() public get GetItems() { return this.ItemList.values(); }
	@ExpNo() public get HasItems() { return this.ItemList.size>0; }
	//@ts-expect-error Private function is used in json export
	@ExpYes() private get ExpItemIDs() { return this.ItemList.size===0 ? null : [...this.ItemList.values()].map(I => I.ID.toString()).join(", "); }

	@ExpNo() private IsRendered=false;
	@ExpNo() private _RenderedString?:string;
	public get RenderedString(): string|undefined
	{
		if(!this.IsRendered)
			[this.IsRendered, this._RenderedString]=[true, this.FinishInternalRender()];
		return this._RenderedString;
	}
	public Add	 (Item:Item) { this.IsRendered=false; this.ItemList.add(Item); }
	public Remove(Item:Item) { this.IsRendered=false; return this.ItemList.delete(Item); }
	public FinishInternalRender(): string|undefined
	{
		return	this.ItemList.size===0 ? undefined
			:	[...this.ItemList].map(Item =>
					`<LinkID=UL-${Item.ID}-${(this.Parent as Item_Friend).GetLinkID}><ATTR=ItemID>${Item.ID}</ATTR><u>${Item.Title}</u></LinkID>`
				).join(`<span style="color:${Share.DS.LinkColors.Sep_AND}">${TDef("SEP_AND", VarDefaults.SEP_AND)}</span>`);
	}
	public Render(FieldTitle:string): string|undefined { return this.ItemList.size===0 ? undefined : `<b>${TSan(FieldTitle)}</b>: ${this.RenderedString}`; }
}

//JSON type conversion
class CreateItemJSC_Str			extends JsonConverter<Item, string, RenderedField|ChainList								> { }
class CreateItemJSC_Store		extends JsonConverter<Item, {Reqs?:string, Needs:string, Rewards:string}[], StoreItems	> { }
class CreateItemJSC_StrArr		extends JsonConverter<Item, string[], string[]											> { }
class CreateItemJSC_NameOnly	extends JsonConverter<undefined, undefined, undefined									> { }
type CreateItemJSC_Types=CreateItemJSC_Str|CreateItemJSC_Store|CreateItemJSC_StrArr|CreateItemJSC_NameOnly;
export namespace CreateItem
{
	export function Process(ID:number, Obj:object) {
		return LoadJson.ClassFromObj<Item>(new Item(ID), Obj, OverrideFuncs as Record<string, JsonConverter_Generic<Item>>)
	}

	const OverrideFuncs:Record<string, CreateItemJSC_Types>={
		WhereAt:new CreateItemJSC_Str	((TheItem, Value) => new RenderedField	(TheItem, Value						)),
		Notes:	new CreateItemJSC_Str	((TheItem, Value) => new RenderedField	(TheItem, Value						)),
		Effect:	new CreateItemJSC_Str	((TheItem, Value) => new RenderedField	(TheItem, Value						)),
		Tip:	new CreateItemJSC_Str	((TheItem, Value) => new RenderedField	(TheItem, Value						)),
		Reqs:	new CreateItemJSC_Str	((TheItem, Value) => new ChainList		(TheItem, Value, ChainType.Reqs		)),
		Needs:	new CreateItemJSC_Str	((TheItem, Value) => new ChainList		(TheItem, Value, ChainType.Needs	)),
		Rewards:new CreateItemJSC_Str	((TheItem, Value) => new ChainList		(TheItem, Value, ChainType.Rewards	)),

		//Store needs to be created separately since it is nested
		Store:	new CreateItemJSC_Store	((TheItem, Value) => {
			const Items=new Array<StoreItem>(Value.length);
			for(const [Index, It] of Value.entries())
				Items[Index]=new StoreItem(
					  It.Reqs===undefined ? undefined
					: new ChainList(TheItem, It.Reqs, ChainType.Reqs),
					  new ChainList(TheItem, It.Needs, ChainType.Needs),
					  new ChainList(TheItem, It.Rewards, ChainType.Rewards)
				);
			return new StoreItems(Items);
		}),
		ImageURLs : new CreateItemJSC_StrArr((_, Value) => new Array<string>(...Value)),
		OtherLinks: new CreateItemJSC_StrArr((_, Value) => new Array<string>(...Value)),

		//Handle compacted Json data from CreateJSONs.php
		C: new CreateItemJSC_NameOnly(null, "CategoryID"),
		T: new CreateItemJSC_NameOnly(null, "Title"		),
		I: new CreateItemJSC_NameOnly(null, "IconID"	),
		R: new CreateItemJSC_NameOnly(null, "Reqs"		),
		A: new CreateItemJSC_NameOnly(null, "WhereAt"	),
		N: new CreateItemJSC_NameOnly(null, "Needs"		),
		W: new CreateItemJSC_NameOnly(null, "Rewards"	),
		E: new CreateItemJSC_NameOnly(null, "Effect"	),
		P: new CreateItemJSC_NameOnly(null, "Tip"		),
		O: new CreateItemJSC_NameOnly(null, "Notes"		),
		L: new CreateItemJSC_NameOnly(null, "OtherLinks"),
		S: new CreateItemJSC_NameOnly(null, "Store"	),
		U: new CreateItemJSC_NameOnly(null, "ImageURLs"	),
	};
}

//Store structures
export class StoreItem
{
	public constructor(
		public readonly Reqs:ChainList|undefined,
		public readonly Needs:ChainList,
		public readonly Rewards:ChainList
	) { }
}

class StoreItems
{
	public get RenderedString() { return this.FinishInternalRender(); }  //Cannot be cached due to changing item collection counts
	public constructor(
		public Items:StoreItem[],
	) { }
	private FinishInternalRender()
	{
		return this.Items.map(I =>
			"\n- "+I.Rewards.RenderedString+TDef("STORE_FOR", " for ")+I.Needs.RenderedString+
			(I.Reqs!==undefined ? Tr.TDef("STORE_REQ", "ItemFields", " (Required: {0})", false, I.Reqs.RenderedString) : StatStr.Empty)
		).join(StatStr.Empty);
	}
	public Render(FieldTitle:string) { return `<b>${TSan(FieldTitle)}</b>: `+this.RenderedString; }
}

export class StaticLink extends Object implements SaveJson.IExpOverride
{
	constructor(
		public readonly Name:string,
		public readonly CategoryID:number,
		public readonly ItemIDs?:number[],
		public readonly SpecialCount:number=-1,
		public readonly FName?:string,
		public readonly CountFunc?:(()=>number)
	) { super(); }

	public static readonly MinID=501;
	public static readonly MaxID=999;
	public static IDInRange(ID:number) { return ID>=StaticLink.MinID && ID<=StaticLink.MaxID; }

	public get NumCollected(): number
	{
		let V:ReturnType<typeof SaveData.PlayerData.Get>;
		//noinspection SuspiciousTypeOfGuard
		return	this.CategoryID!==-1							? Share.DS.Categories.get(this.CategoryID)!.CurrentCount
			:	this.ItemIDs!==undefined						? this.ItemIDs.filter(I => Share.DS.Items.get(I)!.IsFound).length
			:	this.CountFunc!==undefined						? this.CountFunc()
			:	this.FName===undefined							? this.SpecialCount
			:	(V=SaveData.PlayerData.Get(this.FName))===null	? 0
			:	typeof(V)==="number"							? V
			:	typeof(V)==="boolean"							? (V ? 1 : 0)
			:													  0;
	}

	public override toString() { return this.NumCollected+" of "+this.ExpOverride; }

	public get ExpOverride()
	{
		return	this.CategoryID!==-1						? Share.DS.Categories.get(this.CategoryID)!.Title
			:	this.ItemIDs!==undefined					? this.ItemIDs.map(ItemID => `${Share.DS.Items.get(ItemID)!.Title} [${Share.DS.Items.get(ItemID)!.ID}]`).join(", ")
			:	this.CountFunc!==undefined					? this.CountFunc.name
			:	this.FName===undefined						? this.SpecialCount.toString()
			:												this.FName;
	}

	//JSON type conversion
	public static *Process(StaticLinks:Record<string, LoadMisc_StaticLink>, Items:Map<number, Item>, Categories:Map<number, Category>): Generator<[number, StaticLink]>
	{
		//Shortcut functions
		let CurName:string=WillBeSet, RemID:string=WillBeSet;
		let CatID:number;
		function AddSL(ID:number, P:{CategoryID?:number, ItemIDs?:number[], SpecialCount?:number, FName?:string, CountFunc?(): number, OverwriteName?:string, ErrStr?:string}): [number, StaticLink]
		{
			if(P.ErrStr!==undefined)
				LineErr(P.ErrStr);
			return [ID, new StaticLink(P.OverwriteName ?? CurName, P.CategoryID ?? -1, P.ItemIDs, P.SpecialCount ?? 0, P.FName, P.CountFunc)];
		}
		function LineErr(Err:string, CompleteFail:boolean=false): undefined { Log.Error(`Error on Static Link #${RemID}${(CompleteFail ? " [Skipped]" : StatStr.Empty)}: ${Err}`); return undefined; }
		function IsValidSpecialFieldType(MemberName:string) { const T:unknown=SaveData.PlayerData.Get(MemberName); return typeof(T)==="number" || typeof(T)==="boolean"; }
		function GetNum(NumStr:string): number|null { const N=Number(NumStr); return Number.isFinite(N) ? N : null; }

		//Process the static links
		let MyID:number, Special:string, SpecialInt:number;
		for(const [ID, L] of Object.entries(StaticLinks))
			if     ((MyID=GetNum(RemID=ID)!)===null			)	LineErr("ID is not an int",					true);						//ID is not an int
			else if(!StaticLink.IDInRange(MyID)				)	LineErr("ID is not valid for a Static Link",true);						//ID not in StaticLink range
			else if(!Array.isArray(L) || L.length===0		)	LineErr("Array is empty",					true);						//No entries in the array
			else if(typeof(CurName=L[0])!=="string"			)	yield AddSL(MyID, {OverwriteName:"???", ErrStr:"Name is not a string"});//Invalid name
			else if(L.length===1							)	yield AddSL(MyID, {SpecialCount:1});									//Unlinked
			else if(L.length===2 && typeof(L[1])==="string" && (Special=L[1]))															//Special check
				if((SpecialInt=GetNum(Special)!)!==null)		yield AddSL(MyID, {SpecialCount:SpecialInt});							//Special Count Success
				else if(StaticLink.SpecialFuncs.has(Special))	yield AddSL(MyID, {CountFunc:StaticLink.SpecialFuncs.get(Special)!});	//Special GetCount func
				else if(SaveData.PlayerData.Has(Special))																				//Special FieldInfo Check
					if(!IsValidSpecialFieldType(Special))		yield AddSL(MyID, {ErrStr:`PlayerData.${Special} ≠ bool/int/enum`});	//Special FieldInfo failed (not int)
					else										yield AddSL(MyID, {FName:Special});										//Special FieldInfo success
				else											yield AddSL(MyID, {ErrStr:`Invalid value for special: ${Special}`});	//Special FieldInfo failed (doesn’t exist)
			else if(L.length===2 && Category.IDInRange(CatID=(typeof(L[1])==="number" ? L[1] : -1)))									//Category check
				if(Categories.has(CatID))						yield AddSL(MyID, {CategoryID:CatID});									//Category success
				else											yield AddSL(MyID, {ErrStr:`Invalid Category ID ${CatID}`});				//Category failed
			else												yield AddSL(MyID, {ItemIDs:												//Item list
				L.slice(1).map(I =>
					  typeof(I)!=="number"	? LineErr("ItemID is not a number: "		+I)
					: !Item.IDInRange(I)	? LineErr("ItemID is not a valid Item ID: "	+I)
					: !Items.has(I)			? LineErr("ItemID is not a valid Item: "	+I)
					: I
				).filter(I => I!==undefined)});
	}

	//Other special types for use with CountFunc
	private static readonly SpecialFuncs=new Map<string, () => number>([
		[ "ToolSlots", StaticLink.GetToolSlots ],
	]);
	private static GetToolSlots()
	{
		let UnlockedSlotCount=0;
		for(const [, { Name: CrestName, Data: CrestData }] of Object.entries(SaveData.PlayerData.ToolEquips.savedData)) {
			//Hunter crests do not count towards unlocked slot count
			if(CrestName.startsWith("Hunter"))
				continue;

			//Count the unlocked slots for the current crest
			let CurrentToolUnlockedSlotCount=0;
			for(const Slot of CrestData.Slots ?? [])
				if(Slot.IsUnlocked)
					CurrentToolUnlockedSlotCount++;

			//All crests but the Toolmaster have a Silk Skills slot we need to subtract
			if(CrestName!=="Toolmaster" && CurrentToolUnlockedSlotCount>1)
				CurrentToolUnlockedSlotCount--;

			//Add to the total
			UnlockedSlotCount+=CurrentToolUnlockedSlotCount;
		}

		return UnlockedSlotCount;
	}
}