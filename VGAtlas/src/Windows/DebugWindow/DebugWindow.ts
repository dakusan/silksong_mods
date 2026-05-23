import './DebugWindow.scss';
import $						  from 'jquery';
import {Rect, StatStr, Vector2} from '../../Util/SharedClasses';
import GetExtraAssets			  from '../../Util/GetExtraAssets';
import { TranslatePassthrough	} from '../../Util/Translations';
import { Window					} from '../../Util/WindowManager';
import { Share					} from '../../Share';
import HTMLCode					  from './DebugWindow.html?minraw';

const ValuesRows=['ZoomLevel', 'MapBounds', 'MapBox', 'CanvasCoord', 'MapCoord', 'CurrentMapSection'] as const;

const FillTransparency=(112).toString(16);
const NoWidthSectionColor='#333333'; //Generally always overlayed over other colors
const NoWidthSectionRadius=8;
const SectionColors=[
	'#FF00FF', //Pure magenta		(rare in map)
	'#00FFFF', //Pure cyan			(very clean contrast)
	'#FF0055', //Hot pink/red		(cuts through warm regions)
	'#00FF66', //Neon green			(brighter than map greens)
	'#6600FF', //Deep violet		(map has almost none)
	'#FFD400', //High-value yellow	(only bright yellow present)
	'#FFFFFF', //White				(last resort, overlaps light regions)
];

type SectionNumbersWithWidth=readonly [X:number, Y:number, Width:number, Height:number, ColorIndex:number];
type SectionNumbersNoWidth	=readonly [X:number, Y:number];
type SectionNumbers=SectionNumbersWithWidth|SectionNumbersNoWidth;
type SectionsType=Record<string, SectionNumbers>;

export default class DebugWindow extends Window
{
	private static ShowSections=false; //Persists between windows
	public get ctor() { return DebugWindow; }

	private readonly ValueLabels:Record<typeof ValuesRows[number], JQuery>;
	private LastMousePos?:Vector2;
	private Sections?:SectionsType;
	public get OriginalSections(): Readonly<SectionsType>|undefined { return this.Sections; }

	constructor()
	{
		//Base HTML+translations setup
		super({
			SaveID:'Debug', Type:'Debug', Width:460, Height:295,
			TitleTranslator:new TranslatePassthrough('Title', 'DebugWindow', "Debug", Share.Tr),
		});
		this.$Content.append(HTMLCode)[0].dataset.translationSection='DebugWindow';

		//Toggle show sections
		const ShowSectionsBox=this.$Content.find('#ShowSectionsBox');
		ShowSectionsBox.prop('checked', this.ctor.ShowSections);
		if(this.ctor.ShowSections)
			this.ToggleShowSections();
		ShowSectionsBox.on('change', () => {
			this.ctor.ShowSections=ShowSectionsBox.prop('checked');
			this.ToggleShowSections();
		});

		//Canvas events
		Share.MCanvas.Events.MouseMove	.Add('Debug.DisplayMouseMapSections'		,		this.DisplayMouseMapSections.bind(this));
		Share.MCanvas.Events.MouseLeave	.Add('Debug.DisplayMouseMapSections_Leave'	,		this.DisplayMouseMapSections.bind(this));
		Share.MCanvas.Events.Draw		.Add('Debug.DisplayMouseMapSections_Draw'	, () =>	this.DisplayMouseMapSections(this.LastMousePos));
		setTimeout(() => this.DisplayMouseMapSections(), 0);

		//Initialize value rows
		this.ValueLabels=Object.fromEntries(ValuesRows.map(ID => [ID, $('#'+ID)])) as typeof this.ValueLabels;
		Share.Tr.UpdateDOMSubElements(this.$Content[0]);

		//Load in the scene data
		(GetExtraAssets.LoadJson('Assets/SceneRects.json', false, Str => Str.replaceAll('\t', '')) as Promise<SectionsType>)
			.then(this.CompleteInit.bind(this));
	}

	private CompleteInit(MVs:SectionsType)
	{
		this.Sections=MVs;
		if(this.ctor.ShowSections)
			Share.MCanvas.Refresh();
	}

	private ToggleShowSections()
	{
		if(this.ctor.ShowSections)
			Share.MCanvas.Events.Draw.Add	('Debug.ShowSections', this.DrawSections.bind(this), 'GameObjects');
		else
			Share.MCanvas.Events.Draw.Remove('Debug.ShowSections');
		Share.MCanvas.Refresh();
	}

