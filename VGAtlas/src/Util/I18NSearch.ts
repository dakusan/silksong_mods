import  { Iter, PreallocatedPusher, StatStr, WillBeSet } from './SharedClasses';

export class StringSlicePos
{
	constructor(
		public readonly Start:number,
		public readonly End:number,
	) { }
}
type StartHSTypes=string|unknown[]|Record<string, unknown>;
type RetTypes<T extends StartHSTypes>=T extends string ? string : T extends unknown[] ? string[] : Record<string, string>;

/*Creates a normalized mapping of the given haystack strings for searching.
String normalization process:
 - Same process as I18NSearch.NormalizeForSearch
 - Optionally has HTML tags removed
	- If you use this, it is highly recommended both your search needles and haystacks match the encodings from EncodeHTMLSimple for non-html tags
*/
export class FoldedStrings<StartHSType extends StartHSTypes>
{
	public		readonly Haystacks		:readonly string[];
	//JoinedHaystacks=Haystacks.join(FoldedStrings.SectionSeparator)
	protected	readonly HaystacksPos	:readonly StringSlicePos[]; //Where the haystacks are in JoinedHaystacks
	public		readonly FoldedMap		:Readonly<Uint8Array|Uint16Array|Uint32Array>; //Map Folded position back onto JoinedHaystacks positions
	public		readonly Folded			:string; //JoinedHaystacks after normalization
	private /*readonly*/ ObjNames		:StartHSType extends string ? undefined : StartHSType extends unknown[] ? number : string[]=WillBeSet; //If HaystackStrOrObj is a ... then this is: Object=object keys; Array=array length; string=undefined

	private static GetCPoint(Str:string, PointInStr:number): string { return Str.codePointAt(PointInStr)!>0xFF_FF ? Str.slice(PointInStr, PointInStr+2) : Str[PointInStr]; }
	public static readonly SectionSeparator='\u0087'; //If found in search string, this character will be replaced with StatStr.PrivateChar

	constructor(
		Culture:string,
		RemoveHTML:boolean,				//If true, anything in between < and > inclusively is removed for searching and considered word boundaries. However, HTML tags are maintained in returns from UpdateFromSlices()
		HaystackStrOrObj:StartHSType	//String(s) to search through. If an object, all Object.values are turned into strings for searching
	) {
		//Generate Haystacks:string[] (and this.ObjNames) from HaystackStrOrObj
			 if(typeof(HaystackStrOrObj)!=='object')(this as FoldedStrings<string					>).ObjNames=undefined;
		else if(Array.isArray(HaystackStrOrObj))	(this as FoldedStrings<unknown[]				>).ObjNames=-1;
		else										(this as FoldedStrings<Record<string, unknown>	>).ObjNames=Object.keys(HaystackStrOrObj ?? {});
		const Haystacks=
			   typeof(HaystackStrOrObj)!=='object'	? [String(HaystackStrOrObj)]
			: (Array.isArray(HaystackStrOrObj)		? HaystackStrOrObj.map(String)
			: (this as FoldedStrings<Record<string, unknown>>).ObjNames.map(Key => String(HaystackStrOrObj[Key]))
			);

		//Get Haystacks Starts and Ends
		const HaystacksPos=new Array<StringSlicePos>(Haystacks.length);
		for(let i=0, Pos=0; i<Haystacks.length; i++, Pos+=FoldedStrings.SectionSeparator.length)
			HaystacksPos[i]=new StringSlicePos(Pos, Pos+=Haystacks[i].length);
		this.Haystacks=Haystacks;
		this.HaystacksPos=HaystacksPos;

		//Create Folded and FoldedMap
		const JoinedHaystacks=Haystacks.join(FoldedStrings.SectionSeparator);
		const FoldedBuf=new PreallocatedPusher<string>(JoinedHaystacks.length);
		const FoldedMap=new PreallocatedPusher<number>(JoinedHaystacks.length);
		for(let StrPos=0; StrPos<JoinedHaystacks.length;) {
			let C=FoldedStrings.GetCPoint(JoinedHaystacks, StrPos);

			//Remove HTML tags when requested
			if(RemoveHTML && C==='<') {
				const StartPos=StrPos;
				do {
					StrPos+=C.length;
					C=FoldedStrings.GetCPoint(JoinedHaystacks, StrPos)
				} while(StrPos<JoinedHaystacks.length && C!=='>');
				FoldedBuf.push(FoldedStrings.SectionSeparator);
				FoldedMap.push(StartPos);
				StrPos+=(C?.length ?? 0);
				continue;
			}

			//Emit normalized character(s)
			for(const NC of C.normalize('NFKD')) {
				if(I18NSearch.RegEx_IsCombiningMark.test(NC))
					continue;
				const Out=NC.toLocaleUpperCase(Culture);
				FoldedBuf.push(Out);
				//eslint-disable-next-line @typescript-eslint/prefer-for-of -- This has to be normal for loop as foreach would loop over the Unicode code points
				for(let j=0; j<Out.length; j++)
					FoldedMap.push(StrPos);
			}
			StrPos+=C.length;
		}

		//Store final copies of Folded and FoldedMap
		this.Folded=FoldedBuf.finalize.join(StatStr.Empty);
			 if(JoinedHaystacks.length<(1<<8 ))	this.FoldedMap=new Uint8Array (FoldedMap.finalize);
		else if(JoinedHaystacks.length<(1<<16))	this.FoldedMap=new Uint16Array(FoldedMap.finalize);
		else									this.FoldedMap=new Uint32Array(FoldedMap.finalize);
	}

