import Color, { type ColorInstance } from 'color';
import { DevStrings, FriendClass, Iter, Log, PopupMessage, Rect, StatStr, Util, Vector2, WillBeSet } from './Util/SharedClasses';
import { OtherObject } from './Config/Types/ConfigItem_Object';
import { LoadJson } from './Util/JSON';
import { Share } from './Share';
import { MonitorSaveValues } from './TempClasses';
import { MapIcon, Sprite } from './MapIcon';
import { Category, CategoryGroup, CategoryToggleState, ChainItem, ChainList, CreateItem, Item, LoadMisc_StaticLink, StaticLink } from './CategoriesAndItems';

const IconLenX		=10;
const IconLenY		=8;
const IconWidth		=65;
const IconHeight	=65;
const IconPadding	=1;
const NewIconSize	=18;
const NT=StatStr.NeedsTranslate;

//Color type that stores it HTML string and RGB color
class StringColor extends Object {
	constructor(
		public readonly Value:string,
		public readonly AsColor:ColorInstance
	) { super(); }
	public override toString() { return this.Value; }
}

//Shapes when loading from JSON
type LoadMisc_Set=Record<string, Record<string, string|LoadMisc_StaticLink>>;
type LoadCategory=Record<string, Record<string, {OrderID:number, IconID:number, Title:string}>>;

//Create icon sprites as needed
class IconSprites
{
	private SpriteList:(Sprite|null)[]=Array(IconLenX*IconLenY).fill(null);
	private CSSSpriteURL =document.createElement('style');
	private CSSSpriteList=document.createElement('style');
	public constructor()
	{
		//Create the special error sprite (which is always the last square and is of size ErrorTexSize*ErrorTexSize)
		const ErrorTexSize=54;
		const LastSpriteID=IconLenX*IconLenY-1;
		this.SpriteList[LastSpriteID]=this.CreateSprite(IconSprites.GetIconRectByID(LastSpriteID).SetWidth(ErrorTexSize).SetHeight(ErrorTexSize));

		//Create sprite sheets
		document.head.appendChild(this.CSSSpriteURL);
		document.head.appendChild(this.CSSSpriteList);
		this.CSSSpriteList.textContent=this.SpriteList.map((_, IconID) => {
			const X=IconID%IconLenX;
			const Y=Math.floor(IconID/IconLenX);
			return `.ItemIcon.I${IconID} { --x:${X}; --y:${Y}; }`
		}).join(StatStr.NewLine)+`
.ItemIcon {
	--ItemIconWidth		:${IconWidth}px;
	--ItemIconHeight	:${IconHeight}px;
	--ItemIconPadding	:${IconPadding}px;
	--NewIconSize		:${NewIconSize}px;
}`;
	}

	public Get(IconID:number)
	{
		//Instead of dealing with errors, just use an error icon when out of range
		if(IconID<0 || IconID>=(IconLenX*IconLenY))
			IconID=IconLenX*IconLenY-1;

		//Return if already created
		if(this.SpriteList[IconID]!==null)
			return this.SpriteList[IconID]!;

		//Create the sprite
		return this.SpriteList[IconID]=this.CreateSprite(IconSprites.GetIconRectByID(IconID));
	}

	//Set the sprites image
	private IconPicsTex?:ImageBitmap=undefined;
	protected SetIconPics(IconPicsTex:ImageBitmap, ImageURL:string)
	{
		this.IconPicsTex=IconPicsTex;
		for(const MySprite of this.SpriteList.values())
			if(MySprite!==null)
				MySprite.Image=IconPicsTex;

		//Update URL sprite sheet
		this.CSSSpriteURL.textContent=`.ItemIcon:before { background-image:url('${ImageURL}'); }`;
	}

	private static GetIconRectByID(IconID:number)
	{
		const X=IconID%IconLenX;
		const Y=Math.floor(IconID/IconLenX);
		return new Rect(
			X*(IconWidth +IconPadding),
			Y*(IconHeight+IconPadding),
			IconWidth, IconHeight
		);
	}

