import  { DevStrings, Iter, PreallocatedPusher, StatStr } from './SharedClasses';

export type FuncItemTransformer<T>=(Item:T) => string|null; //Return null to skip searching item
export type ReplaceWithFunc=(FindIndex:number, FindValue:string) => string|null; //Index is based off the original string
class FoldedString { constructor(public readonly Folded:string, public readonly OrigStarts:number[]) {} }

//Searching with i18n. If using the array constructor make sure StartSearchTerms have been run through SafeRich (if UseSafeRich=true)
export default class I18NSearch<T>
{
	public static readonly RegEx_RemoveHTMLTags=/<[^>]+>/g;
	public static readonly RegEx_SplitAroundSpaces=/\s+/g;
	public static readonly RegEx_IsCombiningMark=/\p{M}/u;

	public readonly OriginalTerms:string[];
	private readonly Terms:string[]=[];
	public get SearchTerms(): readonly string[] { return this.Terms; }

	public get Culture() { return this._Culture; }
	public set Culture(NewCulture:string)
	{
		if(this._Culture===NewCulture)
			return;
		this._Culture=NewCulture;
		this.Terms.length=0;
		this.Terms.push(...I18NSearch.FormatSearchTerms(this.OriginalTerms, NewCulture));
	}

	public constructor(
		SearchTextOrTerms:string|string[],
		public readonly ItemTransformer:FuncItemTransformer<T>=(Str => Str?.toString() ?? StatStr.Empty),
		public readonly UseSafeRich=true,
		private _Culture:string=Intl.DateTimeFormat().resolvedOptions().locale || 'en-US'
	) {
		const StartSearchTerms=Array.isArray(SearchTextOrTerms)
			? SearchTextOrTerms
			: I18NSearch.RegEx_SplitAroundSpaces[Symbol.split](SearchTextOrTerms);

		this.OriginalTerms=[...StartSearchTerms];
		this.Terms.push(...I18NSearch.FormatSearchTerms(StartSearchTerms, _Culture));
	}

	public Execute(SearchItems:Iterable<T>): Iterable<T>
	{
		return this.Terms.length===0 ? SearchItems : new Iter(SearchItems).filter(V => this.SearchItemMatches(V));
	}

	public SearchItemMatches(Item:T): boolean
	{
		//Get the fixed up search term (return early if null)
		let ItemStr=this.ItemTransformer(Item);
		if(ItemStr===null)
			return false;
		if(this.UseSafeRich)
			ItemStr=DevStrings.HtmlToText(ItemStr.replace(I18NSearch.RegEx_RemoveHTMLTags, '\uEE07'));
		ItemStr=this.NormalizeForSearch(ItemStr);

		//If any search terms do not match, return false
		for(const Term of this.Terms)
			if(!ItemStr.includes(Term))
				return false;

		//All search terms matched
		return true;
	}

	public NormalizeForSearch(Str:string) { return I18NSearch.NormalizeForSearch(Str, this.Culture); }
	public static NormalizeForSearch(Str:string, Culture:string): string
	{
		const Norm=Str.normalize('NFKD');
		const Buf:string[]=new Array(Norm.length);
		let Pos=0;
		for(const C of Norm)
			if(!I18NSearch.RegEx_IsCombiningMark.test(C))
				Buf[Pos++]=C.toLocaleUpperCase(Culture);

		Buf.length=Pos;
		return Buf.join('');
	}

	private static *FormatSearchTerms(SearchTerms:string[], Culture:string): Iterable<string>
	{
		const Seen=new Set<string>();
		for(const Str of SearchTerms) {
			const Fixed=I18NSearch.NormalizeForSearch(Str, Culture).trim();
			if(!Fixed || Seen.has(Fixed))
				continue;
			Seen.add(Fixed);
			yield Fixed;
		}
	}

	public ReplaceSearchTerms(Haystack:string, ReplaceWith:ReplaceWithFunc)
	{
		return I18NSearch.ReplaceSearchTerms(Haystack, this.Terms, ReplaceWith, this.Culture);
	}