	private AppendOrigSlice(
		HaystackResults:string[], CurrentHaystackParts:PreallocatedPusher<string>, JoinedHaystacks:string,
		OrigStart:number, OrigEnd:number, LastHaystackIndex:number, CurHaystackIndex:number
	): void {
		for(let i=LastHaystackIndex; i<=CurHaystackIndex; i++) {
			const PartStart=Math.max(OrigStart, this.HaystacksPos[i].Start);
			const PartEnd=Math.min(OrigEnd, this.HaystacksPos[i].End);
			if(PartStart<PartEnd)
				CurrentHaystackParts.push(JoinedHaystacks.slice(PartStart, PartEnd));

			if(i<CurHaystackIndex) {
				HaystackResults[i]=CurrentHaystackParts.FinalizeSlice.join(StatStr.Empty);
				CurrentHaystackParts.ResetLen();
			}
		}
	}

	//Returns the original haystacks (in their original shape) with search terms (via StringSlicePos) replaced from the ReplaceCB callback
	//Note: You should generally only be using StringSlicePos generated through I18NSearch.GetSearchTermPositions() for this specific FoldedStrings.Folded
	public UpdateFromSlices(Slices:readonly StringSlicePos[], ReplaceCB:(this:FoldedStrings<StartHSType>, Term:string, SP:StringSlicePos) => string): RetTypes<StartHSType>
	{
		function VerifyType<VT extends string|string[]|Record<string, string>>(_Me:FoldedStrings<VT>, Var:VT): VT { return Var; }
		const FormatReturn=(Result:string[], MakeCopy=false) =>
			(
				    typeof(this.ObjNames)==='number' ?	VerifyType(this as FoldedStrings<string[]				>, MakeCopy ? [...Result] : Result)
				: ( this.ObjNames!==undefined ?			VerifyType(this as FoldedStrings<Record<string, string>	>, Object.fromEntries(this.ObjNames.map((Key, Index) => [Key, Result[Index]])))
				:										VerifyType(this as FoldedStrings<string					>, Result[0])
			)) as RetTypes<StartHSType>; //Had to force the type here. No other way to do it

		if(Slices.length===0)
			return FormatReturn(this.Haystacks as string[], typeof(this.ObjNames)==='number'); //Make a copy if returning the array directly
		if(Slices[0].Start<0)
			throw new Error("First slice must start at >=0");

		const JoinedHaystacks=this.Haystacks.join(FoldedStrings.SectionSeparator);
		const HaystackResults=new Array<string>(this.Haystacks.length).fill(StatStr.Empty);
		const CurrentHaystackParts=new PreallocatedPusher<string>(Slices.length*2+1);
		let LastOrigEnd=0, LastHaystackIndex=0;
		for(let i=0; i<Slices.length; i++) {
			const Slice=Slices[i];
			if(Slice.Start>=Slice.End)
				throw new Error(StatStr.NeedsTranslate+`Slice end (${Slice.End}) must be greater than slice start (${Slice.Start})`);
			else if(i>0 && Slice.Start<Slices[i-1].End)
				throw new Error(StatStr.NeedsTranslate+`Slice start (${Slice.Start}) must not be less than end of previous slice (${Slices[i-1].End})`);
			else if(Slice.End>this.Folded.length)
				throw new Error(StatStr.NeedsTranslate+`Slice.End (${Slice.End}) cannot be greater than Folded string length (${this.Folded.length})`);

			const OrigStart=this.FoldedMap[Slice.Start];
			const OrigEnd=this.FoldedMap[Slice.End] ?? JoinedHaystacks.length;

			//Get Haystack Index From StrPos
			let CurHaystackIndex=-1;
			for(let i=LastHaystackIndex; i<this.HaystacksPos.length; i++)
				if(OrigStart<=this.HaystacksPos[i].End) {
					CurHaystackIndex=i;
					break;
				}

			//Slices must stay within a single original source string; folded section separators are boundaries
			if(CurHaystackIndex<0 || OrigEnd>this.HaystacksPos[CurHaystackIndex].End)
				throw new Error(StatStr.NeedsTranslate+`Replacement section (${OrigStart}-${OrigEnd}) cannot span multiple source strings`);

			this.AppendOrigSlice(HaystackResults, CurrentHaystackParts, JoinedHaystacks, LastOrigEnd, OrigStart, LastHaystackIndex, CurHaystackIndex);
			CurrentHaystackParts.push(ReplaceCB.call(this, JoinedHaystacks.slice(OrigStart, OrigEnd), Slice));
			LastOrigEnd=OrigEnd;
			LastHaystackIndex=CurHaystackIndex;
		}
		this.AppendOrigSlice(HaystackResults, CurrentHaystackParts, JoinedHaystacks, LastOrigEnd, JoinedHaystacks.length, LastHaystackIndex, this.Haystacks.length-1);
		HaystackResults[this.Haystacks.length-1]=CurrentHaystackParts.finalize.join(StatStr.Empty);

		return FormatReturn(HaystackResults);
	}