	private CreateSprite(IconRect:Rect) { return new Sprite(this.IconPicsTex, IconRect, new Vector2(0.5, 0.5)); }
}

//noinspection ExceptionCaughtLocallyJS
export default class DataStorage
{
	public readonly CategoryGroups:CategoryGroup[]=[];
	public readonly Categories=new Map<number, Category>();
	public readonly Items=new Map<number, Item>();
	public readonly StaticLinks=new Map<number, StaticLink>();
	public readonly MyIconSprites=new IconSprites();

	protected async Load(CategoriesPath:string, ItemsPath:string, MiscPath:string, IconSetPath:string)
	{
		//Start the async file loads
		const PCategories=LoadJson.FromURL(CategoriesPath);
		const PItems=LoadJson.FromURL(ItemsPath);
		const PMisc=LoadJson.FromURL(MiscPath);
		const PIconSet=Util.LoadImage(IconSetPath);

		//Load the categories
		let CategoryGroupsDict:LoadCategory;
		try {
			if(!(CategoryGroupsDict=(await PCategories) as LoadCategory))
				throw new Error("Categories is null");
		} catch(e) {
			throw new Error("Could not load categories, failing out: "+Util.GetErrorMessage(e));
		}
		if(!Object.keys(CategoryGroupsDict).length)
			throw new Error("Categories cannot be empty");

		//Sort, turn into arrays and dicts, and add IDs/Titles
		let i=0;
		for(const [GroupName, GroupsObj] of Object.entries(CategoryGroupsDict)) {
			const Groups=new CategoryGroup(GroupName, i);
			this.CategoryGroups[i++]=Groups;
			for(const [CatID, CatDataObj] of Object.entries(GroupsObj))
				try {
					if(!/^[1-9]\d*$/.test(CatID))
						throw new Error("Invalid CategoryID: "+CatID);
					const CatIDAsInt=Number.parseInt(CatID, 10);
					if(!Number.isFinite(CatIDAsInt))
						throw new Error("Invalid CategoryID: "+CatID);
					const CatData=LoadJson.ClassFromObj(new Category(CatIDAsInt), CatDataObj);
					Groups.set(CatIDAsInt, CatData);
					this.Categories.set(CatIDAsInt, CatData);
				} catch(e) { Log.Error(NT+`Could not load Category ${CatID}: ${Util.GetErrorMessage(e)}`); }
		}

		//Load the items
		let ItemsDict:object;
		try {
			ItemsDict=await PItems;
			if(!ItemsDict)
				throw new Error("Items is null");
		} catch(e) {
			throw new Error("Could not load items, failing out: "+Util.GetErrorMessage(e));
		}

		for(const [K, V] of Object.entries(ItemsDict))
			try {
				if(!/^[1-9]\d*$/.test(K))
					throw new Error("Invalid ItemID");
				const NewID=Number.parseInt(K, 10);
				if(!Number.isFinite(NewID))
					throw new Error("Invalid ItemID");
				this.Items.set(NewID, CreateItem.Process(NewID, V));
			} catch(e) { Log.Error(NT+`Could not load item ${K}: ${Util.GetErrorMessage(e)}`); }

		const MatchedIcons=Share.MSV.GetMatchedIcons;
		for(const [ItemID, ItemData] of this.Items.entries()) {
			ItemData.IsLinked=MatchedIcons.has(MonitorSaveValues.GetItemIDHash(ItemID, false));
			if(this.Categories.has(ItemData.CategoryID))
				continue;

			Log.Error(NT+`Invalid CategoryID[#${ItemData.CategoryID}] on Item[#${ItemID}]`);
			(ItemData as {CategoryID:number}).CategoryID=this.Categories.keys().next().value!; //Set the readonly value
		}
		for(const ItemData of this.Items.values()) {
			const Cat=this.Categories.get(ItemData.CategoryID)!;
			(Cat as Category_Friend).TotalCount=Cat.TotalCount+1;
		}

		//Load the static links and Misc
		try {
			const MiscDict=await PMisc;
			if(!MiscDict)
				throw new Error("Misc is null");
			(new DataStorage.LoadMisc).Process(this, MiscDict as LoadMisc_Set);
		} catch(e) {
			throw new Error("Could not load misc/static links, failing out: "+Util.GetErrorMessage(e));
		}

		//Create and update the sprite texture
		const LoadIconSet=async (NewIconSet:Promise<ImageBitmap>, ImageURL:string) => {
			try { (this.MyIconSprites as Icon_SpritesFriend).SetIconPics(await NewIconSet, ImageURL); }
			catch(e) { Log.Error("Could not load icons texture: "+Util.GetErrorMessage(e)); }
		};
		async function UpdateIconSet(ImageURL:string) {
			try { await LoadIconSet(Util.LoadImage(ImageURL), ImageURL); }
			catch(e) {
				Log.Error(e);
				throw e;
			}
		}
		await LoadIconSet(PIconSet, IconSetPath);
		Share.LC.IconSet.SettingChanged.Add('DataStorage.UpdateIconSet', UpdateIconSet);

		this.HandleColors();

		//Create the sprites
		for(const Category of this.Categories.values())
			(Category as Category_Friend).Sprite=this.MyIconSprites.Get(Category.IconID);

		this.LoadCategoryToggleStates(true);
	}

