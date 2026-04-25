import $ from 'jquery';
import { CallbackList, FriendClass, Log, StatStr, Util, Vector2, WillBeSet } from './Util/SharedClasses';
import GetExtraAssets from './Util/GetExtraAssets';
import { Share } from './Share';
const MaxZoomOutRatio=4/3; //How much further the map can zoom past 100% fit

export default class MapCanvas
{
	private Canvas:HTMLCanvasElement=WillBeSet;
	private Ctx:CanvasRenderingContext2D=WillBeSet;
	private Image:ImageBitmap=null!;
	private DRP=1;
	private X=0; private Y=0;
	private Scale=1; private MinScale=0.1; private MaxScale=8; public get ScaleRange() { return new Vector2(this.MinScale, this.MaxScale); }
	private MinVisiblePx=32; //Pan clamp config: ensure at least some of the image remains visible
	private NeedsRedraw=true;
	public Refresh() { this.NeedsRedraw=true; }

	//Critical error handling and extra messages
	private _ErrorMessage?:string=undefined;
	private _CanRender=false;
	public get ErrorMessage(): string|undefined { return this._ErrorMessage; }
	public set ErrorMessage(msg:string) { this._ErrorMessage=msg; this.NeedsRedraw=true; }
	public get CanRender() { return this._CanRender; }
	public ExtraMessage?="Loading icons...";

	public get Width() { return this.Canvas.clientWidth; }
	public get Height() { return this.Canvas.clientHeight; }
	public get Pos() { return new Vector2(this.X, this.Y); }
	public get ZoomScale() { return this.Scale; }
	public get CanvasPos() { const Rect=this.Canvas.getBoundingClientRect(); return new Vector2(Rect.x, Rect.y); }

	constructor(
		private readonly MulX:number, private readonly MulY:number, private readonly AddX:number, private readonly AddY:number
	) { }

	protected UpdatePosAndScale(NewScale?:number, NewX:number|undefined=undefined, NewY:number|undefined=undefined, FromMover:boolean=false): boolean
	{
		//If there is no image then do nothing
		if(this.Image===null)
			return false;

		//If mover is currently executing, stop it
		if(!FromMover)
			this.Mover?.Cancel();

		//If there are no updates, then nothing to do
		let SetX		=(NewX		?? this.X		);
		let SetY		=(NewY		?? this.Y		);
		const SetScale	=(NewScale	?? this.Scale	);

		//Clamp pan so the map can’t fully disappear off-screen
		const Pad=this.MinVisiblePx;
		SetX=Math.min(Math.max(SetX, Pad-this.Image.width *SetScale), this.Width -Pad);
		SetY=Math.min(Math.max(SetY, Pad-this.Image.height*SetScale), this.Height-Pad);

		//If there has been no change, then stop here
		if(SetX===this.X && SetY===this.Y && SetScale===this.Scale)
			return false;

		//Update the position and set to draw
		const OldScale=this.Scale;
		this.X=SetX;
		this.Y=SetY;
		this.Scale=SetScale;
		this.NeedsRedraw=true;

		//Execute callbacks
		this.Events.Moved.Execute(new Vector2(SetX, SetY), SetScale);
		if(OldScale!==SetScale)
			this.Events.Scale.Execute(OldScale, SetScale);

		return true;
	}

	public readonly Events={
		Frame		:new CallbackList<[FrameNum:number					]>('MapCanvas.Frame'		),
		Draw		:new CallbackList<[Ctx:CanvasRenderingContext2D		]>('MapCanvas.Draw'			),
		MouseDown	:new CallbackList<[Pos:Vector2						]>('MapCanvas.MouseDown'	),
		MouseMove	:new CallbackList<[Pos:Vector2						]>('MapCanvas.MouseMove'	),
		MouseLeave	:new CallbackList<[									]>('MapCanvas.MouseLeave'	),
		Click		:new CallbackList<[Pos:Vector2						]>('MapCanvas.MouseClick'	),
		Scale		:new CallbackList<[NewScale:number, OldScale:number	]>('MapCanvas.Scale'		), //Scaling will always additionally call Moved
		Moved		:new CallbackList<[Pos:Vector2, Scale:number		]>('MapCanvas.Moved'		),
		UserZoom	:new CallbackList<[Pos:Vector2, {Scale:number}		]>('MapCanvas.ZoomAt'		),
	};

