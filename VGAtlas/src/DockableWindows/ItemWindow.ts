import $ from 'jquery';
import { Rect, StatStr, WillBeSet } from '../Util/SharedClasses';
import { Window } from '../Util/WindowManager';
import { Share } from '../Share';
import LinkedLabel from '../LinkedLabel';
import { Item } from '../CategoriesAndItems';

function TSan(Message:string): string { return Share.Tr.TDef(Message, 'ItemFields', Message, true); }

export default class ItemWindow extends Window
{
	private IsAttached=true; private SelfMove=true; private IsInitializing=true;
	public MyLabel:LinkedLabel=WillBeSet;
	constructor(
		public readonly LinkedItem:Item,
	) {
		super({
			Title: `${LinkedItem.Title} [${LinkedItem.ID}]`,
			Width:350,
			MinWidth:60,
			AcceptsKeyboard:false,
		});
		this.UpdateContents();
		this.UpdateAttachedPosition();
		this.AutoSize(Callback => { Callback.call(this, 300, 350); this.IsInitializing=this.SelfMove=false; });
	}
	private UpdateContents()
	{
		const Cat=Share.DS.Categories.get(this.LinkedItem.CategoryID)!;
		this.$Content.addClass('ItemContents').append($('<div>').append(
			//Title
			$('<div class=Title><span class=Key>'+TSan("Title")+'</span>: </div>').append(
				this.LinkedItem.IconID===-1 ? null! : $(`<span class='ItemIcon I${this.LinkedItem.IconID}'></span>`),
				$('<span class=Value>').text(this.LinkedItem.Title),
			),

			//Category
			$('<div class=Category><span class=Key>'+TSan("Category")+'</span>: </div>').append(
				$(`<span class='ItemIcon I${Cat.IconID}'></span>`),
				$('<span class=Value>').text(Cat.Title),
			),

			//Description and other links
			(this.MyLabel=new LinkedLabel(
				this.LinkedItem.Description+(
					((this.LinkedItem.OtherLinks?.length ?? 0)<=0) ? StatStr.Empty :
						StatStr.NewLine+TSan("Links")+': <b>'
						+this.LinkedItem.OtherLinks!.join(Share.Tr.TDef("SEP_AND", 'ItemFields', ", ", true))
						+'</b>'
				)
			)).Init(),

			//Images
			...(this.LinkedItem.ImageURLs?.map(Src =>
				$('<img alt=Screenshot src=\'\'>').attr('src', Src)
			) ?? []),
		));
	}
	public UpdateAttachedPosition()
	{
		if(!this.IsAttached)
			return;
		this.SelfMove=true;
		const NewPos=Share.MCanvas.MapToCanvas(this.LinkedItem.Pos).Add(Share.MCanvas.CanvasPos);
		this.UpdateBounds({X:NewPos.X, Y:NewPos.Y}, true);
		const IsVis=new Rect(0, 0, document.documentElement.clientWidth, document.documentElement.clientHeight).Intersects(this.Bounds);
		if(this.Visible!==IsVis) {
			this.Visible=IsVis;
			this.UpdateBounds({X:NewPos.X, Y:NewPos.Y}, true);
		}
		this.SelfMove=this.IsInitializing;
	}
	public override LanguageChanged()
	{
		this.$Content.children().remove();
		this.UpdateContents();
	}

	public override OnMoved()
	{
		if(this.SelfMove || !this.IsAttached)
			return;
		this.IsAttached=false;
		this.$Root.addClass('Detached');
	}
	public ItemUnselected()
	{
		if(this.IsAttached)
			this.Close();
	}
}