import $ from 'jquery';
import { InitFuncs, Iter, Log, PopupMessage, Rect, StatStr, Util, Vector2, WillBeSet } from './Util/SharedClasses';
import { type AutoFitText, ExecuteAutoFit, CheckFits_Circle } from './Util/AlignText';
import { Share } from './Share';
import { Category, CategoryToggleState, Item } from './CategoriesAndItems';
import { type MouseButtonEvent } from './MapCanvas';
import { MapIcon } from './MapIcon';
import { type default as ItemWindow, type ItemWindow_Item_Callbacks } from './Windows/ItemWindow/ItemWindow';
import type CustomItemWindow from './Windows/CustomItemWindow/CustomItemWindow';

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
		ManualID?:number, //This will be set automatically if not provided
	) {
		if(ManualID!==undefined)
			if(!Item.IDInRange(ManualID=Number(ManualID)))
				throw new Error("CustomItem ID is not in ItemID range: "+ManualID);
			else if(Share.DS.Items.has(ManualID))
				throw new Error("CustomItem ID is already in use: "+ManualID);

		super(ManualID ?? CustomItem.GetID);
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
		SaveCustomItems();
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
		const $Buttons=$('<div class=ItemWindowButtons>').append(
			$('<button class=\'WinButton TranslationEl ButtonMove\'>'																			).on('click', () =>	this.Move		(IW)),
			$('<button class=\'WinButton TranslationEl ButtonEdit\'   data-translation-key="Button.Edit"	data-translation-default="Edit">'	).on('click', () => void(this.Edit	())),
			$('<button class=\'WinButton TranslationEl ButtonDelete\' data-translation-key="Button.Delete"	data-translation-default="Delete">'	).on('click', () =>	this.Delete		()),
		)
			.prependTo(IW.$Content.children().eq(0));
		Share.Tr.UpdateDOMSubElements($Buttons[0]);
		UpdateMoveButton(IW, Share.MCanvas.Events.Click.Has('MoveCustomItem'+this.ID));
	}

	//If the window is not provided, it will attempt to find it
	public Delete()
	{
		if(this.Detached)
			return;
		for(const W of Share.WM.AllWindows)
			if(
				   (W.Type==='Item'			&& (W as ItemWindow			).LinkedItem===this)
				|| (W.Type==='CustomItem'	&& (W as CustomItemWindow	).EditItem	===this)
			)
				W.Close();

		this.MapIcon?.Delete();
		Share.DS.Items.delete(this.ID);
		Share.MCanvas.Refresh();
		if(Share.MC.SelectedItem===this) //Note: No need to unselect if it’s also the hover icon
			Share.MC.SelectItem(undefined);
		SaveCustomItems();
	}

	private async Edit()
	{
		if(this.Detached)
			return;
		for(const W of Share.WM.AllWindows)
			if(W.Type==='CustomItem' && (W as CustomItemWindow).EditItem===this)
				return W.Focus();

		const CustomItemWindow=(await import('./Windows/CustomItemWindow/CustomItemWindow')).default;
		new CustomItemWindow(0, 0, CreateCustomItem, this);
	}

	private Move(IW:ItemWindow)
	{
		const MCanvas=Share.MCanvas;
		if(MCanvas.Canvas.classList.contains('MovingItem'))
			if(MCanvas.Events.Click.Has('MoveCustomItem'+this.ID))
				return this.MoveComplete(IW);
			else
				return new PopupMessage(Share.Tr.TDef("ErrCannotMoveTwice", 'CustomItems', "Cannot move this while another item is being moved"));

		MCanvas.Canvas.classList.add('MovingItem');
		UpdateMoveButton(IW, true);
		MCanvas.Events.Click.Add('MoveCustomItem'+this.ID, Ev => this.MoveEvent(Ev));
	}

	private MoveEvent(Ev:MouseButtonEvent)
	{
		this.MoveComplete(
			new Iter(Share.WM.AllWindows)
				.filter(W => W.Type==='Item' && (W as ItemWindow).LinkedItem===this)
				.take(1).toArray()[0] as ItemWindow|undefined
		);
		if(Ev.Button!==Ev.Buttons.Left && Ev.Button!==Ev.Buttons.Pointer) //Non-primary mouse buttons cancel the action
			return;

		//Update position
		//TODO: This is an unsafe operation, as it directly accesses/updates readonly/private properties
		const NewPos=Share.MCanvas.CanvasToMap(Ev.Pos);
		(this.MapIcon as unknown as {IconGO:{Pos:Vector2}}).IconGO.Pos=NewPos;
		Util.GetMutable(this).x=NewPos.X; Util.GetMutable(this).y=NewPos.Y;
		SaveCustomItems();

		//Update Canvas and select this item
		Share.MCanvas.Refresh();
		if(Share.MC.SelectedItem===this)
			Share.MC.SelectItem(undefined);
		Share.MC.SelectItem(this);
	}

	private MoveComplete(IW?:ItemWindow)
	{
		Share.MCanvas.Canvas.classList.remove('MovingItem');
		Share.MCanvas.Events.Click.Remove('MoveCustomItem'+this.ID);
		if(IW)
			UpdateMoveButton(IW, false);
	}
}

function UpdateMoveButton(IW:ItemWindow, IsMoving:boolean)
{
	const BMove=IW.$Content.find('.ButtonMove')[0];
	BMove.dataset.translationSection='CustomItems';
	BMove.dataset.translationKey	=IsMoving ? "Button.MoveCancel"	: "Button.Move";
	BMove.dataset.translationDefault=IsMoving ? "Cancel Move"		: "Move";
	Share.Tr.UpdateDOMElement(BMove);
}

function SaveCustomItems()
{
	localStorage.setItem('CustomItems', JSON.stringify([...
		new Iter(Share.DS.Items.values() as Iterable<CustomItem>)
			.filter(Item => Item instanceof CustomItem)
			.map(Item => ({ID:Item.ID, X:Item.x, Y:Item.y, Title:Item.Title, Label:Item.MyLabel, Description:Item.MyDescription}))
	]));
}

function LoadCustomItems()
{
	let Items:{ID:number, X:number, Y:number, Title:string, Label:string, Description:string}[];
	try {
		Items=JSON.parse(localStorage.getItem('CustomItems') ?? '[]');
	} catch(e) {
		const Err="Failed to load custom items: "+Util.GetErrorMessage(e);
		new PopupMessage(Err);
		Log.Error(Err);
		return;
	}

	let HasErrors=false;
	for(const Item of Items)
		try {
			new CustomItem(Item.X, Item.Y, Item.Title, Item.Description, Item.Label, false, undefined, Item.ID);
		} catch(e) {
			HasErrors=true;
			Log.Error(StatStr.NeedsTranslate+`Failed to load custom item ${Item.ID}: `+Util.GetErrorMessage(e));
		}
	if(HasErrors)
		new PopupMessage(Share.Tr.TDef("CustomItemsLoadFailed", 'CustomItems', "Some custom items failed to load. See log window for details."));
}
InitFuncs.push(LoadCustomItems);

export const CreateCustomItem=
	(MyLabel:string, Detached:boolean, X?:number, Y?:number, Title?:string, MyDescription?:string) => new CustomItem(
		X ?? 0, Y ?? 0, Title ?? StatStr.Empty, MyDescription ?? StatStr.Empty, //None of these are needed for detached items
		MyLabel, Detached
	);