	//HTML encodes 3 characters: & < >
	private static readonly RegEx_LTGT=/[<>]/g;
	public static EncodeHTMLSimple(Str:string): string
	{
		return Str
			.replaceAll('&', '&amp;')
			.replace(this.RegEx_LTGT, F => F==='<' ? '&lt;': '&gt;');
	}
}

//Searching with i18n.
export default class I18NSearch<T>
{
	private static readonly RegEx_SplitAroundSpaces=/\s+/g;
	public static readonly RegEx_IsCombiningMark=/\p{M}/u;

	public readonly OriginalTerms:readonly string[];
	private readonly Terms:string[]=[];
	public get SearchTerms(): readonly string[] { return this.Terms; }

	private _Culture:string=WillBeSet;
	public get Culture(): string { return this._Culture; }
	public set Culture(NewCulture:string)
	{
		if(this._Culture===NewCulture)
			return;
		this._Culture=NewCulture;

		//Format search terms
		this.Terms.length=0;
		const Seen=new Set<string>();
		for(const Term of this.OriginalTerms) {
			const Fixed=I18NSearch.NormalizeForSearch(Term, NewCulture).trim();
			if(Fixed && !Seen.has(Fixed))
				Seen.add(Fixed);
		}
		this.Terms.push(...Seen);
	}

