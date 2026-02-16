import { Item } from "./CategoriesAndItems"
import { Iter, KeyState, Rect, Util, Vector2 } from "./SharedClasses"
import { LC } from "./AtlasConfig"
import { Share } from "./main"

//All functions accept/return canvas pixel coordinates
export default class MapControl
{
	//Public members
	public get GameMap() { return Share.MCanvas; }
	private _HoverItem		?:Item=undefined; public get HoverItem	 () { return this._HoverItem	; } private set HoverItem	(Val) { this._HoverItem		=Val; }
	private _SelectedItem	?:Item=undefined; public get SelectedItem() { return this._SelectedItem	; } private set SelectedItem(Val) { this._SelectedItem	=Val; }

	//Private members
	private readonly MoveBackStack		:Item[]=[];
	private readonly MoveForwardStack	:Item[]=[];

	//Zoom states and variables
	private IconSizeScalesWithZoom=LC.IconSizeScalesWithZoom.V;
	private _ZoomScale=this.GameMap.ZoomScale; public get ZoomScale(): number { return this._ZoomScale; }
	private set ZoomScale(Value:number)
	{
		this._ZoomScale=Value;
		if(!this.IconSizeScalesWithZoom)
			this.SetIconSize(LC.IconSize.V);
	}

	constructor()
	{
		this.GameMap.Events.Scale		.Add("MapControl.ZoomScale",	NewScale => this.ZoomScale=NewScale);
		this.GameMap.Events.UserZoom	.Add("MapControl.UserZoom",		this.UserZoom.bind(this));
		this.GameMap.Events.MouseMove	.Add("MapControl.OnMouseMove",	this.OnMouseMove.bind(this));
		this.GameMap.Events.Click		.Add("MapControl.OnClick",		this.OnClick.bind(this));
		this.GameMap.Events.Frame		.Add("MapControl.OnFrame",		this.OnFrame.bind(this));

		//Handle settings changes
		LC.IconSize.SettingChanged.Add("MapControl.SetIconSize", this.SetIconSize.bind(this));
		LC.IconSizeScalesWithZoom.SettingChanged.Add("MapControl.SetIconSize", NewVal => {
			this.IconSizeScalesWithZoom=NewVal;
			this.SetIconSize(LC.IconSize.V);
		});
		this.SetIconSize(LC.IconSize.V);
	}

	//Find the closest visible and intersecting item on the map
	public FindClosestItem(PosOnMap:Vector2, SelectionSize:number=1): Item|undefined
	{
		const ClosestItem=this.FindClosestVector(new Iter(Share.DS.Items.values()).filter(I => I.Visible), PosOnMap);
		return ClosestItem?.MapIcon?.RenderRect?.Intersects(new Rect(PosOnMap.x, PosOnMap.y, SelectionSize, SelectionSize)) ? ClosestItem : undefined;
	}

	//Find the closest item to the given map position
	public FindClosestVector(VList:Iterable<Item>, PosOnMap:Vector2): Item|undefined
	{
		//Find the closest item
		PosOnMap=this.GameMap.CanvasToMap(PosOnMap);
		let ClosestItem:Item|undefined=undefined;
		let ClosestVecDistance=9999;
		for(const TestItem of VList) {
			const CurDistance=PosOnMap.Distance(TestItem.Pos);
			if(CurDistance>=ClosestVecDistance)
				continue;
			ClosestItem=TestItem;
			ClosestVecDistance=CurDistance;
		}
		return ClosestItem;
	}

	//Set the hovered item
	private SetHoverItem(ClosestItem?:Item)
	{
		if(ClosestItem===this.HoverItem)
			return;

		//Deselect the previous hover item and select the new one
		Util.SetNullable(this.HoverItem	?.MapIcon, "IsHovered", false);
		Util.SetNullable(ClosestItem	?.MapIcon, "IsHovered", true );
		this.HoverItem=ClosestItem;
	}

	//Handle key presses
	private static ZoomKeysAmount=new Map<string, number>([
		["Equal"			,  1],
		["Minus"			, -1],
		["NumpadAdd"		,  1],
		["NumpadSubtract"	, -1],
	]);
	private static ArrowKeyDirections=new Map<string, [number, number]>([
		["ArrowLeft",	[ 1, 0]],
		["ArrowRight",	[-1, 0]],
		["ArrowUp",		[ 0, 1]],
		["ArrowDown",	[ 0,-1]]
	]);
	private StackMoveKeyState=false;
	public OnFrame()
	{
		//Handle zooming
		for(const [KeyName, Direction] of MapControl.ZoomKeysAmount.entries())
			if(KeyState.GetKeyDown(KeyName))
				this.Zoom(Direction);

		//Panning
		for(const [KeyName, Directions] of MapControl.ArrowKeyDirections.entries())
			if(KeyState.GetKeyDown(KeyName)) {
				const Rate=LC.PanSpeed.V/this.GameMap.FPS; //Ticks per second
				this.GameMap.PanAt(Directions[0]*Rate, Directions[1]*Rate);
			}

		//Forward/Backward in item stack
		const IsStackKeyDown=(KeyState.GetKeyDown("PageUp") || KeyState.GetKeyDown("PageDown"));
		if(IsStackKeyDown!==this.StackMoveKeyState) {
			this.StackMoveKeyState=IsStackKeyDown;
			if(IsStackKeyDown)
				this.MoveInItemStack(KeyState.GetKeyDown("PageUp"));
		}
	}