	//Store link colors in HTML
	private HandleColors()
	{
		const ColorsStylesheet=document.createElement('style');
		const UpdateColors=(_:unknown, ColorName:string) => {
			if(ColorName!=='Default' && ColorName!=='LinkHover')
				return;
			ColorsStylesheet.textContent=`
.ItemContents a, .ItemContents a:visited	{ color:${this.LinkColors.Default.Value		}; }
.ItemContents a:hover						{ color:${this.LinkColors.LinkHover.Value	}; }
			`;
		};
		document.head.appendChild(ColorsStylesheet);
		UpdateColors(null, 'Default');
		this.LinkColors.Callbacks.push(UpdateColors);
	}

	//Distribute chain system items
	protected CompleteInit()
	{
		//Static helpers
		const GetNonEmptyLists=(...CL:(ChainList|undefined)[]) => CL.filter(CLi => (CLi?.Items?.length ?? 0)>0) as ChainList[];
		const GetListItems=(CL:ChainList) => CL.Items!.flat();
		function AddReqOrNeedToReward(RewardItem:Item, ReqOrNeedList:ChainList, Items:Map<number, Item>)
		{
			//Add the Req/Need ChainList to the Reward
			const Error=RewardItem.AddStoreChainList(ReqOrNeedList);
			if(Error!==null)
				Log.Error(NT+`Error adding ${ReqOrNeedList.Parent.ID}.Store.${ReqOrNeedList.Type} to reward ${RewardItem.ID}: ${Error}`);

			//For Req/Needs items sets “Unlocks” to the reward
			for(const CI of GetListItems(ReqOrNeedList))
				if(Item.IDInRange(CI.LinkID))
					Items.get(CI.LinkID)!.Unlocks!.Add(RewardItem);
		}

		ChainItem_Friend.Process_NeedsIDAndName();

		//Distribute links from each item
		for(const ItemData of this.Items.values()) {
			//Fills in Item.{Unlocks, AQFrom} for items linked from this item
			for(const CL of GetNonEmptyLists(ItemData.Reqs, ItemData.Needs, ItemData.Rewards))
				for(const CI of GetListItems(CL))
					if(Item.IDInRange(CI.LinkID))
						(CL===ItemData.Rewards ? this.Items.get(CI.LinkID)!.AQFrom! : this.Items.get(CI.LinkID)!.Unlocks!).Add(ItemData);

			//Distribute store reward related items: Fills in Item.{Unlocks, AQFrom, Reqs, Needs} for items linked from this item’s store
			for(const SI of ItemData.Store?.Items ?? [])
				if(SI.Rewards.Items!==undefined)
					for(const RewardCI of GetListItems(SI.Rewards))
						if(Item.IDInRange(RewardCI.LinkID)) {
							this.Items.get(RewardCI.LinkID)!.AQFrom!.Add(ItemData); //Set reward’s AQFrom to the vendor
							for(const CL of GetNonEmptyLists(SI.Reqs, SI.Needs))
								AddReqOrNeedToReward(this.Items.get(RewardCI.LinkID)!, CL, this.Items);
						}
		}

		//Remove unused Unlocks/AQFrom
		for(const ItemData of this.Items.values()) {
			if(!ItemData.Unlocks!.HasItems)
				ItemData.Unlocks=undefined;
			if(!ItemData.AQFrom!.HasItems)
				ItemData.AQFrom=undefined;
		}

		this.LoadIcons();
	}

