import $ from 'jquery';
import { DevStrings, StatStr } from './Util/SharedClasses';
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
		const SearchText=this.SearchText.trim();
		if(!SearchText) {
			this._HadOverflow=false;
			this._SearchedItems=[];
			return;
		}

		const Terms=SearchText.split(/\s+/g);
		function Matches(Str:string)
		{
			for(const T of Terms)
				if(Str.indexOf(T)===-1)
					return false;
			return true;
		}

		const Cats=Share.DS.Categories;
		const NewItems:Item[]=[];
		for(const I of Share.DS.Items.values()) {
			const SearchText=[
				String(I.ID), I.Description,
				DevStrings.SafeRich(I.Title),
				DevStrings.SafeRich(Cats.get(I.CategoryID)!.Title),
			].join('\uEE06');
			if(Matches(SearchText)) {
				NewItems.push(I);
				if(NewItems.length>=this.MaxSearchResults)
					break;
			}
		}

		//Account for overflow
		if((this._HadOverflow=(NewItems.length>this.MaxSearchResults)))
			NewItems.pop();

		//Transform the items into rich strings and store with their IDs
		this._SearchedItems=NewItems.map(I => new SearchedItem(I.ID, [
			 	SearchWindow.MakeItemInfoLine(			"Title",		DevStrings.SafeRich(I.Title)+` <size=-4>[${I.ID}]</size>`),
				SearchWindow.MakeItemInfoLine(			"Category",		DevStrings.SafeRich(Cats.get(I.CategoryID)!.Title)),
			I.Description===StatStr.Empty ? null :
				SearchWindow.MakeItemInfoLine(			"Description",	I.Description),
		].filter(S => S).join(StatStr.NewLine)));
	}

	//Create a string with sized down title and Info with highlighted search terms
	private static MakeItemInfoLine(Title:string, Info:string):string|null
	{
		if(!Info)
			return null;
		return `<b>${Title}</b>: ${Info}`;
	}

	public RefreshSearch()
	{
		this.$SearchResults.children().remove();
		this.$SearchResults.removeClass('DoClip');
		for(const SI of this.SearchedItems) {
			const El=$('<div>')
				.html(new LinkedLabel(SI.RichText).Init().html())
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