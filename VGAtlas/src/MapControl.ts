import $ from 'jquery';
import { InitFuncs, Iter, KeyState, Log, PopupMessage, Rect, StatStr, Util, Vector2 } from './Util/SharedClasses';
import { Share } from './Share';
import { ProcessActions } from './Actions';
import { type Item } from './CategoriesAndItems';
import { type MouseButtonEvent } from './MapCanvas';
import ItemWindow from './Windows/ItemWindow/ItemWindow';

//All functions accept/return canvas pixel coordinates
export default class MapControl
{
	//Public members
	public get GameMap(): typeof Share.MCanvas { return Share.MCanvas; }
	private _HoverItem		?:Item=undefined; public get HoverItem	 (): Item|undefined { return this._HoverItem	; } private set HoverItem	(Val) { this._HoverItem		=Val; }
	private _SelectedItem	?:Item=null!;	  public get SelectedItem(): Item|undefined { return this._SelectedItem	; } private set SelectedItem(Val) { this._SelectedItem	=Val; }
	private CurrentItemWindow?:ItemWindow;

	//Private members
	private readonly ItemTooltip=$('<div id=ItemTooltip />').appendTo(document.body);

	//Zoom states and variables
	private IconSizeScalesWithZoom=Share.LC.IconSizeScalesWithZoom.V;
	private _ZoomScale=this.GameMap.ZoomScale; public get ZoomScale(): number { return this._ZoomScale; }
	private set ZoomScale(Value:number)
	{
		this._ZoomScale=Value;
		if(!this.IconSizeScalesWithZoom)
			this.SetIconSize(Share.LC.IconSize.V);
	}

	constructor()
	{
		this.GameMap.Events.Scale		.Add('MapControl.ZoomScale',	NewScale => this.ZoomScale=NewScale);
		this.GameMap.Events.UserZoom	.Add('MapControl.UserZoom',		this.UserZoom	.bind(this));
		this.GameMap.Events.MouseMove	.Add('MapControl.OnMouseMove',	this.OnMouseMove.bind(this));
		this.GameMap.Events.Click		.Add('MapControl.OnClick',		this.OnClick	.bind(this));
		this.GameMap.Events.MouseDown	.Add('MapControl.MouseDown',	this.OnMouseDown.bind(this));
		this.GameMap.Events.Frame		.Add('MapControl.OnFrame',		this.OnFrame	.bind(this));
		this.GameMap.Events.MouseLeave	.Add('MapControl.MouseLeave',	() => this.SetHoverItem(undefined));
		this.GameMap.Events.Moved		.Add('MapControl.Move',			this.OnMove		.bind(this));
		Share.MSV.UpdateAllUsedValuesOnLoad();

		//Handle settings changes
		Share.LC.IconSize.SettingChanged.Add('MapControl.SetIconSize', Size => this.SetIconSize(Size));
		Share.LC.IconSizeScalesWithZoom.SettingChanged.Add('MapControl.SetIconSize', NewVal => {
			this.IconSizeScalesWithZoom=NewVal;
			this.SetIconSize(Share.LC.IconSize.V);
		});
		this.SetIconSize(Share.LC.IconSize.V);

		InitFuncs.push(() => this.InitURLHashes()); //Move to the end of the init chain so that hash commands from page load can be run last
	}