	//In case I decide to store more colors like this, I decided to make it an abstract class
	//Note: Do not add public properties to subclass unless they are colors
	public static readonly CColorsSet=class ColorsSet
	{
		protected readonly DefaultColors=new Map<string, string>();
		private HasInitialized=false;

		public constructor() {
			return new Proxy(this, {
				set:(Target, Prop:string|symbol, Value:unknown, Receiver:unknown) => {
					if(typeof(Prop)!=='string' || !Target.HasInitialized)
						return Reflect.set(Target, Prop, Value, Receiver);
					if(Target.DefaultColors.has(Prop))
						return Target.SetColor(Prop, String(Value));
					else if(Prop in Target)
						return Reflect.set(Target, Prop, Value, Receiver);
					throw new Error("Invalid property name: "+Prop);
				}
			});
		}
		public Init()
		{
			if(this.HasInitialized)
				return this;
			this.HasInitialized=true;

			//Harvest defaults from derived-class instance fields that are StringColor
			for(const [ColorName, ColorDefault] of Object.entries(this))
				if(ColorDefault instanceof(StringColor))
					this.DefaultColors.set(ColorName, ColorDefault.Value);

			//Normalize all members (parse defaults into AsColor, etc.)
			for(const [ColorName, ColorDefault] of this.DefaultColors) {
				try { Color(ColorDefault); }
				catch { throw new Error("Could not parse default color: "+ColorDefault); }
				this.SetColor(ColorName, ColorDefault);
			}

			return this;
		}
		public get ColorNames() { return this.DefaultColors.keys(); }

		private SetColor(ColorName:string, RequestedValue:string): true
		{
			//Get the new color (or use default if invalid)
			let NewColorStr=RequestedValue;
			let NewColorRGB;
			try { NewColorRGB=Color(RequestedValue); }
			catch { NewColorRGB=Color(NewColorStr=this.DefaultColors.get(ColorName)!); }

			//Set the new value
			const NewColorFinal=new StringColor(NewColorStr, NewColorRGB);
			(this as unknown as Record<string, StringColor>)[ColorName]=NewColorFinal;

			//Call the callback
			for(const Callback of this.Callbacks)
				try { Callback(NewColorFinal, ColorName, (this as unknown as Record<string, StringColor>)[ColorName], RequestedValue); }
				catch(e) { Util.OutputException("Set color callback", e); }

			return true;
		}

		public Callbacks:((NewValue:StringColor, ColorName:string, PreviousValue:StringColor, RequestedValue:string) => void)[]=[];
	};