	public MoveInItemStack(StackDirectionForward: boolean)
	{
		//Item selection stack
		if(StackDirectionForward && this.MoveForwardStack.length>0) {
			const NewItem=this.MoveForwardStack.pop()!;
			this.MoveBackStack.push(NewItem);
			this.SelectAndCenterItemI(NewItem.ID, true);
		} else if(
			   !StackDirectionForward
			&& (
				   this.MoveBackStack.length>1
				|| (this.MoveBackStack.length===1 && this.SelectedItem==null)
			)
		) {
			if(this.SelectedItem!=null)
				this.MoveForwardStack.push(this.MoveBackStack.pop()!);
			this.SelectAndCenterItemI(this.MoveBackStack[this.MoveBackStack.length-1]!.ID, true);
		}
	}

	private UserZoom(Pos:Vector2, ScaleObj:{Scale:number})
	{
		if(this.HoverItem && this.IconSizeScalesWithZoom) {
			const NewPos=this.GameMap.MapToCanvas(this.HoverItem.Pos);
			Pos.x=NewPos.x; Pos.y=NewPos.y;
		}
		ScaleObj.Scale=this.GetUpdatedZoomScale(ScaleObj.Scale<1 ? -2 : 2);
	}

	private GetUpdatedZoomScale(Amount:number)
	{
		const ZoomChange=Math.pow(LC.ZoomSpeed.V, Math.abs(Amount));
		return Amount<0 ? 1/ZoomChange : ZoomChange;
	}

	//Zoom in or out
	public Zoom(Amount:number) { this.ZoomTowardsPoint(Amount, new Vector2(this.GameMap.Width/2, this.GameMap.Height/2)); }

	//Zoom towards a given point
	public ZoomTowardsPoint(Amount:number, ZoomAroundPoint:Vector2, UseHoveredItem:boolean=false)
	{
		//Set the hover item as the center if toggled
		if(UseHoveredItem && this.IconSizeScalesWithZoom && this.HoverItem)
			ZoomAroundPoint=this.GameMap.MapToCanvas(this.HoverItem.Pos);
		this.GameMap.ZoomAt(ZoomAroundPoint, this.GetUpdatedZoomScale(Amount))
	}

	//Set the size of icons
	public SetIconSize(IconSize:number)
	{
		const MatchUnityScale=2/3;
		const NewIconScaleSize=(!this.IconSizeScalesWithZoom ? IconSize*this.ZoomScale : IconSize)*MatchUnityScale;
		for(const Item of Share.DS.Items.values())
			Item.MapIcon?.UpdateSize(NewIconScaleSize);
	}

	//Selects a new item
	public SelectItem(NewSelectItem:Item|undefined) { this.SelectItemI(NewSelectItem); }
	private SelectItemI(NewSelectItem:Item|undefined, IsStackMove:boolean=false)
	{
		//If the same item, nothing to do
		if(this.SelectedItem===NewSelectItem)
			return;

		//Force the icon to be invisible if it’s not supposed to be visible (if previously selected through the search window)
		if(!(this.SelectedItem?.Visible ?? false) && (this.SelectedItem?.MapIcon?.IsIconVisible ?? false))
			this.SelectedItem!.MapIcon!.ForceVisibility=false;

		Util.SetNullable(this.SelectedItem?.MapIcon, "IsSelected", false);
		Util.SetNullable(NewSelectItem?.MapIcon, "IsSelected", true);
		NewSelectItem?.MapIcon?.BringToFront();
		this.SelectedItem=NewSelectItem;

		//Add to stack
		if(!IsStackMove && NewSelectItem!==undefined) {
			this.MoveBackStack.push(NewSelectItem);
			this.MoveForwardStack.length=0;
		}
	}

	//Center over and select an item by its ID
	public   SelectAndCenterItem(ItemID:number) { return this.SelectAndCenterItemI(ItemID); }
	protected SelectAndCenterItemI(ItemID:number, IsStackMove:boolean=false)
	{
		const I=Util.ThrowOnNull(Share.DS.Items.get(ItemID), "Invalid ItemID");
		this.GameMap.CenterOnPoint(this.GameMap.MapToCanvas(I.Pos));
		this.SelectItemI(I, IsStackMove);
		Util.SetNullable(I.MapIcon, "ForceVisibility", true); //Force the icon to be visible
	}

	//Handle mouse events
	protected OnMouseMove(Pos:Vector2) { this.SetHoverItem(this.FindClosestItem(Pos)); }
	protected OnClick(Pos:Vector2)
	{
		this.SetHoverItem(this.FindClosestItem(Pos));
		this.SelectItemI(this.HoverItem);
	}

	//Toggle showing if icons have been found yet
	private _ShowLinkedStatus:boolean=false; public get ShowLinkedStatus():boolean { return this._ShowLinkedStatus; }
	public set ShowLinkedStatus(value:boolean)
	{
		if(this._ShowLinkedStatus===value)
			return;
		this._ShowLinkedStatus=value;
		for(const Item of Share.DS.Items.values())
			if(!Item.IsLinked)
				Item.MapIcon!.SetIconColor();
	}
}