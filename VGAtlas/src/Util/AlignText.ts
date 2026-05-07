import { Rect } from './SharedClasses';

export class FitOptions { constructor(
	public readonly MinFont						=10,
	public readonly MaxFont						=80,
	public readonly FontFamily					='sans-serif',
	public readonly CheckIfFits:CheckIfFitsFunc	=CheckFits_Rectangle,
	public readonly ExtraLineHeight				=-2, //Adds Abs(value) to each line height. Negative values also force all lines to use the longest line height
	public readonly AlignHorizontal				=AlignTextType.Middle,
	public readonly AlignVertical				=AlignTextType.Middle,
) { } }

export class LineSize
{
	constructor(
		public readonly Text	:string,
		public readonly Width	:number,
		public readonly Height	:number,
	) { }
}

export class LineRect
{
	constructor(
		public readonly Text:string,
		public readonly Rect:Rect,
	) { }
}

export class AutoFitText
{
	constructor(
		public readonly Size:number,
		public readonly LineRects:LineRect[] //X and Y coordinates are for upper-left corner of the text
	) { }
}

export enum AlignTextType { Beginning, Middle, End } //Left/Top, Center, Right/Bottom

type CheckIfFitsFunc=(Width:number, Height:number, LineRects:LineRect[]) => boolean;

export function ExecuteAutoFit(
	Lines:string|string[],
	Width:number, Height:number,
	Opts?:Partial<FitOptions>,
) {
	return CalculateMaxFontSize(
		new OffscreenCanvas(Width, Height).getContext('2d')!,
		Array.isArray(Lines) ? Lines : Lines.split(/\r?\n/),
		Width, Height,
		Object.assign({}, new FitOptions(), Object.fromEntries(Object.entries(Opts ?? {}).filter(([, Val]) => Val!==undefined)) as Partial<FitOptions>),
	);
}

//CheckIfFitsFunc
export function CheckFits_Rectangle(Width:number, Height:number, LineRects:LineRect[])
{
	for(const {Rect:LR} of LineRects)
		if(LR.X<0 || LR.Y<0 || LR.X+LR.Width>Width || LR.Y+LR.Height>Height)
			return false;
	return true;
}

//Called by RunFit
export function CalculateMaxFontSize(Ctx:OffscreenCanvasRenderingContext2D, StrLines:string[], Width:number, Height:number, Opts:FitOptions)
{
	let Min=Opts.MinFont;
	let Max=Opts.MaxFont;
	let Best:AutoFitText|undefined;

	while(Min<=Max) {
		const Size=Math.floor((Min+Max)/2);
		Ctx.font=`${Size}px ${Opts.FontFamily}`;
		const LineRects=GetLineRects(GetLineSizes(Ctx, StrLines), Width, Height, Opts);
		const AF=new AutoFitText(Size, LineRects);
		if(Size<=Opts.MinFont)
			return AF;
		const Fits=Opts.CheckIfFits(Width, Height, LineRects);
		Best=Fits ? AF : Best;
		if(Fits)
			Min=Size+1;
		else
			Max=Size-1;
	}

	return Best!;
}

//Called by CalculateSize
export function GetLineRects(Lines:LineSize[], Width:number, Height:number, Opts:FitOptions)
{
	//Calculate equal line height if needed
	let MaxLineHeight=0; //0 if not equal line height
	if(Opts.ExtraLineHeight<0 || (Opts.ExtraLineHeight===0 && 1/Opts.ExtraLineHeight===-Infinity)) //If equal line height
		MaxLineHeight=Math.max(...Lines.map(LS => LS.Height));

	//Calculate total line height if needed
	const LineHeightPadding=Math.abs(Opts.ExtraLineHeight);
	let TotalHeight=LineHeightPadding*Lines.length;
	if(Opts.AlignVertical!==AlignTextType.Beginning) //Not needed if at top
		if(MaxLineHeight!==0)
			TotalHeight+=MaxLineHeight*Lines.length;
		else
			for(const Line of Lines)
				TotalHeight+=Line.Height;

	//Calculate the rects
	let CurrentY=CalcAlign(Opts.AlignVertical, Height, TotalHeight);
	const RetRects:LineRect[]=new Array(Lines.length);
	for(let i=0; i<Lines.length; i++) {
		const L=Lines[i];
		const LineHeight=(MaxLineHeight!==0 ? MaxLineHeight : L.Height)+LineHeightPadding;
		RetRects[i]=new LineRect(Lines[i].Text, new Rect(
			CalcAlign(Opts.AlignHorizontal, Width, L.Width),
			CurrentY, L.Width, LineHeight
		));
		CurrentY+=LineHeight;
	}
	return RetRects;
}

//Called by CalculateSize
export function GetLineSizes(Ctx:OffscreenCanvasRenderingContext2D, Lines:string[])
{
	const Size=parseInt(Ctx.font, 10);
	let Index=0;
	const Sizes:LineSize[]=new Array(Lines.length);
	for(const Line of Lines) {
		const M=Ctx.measureText(Line);
		const Ascent =M.actualBoundingBoxAscent  || Size*0.8;
		const Descent=M.actualBoundingBoxDescent || Size*0.2;
		Sizes[Index++]=new LineSize(Line, M.width, Line==='' ? Size : Ascent+Descent);
	}

	return Sizes;
}

function CalcAlign(AlignType:AlignTextType, Outer:number, Inner:number)
{
		return (
			  AlignType===AlignTextType.Beginning	? 0
			: AlignType===AlignTextType.Middle		? (Outer-Inner)/2
			: AlignType===AlignTextType.End			?  Outer-Inner
			: 0
		);
}

//Assumes the circle's bounding box starts at 0,0 and has a diameter matching the shortest side
export function CheckFits_Circle(Width:number, Height:number, LineRects:LineRect[])
{
	const Radius=Math.min(Width, Height)/2;
	const RadiusSq=Radius*Radius;
	const PointFitsCircle=(X:number, Y:number) => {
		const DX=X-Radius;
		const DY=Y-Radius;
		return DX*DX+DY*DY<=RadiusSq;
	};

	for(const {Rect:LR} of LineRects) {
		const Right =LR.X+LR.Width;
		const Bottom=LR.Y+LR.Height;
		if(
			!PointFitsCircle(LR.X,  LR.Y	) ||
			!PointFitsCircle(Right, LR.Y	) ||
			!PointFitsCircle(LR.X,  Bottom	) ||
			!PointFitsCircle(Right, Bottom	)
		)
			return false;
	}

	return true;
}