	/*
	Replaces occurrences of any term in SearchTerms found in Haystack using the same matching semantics as the search:
	IgnoreCase+IgnoreNonSpace (diacritic-insensitive), returning the caller-provided replacement for each match.
	Notes:
	  - Longest-match wins at the same index.
	  - Never produces overlapping matches: after a match, scanning continues at matchEnd.
	*/
	public static ReplaceSearchTerms(Haystack:string, SearchTerms:readonly string[], ReplaceWith:ReplaceWithFunc, Culture:string, NormalizeSearchTerms=false): string
	{
		//Parameter checks
		if(Haystack		==null) throw new Error('Haystack is null');
		if(SearchTerms	==null) throw new Error('SearchTerms is null');
		if(ReplaceWith	==null) throw new Error('ReplaceWith is null');
		if(Culture		==null) throw new Error('Culture is null');
		const TermCount=SearchTerms.length;
		if(TermCount===0 || Haystack.length===0)
			return Haystack;

		const FoldedHaystack=I18NSearch.BuildFoldedString(Haystack, Culture);

		//Precompute per-term next match index (cursor). -1 means no further matches
		const NextIndexes	:number[]=new Array(TermCount);
		const NextLens		:number[]=new Array(TermCount);
		const FoldedTerms	:string[]=new Array(TermCount);
		for(let Index=0; Index<TermCount; Index++) {
			const FoldedTerm=FoldedTerms[Index]=(NormalizeSearchTerms ? I18NSearch.NormalizeForSearch(SearchTerms[Index], Culture) : SearchTerms[Index]);
			NextIndexes[Index]=
				  (NextLens[Index]=FoldedTerm.length)===0 ? -1
				: FoldedHaystack.Folded.indexOf(FoldedTerm, 0);
		}

		//No matches at all
		if(!NextIndexes.some(I => I>-1))
			return Haystack;

		//Run the search/replacements
		const Out:string[]=[];
		let CurPos=0;
		while(true) {
			//Pick earliest next match
			let BestTerm=-1, BestAt=Number.MAX_SAFE_INTEGER, BestLen=0;
			for(let TermIndex=0; TermIndex<NextIndexes.length; TermIndex++) {
				const TermPos=NextIndexes[TermIndex], OTermPos=FoldedHaystack.OrigStarts[TermPos];
				if(
					   (TermPos<0 ? Number.MAX_SAFE_INTEGER : OTermPos)<BestAt			//TermPos inclusive between 0 and BestAt-1
					|| (TermPos>-1 && OTermPos===BestAt && NextLens[TermIndex]>BestLen)	//On tie, prefer the longest term
				)
					[BestAt, BestTerm, BestLen]=[OTermPos, TermIndex, NextLens[TermIndex]];
			}

			//No more matches
			if(BestTerm<0) {
				Out.push(Haystack.slice(CurPos));
				break;
			}

			//Add leading text and replacement from user
			if(BestAt>CurPos)
				Out.push(Haystack.slice(CurPos, BestAt));
			const NextFoldedPos=NextIndexes[BestTerm]+BestLen;
			CurPos=FoldedHaystack.OrigStarts[NextFoldedPos] ?? Haystack.length;
			Out.push(ReplaceWith(BestAt, Haystack.slice(BestAt, CurPos)) ?? StatStr.Empty);

			//Break when string exhausted
			if(CurPos>=Haystack.length)
				break;

			//Advance cursors
			for(let TermIndex=0; TermIndex<NextIndexes.length; TermIndex++)
				if(NextIndexes[TermIndex]>-1 && FoldedHaystack.OrigStarts[NextIndexes[TermIndex]]<CurPos)
					NextIndexes[TermIndex]=FoldedHaystack.Folded.indexOf(FoldedTerms[TermIndex], NextFoldedPos);
		}

		return Out.join(StatStr.Empty);
	}

	private static BuildFoldedString(Str:string, Culture:string)
	{
		const FoldedBuf=new PreallocatedPusher<string>(Str.length);
		const OrigStarts=new PreallocatedPusher<number>(Str.length);
		for(let I=0; I<Str.length;) {
			const C=Str.codePointAt(I)!>0xFFFF ? Str.slice(I, I+2) : Str[I];
			const Norm=C.normalize('NFKD');
			for(const NC of Norm) {
				if(I18NSearch.RegEx_IsCombiningMark.test(NC))
					continue;
				FoldedBuf.push(NC.toLocaleUpperCase(Culture));
				OrigStarts.push(I);
			}
			I+=C.length;
		}

		return new FoldedString(FoldedBuf.finalize.join(''), OrigStarts.finalize);
	}
}