	//noinspection JSUnusedGlobalSymbols :: ... ItemTransformer is used, just not inside the constructor
	public constructor(
		SearchTermsStringOrList:string|readonly string[], //Search terms will be run through NormalizeForSearch. Make sure to use the same encoding as the haystack strings
		public ItemTransformer:(Item:T) => FoldedStrings<StartHSTypes>|null=(MyItem => new FoldedStrings(this.Culture, true, String(MyItem))), //Return null to skip searching the item. For multiple searches, it is recommended your ItemTransformer caches the FoldedStrings for unchanged items
		Culture:string=Intl.DateTimeFormat().resolvedOptions().locale || 'en-US'
	) {
		const StartSearchTerms=Array.isArray(SearchTermsStringOrList)
			? SearchTermsStringOrList
			: I18NSearch.RegEx_SplitAroundSpaces[Symbol.split](SearchTermsStringOrList);

		this.OriginalTerms=StartSearchTerms.map(S => S.replaceAll(FoldedStrings.SectionSeparator, StatStr.PrivateChar));
		this.Culture=Culture;
	}

	public Execute(SearchItems:Iterable<T>): Iterable<T>
	{
		return this.Terms.length===0 ? SearchItems : new Iter(SearchItems).filter(V => this.SearchItemMatches(V));
	}

	public SearchItemMatches(Item:T): boolean
	{
		//Get the fixed-up search term (return early if null)
		const ItemStr=this.ItemTransformer(Item)?.Folded ?? null;
		if(ItemStr===null)
			return false;

		//If any search terms do not match, return false
		for(const Term of this.Terms)
			if(!ItemStr.includes(Term))
				return false;

		//All search terms matched
		return true;
	}

	/*
	YOU DO NOT NEED TO CALL THIS FUNCTION. Strings are always normalized internally. This is just provided for utility.
	Processes to normalize search haystacks and terms:
	 - Unicode normalized through NFKD
	 - CombiningMarks removed
	 - Uppercased (locale dependent)
	*/
	public NormalizeForSearch(Str:string): string { return I18NSearch.NormalizeForSearch(Str, this.Culture); }
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

	/*
	Returns positions from the SearchString where search terms are found.
	If using the result from this function with FoldedStrings.UpdateFromSlices(), SearchString must be the FoldedStrings.Folded.
	Notes:
	  - Longest-match wins at the same index.
	  - Never produces overlapping matches: after a match, scanning continues at matchEnd.
	*/
	public GetSearchTermPositions(SearchString:string): StringSlicePos[]
	{
		//Parameter checks
		const TermCount=this.Terms.length;
		if(TermCount===0 || !SearchString)
			return [];

		//Precompute the per-term next-match index (cursor). -1 means no further matches
		const NextIndexes	:number[]=new Array(TermCount);
		const NextLens		:number[]=new Array(TermCount);
		for(let Index=0; Index<TermCount; Index++)
			NextIndexes[Index]=
				  (NextLens[Index]=this.Terms[Index].length)===0 ? -1
				: SearchString.indexOf(this.Terms[Index]);

		//No matches at all
		if(!NextIndexes.some(I => I>-1))
			return [];

		//Run the search
		const Out:StringSlicePos[]=[];
		let CurPos=0;
		while(true) {
			//Pick earliest next match
			let BestTerm=-1, BestAt=Number.MAX_SAFE_INTEGER, BestLen=0;
			for(let TermIndex=0; TermIndex<NextIndexes.length; TermIndex++) {
				const TermPos=NextIndexes[TermIndex];
				if(!(
					   (TermPos<0 ? Number.MAX_SAFE_INTEGER : TermPos)<BestAt	//TermPos inclusive between 0 and BestAt-1
					|| (TermPos===BestAt && NextLens[TermIndex]>BestLen)		//On tie, prefer the longest term
				))
					continue;
				BestAt=TermPos;
				BestTerm=TermIndex;
				BestLen=NextLens[TermIndex];
			}

			//No more matches
			if(BestTerm<0)
				break;

			Out.push(new StringSlicePos(BestAt, CurPos=BestAt+BestLen));

			//Break when string exhausted
			if(CurPos>=SearchString.length)
				break;

			//Advance cursors
			for(let TermIndex=0, TermPos=NextIndexes[0]; TermIndex<NextIndexes.length; TermPos=NextIndexes[++TermIndex])
				if(TermPos>-1 && TermPos<CurPos)
					NextIndexes[TermIndex]=SearchString.indexOf(this.Terms[TermIndex], CurPos);
		}

		return Out;
	}
}