	public async Init(ImageURL:string)
	{
		//Initialize the canvas
		$('#map').empty().append(
			this.Canvas=document.createElement('canvas')
		);
		this.Ctx=Util.ThrowOnNull(this.Canvas.getContext('2d'), "2D context unavailable"); //Get the canvas context

		this.ResizeToWindow();
		$(window).on('resize', this.ResizeToWindow.bind(this));
		this.Loop();
		this._CanRender=true;

		this.BindInput();

		try {
			const ImgLoader=GetExtraAssets.LoadImage(ImageURL);
			this.Image=await ImgLoader;
			this.ResizeToWindow();
			this.Scale=this.MinScale*MaxZoomOutRatio;
			this.UpdatePosAndScale( //Center bitmap at current scale
				this.Scale,
				(this.Width -this.Image.width *this.Scale)/2,
				(this.Height-this.Image.height*this.Scale)/2,
			);
		} catch(e) {
			throw new Error("Failed to load map:"+StatStr.NewLine+Util.GetErrorMessage(e));
		}
	}

	private ResizeToWindow()
	{
		const CRect=this.Canvas.getBoundingClientRect();
		this.DRP=window.devicePixelRatio||1;
		this.Canvas.width =Math.floor(CRect.width *this.DRP);
		this.Canvas.height=Math.floor(CRect.height*this.DRP);

		this.Ctx.resetTransform();
		this.Ctx.scale(this.DRP, this.DRP);

		this.NeedsRedraw=true;

		//Adjust MinScale to not allow zooming out more than 25% past full-fit
		if(this.Image===null)
			return;
		const FitScale=Math.min(
			this.Width /this.Image.width,
			this.Height/this.Image.height
		);
		this.MinScale=FitScale/MaxZoomOutRatio;
		this.UpdatePosAndScale(Math.min(Math.max(this.Scale, this.MinScale), this.MaxScale)); //Ensure current scale respects new bounds
	}

	private BindInput()
	{
		$(this.Canvas)
			.on('dragstart'	 , e => e.preventDefault())
			.on('contextmenu', e => e.preventDefault());

		if('PointerEvent' in window)
			this.BindInputPointer();
		else
			this.BindInputMouse();

		//Bind the wheel
		this.Canvas.addEventListener('wheel', e => {
			e.preventDefault();
			const ZoomAround=this.EvPos(e);
			const ScaleAt={Scale:e.deltaY>0 ? 0.9 : 1.1};
			this.Events.UserZoom.Execute(ZoomAround, ScaleAt);
			this.ZoomAt(ZoomAround, ScaleAt.Scale);
		}, {passive:false});
	}

	private BindInputMouse()
	{
		let IsDragging=false;
		let LastX=0, LastY=0, StartX=0, StartY=0;

		$(this.Canvas)
			.on('mousedown', e => {
				if(e.which!==1)
					return;
				IsDragging=true;
				const MousePos=this.EvPos(e);
				StartX=LastX=MousePos.X;
				StartY=LastY=MousePos.Y;
				this.Events.MouseDown.Execute(MousePos);
			})
			.on('mouseleave', () => { IsDragging=false; this.Events.MouseLeave.Execute(); })
			.on('mouseup', e => {
				const MousePos=this.EvPos(e);
				IsDragging=false;
				if(MousePos.Distance(new Vector2(StartX, StartY))<Math.sqrt(3*3+3*3))
					this.Events.Click.Execute(MousePos);
			})
			.on('mousemove', e => {
				const MousePos=this.EvPos(e);
				this.Events.MouseMove.Execute(MousePos);

				if(!IsDragging)
					return;

				this.UpdatePosAndScale(undefined, this.X+MousePos.X-LastX, this.Y+MousePos.Y-LastY);
				LastX  =MousePos.X;
				LastY  =MousePos.Y;
			});
	}

