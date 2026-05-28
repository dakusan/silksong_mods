import { DevStrings, FriendClass, Iter, Log, PopupMessage, Rect, StatStr, type StoreRef, Util, Vector2, WillBeSet } from './Util/SharedClasses';
import { OtherObject } from './Config/Types/ConfigItem_Object';
import GetExtraAssets from './Util/GetExtraAssets';
import { LoadJson } from './Util/JSON';
import { Share } from './Share';
import { MapIcon, Sprite, DefaultSSV } from './MapIcon';
import { Category, CategoryGroup, CategoryToggleState, ChainItem, ChainList, CreateItem, Item, type LoadMisc_StaticLink, StaticLink } from './CategoriesAndItems';

const IconLenX		=10;
const IconLenY		=8;
const IconWidth		=65;
const IconHeight	=65;
const IconPadding	=1;
const NewIconSize	=18;
const NT=StatStr.NeedsTranslate;

//Shapes when loading from JSON
type LoadMisc_Set=Record<string, Record<string, string|LoadMisc_StaticLink>>;
type LoadCategory=Record<string, Record<string, {OrderID:number, IconID:number, Title:string}>>;

//Create icon sprites as needed
class IconSprites
{
	public get NumSpritesAvailable(): number { return IconLenX*IconLenY; }
	private SpriteList:(Sprite|null)[]=Array(this.NumSpritesAvailable).fill(null);
	private CSSSpriteURL =document.createElement('style');
	private CSSSpriteList=document.createElement('style');
	public constructor()
	{
		//Create the special error sprite (which is always the last square and is of size ErrorTexSize*ErrorTexSize)
		const ErrorTexSize=54;
		const LastSpriteID=this.NumSpritesAvailable-1;
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

	public Get(IconID:number): Sprite
	{
		//Instead of dealing with errors, just use an error icon when out of range
		if(IconID<0 || IconID>=this.NumSpritesAvailable)
			IconID=this.NumSpritesAvailable-1;

		//Return if already created
		if(this.SpriteList[IconID]!==null)
			return this.SpriteList[IconID]!;

		//Create the sprite
		return this.SpriteList[IconID]=this.CreateSprite(IconSprites.GetIconRectByID(IconID));
	}

	//Set the sprite’s image
	protected SetIconPics(IconPicsTex:StoreRef<ImageBitmap>, ImageURL:string): void
	{
		MapIcon.UpdateDefaultSpriteSheet(IconPicsTex);
		GetExtraAssets.GetPath(ImageURL).then(NewURL =>
			this.CSSSpriteURL.textContent=`.ItemIcon:before { background-image:url('${NewURL}'); }` //Update URL sprite sheet
		);
	}

	private static GetIconRectByID(IconID:number): Rect
	{
		const X=IconID%IconLenX;
		const Y=Math.floor(IconID/IconLenX);
		return new Rect(
			X*(IconWidth +IconPadding),
			Y*(IconHeight+IconPadding),
			IconWidth, IconHeight
		);
	}

	private CreateSprite(IconRect:StoreRef<Rect>): Sprite { return new Sprite(DefaultSSV, IconRect, new Vector2(0.5, 0.5)); }
}

//noinspection ExceptionCaughtLocallyJS
export default class DataStorage
{
	public readonly CategoryGroups:CategoryGroup[]=[];
	public readonly Categories=new Map<number, Category>();
	public readonly Items=new Map<number, Item>();
	public readonly StaticLinks=new Map<number, StaticLink>();
	public readonly MyIconSprites=new IconSprites();

	protected async Load(CategoriesPath:string, ItemsPath:string, MiscPath:string, IconSetPath:string): Promise<void>
	{
		//Start the async file loads
		const PCategories	=GetExtraAssets.LoadJson (CategoriesPath);
		const PItems		=GetExtraAssets.LoadJson (ItemsPath		);
		const PMisc			=GetExtraAssets.LoadJson (MiscPath		);
		const PIconSet		=GetExtraAssets.LoadImage(IconSetPath	);

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
					const CatIDAsInt=Util.GetInt(CatID);
					if(CatIDAsInt===null)
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
				const NewID=+K;
				if(!Number.isFinite(NewID))
					throw new Error("Invalid ItemID");
				this.Items.set(NewID, CreateItem.Process(NewID, V));
			} catch(e) { Log.Error(NT+`Could not load item ${K}: ${Util.GetErrorMessage(e)}`); }

		for(const [ItemID, ItemData] of this.Items.entries()) {
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
		const LoadIconSet=async (NewIconSet:Promise<StoreRef<ImageBitmap>>, ImageURL:string) => {
			try { (this.MyIconSprites as Icon_SpritesFriend).SetIconPics(await NewIconSet, ImageURL); }
			catch(e) { Log.Error("Could not load icons texture: "+Util.GetErrorMessage(e)); }
		};
		async function UpdateIconSet(ImageURL:string): Promise<void>
		{
			try { await LoadIconSet(GetExtraAssets.LoadImage(ImageURL), ImageURL); }
			catch(e) {
				Log.Error(e);
				throw e;
			}
		}
		await LoadIconSet(PIconSet, IconSetPath);
		Share.LC.IconSet.SettingChanged.Add('DataStorage.UpdateIconSet', UpdateIconSet);

		//Create the sprites
		for(const Category of this.Categories.values())
			(Category as Category_Friend).Sprite=this.MyIconSprites.Get(Category.IconID);

		this.LoadCategoryToggleStates(true);
	}