	//Link colors. Do not add any other public properties unless they are colors
	public static readonly LinkColorsT=class LinkColorsT extends DataStorage.CColorsSet
	{
		//The following of these are set statically so realtime changing is not supported (for now): Flag_{NOT,STARTED,RECOMMENDED}, Sep_{OR,AND}
		public constructor() { super(); }
		public Default			=new StringColor('cyan',		null!); //Default link color
		public LinkHover		=new StringColor('yellow',		null!); //Color when a link has the mouse over it
		public LabelHover		=new StringColor('#4678C880',	null!); //Box color for the entire label when mouse over (in the search box); Desaturated, mid-luminance blue goes well with: red, teal, plum, yellow, cyan, white, black, green
		public Flag_NOT			=new StringColor('red',			null!); //Flag color (precedence=0) for NOT
		public Flag_STARTED		=new StringColor('teal',		null!); //Flag color (precedence=1) for STARTED
		public Flag_RECOMMENDED	=new StringColor('#dda0dd',		null!); //Flag color (precedence=2) for RECOMMENDED [#=plum]
		public Sep_OR			=new StringColor('purple',		null!); //Separator for boolean OR “ OR ”
		public Sep_AND			=new StringColor('white',		null!); //Separator for boolean AND “, ”
		public Strike_Found		=new StringColor('white',		null!); //Straight line through link when item has been found
		public Strike_Started	=new StringColor('silver',		null!); //Wavy line through link when item has been started (and not found)
		public Search_Highlight	=new StringColor('green',		null!); //Highlighting searched string
		public CollectedCounts	=new StringColor('grey',		null!); //Amounts the player has and needs to finish an item
	};
	public readonly LinkColors=new DataStorage.LinkColorsT().Init();

