import $ from 'jquery';
import { Iter, Log, PopupMessage, StatStr, Util, WillBeSet } from './Util/SharedClasses';
import I18NSearch, { FoldedStrings } from './Util/I18NSearch';
import { Window } from './Util/WindowManager';
import { Share } from './Share';
import { type Item } from './CategoriesAndItems';
import LinkedLabel from './LinkedLabel';

class SearchedItem { constructor(public readonly ID:number, public readonly RichText:string) { } }
type SearchFields='ID'|'Description'|'Title'|'Category';

export default class SearchWindow extends Window
{
	//Configurations
	public MaxSearchResults=50;

	//Search text and results
	private _SearchText="";						public get SearchText	()							{ return this._SearchText	; }
	private _HadOverflow=false;					public get HadOverflow	()							{ return this._HadOverflow	; }
	private _SearchedItems:SearchedItem[]=[];	public get SearchedItems(): readonly SearchedItem[] { return this._SearchedItems; }

	protected $Container=$('<div class=SearchContainer>').appendTo(this.$Content);
	protected $NumResults=$('<span class=NumResults>').appendTo(this.$Container);
	protected $SearchBox=$('<input type=search class=\'SearchBox WinButton\' placeholder="-">').appendTo(this.$Container);
	protected $SearchResults=$('<div class="Results ItemContents">').appendTo(this.$Container);
	constructor()
	{
		super({SaveID:'Search', Type:'Search', Width:750, Height:550});

		this.$SearchBox.on('input', () => {
			const NewText=String(this.$SearchBox.val()).trim();
			if(NewText===this.SearchText)
				return;
			this._SearchText=NewText;
			this.RunSearch();
			this.RefreshSearch();
		});

		this.LanguageChanged();
	}

	//Split the search text into terms and find any item that has all search terms
	private FoldedStrings=new Map<Item, FoldedStrings<Record<SearchFields, string>>>();
	public RunSearch()
	{
		//Search item string transformer
		const YieldedMatchedIds=new Set<number>(), MatchingIDs=new Set<number>();
		const Cats=Share.DS.Categories;

		let MyCulture:string=WillBeSet;
		const SearchTransformer=(I:Item):FoldedStrings<Record<SearchFields, string>>|null => {
			//Store the folded string for the current language for future searches
			let ItemFS=this.FoldedStrings.get(I);
			if(!ItemFS)
				this.FoldedStrings.set(I, ItemFS=new FoldedStrings(
					MyCulture, true, {
						ID:String(I.ID),
						Description:new LinkedLabel(I.Description).Init().html(),
						Title:FoldedStrings.EncodeHTMLSimple(I.Title),
						Category:FoldedStrings.EncodeHTMLSimple(Cats.get(I.CategoryID)!.Title),
					},
				));

			//Matching IDs processed first. Make sure they aren’t processed again after that
			if(MatchingIDs.has(I.ID))
				if(YieldedMatchedIds.has(I.ID))
					return null;
				else
					YieldedMatchedIds.add(I.ID);

			return ItemFS;
		};

		//Create the search and exit if no search terms detected
		const DoSearch=new I18NSearch<Item>(FoldedStrings.EncodeHTMLSimple(this.SearchText), SearchTransformer);
		if(!DoSearch.SearchTerms.length)
			return void([this._HadOverflow, this._SearchedItems]=[false, []]);

		//Get a list of items with exact ID matches
		for(const Str of DoSearch.OriginalTerms) {
			const ID=Number.parseInt(Str, 10);
			if(!Number.isNaN(ID) && Share.DS.Items.has(ID))
				MatchingIDs.add(ID);
		}

		//Run the search
		MyCulture=DoSearch.Culture;
		const NewItems=[...new Iter(DoSearch.Execute(
			new Iter(MatchingIDs.values())
				.map(ItemID => Share.DS.Items.get(ItemID)!)
				.concat(Share.DS.Items.values())
			)).take(this.MaxSearchResults+1)
		];

		//Account for overflow
		if((this._HadOverflow=(NewItems.length>this.MaxSearchResults)))
			NewItems.pop();

		//Create a string with sized-down title and Info with highlighted search terms
		const MakeItemInfoLine=(Title:string, Info:string):string|null =>
			!Info ? null : `<span class='FontSize FN2'>${Share.Tr.T(Title, 'ItemFields', true)}</span>: `+Info;

		//Store search results with Item ID and text with search terms highlighted
		this._SearchedItems=NewItems.map(I => {
			const ItemFS=this.FoldedStrings.get(I)!;
			const ColorTag='<span class=SearchHighlight>', ColorEndTag='</span>';
			const ItemStrs=ItemFS.UpdateFromSlices(DoSearch.GetSearchTermPositions(ItemFS.Folded), FindValue => ColorTag+FindValue+ColorEndTag);

			return new SearchedItem(I.ID, [
				 	MakeItemInfoLine(		"Title",		`${ItemStrs.Title} <span class='FontSize FN4'>[${ItemStrs.ID}]</span>`),
					MakeItemInfoLine(		"Category",		ItemStrs.Category),
				I.Description===StatStr.Empty ? null :
					MakeItemInfoLine(		"Description",	ItemStrs.Description),
			].filter(S => S).join(StatStr.NewLine))
		});
	}

	public RefreshSearch()
	{
		this.$SearchResults.children().remove();
		this.$SearchResults.removeClass('DoClip');
		for(const SI of this.SearchedItems) {
			const El=$('<div>')
				.html(SI.RichText)
				.on('click', () => {
					try {
						Share.MC.SelectAndCenterItem(SI.ID);
					} catch(Err) {
						const ErrMsg=StatStr.NeedsTranslate+`Item #${SI.ID} is no longer available: `+Util.GetErrorMessage(Err);
						Log.Error(ErrMsg);
						new PopupMessage(ErrMsg);
					}
				});

			El.add('<div class=Separator>').appendTo(this.$SearchResults);
			if(El.height()!>130)
				El.addClass('Clipped');
		}
		this.$SearchResults.addClass('DoClip');

		this.$NumResults.text(!this._SearchText ? StatStr.Empty : this.SearchedItems.length+(this.HadOverflow ? '+' : StatStr.Empty));
	}

	public override LanguageChanged()
	{
		Share.Tr.OnLanguageLoadedOnce(() => {
			this.Title=Share.Tr.TDef("SearchWindow.Title", undefined, "Search");
			this.$SearchBox.attr('placeholder', Share.Tr.T('Type in here to search'));
			this.FoldedStrings.clear();
			this.RunSearch();
			this.RefreshSearch();
		});
	}

	public override Refresh()
	{
		this.FoldedStrings.clear();
		this.RunSearch();
		this.RefreshSearch();
		super.Refresh();
	}
}