	//Distribute chain system items
	protected CompleteInit(): void
	{
		//Static helpers
		const GetNonEmptyLists=(...CL:(ChainList|undefined)[]) => CL.filter(CLi => (CLi?.Items?.length ?? 0)>0) as ChainList[];
		const GetListItems=(CL:ChainList) => CL.Items!.flat();
		function AddReqOrNeedToReward(RewardItem:Item, ReqOrNeedList:ChainList, Items:Map<number, Item>): void
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

			//Distribute store reward-related items: Fills in Item.{Unlocks, AQFrom, Reqs, Needs} for items linked from this item’s store
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

	//noinspection ExceptionCaughtLocallyJS
	private static readonly LoadMisc=class LoadMisc
	{
		private StaticLinks		:Record<string, LoadMisc_StaticLink>=WillBeSet;
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
			PrefixList:Readonly<Record<string, string>>,
			ModifyList:Iterable<readonly [I:Item, ItemList:string[]]>,
			FinishProcessing?:((Str:string, ItemID:number, Index:number) => string)
		): void {
			//Get the regular expression rewrites
			const Rewrites:[RegExp, string, string][]=[];
			for(const [PrefixSymbol, RegExStr] of Object.entries(PrefixList))
				try {
					if(PrefixSymbol.length===0)
						throw new Error("PrefixSymbol cannot be blank");
					else if(RegExStr.length<4)
						throw new Error("RegEx must have at least 4 characters");
					else if(RegExStr[0].codePointAt(0)!>0xFF_FF)
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

		public Process(DS:DataStorage, Obj:LoadMisc_Set): void
		{
			this.StaticLinks	=(Obj.StaticLinks		as Record<string, LoadMisc_StaticLink>);
			this.ImagePrefix	=(Obj.ImagePrefix		as Record<string, string>) ?? {};
			this.OtherLinkPrefix=(Obj.OtherLinkPrefix	as Record<string, string>) ?? {};

			//StaticLinks
			for(const [K, V] of StaticLink.Process(this.StaticLinks, DS.Items, DS.Categories))
				DS.StaticLinks.set(K, V);

			//Rewrite from Image and OtherLink prefixes - NOTE: Mutating readonly arrays here, which is only allowed during initialization
			LoadMisc.RewriteList(this.ImagePrefix,	 	new Iter(DS.Items.values()).filter(I => !!I.ImageURLs ?.length).map(I => [I, Util.GetMutable(I.ImageURLs !)] as const));
			LoadMisc.RewriteList(this.OtherLinkPrefix,	new Iter(DS.Items.values()).filter(I => !!I.OtherLinks?.length).map(I => [I, Util.GetMutable(I.OtherLinks!)] as const),
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
	private LoadCategoryToggleStates(FirstRun:boolean): void
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
	private LoadIcons(): void
	{
		for(const Item of this.Items.values())
			Item.MapIcon=new MapIcon(
				Item,
				this.MyIconSprites.Get(Item.IconID!==-1 ? Item.IconID : this.Categories.get(Item.CategoryID)!.IconID)
			);
	}

	//Category state updating functions
	public CycleGroupCategoryState(CG:Readonly<CategoryGroup>): void
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

	public SetAllCategoriesStates(NewState:CategoryToggleState): void
	{
		if(NewState===CategoryToggleState.Unknown)
			return;
		for(const Category of this.Categories.values())
			Category.ToggleState=NewState;
		this.SaveAndUpdateAllCategoryToggleStates();
	}

	public SetCategoryState(TheCat:Category, NewState:CategoryToggleState): void
	{
		if(NewState===CategoryToggleState.Unknown)
			return;
		TheCat.ToggleState=NewState;
		this.SaveAndUpdateAllCategoryToggleStates();
	}

	public SetCategoriesStatesFor100Percent(): void
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

	private SaveAndUpdateAllCategoryToggleStates(): void
	{
		const SaveLists:[number[], number[], number[]]=[[], [], []];
		for(const Cat of this.Categories.values())
			SaveLists[Cat.ToggleState as number].push(Cat.ID);
		Share.LC.CategoryToggleStates.V=new OtherObject(SaveLists);

		for(const Item of this.Items.values())
			Item.CurrentToggleState=this.Categories.get(Item.CategoryID)!.ToggleState;
	}

	public LinkSelected(ID:number|string): void
	{
		//Convert from a string to an int
		if(typeof(ID)==='string') {
			ID=Util.GetInt(ID)!;
			if(ID===null)
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
	public override set TotalCount	(_Value:number)				{ this.Stub(); }
	public override set Sprite		(_Value:StoreRef<Sprite>)	{ this.Stub(); }
	//Ignore these
	protected constructor() { super(-1); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}
abstract class ChainItem_Friend extends ChainItem implements FriendClass
{
	public static override Process_NeedsIDAndName(): void { return super.Process_NeedsIDAndName(); }
	//Ignore these
	protected constructor() { super(null!, null!); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}
abstract class Icon_SpritesFriend extends IconSprites implements FriendClass
{
	public override SetIconPics(_IconPicsTex:StoreRef<ImageBitmap>, _ImageURL:string): void { this.Stub(); }
	//Ignore these
	protected constructor() { super(); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}