	//Handle the state change for choosing icons
	private CurrentHistoryIndex=0;
	private MaxHistoryIndex=0;
	private IgnoreNextHashUpdate=false;
	private InitURLHashes(): void
	{
		history.replaceState({Index:0}, StatStr.Empty, location.pathname+location.search+location.hash);
		this.HashUpdate(true);
		window.addEventListener('hashchange', () => this.HashUpdate(false));
		window.addEventListener('popstate', e => {
			const State=e.state as {Index:number} ?? null;
			if(State)
				this.CurrentHistoryIndex=State.Index;
		});
		const ExecStackMove=(MoveAllowed:boolean, MoveFunc:() => void) =>
		{
			const IsSuccess=!Share.WM.ControlsKeyboard && MoveAllowed;
			if(IsSuccess)
				MoveFunc();
			return IsSuccess;
		};
		Share.LC.Shortcut_SelStack_Prev.OnKeypress.Add('Shortcut_SelStack_Prev', () => ExecStackMove(this.CurrentHistoryIndex>0,					() => history.back()	));
		Share.LC.Shortcut_SelStack_Next.OnKeypress.Add('Shortcut_SelStack_Next', () => ExecStackMove(this.CurrentHistoryIndex<this.MaxHistoryIndex,	() => history.forward()	));
	}
	private HashUpdate(IsInitial:boolean): void
	{
		if(this.IgnoreNextHashUpdate)
			return void(this.IgnoreNextHashUpdate=false);

		//TODO: Support categories
		let NewHash=(window.location.hash ?? StatStr.Empty);
		if(NewHash[0]==='#')
			NewHash=NewHash.slice(1);
		if(NewHash==='REMOVED')
			return this.RemoveHistoryEvent();
		if(NewHash.length===0) {
			this.SelectItemI(undefined, true);
			if(!IsInitial)
				Log.Debug('Stack Update: Empty');
			return;
		}

		//Process options
		const Options:{ZoomScale?:number, Duration?:number, X?:number, Y?:number}={};
		const SplitPos=NewHash.indexOf('&');
		if(SplitPos!==-1) {
			const Values=new URLSearchParams(NewHash.slice(SplitPos+1));
			NewHash=NewHash.slice(0, SplitPos); //The ItemID is now what’s before the first comma in the hash
			for(const Name of ['ZoomScale', 'Duration', 'X', 'Y'] as const) {
				const Value=Util.GetNumber(Values.get(Name));
				if(Value!==null)
					Options[Name]=Value;
			}
			if(!ProcessActions(Values.entries()))
				new PopupMessage("Errors in hash commands. See log for details.");
		}

		//Select the new item
		if(NewHash)
			try {
				return this.SelectNewItemFromHash(IsInitial, NewHash, SplitPos!==-1, Options.ZoomScale, Options.Duration);
			} catch {
				Log.Error("Invalid ItemID in URL: "+NewHash);
				return this.RemoveHistoryEvent();
			}

		//If there is no new item given, instead, try to move to X, Y and/or scale to ZoomScale
		if(Options.ZoomScale!==undefined || (Options.X!==undefined && Options.Y!==undefined))
			this.GameMap.CenterOnPoint(
				Options.X!==undefined && Options.Y!==undefined
					? this.GameMap.MapToCanvas(new Vector2(Options.X, Options.Y))
					: new Vector2(this.GameMap.Width/2, this.GameMap.Height/2),
				Options.Duration,
				Options.ZoomScale,
			);
		this.RemoveHistoryEvent();
	}
	private SelectNewItemFromHash(IsInitial:boolean, NewItemID:string, HasCommands:boolean, ZoomScale?:number, Duration?:number): void
	{
		const PreviousSelectedItemID=this.SelectedItem?.ID;
		if(IsInitial) {
			const ScaleRange=Share.MCanvas.ScaleRange;
			ZoomScale ??= (ScaleRange.Y-ScaleRange.X)/2+ScaleRange.X;
			Duration ??= 1.75;
		}

		const ItemID=Util.GetInt(NewItemID);
		if(ItemID===null)
			return this.RemoveHistoryEvent();

		this.SelectAndCenterItemI(ItemID, true, ZoomScale, Duration);
		Log.Debug(`Stack ${IsInitial ? 'Initial' : 'Update'}: #${NewItemID}`);
		if(!HasCommands)
			return;
		if(ItemID===PreviousSelectedItemID)
			this.RemoveHistoryEvent();
		else
			ReplaceCurrentHistoryHash('#'+ItemID);
	}
	private RemoveHistoryEvent(): void
	{
		ReplaceCurrentHistoryHash('#REMOVED');
		this.MaxHistoryIndex=this.CurrentHistoryIndex;
		if(history.state?.Index===0)
			return ReplaceCurrentHistoryHash(StatStr.Empty);
		this.IgnoreNextHashUpdate=true;
		history.back();
	}

