using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SilkDev;
public static partial class DevStrings { //Namespace hack since Namespace and class name cannot coincide

	//Searching with i18n. If using the primary constructor make sure StartSearchTerms have been run through SafeRich (if UseSafeRich=true)
	public class I18NSearch<T>(string[] StartSearchTerms, I18NSearch<T>.FuncItemTransformer? ItemTransformer=null, bool UseSafeRich=true)
	{
		public static readonly Regex RegEx_RemoveHTMLTags=new(@"</?\w[^>]+>", RegexOptions.Compiled);
		public static readonly Regex RegEx_SplitAroundSpaces=new(@"\s+", RegexOptions.Compiled);
		public delegate string? FuncItemTransformer(T Item); //Return null to skip searching item
		public readonly FuncItemTransformer ItemTransformer=ItemTransformer ?? (static Str => Str?.ToString() ?? string.Empty);
		public readonly bool UseSafeRich=UseSafeRich;
		public readonly string[] OriginalTerms=[.. StartSearchTerms];
		private readonly List<string> Terms=[.. FormatSearchTerms(StartSearchTerms, CultureInfo.CurrentCulture)];
		public IReadOnlyList<string> SearchTerms => Terms;
		public CultureInfo Culture
		{
			get;
			set {
				if(Equals(field, value))
					return;
				Terms.Clear();
				Terms.AddRange(FormatSearchTerms(OriginalTerms, field=value));
			}
		} = CultureInfo.CurrentCulture;

		public I18NSearch(string SearchText, FuncItemTransformer? ItemTransformer=null, bool UseSafeRich=true) :
			this(RegEx_SplitAroundSpaces.Split(UseSafeRich ? SafeRich(SearchText) : SearchText), ItemTransformer, UseSafeRich) { }

		public IEnumerable<T> Execute(IEnumerable<T> SearchItems) =>
			Terms.Count==0 ? SearchItems : SearchItems.Where(SearchItemMatches);

		public bool SearchItemMatches(T Item)
		{
			//Get the fixed up search term (return early if null)
			string? ItemStr=ItemTransformer(Item);
			if(ItemStr==null)
				return false;
			if(UseSafeRich)
				ItemStr=RegEx_RemoveHTMLTags.Replace(ItemStr, ((char)0x0086).ToString());
			ItemStr=NormalizeForSearch(ItemStr);

			//If any search terms do not match, return false
			foreach(string Term in Terms)
				if(!ItemStr.Contains(Term, StringComparison.Ordinal))
					return false;

			//All search terms matched
			return true;
		}

		public string NormalizeForSearch(string Str) => NormalizeForSearch(Str, Culture);
		public static string NormalizeForSearch(string Str, CultureInfo Culture)
		{
			var Norm=Str.Normalize(NormalizationForm.FormKD);
			var SB=new StringBuilder(Norm.Length);
			foreach(char C in Norm)
				if(Char.GetUnicodeCategory(C) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
					_=SB.Append(char.ToUpper(C, Culture));

			return SB.ToString();
		}
		private static IEnumerable<string> FormatSearchTerms(string[] SearchTerms, CultureInfo Culture) =>
			SearchTerms
				.Select(		Str => NormalizeForSearch(Str, Culture))
				.Select(static	Str => Str.Trim())
				.Where (static	Str => !string.IsNullOrEmpty(Str))
				.Distinct();

		/// <summary>
		/// Replaces occurrences of any term in <paramref name="SearchTerms"/> found in <paramref name="Haystack"/> using the same matching
		/// semantics as the search: IgnoreCase+IgnoreNonSpace (diacritic-insensitive), returning the caller-provided replacement for each match.<br/>
		/// Notes:<br/>
		/// - Longest-match wins at the same index.<br/>
		/// - Never produces overlapping matches: after a match, scanning continues at matchEnd.
		/// </summary>
		public string ReplaceSearchTerms(string Haystack, ReplaceWithFunc ReplaceWith)
			=> ReplaceSearchTerms(Haystack, Terms, ReplaceWith, Culture);

		public delegate string? ReplaceWithFunc(int FindIndex, string FindValue); //Index is based off the original string
		private const CompareOptions ReplaceOpts=CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace;
		public static string ReplaceSearchTerms(string Haystack, IReadOnlyList<string> SearchTerms, ReplaceWithFunc ReplaceWith, CultureInfo Culture)
		{
			//Parameter checks
			if(Haystack		is null) throw new ArgumentNullException(nameof(Haystack	));
			if(SearchTerms	is null) throw new ArgumentNullException(nameof(SearchTerms	));
			if(ReplaceWith	is null) throw new ArgumentNullException(nameof(ReplaceWith	));
			if(Culture		is null) throw new ArgumentNullException(nameof(Culture		));
			int TermCount=SearchTerms.Count;
			if(TermCount==0 || Haystack.Length==0)
				return Haystack;
			CompareInfo Compare=Culture.CompareInfo;

			//Precompute per-term next match index (cursor). -1 means no further matches
			int[] NextIndexes=new int[TermCount];
			int[] NextLens	 =new int[TermCount];
			foreach((int Index, string Term) in SearchTerms.Entries)
				NextIndexes[Index]=
					  (NextLens[Index]=Term?.Length ?? 0)==0 ? -1
					: Compare.IndexOf(Haystack, Term, 0, ReplaceOpts);

			//No matches at all
			if(!NextIndexes.Any(static I => I>-1))
				return Haystack;

			//Run the search/replacements
			StringBuilder SB=new(Haystack.Length+32);
			int CurPos=0;
			while(true) {
				//Pick earliest next match
				int BestTerm=-1, BestAt=int.MaxValue, BestLen=0;
				foreach((int TermIndex, int TermPos) in NextIndexes.Entries)
					if(
						   (TermPos<0 ? int.MaxValue : TermPos)<BestAt		//TermPos inclusive between 0 and BestAt-1
						|| (TermPos==BestAt && NextLens[TermIndex]>BestLen)	//On tie, prefer longest term
					)
						(BestAt, BestTerm, BestLen)=(TermPos, TermIndex, NextLens[TermIndex]);

				//No more matches
				if(BestTerm<0) {
					_=SB.Append(Haystack, CurPos, Haystack.Length-CurPos);
					break;
				}

				//Add leading text and replacement from user
				if(BestAt>CurPos)
					_=SB.Append(Haystack, CurPos, BestAt-CurPos);
				_=SB.Append(ReplaceWith(BestAt, Haystack[BestAt..(BestAt+BestLen)]));

				//Break when string exhausted
				CurPos=BestAt+BestLen;
				if(CurPos>=Haystack.Length)
					break;

				//Advance cursors
				foreach((int TermIndex, int TermPos) in NextIndexes.Entries)
					if(TermPos>-1 && TermPos<CurPos)
						NextIndexes[TermIndex]=Compare.IndexOf(Haystack, SearchTerms[TermIndex], CurPos, ReplaceOpts);
			}

			return SB.ToString();
		}
	}
}