	private DisplayMouseMapSections(Pos?:Vector2)
	{
		function P(Num:number, LeadDigits=4, FixedDigits=3) { return Num.toFixed(FixedDigits).padStart(LeadDigits+FixedDigits+1, ' '); }
		this.LastMousePos=Pos;
		const MapPos=Pos ? Share.MCanvas.CanvasToMap(Pos) : undefined;
		const ULBound=Share.MCanvas.CanvasToMap(new Vector2(0, 0));
		const LRBound=Share.MCanvas.CanvasToMap(new Vector2(Share.MCanvas.Width, Share.MCanvas.Height));
		const MapSize=new Vector2(LRBound.X-ULBound.X, LRBound.Y-ULBound.Y);
		this.ValueLabels.CanvasCoord		.text(!Pos		? '' : `${P(Pos   .X, 4, 0)} × ${P(Pos   .Y, 4, 0)}`);
		this.ValueLabels.MapCoord			.text(!MapPos	? '' : `${P(MapPos.X, 4, 6)} × ${P(MapPos.Y, 4, 6)}`);
		this.ValueLabels.ZoomLevel			.text(P(Share.MC.ZoomScale, 2, 5));
		this.ValueLabels.MapBounds			.text([
			P(ULBound.X),				'×' ,
			P(ULBound.Y),				', ',
			P(LRBound.X),				'×' ,
			P(LRBound.Y)
		].join(StatStr.Empty));
		this.ValueLabels.MapBox				.text([
			P(ULBound.X+MapSize.X/2),	'×' ,
			P(ULBound.Y+MapSize.Y/2),	', ',
			P(MapSize.X),				'×' ,
			P(MapSize.Y)
		].join(StatStr.Empty));
		this.ValueLabels.CurrentMapSection	.empty().append(
			[...this.GetMouseMapSections(MapPos)].flatMap(
				El => [El, document.createTextNode(', ')] //Comma separators
			).slice(0, -1) //Remove the dangling comma
		);
	}

	public *GetMouseMapSections(MapPos?:Vector2)
	{
		if(!MapPos || !this.Sections)
			return;

		const MapRadius=this.ctor.CanvasToMapRadius(NoWidthSectionRadius*Share.MC.ZoomScale);
		for(const [Name, Section] of Object.entries(this.OriginalSections!))
			if(this.ctor.IsMouseOverSection(MapPos, Section, MapRadius))
				yield $(document.createElement('span')).text(Name).css('color', SectionColors[Section[4] ?? -1] ?? '#BBBBBB');
	}

	public static CanvasToMapRadius(CanvasRadius:number)
	{
		const S1=CanvasRadius/Math.sqrt(2);
		const P1=Share.MCanvas.CanvasToMap(new Vector2(0, 0));
		const P2=Share.MCanvas.CanvasToMap(new Vector2(S1, S1));
		return Math.hypot(P2.X-P1.X, P2.Y-P1.Y);
	}

	public static IsMouseOverSection(MapPos:Vector2, Section:SectionNumbers, MapRadius:number): boolean
	{
		//Handle rectangles
		const [X, Y, Width, Height]=Section;
		if(Width)
			return MapPos.X>=X
				&& MapPos.Y>=Y
				&& MapPos.X<=X+Width
				&& MapPos.Y<=Y+Height!;

		//Handle no-width section via radius
		const XDiff=MapPos.X-X, YDiff=MapPos.Y-Y;
		return XDiff*XDiff+YDiff*YDiff<=MapRadius*MapRadius;
	}

	private DrawSections(Ctx:CanvasRenderingContext2D)
	{
		if(!this.Sections)
			return;

		const NoWidthSections:Vector2[]=[]; //These are drawn last
		for(const [_Name, Section] of Object.entries(this.Sections)) {
			const [X, Y, Width, Height, ColorIndex]=this.ctor.SectionToCanvas(Section);
			Ctx.fillStyle=!Width ? '' : (SectionColors[ColorIndex!] ?? '#FFFFFF')+FillTransparency;
			if(!Width) //Store no width sections for later drawing
				NoWidthSections.push(new Vector2(X, Y));
			else //Draw the rectangle
				Ctx.fillRect(X, Y, Width, Height!);
		}

		//Draw no width sections
		Ctx.fillStyle=NoWidthSectionColor+FillTransparency;
		for(const NWS of NoWidthSections) {
			Ctx.beginPath();
			Ctx.arc(NWS.X, NWS.Y, NoWidthSectionRadius*Share.MC.ZoomScale, 0, Math.PI*2);
			Ctx.fill();
		}
	}

	public static SectionToCanvas<T extends SectionNumbers>(Section:T): T
	{
		const [X, Y, Width, Height, ColorIndex]=Section;
		const StartPos=Share.MCanvas.MapToCanvas(new Vector2(X, Y));
		if(Width===undefined)
			return [StartPos.X, StartPos.Y] as SectionNumbersNoWidth as T;

		//Determine the canvas rectangle
		const EndPos=Share.MCanvas.MapToCanvas(new Vector2(X+Width, Y+Height!));
		const MyRect=new Rect(StartPos.X, StartPos.Y, EndPos.X-StartPos.X, EndPos.Y-StartPos.Y);
		if(MyRect.Height<0) {
			MyRect.Y+=MyRect.Height;
			MyRect.Height*=-1;
		}
		return [MyRect.X, MyRect.Y, MyRect.Width, MyRect.Height, ColorIndex!] as SectionNumbersWithWidth as T;
	}

	public override OnClosing(): boolean
	{
		const MCan=Share.MCanvas;
		MCan.Events.MouseMove	.Remove('Debug.DisplayMouseMapSections');
		MCan.Events.MouseLeave	.Remove('Debug.DisplayMouseMapSections_Leave');
		MCan.Events.Draw		.Remove('Debug.DisplayMouseMapSections_Draw');
		if(this.ctor.ShowSections) {
			MCan.Events.Draw	.Remove('Debug.ShowSections');
			MCan.Refresh();
		}
		return false;
	}
}