	private BindInputPointer()
	{
		let IsDragging=false, IsPinching=false;
		let LastX=0, LastY=0, PinchMapX=0, PinchMapY=0, PinchStartDist=0, PinchStartScale=1, StartX=0, StartY=0;
		const Pointers=new Map<number, Vector2>();

		const GetCenter	=(A:Vector2, B:Vector2) => new Vector2((A.X+B.X)/2, (A.Y+B.Y)/2);
		const BeginPinch=() => {
			if(Pointers.size!==2)
				return;
			const It=[...Pointers.values()];
			IsPinching=(PinchStartDist=It[0].Distance(It[1]))>0;
			IsDragging=false;
			PinchStartScale=this.Scale;

			const C=GetCenter(It[0], It[1]);
			PinchMapX=(C.X-this.X)/this.Scale;
			PinchMapY=(C.Y-this.Y)/this.Scale;
		};
		const UpdatePinch=() => {
			if(!IsPinching || Pointers.size!==2)
				return;
			const It=[...Pointers.values()];
			const Dist=It[0].Distance(It[1]);
			const NewScale=Math.min(Math.max(PinchStartScale*(Dist/PinchStartDist), this.MinScale), this.MaxScale);
			const C=GetCenter(It[0], It[1]);
			this.UpdatePosAndScale(
				NewScale,
				C.X-PinchMapX*NewScale,
				C.Y-PinchMapY*NewScale,
			);
		};
		const EndPinch= () => IsPinching=false;

		$(this.Canvas)
			.on('pointerdown', e => {
				if(Pointers.size>=2)
					return;

				const Pe=e.originalEvent as PointerEvent;
				const MousePos=this.EvPos(Pe);
				Pointers.set(Pe.pointerId, MousePos);
				try { this.Canvas.setPointerCapture(Pe.pointerId); } catch {}
				if(Pointers.size===2)
					return void(BeginPinch());

				if(
					   (Pe.pointerType==='mouse' || Pe.pointerType==='pen')
					&& (!Pe.isPrimary || Pe.button!==0)
				)
					return;

				IsDragging=true;
				StartX=LastX=MousePos.X;
				StartY=LastY=MousePos.Y;
				this.Events.MouseDown.Execute(MousePos);
			})
			.on('pointermove', e => {
				const Pe=e.originalEvent as PointerEvent;
				const P=Pointers.get(Pe.pointerId);
				const MousePos=this.EvPos(Pe);
				this.Events.MouseMove.Execute(MousePos);

				if(P)
					[P.X, P.Y]=[MousePos.X, MousePos.Y];
				if(IsPinching)
					return void(UpdatePinch());
				else if(!IsDragging)
					return;
				else if((Pe.buttons&1)===0)
					return void(IsDragging=false);

				this.UpdatePosAndScale(undefined, this.X+MousePos.X-LastX, this.Y+MousePos.Y-LastY);
				LastX=MousePos.X;
				LastY=MousePos.Y;
			})
			.on('pointerleave', () => { IsDragging=false; this.Events.MouseLeave.Execute(); })
			.on('pointerup pointercancel', e => {
				const Pe=e.originalEvent as PointerEvent;
				const MousePos=this.EvPos(Pe);
				if(MousePos.Distance(new Vector2(StartX, StartY))<Math.sqrt(3*3+3*3))
					this.Events.Click.Execute(MousePos);
				Pointers.delete(Pe.pointerId);
				try { this.Canvas.releasePointerCapture(Pe.pointerId); } catch {}

				IsDragging=false;
				if(Pointers.size<2)
					EndPinch();

				if(Pointers.size===1) {
					const It=[...Pointers.values()];
					LastX=It[0].X;
					LastY=It[0].Y;
					IsDragging=true;
				}
			});

		this.Canvas.addEventListener('touchmove' , e => e.preventDefault(), {passive:false});
		this.Canvas.addEventListener('touchstart', e => e.preventDefault(), {passive:false});
	}

	public ZoomAt(Pos:Vector2, ScaleAmount:number)
	{
		const NewScale=Math.min(Math.max(this.Scale*ScaleAmount, this.MinScale), this.MaxScale);
		if(NewScale===this.Scale)
			return;

		const Ratio=NewScale/this.Scale;
		this.UpdatePosAndScale(
			NewScale,
			Pos.X-(Pos.X-this.X)*Ratio,
			Pos.Y-(Pos.Y-this.Y)*Ratio,
		);
	}

	public PanAt(DeltaX:number, DeltaY:number)
	{
		this.UpdatePosAndScale(undefined, this.X+DeltaX, this.Y+DeltaY);
	}

