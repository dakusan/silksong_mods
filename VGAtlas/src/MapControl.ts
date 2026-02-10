import $ from "jquery";

type Vector2={X:number, Y:number};
export class MapControl {
	private Canvas!:HTMLCanvasElement;
	private Ctx!:CanvasRenderingContext2D;
	private Image=new Image();
	private ImageFailed=false;

	private DRP=1;
	private X=0; Y=0;
	private Scale=1; MinScale=0.1; MaxScale=8;

	private NeedsRedraw=true;

	public Init(ImageURL:string): void
	{
		//Initialize the canvas
		$("#app").empty().append(
			this.Canvas=document.createElement("canvas")
		);

		//Get the canvas context
		const Ctx=this.Canvas.getContext("2d");
		if(!Ctx)
			throw new Error("2D context unavailable");
		this.Ctx=Ctx;

		//Set up window->canvas resizing
		this.ResizeToWindow();
		$(window).on("resize", this.ResizeToWindow.bind(this));

		this.BindInput();

		this.Image.onload=() =>
			this.CenterOnPoint(
				(this.Image.naturalWidth ||this.Image.width )/2,
				(this.Image.naturalHeight||this.Image.height)/2
			);
		this.Image.onerror=() => this.ImageFailed=this.NeedsRedraw=true;
		this.Image.src=ImageURL;

		this.Loop();
	}

	private ResizeToWindow(): void
	{
		this.Canvas.style.width =window.innerWidth +"px";
		this.Canvas.style.height=window.innerHeight+"px";

		this.DRP=window.devicePixelRatio||1;
		this.Canvas.width =Math.floor(window.innerWidth *this.DRP);
		this.Canvas.height=Math.floor(window.innerHeight*this.DRP);

		this.Ctx.resetTransform();
		this.Ctx.scale(this.DRP, this.DRP);

		this.NeedsRedraw=true;
	}

	private BindInput(): void
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

	private BindInputMouse(): void
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
				if(!IsDragging)
					return;

				this.X+=e.clientX-LastX;
				this.Y+=e.clientY-LastY;
				LastX  =e.clientX;
				LastY  =e.clientY;

				this.NeedsRedraw=true;
			});
	}

	private BindInputPointer(): void
	{
		let IsDragging=false, IsPinching=false;
		let LastX=0, LastY=0, PinchMapX=0, PinchMapY=0, PinchStartDist=0, PinchStartScale=1;
		const Pointers=new Map<number, Vector2>();

		const GetDist	=(A:Vector2, B:Vector2) => Math.hypot(A.X-B.X, A.Y-B.Y);
		const GetCenter	=(A:Vector2, B:Vector2) => ({X:(A.X+B.X)/2, Y:(A.Y+B.Y)/2});
		const BeginPinch=() => {
			if(Pointers.size!==2)
				return;
			const It=[...Pointers.values()];
			IsPinching=(PinchStartDist=GetDist(It[0], It[1]))>0;
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
			const Dist=GetDist(It[0], It[1]);
			this.Scale=Math.min(Math.max(PinchStartScale*(Dist/PinchStartDist), this.MinScale), this.MaxScale);
			const C=GetCenter(It[0], It[1]);
			this.X=C.X-PinchMapX*this.Scale;
			this.Y=C.Y-PinchMapY*this.Scale;
			this.NeedsRedraw=true;
		};
		const EndPinch= () => IsPinching=false;

		$(this.Canvas)
			.on("pointerdown", e => {
				const Pe=e.originalEvent as PointerEvent;
				Pointers.set(Pe.pointerId, {X:Pe.clientX, Y:Pe.clientY});
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
				if(P)
					[P.X, P.Y]=[Pe.clientX, Pe.clientY];
				if(IsPinching)
					return void(UpdatePinch());
				else if(!IsDragging)
					return;
				else if((Pe.buttons&1)===0)
					return void(IsDragging=false);

				this.X+=Pe.clientX-LastX;
				this.Y+=Pe.clientY-LastY;
				LastX=Pe.clientX;
				LastY=Pe.clientY;

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
					LastX=It[0].X;
					LastY=It[0].Y;
					IsDragging=true;
				}
			});

		this.Canvas.addEventListener("touchmove" , e => e.preventDefault(), {passive:false});
		this.Canvas.addEventListener("touchstart", e => e.preventDefault(), {passive:false});
	}

	private ZoomAt(PosX:number, PosY:number, ScaleAmount:number): void
	{
		const NewScale=Math.min(Math.max(this.Scale*ScaleAmount, this.MinScale), this.MaxScale);
		if(NewScale===this.Scale)
			return;

		const Ratio=NewScale/this.Scale;
		this.X=PosX-(PosX-this.X)*Ratio;
		this.Y=PosY-(PosY-this.Y)*Ratio;
		this.Scale=NewScale;
		this.NeedsRedraw=true;
	}

	public CenterOnPoint(PosX:number, PosY:number): void
	{
		const W=window.innerWidth;
		const H=window.innerHeight;
		this.Scale=Math.max(this.MinScale, Math.min(
			W/(this.Image.naturalWidth ||this.Image.width ),
			H/(this.Image.naturalHeight||this.Image.height),
			this.MaxScale
		));

		this.X=W/2-PosX*this.Scale;
		this.Y=H/2-PosY*this.Scale;
		this.NeedsRedraw=true;
	}

	private Loop(): void
	{
		requestAnimationFrame(this.Loop.bind(this));
		if(!this.NeedsRedraw)
			return;
		this.NeedsRedraw=false;
		this.Draw();
	}

	private Draw(): void
	{
		this.Ctx.resetTransform();
		this.Ctx.scale(this.DRP, this.DRP);
		this.Ctx.clearRect(0, 0, window.innerWidth, window.innerHeight);

		//Draw image load status indicators
		if(this.ImageFailed)
			return void(this.DrawCenteredAutoFitText("Failed to load map"));
		else if(!this.Image.complete || this.Image.naturalWidth<=0)
			return void(this.DrawCenteredAutoFitText("Loading map..."));

		this.Ctx.imageSmoothingEnabled=true;
		this.Ctx.translate(this.X, this.Y);
		this.Ctx.scale(this.Scale, this.Scale);
		this.Ctx.drawImage(this.Image, 0, 0);
	}

	private DrawCenteredAutoFitText(Text:string): void
	{
		const W=window.innerWidth;
		const H=window.innerHeight;
		const MaxFont=80, MinFont=10, Pad=24;
		this.Ctx.textAlign="center";
		this.Ctx.textBaseline="middle";
		this.Ctx.fillStyle="#fff";
		for(let Size=MaxFont; Size>=MinFont; Size--)
		{
			this.Ctx.font=`${Size}px sans-serif`;
			const Metrics=this.Ctx.measureText(Text);
			const TextW=Metrics.width;
			const TextH=(Metrics.actualBoundingBoxAscent||Size*0.8)+(Metrics.actualBoundingBoxDescent||Size*0.2);
			if(TextW<=W-Pad*2 && TextH<=H-Pad*2)
				return void(this.Ctx.fillText(Text, W/2, H/2));
		}

		//Fallback: smallest size even if it still doesn’t fit perfectly
		this.Ctx.font=`${MinFont}px sans-serif`;
		this.Ctx.fillText(Text, W/2, H/2);
	}
}