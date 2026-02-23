import $ from "jquery";
import "./Window.scss"
import { FriendClass, Vector2, Rect } from "./SharedClasses";

type KeyHandler<T extends Window|null>=(this:T, e:KeyboardEvent)=>boolean|undefined;
type ResizeDir="n"|"s"|"e"|"w"|"ne"|"nw"|"se"|"sw";

class DragState
{
	public constructor(Kind:"move"  , StartMX:number, StartMY:number, StartX:number, StartY:number, PointerId:number);
	public constructor(Kind:"resize", StartMX:number, StartMY:number, StartX:number, StartY:number, PointerId:number, Dir:ResizeDir, StartW:number, StartH:number);
	public constructor(
		public Kind:"move"|"resize",
		public StartMX:number,
		public StartMY:number,
		public StartX:number,
		public StartY:number,
		public PointerId:number,
		public Dir:ResizeDir="n",
		public StartW:number=-1,
		public StartH:number=-1
	) { }
}

function Clamp(n:number, min:number, max:number): number
{
	return	n<min ? min
		:	n>max ? max
		:			n;
}

export class WindowManager
{
	private Windows:Window[]=[];
	private _Active:Window|null=null; public get Active() { return this._Active; } private set Active(Val) { this._Active=Val; }
	public get ControlsKeyboard(): boolean { return !!this._Active?.AcceptsKeyboard; }

	private Z=10000;

	public constructor()
	{
		window.addEventListener("keydown", e => this.OnKey(e, "Down"), {capture:true});
		window.addEventListener("keyup"  , e => this.OnKey(e, "Up"	), {capture:true});
		window.addEventListener("resize" , ()=> this.Windows.forEach(W => W.EnsureOnScreen()));
	}

	protected Register(W:Window): void
	{
		this.Windows.push(W);
		this.SetFocus(W);
	}
	protected Unregister(W:Window): void
	{
		this.Windows=this.Windows.filter(x => x!==W);
		if(this.Active===W)
			this.Active=null;
	}

	public SetFocus(W:Window|null): void
	{
		if(!(W?.Visible ?? true) || W===this.Active)
			return;

		(this.Active as Window_Friend)?.SetActive(false);
		this.Active=W;

		if(W===null)
			return;
		this.Z++;
		(W as Window_Friend).SetActive(true);
		W.$Root.css("z-index", String(this.Z));
	}

	private OnKey(e:KeyboardEvent, Type:"Down"|"Up"): void
	{
		if(!this.ControlsKeyboard || !this.Active![Type==="Down" ? "OnKeyDown" : "OnKeyUp"]?.(e))
			return;
		e.preventDefault();
		e.stopPropagation();
	}
}
export const WM=new WindowManager();

type WindowInit=Partial<Pick<Window,
	"Title"|"Parent"|"X"|"Y"|"Width"|"Height"|"MinWidth"|"MinHeight"|"CanClose"|
	"CanResize"|"Visible"|"AcceptsKeyboard"|"OnKeyDown"|"OnKeyUp"|"OnClosed"
>>;
export class Window
{
	//Settable properties
	private _Title			="Window"		; public get Title			() { return this._Title				; }; public set Title			(Value) { this.$Title?.text		(this._Title	=Value); }
	private _Parent			=document.body	; public get Parent			() { return this._Parent			; }; public set Parent			(Value) { $(this.$Root).appendTo(this._Parent	=Value); }
	private _X				=80				; public get X				() { return this._X					; }; public set X				(Value) { this.UpdateBox		(this._X		=Value); }
	private _Y				=80				; public get Y				() { return this._Y					; }; public set Y				(Value) { this.UpdateBox		(this._Y		=Value); }
	private _Width			=420			; public get Width			() { return this._Width				; }; public set Width			(Value) { this.UpdateBox		(this._Width	=Value); }
	private _Height			=280			; public get Height			() { return this._Height			; }; public set Height			(Value) { this.UpdateBox		(this._Height	=Value); }
	private _MinWidth		=180			; public get MinWidth		() { return this._MinWidth			; }; public set MinWidth		(Value) { this.UpdateBox		(this._MinWidth	=Value); }
	private _MinHeight		=120			; public get MinHeight		() { return this._MinHeight			; }; public set MinHeight		(Value) { this.UpdateBox		(this._MinHeight=Value); }
	private _CanClose		=true			; public get CanClose		() { return this._CanClose			; }; public set CanClose		(Value) { this.SetCanClose		(this._CanClose	=Value); }
	private _CanResize		=true			; public get CanResize		() { return this._CanResize			; }; public set CanResize		(Value) { this.SetCanResize		(this._CanResize=Value); }
	private _Visible		=true			; public get Visible		() { return this._Visible			; }; public set Visible			(Value) { this.SetVisible		(this._Visible	=Value); }
	public get Pos ():Vector2 { return new Vector2(this._X,		this._Y		); } public set Pos	(Value:Vector2) { [this._X,		this._Y		]=[Value.x, Value.y]; this.UpdateBox(); }
	public get Size():Vector2 { return new Vector2(this._Width,	this._Height); } public set Size(Value:Vector2) { [this._Width, this._Height]=[Value.x, Value.y]; this.UpdateBox(); }
	public get Rect():Rect { return new Rect(this._X, this._Y, this._Width,	this._Height); }
		public set Rect(Value:Rect) { [this._X, this._Y, this._Width, this._Height]=[Value.x, Value.y, Value.Width, Value.Height]; this.UpdateBox(); }