	protected Mover?:Mover=undefined;
	public CenterOnPoint(Pos:Vector2, Duration?:number, NewScale?:number)
	{
		const NewX=this.Width /2-(Pos.X-this.X);
		const NewY=this.Height/2-(Pos.Y-this.Y);
		if((Duration ??= Share.LC.AutoPanTime.V)<=0)
			return void this.UpdatePosAndScale(NewScale, NewX, NewY);
		this.Mover?.Cancel();
		this.Mover=new Mover(new Vector2(this.X, this.Y), new Vector2(NewX, NewY), Duration, NewScale)
	}

	private static readonly FPSAverageOver=2000;
	private FrameTimes:number[]=[];
	private _FrameNum:number=0; public get FrameNum() { return this._FrameNum; }
	public get FPS() { return this.FrameTimes.length/MapCanvas.FPSAverageOver*1000; }
	private BindLoop=this.Loop.bind(this);
	private Loop()
	{
		//Calculate FPS
		const Now=performance.now();
		this.FrameTimes.push(Now);
		const Cutoff=Now-MapCanvas.FPSAverageOver;
		while(this.FrameTimes.length && this.FrameTimes[0]<Cutoff)
			this.FrameTimes.shift();

		this.Events.Frame.Execute(this._FrameNum++);
		requestAnimationFrame(this.BindLoop);

		if(!this.NeedsRedraw)
			return;
		this.NeedsRedraw=false;
		this.Draw();
	}

	private Draw(): void
	{
		this.Ctx.resetTransform();
		this.Ctx.scale(this.DRP, this.DRP);
		this.Ctx.clearRect(0, 0, this.Width, this.Height);

		//Draw image load status indicators
		if(this.ErrorMessage!==undefined)
			return void(this.DrawCenteredAutoFitText(this.ErrorMessage));
		else if(this.Image===null)
			return void(this.DrawCenteredAutoFitText("Loading map..."));

		this.Ctx.imageSmoothingEnabled=true;
		this.Ctx.drawImage(
			this.Image, this.X, this.Y,
			this.Image.width*this.Scale,
			this.Image.height*this.Scale
		);
		this.Events.Draw.Execute(this.Ctx);

		if(this.ExtraMessage!==undefined)
			this.DrawCenteredAutoFitText(this.ExtraMessage);
	}

	private DrawCenteredAutoFitText(Text:string)
	{
		const MaxFont=80, MinFont=10, Pad=24;
		const Lines=Text.split(/\r?\n/);

		this.Ctx.textAlign='center';
		this.Ctx.textBaseline='middle';
		this.Ctx.fillStyle='#fff';

		const MeasureMultiline=(Size:number) => {
			this.Ctx.font=`${Size}px sans-serif`;
			let MaxW=0;
			let LineH=Size;
			for(const Line of Lines) {
				const M=this.Ctx.measureText(Line);
				MaxW=Math.max(MaxW, M.width);
				const Ascent =M.actualBoundingBoxAscent  || Size*0.8;
				const Descent=M.actualBoundingBoxDescent || Size*0.2;
				LineH=Math.max(LineH, Ascent+Descent);
			}

			const TotalH=LineH*Lines.length;
			return { MaxW, LineH, TotalH };
		};

		const W=this.Width; const H=this.Height;
		for(let Size=MaxFont; Size>=MinFont; Size--) {
			const { MaxW, LineH, TotalH }=MeasureMultiline(Size);
			if(Size>MinFont && (MaxW>W-Pad*2 || TotalH>H-Pad*2))
				continue;

			const StartY=H/2-TotalH/2+LineH/2;
			for(let i=0; i<Lines.length; i++)
				this.Ctx.fillText(Lines[i], W/2, StartY+i*LineH);
			break;
		}
	}

