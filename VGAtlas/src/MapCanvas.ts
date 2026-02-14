import $ from "jquery";
import { Util, Vector2, WillBeSet } from "./SharedClasses"

export class MapCanvas
{
	private Canvas:HTMLCanvasElement=WillBeSet;
	private Ctx:CanvasRenderingContext2D=WillBeSet;
	private Image:ImageBitmap=null!;
	private DRP=1;
	private x=0; private y=0;
	private Scale=1; private MinScale=0.1; private MaxScale=8;
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

	public DrawCallbacks:((Ctx:CanvasRenderingContext2D) => void)[]=[];
	public MouseMoveCallbacks:((x:number, y:number) => void)[]=[];

	public async Init(ImageURL:string)
	{
		//Initialize the canvas
		$("#map").empty().append(
			this.Canvas=document.createElement("canvas")
		);

		//Get the canvas context
		const Ctx=this.Canvas.getContext("2d");
		if(!Ctx)
			throw new Error("2D context unavailable");
		this.Ctx=Ctx;

		this.ResizeToWindow();
		$(window).on("resize", this.ResizeToWindow.bind(this));
		this.Loop();
		this._CanRender=true;

		this.BindInput();

		try {
			const ImgLoader=Util.LoadImage(ImageURL);
			this.Image=await ImgLoader;
			this.ResizeToWindow();
			this.CenterOnPoint(this.Image.width/2, this.Image.height/2);
		} catch(e) {
			throw new Error("Failed to load map:\n"+Util.GetErrorMessage(e));
		}
	}

	private ResizeToWindow()
	{
		this.Canvas.style.width =window.innerWidth +"px";
		this.Canvas.style.height=window.innerHeight+"px";

		this.DRP=window.devicePixelRatio||1;
		this.Canvas.width =Math.floor(window.innerWidth *this.DRP);
		this.Canvas.height=Math.floor(window.innerHeight*this.DRP);

		this.Ctx.resetTransform();
		this.Ctx.scale(this.DRP, this.DRP);

		this.ClampPan();
		this.NeedsRedraw=true;

		//Adjust MinScale to not allow zooming out more than 25% past full-fit
		if(this.Image===null)
			return;
		const FitScale=Math.min(
			this.Width /this.Image.width,
			this.Height/this.Image.height
		);
		this.MinScale=FitScale*0.75;
		this.Scale=Math.min(Math.max(this.Scale, this.MinScale), this.MaxScale); //Ensure current scale respects new bounds
	}

	private BindInput()
	{
		$(this.Canvas)
			.on("dragstart"	 , e => e.preventDefault())
			.on("contextmenu", e => e.preventDefault());

		if("PointerEvent" in window)
			this.BindInputPointer();
		else
			this.BindInputMouse();

		//Bind the wheel
		this.Canvas.addEventListener("wheel", e => {
			e.preventDefault();
			const Rect=this.Canvas.getBoundingClientRect();
			this.ZoomAt(
				e.clientX-Rect.left,
				e.clientY-Rect.top,
				e.deltaY>0 ? 0.9 : 1.1
			);
		}, {passive:false});
	}

	private BindInputMouse()
	{
		let IsDragging=false;
		let LastX=0, LastY=0;

		$(this.Canvas)
			.on("mousedown", e => {
				if(e.which!==1)
					return;
				IsDragging=true;
				LastX=e.clientX;
				LastY=e.clientY;
			})
			.on("mouseup mouseleave", () =>
				IsDragging=false
			)
			.on("mousemove", e => {
				for(const Cb of this.MouseMoveCallbacks)
					try { Cb(e.clientX, e.clientY); }
					catch(e) { Util.OutputException("Mouse move callback", e); }

				if(!IsDragging)
					return;

				this.x+=e.clientX-LastX;
				this.y+=e.clientY-LastY;
				LastX  =e.clientX;
				LastY  =e.clientY;

				this.ClampPan();
				this.NeedsRedraw=true;
			});
	}