	public AcceptsKeyboard=true;
	public OnKeyDown?:KeyHandler<Window>;
	public OnKeyUp	?:KeyHandler<Window>;
	public OnClosed	?(): boolean; //Return true to cancel close

	//Unsettable properties
	private	static		IDCounter		=0;
	public	readonly	ID				="Win"+Window.IDCounter++;
	private	readonly	EventNS			="."+this.ID;
	private				Drag?:DragState	=undefined;
	private				Disposed		=false;
	public	readonly	$Root		:JQuery<HTMLDivElement>;
	public	readonly	$Content	:JQuery<HTMLDivElement>;
	private	readonly	$Titlebar	:JQuery<HTMLDivElement>;
	private	readonly	$Title		:JQuery<HTMLDivElement>;
	private	readonly	$Close		:JQuery<HTMLButtonElement>;

	//Keep some part visible so you can always recover it
	private readonly VisibleEdge:number=24;
	private readonly VisibleTitle:number=28;

	private static AssignProps<T extends object>(Target:T, Src:Partial<T>): void
	{
		for(const K in Src)
		{
			const KK=K as keyof T;
			const V=Src[KK];
			if(V!==undefined)
				Target[KK]=V;
		}
	}
	public constructor(Init:WindowInit={})
	{
		Window.AssignProps(this, Init);

		//Build DOM
		this.$Root		=$("<div/>",	{class:"WinRoot", "data-id":this.ID		});
		this.$Titlebar	=$("<div/>", 	{class:"Titlebar"						});
		this.$Title		=$("<div/>", 	{class:"Title"							});
		this.$Close		=$("<button/>", {class:"Close", type:"button", text:"✕"	});
		const $Buttons	=$("<div/>", 	{class:"Buttons"						});
		this.$Content	=$("<div/>",	{class:"Content"						});
		this.$Root.append(
			this.$Titlebar.append(this.$Title, $Buttons.append(this.$Close)),
			this.$Content
		);

		//Create resize elements
		for(const Dir of "n,s,e,w,ne,nw,se,sw".split(','))
			this.$Root.append($("<div/>", {class:`ResizeHandle B${Dir.toUpperCase()}`}).attr("data-dir", Dir));

		//Initialize parts
		this.SetCanClose (this.CanClose	);
		this.SetCanResize(this.CanResize);
		this.SetVisible  (this.Visible	);
		this.UpdateBox();
		this.Title=this.Title;
		this.Parent=this.Parent;

		this.WireEvents();
		(WM as WindowManager_Friend).Register(this);
	}

	public		Close			():						void { this.TryDispose(); }
	public		EnsureOnScreen	():						void { this.UpdateBox()	; }
	public		Focus			(): 					void { WM.SetFocus(this); }
	private		SetCanClose		(CanClose	:boolean):	void { this.$Close?.toggleClass("Disabled"	, !CanClose	).prop("disabled", !CanClose); }
	private		SetCanResize	(CanResize	:boolean):	void { this.$Root?.	toggleClass("NoResize"	, !CanResize); }
	protected	SetActive		(Active		:boolean):	void { this.$Root.	toggleClass("Active"	, Active	); }
	private		SetVisible		(Visible	:boolean):	void
	{
		this.$Root?.toggleClass("Invisible", !Visible);
		if(Visible)
			this.UpdateBox();
		else if(WM.Active===this)
			 WM.SetFocus(null);
	}

	private UpdateBox(_?:unknown): void
	{
		this._Width =Math.max(this._Width , this._MinWidth );
		this._Height=Math.max(this._Height, this._MinHeight);
		this._X=Clamp(this._X, -(this._Width-this.VisibleEdge)	, document.documentElement.clientWidth -this.VisibleEdge );
		this._Y=Clamp(this._Y, 0								, document.documentElement.clientHeight-this.VisibleTitle);
		this.$Root?.css({
			left	:`${this._X		}px`,
			top		:`${this._Y		}px`,
			width	:`${this._Width	}px`,
			height	:`${this._Height}px`,
		});
	}

