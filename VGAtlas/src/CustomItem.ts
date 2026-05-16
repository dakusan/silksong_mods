import $ from 'jquery';
import { Rect, StatStr, Util, WillBeSet } from './Util/SharedClasses';
import { type AutoFitText, ExecuteAutoFit, CheckFits_Circle } from './Util/AlignText';
import { Share } from './Share';
import { Category, CategoryToggleState, Item } from './CategoriesAndItems';
import { MapIcon } from './MapIcon';
import ItemWindow, { type ItemWindow_Item_Callbacks } from './DockableWindows/ItemWindow';

const CustomCategoryID=-20934;

/*SYSTEM INTERFACE NOTES:
The second-to-last icon in the spritesheets is used for custom icons.
The last category group is used for the Custom Icons category.
*/

export default class CustomItem extends Item implements ItemWindow_Item_Callbacks
{
	private static readonly MyCategory:Category=WillBeSet;
	private static InitCategory=() => Object.assign<Category, Partial<Category>>(
		new Category(CustomCategoryID),
		{
			IconID:Share.DS.MyIconSprites.NumSpritesAvailable-2,
			Title:"Custom Icons",
			Sprite:Share.DS.MyIconSprites.Get(Share.DS.MyIconSprites.NumSpritesAvailable-2),
			ToggleState:CategoryToggleState.All,
		},
	);
	private static StaticInit()
	{
		(this as unknown as {MyCategory:Category}).MyCategory=this.InitCategory();
		Share.DS.Categories.set(CustomCategoryID, this.MyCategory);
		const CatGroup=Share.DS.CategoryGroups[Share.DS.CategoryGroups.length-1];
		Util.GetMutable(this.MyCategory).Order=CatGroup.size;
		CatGroup.set(CustomCategoryID, this.MyCategory);
	}

	private static LastID=Item.MinID-1;
	private static get GetID()
	{
		while(Share.DS.Items.has(++this.LastID)) { }
		if(this.LastID>Item.MaxID)
			throw new Error(StatStr.NeedsTranslate+`Ran out of IDs for CustomItem! ${this.LastID}>${Item.MaxID}`);
		return this.LastID;
	}

	public readonly AFT:AutoFitText;
	private get SpriteSize() { return this.MySprite.ImageRect.Width; }
	public get MySprite() { return CustomItem.MyCategory.Sprite; }
	public constructor(
		X:number, Y:number, Title:string,
		public readonly MyDescription:string, //May have LinkedLabel suitable HTML
		public readonly MyLabel:string, //Displays on the icon
		public readonly Detached=false,
		public readonly FontFamily='sans-serif',
	) {
		super(CustomItem.GetID);
		if(!CustomItem.MyCategory)
			CustomItem.StaticInit();

		const Me=Util.GetMutable(this);
		[Me.CategoryID, Me.x, Me.y, Me.Title]=[CustomCategoryID, X, Y, Title];
		Me.CurrentToggleState=CustomItem.MyCategory.ToggleState;

		this.AFT=ExecuteAutoFit(
			MyLabel, this.SpriteSize, this.SpriteSize, {
				FontFamily, MinFont:3, MaxFont:50,
				CheckIfFits:CheckFits_Circle,
			},
		);

		if(Detached)
			return;
		Share.DS.Items.set(this.ID, this);
		Me.MapIcon=new MapIcon(this, this.MySprite, this.DrawSymbol.bind(this));
		Share.MC.SetIconSize(Share.LC.IconSize.V, this);
	}
	public override toString() { return this.MyDescription; }

	public DrawSymbol(Ctx:CanvasRenderingContext2D, CanvasRect:Rect)
	{
		//Only update context states if necessary
		const ScaleSize=(CanvasRect.Width/this.SpriteSize);
		Util.AssignProps(Ctx, {
			font:`${Math.round(this.AFT.Size*ScaleSize*1000)/1000}px ${this.FontFamily}`,
			//eslint-disable-next-line @typescript-eslint/naming-convention
			textAlign:'left', textBaseline:'middle', fillStyle:'#000000',
		}, false);

		for(const Line of this.AFT.LineRects)
			Ctx.fillText(
				Line.Text,
				CanvasRect.X+ScaleSize* Line.Rect.X,
				CanvasRect.Y+ScaleSize*(Line.Rect.Y+Line.Rect.Height/2) //for textBaseline='middle'
			);
	}

	public WindowCB_ContentsUpdated(IW:ItemWindow): void
	{
		const $DeleteButton=$('<button class=\'WinButton TranslationEl ItemWindowDeleteButton\' data-translation-key="Button.Delete" data-translation-default="Delete">')
			.on('click', () => this.Delete(IW))
			.prependTo(IW.$Content.children().eq(0));
		Share.Tr.UpdateDOMElement($DeleteButton[0]);
	}

	//If the window is not provided, it will attempt to find it
	public Delete(WindowToDelete?:ItemWindow)
	{
		if(this.Detached)
			return;
		if(WindowToDelete)
			WindowToDelete.Close();
		else
			for(const W of Share.WM.AllWindows)
				if(W instanceof ItemWindow && (W as ItemWindow).LinkedItem===this)
					W.Close();

		this.MapIcon?.Delete();
		Share.DS.Items.delete(this.ID);
		Share.MCanvas.Refresh();
	}
}