	private BindInputPointer()
	{
		let IsDragging=false, IsPinching=false;
		let LastX=0, LastY=0, PinchMapX=0, PinchMapY=0, PinchStartDist=0, PinchStartScale=1;
		const Pointers=new Map<number, Vector2>();

		const GetDist	=(A:Vector2, B:Vector2) => Math.hypot(A.x-B.x, A.y-B.y);
		const GetCenter	=(A:Vector2, B:Vector2) => new Vector2((A.x+B.x)/2, (A.y+B.y)/2);
		const BeginPinch=() => {
			if(Pointers.size!==2)
				return;
			const It=[...Pointers.values()];
			IsPinching=(PinchStartDist=GetDist(It[0], It[1]))>0;
			IsDragging=false;
			PinchStartScale=this.Scale;

			const C=GetCenter(It[0], It[1]);
			PinchMapX=(C.x-this.x)/this.Scale;
			PinchMapY=(C.y-this.y)/this.Scale;
		};
		const UpdatePinch=() => {
			if(!IsPinching || Pointers.size!==2)
				return;
			const It=[...Pointers.values()];
			const Dist=GetDist(It[0], It[1]);
			this.Scale=Math.min(Math.max(PinchStartScale*(Dist/PinchStartDist), this.MinScale), this.MaxScale);
			const C=GetCenter(It[0], It[1]);
			this.x=C.x-PinchMapX*this.Scale;
			this.y=C.y-PinchMapY*this.Scale;
			this.ClampPan();
			this.NeedsRedraw=true;
		};
		const EndPinch= () => IsPinching=false;

		$(this.Canvas)
			.on("pointerdown", e => {
				const Pe=e.originalEvent as PointerEvent;
				Pointers.set(Pe.pointerId, new Vector2(Pe.clientX, Pe.clientY));
				try { this.Canvas.setPointerCapture(Pe.pointerId); } catch {}
				if(Pointers.size===2)
					return void(BeginPinch());

				if(
					   (Pe.pointerType==="mouse" || Pe.pointerType==="pen")
					&& (!Pe.isPrimary || Pe.button!==0)
				)
					return;

				IsDragging=true;
				LastX=Pe.clientX;
				LastY=Pe.clientY;
			})
			.on("pointermove", e => {
				const Pe=e.originalEvent as PointerEvent;
				const P=Pointers.get(Pe.pointerId);
				for(const Cb of this.MouseMoveCallbacks)
					try { Cb(Pe.clientX, Pe.clientY); }
					catch(e) { Util.OutputException("Pointer move callback", e); }

				if(P)
					[P.x, P.y]=[Pe.clientX, Pe.clientY];
				if(IsPinching)
					return void(UpdatePinch());
				else if(!IsDragging)
					return;
				else if((Pe.buttons&1)===0)
					return void(IsDragging=false);

				this.x+=Pe.clientX-LastX;
				this.y+=Pe.clientY-LastY;
				LastX=Pe.clientX;
				LastY=Pe.clientY;

				this.ClampPan();
				this.NeedsRedraw=true;
			})
			.on("pointerup pointercancel", e => {
				const Pe=e.originalEvent as PointerEvent;
				Pointers.delete(Pe.pointerId);
				try { this.Canvas.releasePointerCapture(Pe.pointerId); } catch {}

				IsDragging=false;
				if(Pointers.size<2)
					EndPinch();

				if(Pointers.size===1) {
					const It=[...Pointers.values()];
					LastX=It[0].x;
					LastY=It[0].y;
					IsDragging=true;
				}
			});

		this.Canvas.addEventListener("touchmove" , e => e.preventDefault(), {passive:false});
		this.Canvas.addEventListener("touchstart", e => e.preventDefault(), {passive:false});
	}

	private ZoomAt(PosX:number, PosY:number, ScaleAmount:number)
	{
		const NewScale=Math.min(Math.max(this.Scale*ScaleAmount, this.MinScale), this.MaxScale);
		if(NewScale===this.Scale)
			return;

		const Ratio=NewScale/this.Scale;
		this.x=PosX-(PosX-this.x)*Ratio;
		this.y=PosY-(PosY-this.y)*Ratio;
		this.Scale=NewScale;
		this.ClampPan();
		this.NeedsRedraw=true;
	}

	public CenterOnPoint(PosX:number, PosY:number)
	{
		const W=this.Width;
		const H=this.Height;
		this.Scale=Math.max(
			this.MinScale, Math.min(
				W/this.Image.width, H/this.Image.height, this.MaxScale
			)
		);

		this.x=W/2-PosX*this.Scale;
		this.y=H/2-PosY*this.Scale;
		this.ClampPan();
		this.NeedsRedraw=true;
	}

	//Clamp pan so the map can’t fully disappear off-screen
	private ClampPan()
	{
		if(this.Image===null)
			return;
		const Pad=this.MinVisiblePx;
		this.x=Math.min(Math.max(this.x, Pad-this.Image.width *this.Scale), this.Width -Pad);
		this.y=Math.min(Math.max(this.y, Pad-this.Image.height*this.Scale), this.Height-Pad);
	}

	private BindLoop=this.Loop.bind(this);
	private Loop()
	{
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
			this.Image, this.x, this.y,
			this.Image.width*this.Scale,
			this.Image.height*this.Scale
		);
		for(const CB of this.DrawCallbacks)
			try { CB(this.Ctx); }
			catch(e) { Util.OutputException("Draw callback", e); }

		if(this.ExtraMessage!==undefined)
			this.DrawCenteredAutoFitText(this.ExtraMessage);
	}

	private DrawCenteredAutoFitText(Text:string)
	{
		const MaxFont=80, MinFont=10, Pad=24;
		const Lines=Text.split(/\r?\n/);

		this.Ctx.textAlign="center";
		this.Ctx.textBaseline="middle";
		this.Ctx.fillStyle="#fff";

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

	private MulX=87.7487; private MulY=-87.5855; private AddX=2090; private AddY=1569;
	private MapToCanvasCoord(MapV:number, InV:number, Mul:number, Add:number) { return MapV+(InV*Mul+Add)*this.Scale; }
	private CanvasToMapCoord(MapV:number, InV:number, Mul:number, Add:number) { return ((InV-MapV)/this.Scale-Add)/Mul; }
	public MapToCanvas(x:number, y:number) { return new Vector2(this.MapToCanvasCoord(this.x, x, this.MulX, this.AddX), this.MapToCanvasCoord(this.y, y, this.MulY, this.AddY)); }
	public CanvasToMap(x:number, y:number) { return new Vector2(this.CanvasToMapCoord(this.x, x, this.MulX, this.AddX), this.CanvasToMapCoord(this.y, y, this.MulY, this.AddY)); }
}