	//noinspection ExceptionCaughtLocallyJS
	private static readonly LoadMisc=class LoadMisc
	{
		private StaticLinks		:Record<string, LoadMisc_StaticLink>=WillBeSet;
		private LinkColors		:Record<string, string>=WillBeSet;
		private ImagePrefix		:Record<string, string>=WillBeSet;
		private OtherLinkPrefix	:Record<string, string>=WillBeSet;

		/*
		Rewrite Item’s ImageURLs/OtherLinks entries based on per-prefix regex rules.

		For each string in ModifyList:
		- If it starts with a rule’s PrefixSymbol (PrefixList.Key), remove the prefix and apply the rule’s regex rewrite.
		- The rewrite specification is PrefixList.Value in the form: <D><SEARCH><D><REPLACE> (e.g., “~SEARCH~REPLACE”) where <D> is a single UTF-16 code unit delimiter.
		- The SEARCH regex has no flags inherently. This means RegexOptions.CultureInvariant IS NOT turned on. Meaning \d matches more than [0-9].
		- The delimiter must appear exactly twice (at the start and between SEARCH and REPLACE) and must not appear inside SEARCH or REPLACE.

		If FinishProcessing is provided, it is run on every final value after all rewrites have been applied.
		*/
		private static RewriteList(
			PrefixList:Record<string, string>,
			ModifyList:Iterable<[I:Item, ItemList:string[]]>,
			FinishProcessing?:((Str:string, ItemID:number, Index:number) => string)
		) {
			//Get the regular expression rewrites
			const Rewrites:[RegExp, string, string][]=[];
			for(const [PrefixSymbol, RegExStr] of Object.entries(PrefixList))
				try {
					if(PrefixSymbol.length===0)
						throw new Error("PrefixSymbol cannot be blank");
					else if(RegExStr.length<4)
						throw new Error("RegEx must have at least 4 characters");
					else if(RegExStr[0].codePointAt(0)!>0xFFFF)
						throw new Error("RegEx split character must fit within a UTF16 code unit");
					const RegExParts=RegExStr.slice(1).split(RegExStr[0]);
					if(RegExParts.length!==2)
						throw new Error(NT+`Must contain first (${RegExStr[0]}) character exactly once more to split SEARCH and REPLACE`);
					else if(RegExParts[0].length===0)
						throw new Error("SEARCH cannot be blank");
					else if(RegExParts[1].length===0)
						throw new Error("REPLACE cannot be blank");
					Rewrites.push([new RegExp(RegExParts[0], 'g'), PrefixSymbol, RegExParts[1]]);
				} catch(e) {
					Log.Error(NT+`Error parsing Rewrite RegEx “${RegExStr}” for “${PrefixSymbol}”: ${Util.GetErrorMessage(e)}`);
				}

			//Rewrite entries
			for(const [I, ItemList] of ModifyList)
				for(let Index=0; Index<ItemList.length; Index++) {
					let FinalVal=ItemList[Index] ?? StatStr.Empty;
					for(const [SearchRegEx, PrefixSymbol, ReplaceWith] of Rewrites)
						if(FinalVal.startsWith(PrefixSymbol))
							FinalVal=FinalVal.slice(PrefixSymbol.length).replace(SearchRegEx, ReplaceWith);
					if(FinishProcessing!==undefined)
						FinalVal=FinishProcessing(FinalVal, I.ID, Index);
					ItemList[Index]=FinalVal;
				}
		}

		public Process(DS:DataStorage, Obj:LoadMisc_Set)
		{
			this.StaticLinks	=(Obj.StaticLinks		as Record<string, LoadMisc_StaticLink>);
			this.LinkColors		=(Obj.LinkColors		as Record<string, string>) ?? {};
			this.ImagePrefix	=(Obj.ImagePrefix		as Record<string, string>) ?? {};
			this.OtherLinkPrefix=(Obj.OtherLinkPrefix	as Record<string, string>) ?? {};

			//StaticLinks
			for(const [K, V] of StaticLink.Process(this.StaticLinks, DS.Items, DS.Categories))
				DS.StaticLinks.set(K, V);

			//LinkColors
			for(const ColorName of DS.LinkColors.ColorNames)
				if(this.LinkColors.hasOwnProperty(ColorName))
					(DS.LinkColors as unknown as Record<string, string>)[ColorName]=this.LinkColors[ColorName];

			//Rewrite from Image and OtherLink prefixes
			LoadMisc.RewriteList(this.ImagePrefix,	 	new Iter(DS.Items.values()).filter(I => !!I.ImageURLs ?.length).map(I => [I, I.ImageURLs !]));
			LoadMisc.RewriteList(this.OtherLinkPrefix,	new Iter(DS.Items.values()).filter(I => !!I.OtherLinks?.length).map(I => [I, I.OtherLinks!]),
				//URL can be followed by an optional link name (URL escape not necessary) prefixed with a pipe “|”. If not given, the URL will be the link name. The Link name will have UrlDecode() ran on it for display.
				(Str, ItemID, Index) => {
					const Parts=Str.split('|', 2);
					const [URL, Name]=(Parts.length===2 ? [Parts[0], Parts[1]] : [Str, Str]);
					return `<LinkID=OL-${ItemID}-${Index}><ATTR=href>${DevStrings.SafeRich(URL)}</ATTR>${DevStrings.SafeRich(decodeURIComponent(Name))}</LinkID>`;
				}
			);
		}
	};

	//Load the category toggle states
	private LoadCategoryToggleStates(FirstRun:boolean)
	{
		try {
			for(const Cat of this.Categories.values())
				Cat.ToggleState=CategoryToggleState.Incomplete;

			//Load the categories from the settings
			for(const [CatToggleState, CatIDs] of Share.LC.CategoryToggleStates.V.Obj.slice(0, CategoryToggleState.Unknown).entries())
				for(const CatID of CatIDs)
					Util.SetNullable(this.Categories.get(CatID)!, 'ToggleState', CatToggleState as CategoryToggleState);

			//Resave in case there were errors or changes
			if(FirstRun)
				this.SaveAndUpdateAllCategoryToggleStates();
		} catch {
			if(FirstRun)
				this.SetCategoriesStatesFor100Percent();
		}
	}

	//Create all the icons
	private LoadIcons()
	{
		for(const Item of this.Items.values())
			Item.MapIcon=new MapIcon(
				Item,
				this.MyIconSprites.Get(Item.IconID!==-1 ? Item.IconID : this.Categories.get(Item.CategoryID)!.IconID)
			);
	}