	private TryDispose(): void
	{
		if(this.Disposed || this.OnClosed?.())
			return;
		this.Disposed=true;
		this.Drag=undefined;
		([this.$Root, this.$Titlebar, this.$Close] as JQuery<HTMLElement>[])
			.forEach(El => El.off(this.EventNS));
		this.$Root.remove();
		(WM as WindowManager_Friend).Unregister(this);
	}

	private WireEvents(): void
	{
		this.$Root	.on(`pointerdown${this.EventNS}`, () => !this.Visible	? void(1) : this.Focus());
		this.$Close	.on(`click${this.EventNS}`		, () => !this.CanClose	? void(1) : this.Close());

		this.$Titlebar
			.on(`pointerdown${this.EventNS}`, (e:JQuery.TriggeredEvent) =>
			{
				const PE=e.originalEvent as PointerEvent|undefined;
				if(!PE || !this.Visible || $(PE.target!).closest(".Close").length)
					return;

				PE.preventDefault();
				this.Focus();
				(this.$Titlebar[0] as HTMLElement).setPointerCapture(PE.pointerId);
				this.Drag=new DragState("move", PE.clientX, PE.clientY, this.X, this.Y, PE.pointerId);
			})
			.on(`pointermove${this.EventNS}`, (e:JQuery.TriggeredEvent) =>
			{
				const PE=e.originalEvent as PointerEvent|undefined;
				if(PE && this.Drag?.Kind==="move" && PE.pointerId===this.Drag.PointerId)
					this.Pos=new Vector2(
						this.Drag.StartX+PE.clientX-this.Drag.StartMX,
						this.Drag.StartY+PE.clientY-this.Drag.StartMY
					);
			})
			.on(`pointerup${this.EventNS} pointercancel${this.EventNS}`, (e:JQuery.TriggeredEvent) =>
			{
				const PE=e.originalEvent as PointerEvent|undefined;
				if(PE && this.Drag?.PointerId===PE.pointerId)
					this.Drag=undefined;
			});

		this.$Root
			.on(`pointerdown${this.EventNS}`, ".ResizeHandle", (e:JQuery.TriggeredEvent) =>
			{
				const PE=e.originalEvent as PointerEvent|undefined;
				if(!PE || !this.Visible)
					return;

				this.Focus();
				if(!this.CanResize)
					return void PE.preventDefault();

				const Dir=$(e.currentTarget).attr("data-dir") as ResizeDir|undefined;
				if(!Dir)
					return;
				PE.preventDefault();
				(e.currentTarget as HTMLElement).setPointerCapture(PE.pointerId);
				this.Drag=new DragState("resize", PE.clientX, PE.clientY, this.X, this.Y, PE.pointerId, Dir, this.Width, this.Height);
			})
			.on(`pointermove${this.EventNS}`, ".ResizeHandle", (e:JQuery.TriggeredEvent) =>
			{
				const PE=e.originalEvent as PointerEvent|undefined;
				if(!PE || this.Drag?.Kind!=="resize" || PE.pointerId!==this.Drag.PointerId)
					return;

				const DX=PE.clientX-this.Drag.StartMX;
				const DY=PE.clientY-this.Drag.StartMY;
				let NX=this.Drag.StartX;
				let NY=this.Drag.StartY;
				let NW=this.Drag.StartW;
				let NH=this.Drag.StartH;
				const D=(Dir:ResizeDir) => this.Drag!.Dir.includes(Dir);

				if(D("e")) { NW=this.Drag.StartW+DX;						}
				if(D("w")) { NW=this.Drag.StartW-DX; NX=this.Drag.StartX+DX;}
				if(D("s")) { NH=this.Drag.StartH+DY;						}
				if(D("n")) { NH=this.Drag.StartH-DY; NY=this.Drag.StartY+DY;}
				if(NW<this.MinWidth ) { if(D("w")) NX=NX-(this.MinWidth -NW); NW=this.MinWidth ; }
				if(NH<this.MinHeight) { if(D("n")) NY=NY-(this.MinHeight-NH); NH=this.MinHeight; }

				this.Rect=new Rect(NX, NY, NW, NH);
			}).on(`pointerup${this.EventNS} pointercancel${this.EventNS}`, ".ResizeHandle", (e:JQuery.TriggeredEvent) =>
			{
				const PE=e.originalEvent as PointerEvent|undefined;
				if(PE && this.Drag?.PointerId===PE.pointerId)
					this.Drag=undefined;
			});
	}
}

abstract class WindowManager_Friend extends WindowManager implements FriendClass
{
	public override Register  (W:Window): void { return void this.Stub(W); }
	public override Unregister(W:Window): void { return void this.Stub(W); }
	//Ignore these
	protected constructor() { super(); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error("This function is a stub"); }
}

abstract class Window_Friend extends Window implements FriendClass
{
	public override SetActive(_Active:boolean): void { this.Stub(); }
	//Ignore these
	protected constructor() { super(); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error("This function is a stub"); }
}