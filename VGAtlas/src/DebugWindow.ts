import $ from 'jquery';
import { Rect, Vector2 } from './Util/SharedClasses';
import GetExtraAssets from './Util/GetExtraAssets';
import { Window } from './Util/WindowManager';
import { Share } from './Share';

const HTMLCode=`
<div>
	<input type=checkbox id=ShowSectionsBox>
	<label for=ShowSectionsBox class=TranslationEl data-translation-section=DebugWindow data-translation-key=ShowSectionsBox data-translation-default="Show map sections"></label>
</div>
<table id=DebugValues></table>
`;

function MakeRow(ID:string, Default:string, Outer='tr', Inner1='th', Inner2='td')
{
	return $(document.createElement(Outer)).append(
		$(document.createElement(Inner1))
			.addClass('TranslationEl')
			.attr('data-translation-section', 'DebugWindow')
			.attr('data-translation-key', ID)
			.attr('data-translation-default', Default),
		$(document.createElement(Inner2))
			.attr('id', ID),
	);
}

const ValuesRows={
	ZoomLevel:			["Zoom"],
	CanvasCoord:		["Canvas coordinate"],
	MapCoord:			["Map coordinate"],
	CurrentMapSection:	["Map sections your mouse is over: ", 'div', 'span', 'span'],
};

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

	private readonly ValueLabels:Record<keyof typeof ValuesRows, JQuery>;
	private LastMousePos?:Vector2;
	private Sections?:SectionsType;
	public get OriginalSections(): Readonly<SectionsType>|undefined { return this.Sections; }

	constructor()
	{
		//Base HTML+translations setup
		super({SaveID:'Debug', Type:'Debug', Width:400, Height:250});
		this.$Content.append(HTMLCode);
		this.LanguageChanged();

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

		//Initialize value rows
		const DV=this.$Content.find('#DebugValues');
		this.ValueLabels=Object.fromEntries(
			Object.entries(ValuesRows).map(([ID, Info]) =>
				[ID, MakeRow(ID, ...(Info as [string, string?, string?, string?])).appendTo(DV).children().eq(1)]
			)
		) as typeof this.ValueLabels;
		this.ValueLabels.CurrentMapSection.parent().appendTo(this.$Content); //Move CurrentMapSection outside the table
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
		function P(Num:number, LeadDigits=3, FixedDigits=6) { return Num.toFixed(FixedDigits).padStart(LeadDigits+FixedDigits+1, ' '); }
		this.LastMousePos=Pos;
		const MapPos=Pos ? Share.MCanvas.CanvasToMap(Pos) : undefined;
		this.ValueLabels.CanvasCoord		.text(!Pos		? '' : `${P(Pos.X, 4, 0)} × ${P(Pos.Y, 4, 0)}`);
		this.ValueLabels.MapCoord			.text(!MapPos	? '' : `${P(MapPos.X)} × ${P(MapPos.Y)}`);
		this.ValueLabels.ZoomLevel			.text(P(Share.MC.ZoomScale, 2, 5));
		this.ValueLabels.CurrentMapSection	.text([...this.GetMouseMapSections(MapPos)].join(', '));
	}

	public *GetMouseMapSections(MapPos?:Vector2)
	{
		if(!MapPos || !this.Sections)
			return;

		const MapRadius=this.ctor.CanvasToMapRadius(NoWidthSectionRadius*Share.MC.ZoomScale);
		for(const [Name, Section] of Object.entries(this.OriginalSections!))
			if(this.ctor.IsMouseOverSection(MapPos, Section, MapRadius))
				yield Name;
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

	public override LanguageChanged()
	{
		Share.Tr.OnLanguageLoadedOnce(() => {
			this.Title=Share.Tr.TDef('Title', 'DebugWindow', "Debug");
		});
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