	//Category state updating functions
	public CycleGroupCategoryState(CG:CategoryGroup)
	{
		let ConfirmState=CG.values().next().value?.ToggleState ?? CategoryToggleState.None;
		for(const Cat of CG.values())
			if(Cat.ToggleState!==ConfirmState) {
				ConfirmState=CategoryToggleState.None;
				break;
			}
		ConfirmState=DataStorage.GetNextToggleState(ConfirmState);
		for(const Cat of CG.values())
			Cat.ToggleState=ConfirmState;
		this.SaveAndUpdateAllCategoryToggleStates();
	}

	public SetAllCategoriesStates(NewState:CategoryToggleState)
	{
		if(NewState===CategoryToggleState.Unknown)
			return;
		for(const Category of this.Categories.values())
			Category.ToggleState=NewState;
		this.SaveAndUpdateAllCategoryToggleStates();
	}

	public SetCategoryState(TheCat:Category, NewState:CategoryToggleState)
	{
		if(NewState===CategoryToggleState.Unknown)
			return;
		TheCat.ToggleState=NewState;
		this.SaveAndUpdateAllCategoryToggleStates();
	}

	public SetCategoriesStatesFor100Percent()
	{
		const RequiredCategories=['Mask Shard', 'Spool Fragment', 'Silk Heart', 'Kit/Pouch Update'];
		for(const Cat of this.Categories.values())
			Cat.ToggleState=RequiredCategories.includes(Cat.Title) ? CategoryToggleState.Incomplete : CategoryToggleState.None;
		this.SaveAndUpdateAllCategoryToggleStates();
	}

	public static GetNextToggleState(TS:CategoryToggleState): CategoryToggleState
	{
		switch(TS) {
			case CategoryToggleState.None:		return CategoryToggleState.All;
			case CategoryToggleState.All:		return CategoryToggleState.Incomplete;
			case CategoryToggleState.Incomplete:return CategoryToggleState.None;
			default:							return CategoryToggleState.All;
		}
	}

	private SaveAndUpdateAllCategoryToggleStates()
	{
		const SaveLists:[number[], number[], number[]]=[[], [], []];
		for(const Cat of this.Categories.values())
			SaveLists[Cat.ToggleState as number].push(Cat.ID);
		Share.LC.CategoryToggleStates.V=new OtherObject(SaveLists);

		for(const Item of this.Items.values())
			Item.CurrentToggleState=this.Categories.get(Item.CategoryID)!.ToggleState;
	}

	public LinkSelected(ID:number|string)
	{
		//Convert from a string to an int
		if(typeof(ID)==='string') {
			ID=Number.parseInt(ID, 10);
			if(!Number.isFinite(ID))
				return;
		}

		let SelectedObj:StaticLink|Item|undefined;
		if(StaticLink.IDInRange(ID))
			if((SelectedObj=this.StaticLinks.get(ID))!==undefined)
				SelectedObj.Selected();
			else
				new PopupMessage("Invalid Static Link ID");
		else if(Item.IDInRange(ID))
			if((SelectedObj=this.Items.get(ID))!==undefined)
				SelectedObj.Selected();
			else
				new PopupMessage("Invalid Item ID");
		else
			new PopupMessage("Invalid ID");
	}
}

//Mimic C++ friend / C# internal
abstract class Category_Friend extends Category implements FriendClass
{
	public override set TotalCount	(_Value:number) { this.Stub(); }
	public override set Sprite		(_Value:Sprite)	{ this.Stub(); }
	//Ignore these
	protected constructor() { super(-1); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}
abstract class ChainItem_Friend extends ChainItem implements FriendClass
{
	public static override Process_NeedsIDAndName() { return super.Process_NeedsIDAndName(); }
	//Ignore these
	protected constructor() { super(null!, null!); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}
abstract class Icon_SpritesFriend extends IconSprites implements FriendClass
{
	public override SetIconPics(_IconPicsTex:ImageBitmap, _ImageURL:string) { this.Stub(); }
	//Ignore these
	protected constructor() { super(); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}