import { Log, Rect, Util, Vector2 } from "./SharedClasses"
import { Category, CategoryGroup, CreateItem, Item } from "./CategoriesAndItems"
import { LoadJson } from "./LoadJSON"
import { MapIcon, Sprite } from "./MapIcon"

const IconLenX		=10;
const IconLenY		=8;
const IconWidth		=65;
const IconHeight	=65;
const IconPadding	=1;

//Shapes when loading from JSON
type LoadCategory=Record<string, Record<string, {OrderID:number, IconID:number, Title:string}>>;

//Create icon sprites as needed
class IconSprites
{
	private SpriteList:(Sprite|null)[]=Array(IconLenX*IconLenY).fill(null);
	public constructor()
	{
		//Create the special error sprite (which is always the last square and is of size ErrorTexSize*ErrorTexSize)
		const ErrorTexSize=54;
		const LastSpriteID=IconLenX*IconLenY-1;
		this.SpriteList[LastSpriteID]=this.CreateSprite(IconSprites.GetIconRectByID(LastSpriteID).SetWidth(ErrorTexSize).SetHeight(ErrorTexSize));
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
	protected SetIconPics(IconPicsTex:ImageBitmap)
	{
		this.IconPicsTex=IconPicsTex;
		for(const MySprite of this.SpriteList.values())
			if(MySprite!==null)
				MySprite.Image=IconPicsTex;
	}

	private static GetIconRectByID(IconID:number)
	{
		const x=IconID%IconLenX;
		const y=Math.floor(IconID/IconLenX);
		return new Rect(
			x*(IconWidth +IconPadding),
			y*(IconHeight+IconPadding),
			IconWidth, IconHeight
		);
	}

	private CreateSprite(IconRect:Rect) { return new Sprite(this.IconPicsTex, IconRect, new Vector2(0.5, 0.5)); }
}

//noinspection ExceptionCaughtLocallyJS
export class DataStorage
{
	public readonly CategoryGroups:CategoryGroup[]=[];
	public readonly Categories=new Map<number, Category>();
	public readonly Items=new Map<number, Item>();
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
				} catch(e) { Log.Error(`Could not load Category ${CatID}: ${Util.GetErrorMessage(e)}`); }
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
			} catch(e) { Log.Error(`Could not load item ${K}: ${Util.GetErrorMessage(e)}`); }

		for(const [ItemID, ItemData] of this.Items.entries()) {
			if(this.Categories.has(ItemData.CategoryID))
				continue;

			Log.Error(`Invalid CategoryID[#${ItemData.CategoryID}] on Item[#${ItemID}]`);
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
		} catch(e) {
			throw new Error("Could not load misc/static links, failing out: "+Util.GetErrorMessage(e));
		}

		//Create and update the sprite texture
		const LoadIconSet=async (NewIconSet:Promise<ImageBitmap>) => {
			try { (this.MyIconSprites as Icon_SpritesFriend).SetIconPics(await NewIconSet); }
			catch(e) { Log.Error("Could not load icons texture: "+Util.GetErrorMessage(e)); }
		};
		await LoadIconSet(PIconSet);

		//Create the sprites
		for(const Category of this.Categories.values())
			(Category as Category_Friend).Sprite=this.MyIconSprites.Get(Category.IconID);
	}

	//Distribute chain system items
	protected CompleteInit()
	{
		this.LoadIcons();
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

}

//Mimic C++ friend / C# internal
class Category_Friend extends Category
{
	public override set TotalCount	(_Value:number) { }
	public override set Sprite		(_Value:Sprite)	{ }
}
class Icon_SpritesFriend extends IconSprites
{
	public override SetIconPics(_IconPicsTex:ImageBitmap) { }
}