	//Find the closest visible and intersecting item on the map
	public FindClosestItem(PosOnMap:Vector2, SelectionSize:number=1): Item|undefined
	{
		const ClosestItem=this.FindClosestVector(new Iter(Share.DS.Items.values()).filter(I => I.Visible), PosOnMap);
		return ClosestItem?.MapIcon?.RenderRect?.Intersects(new Rect(PosOnMap.X, PosOnMap.Y, SelectionSize, SelectionSize)) ? ClosestItem : undefined;
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
	private SetHoverItem(ClosestItem?:Item): void
	{
		if(ClosestItem===this.HoverItem || Util.IsMobile())
			return;

		//Deselect the previous hover item and select the new one
		Util.SetNullable(this.HoverItem	?.MapIcon, 'IsHovered', false);
		Util.SetNullable(ClosestItem	?.MapIcon, 'IsHovered', true );
		this.HoverItem=ClosestItem;
		this.ItemTooltip.toggleClass('Active', ClosestItem!==undefined);
		if(ClosestItem)
			this.ItemTooltip
				.empty().append($('<div/>').append(
					$('<span class=\'ItemIcon I'+(ClosestItem.IconID!==-1 ? ClosestItem.IconID : Share.DS.Categories.get(ClosestItem.CategoryID)!.IconID)+'\'></span>'),
					$('<span/>').text(`${ClosestItem?.Title} [${ClosestItem?.ID}]`),
				));
	}

	//Handle key presses
	private static ArrowKeyDirections=new Map<string, [number, number]>([
		['ArrowLeft',	[ 1, 0]],
		['ArrowRight',	[-1, 0]],
		['ArrowUp',		[ 0, 1]],
		['ArrowDown',	[ 0,-1]]
	]);
	public OnFrame(): void
	{
		if(Share.WM.ControlsKeyboard || Util.IsMobile())
			return;

		//Handle zooming
		const ZoomAmount=
			 (Share.LC.Shortcut_ZoomIn .IsActive() ?  1 : 0)
			+(Share.LC.Shortcut_ZoomOut.IsActive() ? -1 : 0);
		if(ZoomAmount!==0)
			this.Zoom(ZoomAmount);

		//Panning
		if(!KeyState.GetKeyDown('AltLeft') && !KeyState.GetKeyDown('AltRight'))
			for(const [KeyName, Directions] of MapControl.ArrowKeyDirections.entries())
				if(KeyState.GetKeyDown(KeyName)) {
					const Rate=Share.LC.PanSpeed.V/this.GameMap.FPS; //Ticks per second
					this.GameMap.PanAt(Directions[0]*Rate, Directions[1]*Rate);
				}
	}

	private UserZoom(Pos:Util.Mutable<Vector2>, ScaleObj:{Scale:number}): void
	{
		if(this.HoverItem && this.IconSizeScalesWithZoom) {
			const NewPos=this.GameMap.MapToCanvas(this.HoverItem.Pos);
			Pos.X=NewPos.X; Pos.Y=NewPos.Y;
		}
		ScaleObj.Scale=this.GetUpdatedZoomScale(ScaleObj.Scale<1 ? -2 : 2);
	}

	private GetUpdatedZoomScale(Amount:number): number
	{
		const ZoomChange=Math.pow(Share.LC.ZoomSpeed.V, Math.abs(Amount));
		return Amount<0 ? 1/ZoomChange : ZoomChange;
	}

	//Zoom in or out
	public Zoom(Amount:number): void { this.ZoomTowardsPoint(Amount, new Vector2(this.GameMap.Width/2, this.GameMap.Height/2)); }

	//Zoom towards a given point
	public ZoomTowardsPoint(Amount:number, ZoomAroundPoint:Vector2, UseHoveredItem:boolean=false): void
	{
		//Set the hover item as the center if toggled
		if(UseHoveredItem && this.IconSizeScalesWithZoom && this.HoverItem)
			ZoomAroundPoint=this.GameMap.MapToCanvas(this.HoverItem.Pos);
		this.GameMap.ZoomAt(ZoomAroundPoint, this.GetUpdatedZoomScale(Amount))
	}

	//Set the size of icon(s) - If IconToScale is not given, all icons are updated
	public SetIconSize(IconSize:number, IconToSize?:Item): void
	{
		const MatchUnityScale=2/3;
		const NewIconScaleSize=(!this.IconSizeScalesWithZoom ? IconSize*this.ZoomScale : IconSize)*MatchUnityScale;
		for(const Item of (IconToSize ? [IconToSize] : Share.DS.Items.values()))
			Item.MapIcon!.UpdateSize(NewIconScaleSize);
	}

	//Selects a new item
	public  SelectItem (NewSelectItem:Item|undefined): void { this.SelectItemI(NewSelectItem); }
	private SelectItemI(NewSelectItem:Item|undefined, IsStackMove:boolean=false): void
	{
		//If the same item, nothing to do
		if(this.SelectedItem===NewSelectItem)
			return;

		//Force the icon to be invisible if it’s not supposed to be visible (if previously selected through the search window)
		if(!(this.SelectedItem?.Visible ?? false) && (this.SelectedItem?.MapIcon?.IsIconVisible ?? false))
			this.SelectedItem!.MapIcon!.ForceVisibility=false;

		//Update browser state
		if(!IsStackMove) {
			const IsReplace=!this.CurrentItemWindow;
			Log.Debug(`Stack ${IsReplace ? 'Replace' : 'Add'}: ${NewSelectItem ? '#'+NewSelectItem.ID : 'Empty'}`);
			history[IsReplace ? 'replaceState' : 'pushState'](
				{Index:IsReplace ? this.CurrentHistoryIndex : this.MaxHistoryIndex=++this.CurrentHistoryIndex}, StatStr.Empty,
				location.pathname+location.search+(NewSelectItem ? '#'+NewSelectItem.ID : StatStr.Empty)
			);
		}
		document.title="VGAtlas - SilkSong"+(NewSelectItem ? ` - ${NewSelectItem.Title} [#${NewSelectItem.ID}]` : StatStr.Empty);

		//Handle updating the popup item window
		this.CurrentItemWindow?.ItemUnselected();
		this.CurrentItemWindow=undefined;
		if(NewSelectItem) {
			for(const W of Share.WM.AllWindows) {
				const IW=(W as unknown as ItemWindow);
				if(IW.LinkedItem?.ID!==NewSelectItem.ID)
					continue;
				(this.CurrentItemWindow=IW).Focus();
				break;
			}
			this.CurrentItemWindow ??= new ItemWindow(NewSelectItem);
		}

		Util.SetNullable(this.SelectedItem?.MapIcon, 'IsSelected', false);
		Util.SetNullable(NewSelectItem?.MapIcon, 'IsSelected', true);
		NewSelectItem?.MapIcon?.BringToFront();
		this.SelectedItem=NewSelectItem;
	}

	//Center over and select an item by its ID
	public    SelectAndCenterItem (ItemID:number): void { return this.SelectAndCenterItemI(ItemID); }
	protected SelectAndCenterItemI(ItemID:number, IsStackMove:boolean=false, NewScale?:number, Duration?:number): void
	{
		const I=Util.ThrowOnNull(Share.DS.Items.get(ItemID), "Invalid ItemID");
		this.GameMap.CenterOnPoint(this.GameMap.MapToCanvas(I.Pos), Duration, NewScale);
		this.SelectItemI(I, IsStackMove);
		Util.SetNullable(I.MapIcon, 'ForceVisibility', true); //Force the icon to be visible
	}

	private OnMove(): void
	{
		this.CurrentItemWindow?.UpdateAttachedPosition();
	}

	//Handle mouse events
	protected OnMouseMove(Pos:Vector2): void
	{
		if(Util.IsMobile())
			return;
		this.SetHoverItem(this.FindClosestItem(Pos));
		const RealPos=Pos.Add(this.GameMap.CanvasPos);
		this.ItemTooltip.css({
			left:(RealPos.X+4)+'px',
			top :(RealPos.Y+4)+'px',
		});
	}
	protected OnClick(Ev:MouseButtonEvent): void
	{
		if(Ev.Button!==Ev.Buttons.Left && Ev.Button!==Ev.Buttons.Pointer)
			return;
		const ClosestItem=this.FindClosestItem(Ev.Pos);
		if(!Util.IsMobile())
			this.SetHoverItem(ClosestItem);
		this.SelectItemI(ClosestItem);
	}
	protected OnMouseDown(Ev:MouseButtonEvent): void
	{
		if(Ev.Button===Ev.Buttons.Left || Ev.Button===Ev.Buttons.Pointer)
			Share.WM.SetFocus(null);
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

function ReplaceCurrentHistoryHash(Hash:string): void
{
	history.replaceState(history.state, StatStr.Empty, location.pathname+location.search+Hash);
}