import $ from 'jquery';
import { DevStrings, Iter, StatStr } from './Util/SharedClasses';
import I18NSearch from './Util/I18NSearch';
import { Window } from './Util/WindowManager';
import { Share } from './Share';
import { type Item } from './CategoriesAndItems';
import LinkedLabel from './LinkedLabel';

class SearchedItem { constructor(public readonly ID:number, public readonly RichText:string) { } }

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
	protected $SearchResults=$('<div class=Results>').appendTo(this.$Container);
	constructor()
	{
		super({SaveID:'Search', Width:750, Height:550});

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
	public RunSearch()
	{
		//Search item string transformer
		const YieldedMatchedIds=new Set<number>(), MatchingIDs=new Set<number>();
		const Cats=Share.DS.Categories;

		const SearchTransformer=(I:Item):string|null => {
			if(MatchingIDs.has(I.ID))
				if(YieldedMatchedIds.has(I.ID))
					return null;
				else
					YieldedMatchedIds.add(I.ID);
			return [
				String(I.ID), I.Description,
				DevStrings.SafeRich(I.Title),
				DevStrings.SafeRich(Cats.get(I.CategoryID)!.Title),
			].join('\u0088');
		};

		//Get a list of items with exact ID matches
		const DoSearch=new I18NSearch<Item>(this.SearchText, SearchTransformer);
		for(const Str of DoSearch.OriginalTerms) {
			const ID=Number.parseInt(Str, 10);
			if(!Number.isNaN(ID) && Share.DS.Items.has(ID))
				MatchingIDs.add(ID);
		}

		//Run the search
		const NewItems=[...new Iter(DoSearch.Execute(
			new Iter(MatchingIDs.values())
				.map(ItemID => Share.DS.Items.get(ItemID)!)
				.concat(Share.DS.Items.values())
			)).take(this.MaxSearchResults+1)
		];

		//Account for overflow
		if((this._HadOverflow=(NewItems.length>this.MaxSearchResults)))
			NewItems.pop();

		//Transform the items into rich strings and store with their IDs
		this._SearchedItems=NewItems.map(I => new SearchedItem(I.ID, [
			 	SearchWindow.MakeItemInfoLine(			"Title",		DevStrings.SafeRich(I.Title)+` <size=-4>[${I.ID}]</size>`,	DoSearch),
				SearchWindow.MakeItemInfoLine(			"Category",		DevStrings.SafeRich(Cats.get(I.CategoryID)!.Title),			DoSearch),
			I.Description===StatStr.Empty ? null :
				SearchWindow.MakeItemInfoLine(			"Description",	I.Description,												DoSearch),
		].filter(S => S).join(StatStr.NewLine)));
	}

	//Create a string with sized down title and Info with highlighted search terms
	private static MakeItemInfoLine(Title:string, Info:string, DoSearch:I18NSearch<Item>):string|null
	{
		if(!Info)
			return null;

		const TagEnum=Info.matchAll(I18NSearch.RegEx_RemoveHTMLTags);
		let CurTag=TagEnum.next();
		function InTag(Pos:number)
		{
			while(!CurTag.done) {
				if(Pos<CurTag.value.index)
					return false;
				if(Pos<CurTag.value.index+CurTag.value[0].length)
					return true;
				CurTag=TagEnum.next();
			}
			return false;
		}

		const ColorTag=`<b><color=SEARCH_HIGHLIGHT>`, ColorEndTag='</color></b>';
		return `<size=-2>${Share.Tr.T(Title, 'ItemFields', true)}</size>: `+DoSearch.ReplaceSearchTerms(
			Info,
			(FindIndex, FindValue) => InTag(FindIndex) ? FindValue : ColorTag+FindValue+ColorEndTag
		);
	}

	public RefreshSearch()
	{
		this.$SearchResults.children().remove();
		this.$SearchResults.removeClass('DoClip');
		for(const SI of this.SearchedItems) {
			const El=$('<div>')
				.html(new LinkedLabel(SI.RichText).Init().html().replace(/<span style="color:SEARCH_HIGHLIGHT">/g, '<span class=SearchHighlight>'))
				.on('click', () => Share.MC.SelectAndCenterItem(SI.ID));

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
			this.RunSearch();
			this.RefreshSearch();
		});
	}
}