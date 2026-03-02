import { computePosition, offset, flip, shift, autoUpdate, limitShift } from "@floating-ui/dom"
export default class PopupUtil
{
	/** @type {HTMLElement} */ PopupEl=null;
	/** @type {HTMLElement} */ AnchorEl=null;
	IsOpen=false;
	IsFullScreen=false;
	CleanupAutoUpdate=null;
	/** @type {HTMLElement} */ FullScreenCloseButton=null;
	constructor(PopupEl, AnchorEl, Opts={})
	{
		[this.PopupEl, this.AnchorEl]=[PopupEl, AnchorEl];
		this.opts={
			Placement	    :"bottom-start",
			XGap            :-15,
			YGap		    :-15,
			BorderPadding	:8,
			mobileQuery     :"(max-width:480px)",
			...Opts
		};
		this.OnPointerDown=this.OnPointerDown.bind(this);
	}

	Toggle() { this[this.IsOpen ? 'Hide' : 'Show'](); }
	async Show()
	{
		if(this.IsOpen)
			return;
		this.IsOpen=true;
		this.PopupEl.style.display="";

		this.IsFullScreen=window.matchMedia(this.opts.mobileQuery).matches;
		this.PopupEl.classList.toggle("FullScreen", this.IsFullScreen);
		this.PopupEl.classList.add("Active");
		if(!this.IsFullScreen) {
			await this.UpdatePosition();
			this.CleanupAutoUpdate=autoUpdate(this.AnchorEl, this.PopupEl, () => this.UpdatePosition(), {animationFrame:true});
		} else if(!this.FullScreenCloseButton) {
			const BTN=this.FullScreenCloseButton=document.createElement("button");
			BTN.className="PopupClose";
			BTN.type="button";
			BTN.setAttribute("aria-label","Close");
			BTN.innerText='×';
			BTN.addEventListener('click', this.Hide.bind(this));
			this.PopupEl.prepend(BTN);
		}

		document.addEventListener("pointerdown", this.OnPointerDown, true);
	}

	Hide()
	{
		if(!this.IsOpen)
			return;
		this.IsOpen=false;
		document.removeEventListener("pointerdown", this.OnPointerDown, true);

		this.CleanupAutoUpdate?.();
		this.CleanupAutoUpdate=null;
		this.PopupEl.style.display="none";
		this.PopupEl.classList.remove("Active");
		this.PopupEl.classList.remove("FullScreen");
	}


	async UpdatePosition()
	{
		const {x, y}=await computePosition(this.AnchorEl, this.PopupEl, {
			strategy:"fixed",
			placement:this.opts.Placement,
			middleware:[
				offset(({rects}) => ({mainAxis:this.opts.YGap, crossAxis:rects.reference.width+this.opts.XGap})),
				flip({rootBoundary:"viewport", fallbackPlacements:["top-start"]}),
				shift({padding:this.opts.BorderPadding, rootBoundary:"viewport", limiter:limitShift()}),
			],
		});

		Object.assign(this.PopupEl.style, {
			left:`${x}px`,
			top :`${y}px`,
		});
	}

	OnPointerDown(e)
	{
		const Target=e.target;
		if(
			   this.AnchorEl!==Target
			&& !this.AnchorEl.contains(Target)
		)
			this.Hide();
	}
}