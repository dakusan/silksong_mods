import './CustomItemWindow.scss';
import { Log, Rect, Util		} from '../../Util/SharedClasses';
import { TranslatePassthrough	} from '../../Util/Translations';
import { Window					} from '../../Util/WindowManager';
import { Share					} from '../../Share';
import LinkedLabel				  from '../../LinkedLabel';
import type CustomItem			  from '../../CustomItem';
import HTMLCode					  from './CustomItemWindow.html?minraw';

const TranslationSection='CustomItems';
const StartWidth=370, StartHeight=445;
const ElLookups={
	Title:'[name=Title]',
	Description:'[name=Description]',
	Label:'[name=Label]',
	IconPreview:'.IconPreview',
	RenderDescription:'.RenderDescription',
	SubmitButton:'.Submit',
};

export default class CustomItemWindow extends Window
{
	private static IssuedRenderErr=false;
	private readonly Els=<Record<keyof typeof ElLookups, JQuery>>{};
	private readonly CustomItemCache:Record<string, CustomItem>={};
	private readonly IconPreviewCanvas:HTMLCanvasElement;
	private LastDrawnItem?:CustomItem;
	constructor(
		X:number, Y:number,
		public readonly CreateCustomItem:(MyLabel:string, Detached:boolean, X?:number, Y?:number, Title?:string, MyDescription?:string) => CustomItem, //Callback avoids a runtime CustomItem import, so this lazy window chunk does not pull in CustomItem from the main graph
	) {
		//Base HTML+translations setup
		const CanvasPos=Share.MCanvas.CanvasPos;
		super({
			Type:'CustomItem',
			Width:StartWidth, Height:StartHeight,
			X:CanvasPos.X+Share.MCanvas.Width /2-StartWidth /2,
			Y:CanvasPos.Y+Share.MCanvas.Height/2-StartHeight/2,
			TitleTranslator:new TranslatePassthrough("WindowTitle.Add", TranslationSection, "Create Custom Icon", Share.Tr),
		});
		this.$Content.append(HTMLCode);
		Share.Tr.UpdateDOMSubElements(this.$Content[0]);

		//Gather the dynamic elements
		for(const [ElName, Selector] of Object.entries(ElLookups))
			this.Els[ElName as keyof typeof ElLookups]=this.$Content.find(Selector);
		this.IconPreviewCanvas=this.Els.IconPreview[0] as HTMLCanvasElement;

		//Update coordinates (3 decimal places)
		this.$Content.find('.XCoordLabel').text(Math.floor(X*1000)/1000);
		this.$Content.find('.YCoordLabel').text(Math.floor(Y*1000)/1000);

		//Fix canvas render size
		const PreviewRect=this.IconPreviewCanvas.getBoundingClientRect();
		this.IconPreviewCanvas.width =Math.round(PreviewRect.width );
		this.IconPreviewCanvas.height=Math.round(PreviewRect.height);

		//Run first initialization
		this.RenderPreview();
		this.CheckIfFormIsValid();
		this.RenderDescription();

		//Hook up input elements
		this.Els.Title		.on('input', () => { this.CheckIfFormIsValid(); });
		this.Els.Description.on('input', () => { this.CheckIfFormIsValid(); this.RenderDescription(); });
		this.Els.Label		.on('input', () => { this.CheckIfFormIsValid(); this.RenderPreview(); });

		//Handle submit
		this.$Content.find('.Cancel').on('click', () => this.Close());
		this.$Content.find('.CustomItemForm').on('submit', e => {
			e.preventDefault();
			if(!this.CheckIfFormIsValid())
				return;

			this.CreateCustomItem(
				String(this.Els.Label.val()!), false, X, Y,
				String(this.Els.Title.val()!),
				String(this.Els.Description.val()!),
			);

			this.Close()
		});
	}

	private RenderPreview()
	{
		const Label=String(this.Els.Label.val()!).trim();
		const CurrentCustomItem=(this.CustomItemCache[Label] ??= this.CreateCustomItem(Label || ' ', true));

		if(CurrentCustomItem===this.LastDrawnItem)
			return;
		try {
			const MySprite=CurrentCustomItem.MySprite;
			const SRI=MySprite.GetRenderInfo()!;
			const Ctx=this.IconPreviewCanvas.getContext('2d')!;
			const DrawRect=new Rect(0, 0, Ctx.canvas.width, Ctx.canvas.height);
			Ctx.clearRect(0, 0, DrawRect.Width, DrawRect.Height);
			Ctx.drawImage(
				SRI.SSV.Vars.Default.Canvas!,
				SRI.ImageRect.X, SRI.ImageRect.Y, SRI.ImageRect.Width, SRI.ImageRect.Height,
				DrawRect.X, DrawRect.Y, DrawRect.Width, DrawRect.Height,
			);
			CurrentCustomItem.DrawSymbol(Ctx, DrawRect);
			this.LastDrawnItem=CurrentCustomItem;
		} catch(e) {
			if(!CustomItemWindow.IssuedRenderErr) {
				CustomItemWindow.IssuedRenderErr=true;
				Log.Error("Failed to render custom item preview: "+Util.GetErrorMessage(e));
			}
		}
	}

	private CheckIfFormIsValid()
	{
		let HasIncomplete=false;
		for(const El of [this.Els.Title, this.Els.Label, this.Els.Description]) {
			const IsIncomplete=!String(El.val()!).trim();
			HasIncomplete ||= IsIncomplete;
			El.toggleClass('Incomplete', IsIncomplete);
		}
		this.Els.SubmitButton.prop('disabled', HasIncomplete).toggleClass('Disabled', HasIncomplete);
		return !HasIncomplete;
	}

	private RenderDescription()
	{
		this.Els.RenderDescription.empty().append(new LinkedLabel(String(this.Els.Description.val()!)).Init());
	}
}