	private MapToCanvasCoord(MapV:number, InV:number, Mul:number, Add:number) { return MapV+(InV*Mul+Add)*this.Scale; }
	private CanvasToMapCoord(MapV:number, InV:number, Mul:number, Add:number) { return ((InV-MapV)/this.Scale-Add)/Mul; }
	private MapToCanvasRel	(Pos:Vector2, RelX:number, RelY:number) { return new Vector2(this.MapToCanvasCoord(RelX, Pos.X, this.MulX, this.AddX), this.MapToCanvasCoord(RelY, Pos.Y, this.MulY, this.AddY)); }
	private CanvasToMapRel	(Pos:Vector2, RelX:number, RelY:number) { return new Vector2(this.CanvasToMapCoord(RelX, Pos.X, this.MulX, this.AddX), this.CanvasToMapCoord(RelY, Pos.Y, this.MulY, this.AddY)); }
	public MapToCanvas		(Pos:Vector2) { return this.MapToCanvasRel(Pos, this.X, this.Y		); }
	public CanvasToMap		(Pos:Vector2) { return this.CanvasToMapRel(Pos, this.X, this.Y		); }
	public MapToCanvasUniv	(Pos:Vector2) { return this.MapToCanvasRel(Pos, 0,			0		); }
	public CanvasUnivToMap	(Pos:Vector2) { return this.CanvasToMapRel(Pos, 0,			0		); }
	public CanvasToCanvasUniversal(Pos:Vector2) { return new Vector2(Pos.X-this.X, Pos.Y-this.Y	); }
	public CanvasUniversalToCanvas(Pos:Vector2) { return new Vector2(Pos.X+this.X, Pos.Y+this.Y	); }

	//eslint-disable-next-line @typescript-eslint/naming-convention
	private EvPos(e:{clientX:number, clientY:number}) { return new Vector2(e.clientX, e.clientY).Sub(this.CanvasPos); }
}
abstract class MapCanvas_Friend extends MapCanvas implements FriendClass
{
	public override UpdatePosAndScale(_NewScale:number|undefined, _NewX:number|undefined, _NewY:number|undefined, _FromMover:boolean): boolean { return this.Stub(true); }
	public override Mover?:Mover=undefined;
	//Ignore these
	protected constructor() { super(-1, -1, -1, -1); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}

class Mover
{
	private static ClassUniqueID=0;
	private readonly MyUniqueID=Mover.ClassUniqueID++;
	private readonly StartZoom=Share.MCanvas.ZoomScale;

	public readonly StartTime=Date.now();
	private IsComplete=false;
	constructor(
		public readonly Start:Vector2,
		public readonly End  :Vector2,
		public readonly Duration:number, //In seconds
		public readonly ZoomTo?:number,
	) {
		this.Duration*=1000;
		Share.MCanvas.Events.Draw.Add('MoveToPointAction'+this.MyUniqueID, this.OnFrame.bind(this));

		//If also zooming then we need to use map coordinates
		if(this.ZoomTo!==undefined) {
			const Ratio=this.ZoomTo/this.StartZoom;
			const CX=Share.MCanvas.Width /2;
			const CY=Share.MCanvas.Height/2;
			this.End=new Vector2(
				CX-(CX-this.End.X)*Ratio,
				CY-(CY-this.End.Y)*Ratio
			);
		}
	}

	private static Ease(T:number, Pow=4) { return T<0.5 ? 0.5*Math.pow(2*T, Pow) : 1-0.5*Math.pow(2*(1-T), Pow); }
	public static Lerp(a:number, b:number, t:number) { return a+(b-a)*t; }
	private OnFrame()
	{
		const LinearProgressPoint=Math.min((Date.now()-this.StartTime)/this.Duration, 1);
		if(LinearProgressPoint===0)
			return;
		const EaseProgress=Mover.Ease(LinearProgressPoint, Share.LC.AutoPanEase.V);
		if(!(Share.MCanvas as MapCanvas_Friend).UpdatePosAndScale(
			this.ZoomTo===undefined ? undefined : Mover.Lerp(this.StartZoom, this.ZoomTo, EaseProgress),
			Mover.Lerp(this.Start.X, this.End.X, EaseProgress),
			Mover.Lerp(this.Start.Y, this.End.Y, EaseProgress),
			true
		)) {
			Log.Debug("Stopping mover early");
			return void this.Cancel();
		}

		if(LinearProgressPoint>=1)
			this.Cancel();
	}
	public Cancel()
	{
		if(this.IsComplete)
			return;
		this.IsComplete=true;
		Share.MCanvas.Events.Draw.Remove('MoveToPointAction'+this.MyUniqueID);
		(Share.MCanvas as MapCanvas_Friend).Mover=undefined;
	}
}