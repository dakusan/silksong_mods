using SilkDev;
using SilkDev.Textures;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using FieldInfo = System.Reflection.FieldInfo;

namespace PharloomAtlas;

public enum CategoryToggleState
{
	All=0, Incomplete, None, Unknown //Unknown must be last
}

//Category groups (title and list of categories)
public class CategoryGroup : Dictionary<int, Category>
{
	public string Title=string.Empty;
	public int Order { get; internal set; }
	public Category[] AsOrdered { get
	{
		if(field==null) {
			field=new Category[Count];
			foreach(Category Cat in Values)
				field[Cat.Order]=Cat;
		}
		return field;
	} }
//		internal CategoryGroup() {} //Not yet ready for other people to make these. Would need some work.
}

//Categories (All Items have a category)
public class Category
{
	public readonly int Order, IconID;
	public int ID			{ get; internal set; }
	public int TotalCount	{ get; internal set; }
	public int CurrentCount { get; internal set; } = 0;
	public string Title=string.Empty;
	public Sprite Sprite	{ get; internal set; } = null!;
	public CategoryToggleState ToggleState=CategoryToggleState.Unknown;
//		internal Category() {} //Not yet ready for other people to make these. Would need some work.

	public const int MinID=101, MaxID=499;
	public static bool IDInRange(int ID) => ID is >=MinID and <=MaxID;
}

//Items (icons)
public class Item
{
	public int ID			{ get; internal set; }
	public int CategoryID	{ get; internal set; } //Locking down CategoryID to make sure only registered categories are used
	public Sprite Sprite	{ get; internal set; } = null!;
	public int IconID=-1;
	public string Title=string.Empty;
	public RenderedField? WhereAt, Notes, Effect, Tip;
	public ChainList? Reqs, Needs, Rewards;
	public string? IgnPageName;
	public float x, y;
	public string[]? ImageURLs;
	public StoreItems? Store;
	public Vector2 Pos => new(x, y);
	private int UniqueLinkIndex=0;
	private string GetLinkID => $"{ID}.{UniqueLinkIndex++}";
//		internal Item() {} //Not yet ready for other people to make these. Would need some work.

	public const int MinID=100001, MaxID=int.MaxValue;
	private const char TrVarChar=(char)2; //Translation variable character - This is placed around any translation names in strings for quick variable fill-in
	public enum ChainType { Reqs, Needs, Rewards }
	public static bool IDInRange(int ID) => ID is >=MinID and <=MaxID;
	private static string TSan(string Message) => Tr.TDef(Message, "ItemFields", Message, true);
	private static string TDef(string Message, string? Default) => Tr.TDef(Message, "ItemFields", Default!, true);
	private static string TrVar(string Name) => TrVarChar+Name+TrVarChar;
	private static readonly Dictionary<string, string> VarDefaults=new() { {"SEP_AND", ", "}, {"SEP_OR", "OR"}, {"FLAG_NOT", "NOT"}, {"FLAG_STARTED", "STARTED"}, {"FLAG_RECOMMENDED", "RECOMMENDED"} };
	private static readonly Translations Tr=Config.C.Tr;
	private static MapControl MC => field ??= MapControl.Self; //None of the items in this class would call this property until MapControl was already created

	//Render the description
	public string Description => ToString();
	public override string ToString() =>
		string.Join(DevStrings.NewLine, ((string?[])[
			WhereAt	?.Render("Where"		),
			Notes	?.Render("Notes"		),
			Effect	?.Render("Effect"		),
			Tip		?.Render("Tips"			),
			Reqs	?.Render("Requirements"	),
			Needs	?.Render("Needs"		),
			Rewards	?.Render("Rewards"		),
			Store	?.Render("Store"		),
		]).Where(static V => V!=null));

	//Get the title from the item ID (cannot be ran until after all Items are loaded, which is why below objects have delayed string rendering)
	private static string? GetItemTitleFromID(string ID) =>
		  !int.TryParse(ID, out int i) ? null
		: StaticLink.IDInRange(i) ? (MC.DS.StaticLinks.Get(i)?.Name)
		: MC.DS.Items.Get(i)?.Title;

	//A full chain list for a single field
	public class ChainList
	{
		public readonly Item Parent;
		public readonly string StartString;
		public readonly RenderedField? ExtraStr;
		public readonly ChainItem[][]? Items;
		public readonly ChainType Type;

		internal ChainList(Item Parent, string ItemList, ChainType Type)
		{
			(this.Parent, StartString, this.Type)=(Parent, ItemList, Type);

			//Get and remove the extra string part
			int ExtraStrPos=ItemList.IndexOf('^');
			if(ExtraStrPos!=-1) {
				ExtraStr=new RenderedField(Parent, ItemList[(ExtraStrPos+1)..]);
				ItemList=ItemList[0..ExtraStrPos];
			}

			//Parse the list
			if(ItemList!=string.Empty)
				Items=[..ItemList.Split('|').Select((OrStr, GroupIndex) =>
					OrStr.Split('`').Select((ItemStr, ItemIndex) =>
						new ChainItem(this, ItemStr, GroupIndex, ItemIndex)
					).ToArray()
				)];
		}

		//--------------------String rendering--------------------
		//StringCountPair are created such that we can essentially do a `strings.Join(RenderParts.Select(RP => RP.StrBeforeCount+RP.SL.NumCollected)`
		private static readonly Regex ExtractItemCounts=new($"{ChainItem.AmountChar}\\d+{ChainItem.AmountChar}", RegexOptions.Compiled); //LinkIDs are inside a set of AmountChar characters
		private static readonly Regex ReplaceLangVars=new($"{TrVarChar}\\w+{TrVarChar}", RegexOptions.Compiled);
		private record struct StringCountPair(string StrBeforeCount, StaticLink? SL); //Only last item in RenderParts will have SL=null
		private StringCountPair[] RenderParts=null!;
		private string[] RenderPartsAgnostic=null!; //Original RenderParts strings before replacing language variables
		public string RenderedString => CompileRenderString();
		private string CurrentLang=null!;
		private string CompileRenderString()
		{
			//Fill in RenderParts on language change
			if(CurrentLang!=Config.C.Language.Value) {
				CurrentLang=Config.C.Language.Value;

				//Only need to render parts and fill in RenderPartsAgnostic once
				if(RenderPartsAgnostic==null) {
					RenderParts=GetRenderParts();
					RenderPartsAgnostic=new string[RenderParts.Length];
					foreach((int Index, StringCountPair SCP) in RenderParts.Entries)
						RenderPartsAgnostic[Index]=SCP.StrBeforeCount;
				}

				//Translate strings from agnostic back into RenderParts
				foreach((int Index, string AgStr) in RenderPartsAgnostic.Entries)
					RenderParts[Index].StrBeforeCount=ReplaceLangVars.Replace(AgStr, M => TDef(M.Value[1..^1], null) ?? VarDefaults[M.Value[1..^1]]);
			}

			//Shortcut if no parts to fill in
			if(RenderParts.Length==1)
				return RenderParts[0].StrBeforeCount;

			//Build string from RenderParts
			string[] Parts=new string[RenderParts.Length*2-1];
			foreach((int Index, StringCountPair Part) in RenderParts.Entries) {
				Parts[Index*2]=Part.StrBeforeCount;
				if(Part.SL!=null)
					Parts[Index*2+1]=Part.SL.NumCollected.ToString();
			}
			return string.Join(null, Parts);
		}
		private StringCountPair[] GetRenderParts()
		{
			//If no list, just use the extra string
			if(Items==null)
				return [new StringCountPair(ExtraStr?.ToString() ?? string.Empty, null)];

			//Reformat the list
			string Ret=string.Join($" <b><color={MC.DS.LinkColors.Sep_OR}>{TrVar("SEP_OR")}</color></b> ", Items.Select(static ItemList =>
				string.Join($"<color={MC.DS.LinkColors.Sep_AND}>{TrVar("SEP_AND")}</color>", ItemList.Select(static I => I.RenderedStringInternal))
			))+(ExtraStr==null ? null : $"; {ExtraStr}");

			//Extract ExtractItemCounts sections as StringCountPair. Only StaticLinks are used since items cannot have a count and are just set as “1”
			var Parts=new List<StringCountPair>(Items.Sum(static ItemList => ItemList.Length));
			var PendingStr=new System.Text.StringBuilder();
			int CurPos=0;
			foreach(Match m in ExtractItemCounts.Matches(Ret)) {
				//Only add when non empty
				if(m.Index>CurPos)
					_=PendingStr.Append(Ret, CurPos, m.Index-CurPos);
				CurPos=m.Index+m.Length;

				//Add to pending string as Count=1 if not a static link
				int ID=int.Parse(m.Value[1..^1]);
				if(!StaticLink.IDInRange(ID)) {
					_=PendingStr.Append("1");
					continue;
				}

				//Create a new StringCountPair and reset for the next string part
				Parts.Add(new StringCountPair(PendingStr.ToString(), MC.DS.StaticLinks[ID]));
				_=PendingStr.Clear();
			}
			if(CurPos<Ret.Length)
				_=PendingStr.Append(Ret, CurPos, Ret.Length-CurPos);
			Parts.Add(new StringCountPair(PendingStr.ToString(), null));
			return [.. Parts];
		}

		public string Render(string FieldTitle) => $"<b>{TSan(FieldTitle)}</b>: "+RenderedString;
	}

	//A single item in a ChainList
	public class ChainItem
	{
		internal static string AmountChar=((char)1).ToString();
		public readonly ChainList Parent;
		public readonly string StartString;
		private  string RenderedStringReal		=> field ??= FinishInternalRender(); //Contains AmountChar where the live collected count will need to be inserted
		internal string RenderedStringInternal	=> GetProcessedRenderString(RenderedStringReal, $"{AmountChar}{LinkID}{AmountChar}"); //AmountChar becomes LinkID surround by AmountChar
		public   string RenderedString			=> GetProcessedRenderString(RenderedStringReal, "?"); //Changes AmountChar to a question mark
		private string GetProcessedRenderString(string Str, string Replacement) => LinkID==-1 ? Str : Str.Replace(AmountChar, Replacement);

		public readonly bool FlagNot=false, FlagStarted=false, FlagRecommend=false, FlagUnlinked=false;
		public readonly int FlagAmount=1, GroupID, GroupIndex;
		public string Name { get; internal set; } //Not set until after FinishInternalRender()
		public int LinkID { get; internal set; } = -1; //Not set until after FinishInternalRender()
		internal ChainItem(ChainList Parent, string Item, int GroupID, int GroupIndex)
		{
			(this.Parent, this.GroupID, this.GroupIndex, StartString)=(Parent, GroupID, GroupIndex, Item);

			//Find flags
			bool LoopDone=false;
			int CharIndex;
			for(CharIndex=0; CharIndex<Item.Length && !LoopDone; CharIndex++)
				switch(Item[CharIndex]) {
					case '!': FlagNot				=true; break;
					case '~': FlagStarted			=true; break;
					case '@': FlagRecommend			=true; break;
					case '?': FlagUnlinked= LoopDone=true; break;
					default : CharIndex--;  LoopDone=true; break;
					case '*':
						FlagAmount=0;
						while(Item[++CharIndex] is >='0' and <='9')
							FlagAmount=FlagAmount*10+(Item[CharIndex]-'0');
						if(Item[CharIndex]=='*')
							LoopDone=true;
						else
							CharIndex--;
						break;
				}
			Name=Item[CharIndex..]; //Temporarily use name until final render happens
		}
		private static string MakeAttr(string AttrName, object AttrVal) => $"<ATTR={AttrName}>{AttrVal}</ATTR>";
		private string FinishInternalRender()
		{
			//Add flags back
			List<string> Parts=[];
			if(FlagNot		) Parts.Add($"<i>{TrVar("FLAG_NOT")}</i> "			);
			if(FlagStarted	) Parts.Add($"<i>{TrVar("FLAG_STARTED")}</i> "		);
			if(FlagRecommend) Parts.Add($"<i>{TrVar("FLAG_RECOMMENDED")}</i> "	);
			string? Amounts=
				  FlagAmount==1 ? null
				: "<color=grey>"+(Parent.Type!=ChainType.Rewards ? AmountChar : null)
				+ $"<b>{FlagAmount}</b>×</color>";

			//If unlinked or linking failed do do not make it a real link
			string ItemValue=Name, NewItemValue;
			if(FlagUnlinked || (NewItemValue=GetItemTitleFromID(ItemValue)!)==null) {
				Name=ItemValue;
				return Amounts?.Replace(AmountChar, null)+"<u>"+string.Join(null, [.. Parts, ItemValue])+"</u>";
			}

			//Prepare variables for rendered string
			Name=NewItemValue;
			LinkID=int.Parse(ItemValue);
			string? ExtraColor=
				  FlagNot		? MC.DS.LinkColors.Flag_NOT
				: FlagStarted	? MC.DS.LinkColors.Flag_STARTED
				: FlagRecommend	? MC.DS.LinkColors.Flag_RECOMMENDED
				: null;

			//Render as a linked item
			return string.Join(null, [
				$"<LinkID={Parent.Parent.GetLinkID}>",
//				MakeAttr("GroupID",		GroupID		),
//				MakeAttr("GroupIndex",	GroupIndex	),
				MakeAttr("ItemID",		LinkID		),
				ExtraColor!=null ? MakeAttr("NormalColor", ExtraColor) : null,
				Amounts?.Replace(AmountChar, $"<b><size=-4>{AmountChar}</size></b><color=white>/</color>"),
				"<u>",
				.. Parts,
				NewItemValue,
				"</u></LinkID>",
			]);
		}
	}

	//A string with item links inside square brackets rendered as actual links
	public class RenderedField
	{
		//Turn item links in a string into actual links
		private static readonly Regex GetLinks=new(@"\[(\d+)(~[^^|`\]]+)?]", RegexOptions.Compiled);

		public readonly Item Parent;
		public readonly string StartString;
		public string RenderedString => field ??= FinishInternalRender();
		internal RenderedField(Item Parent, string FieldValue) => (this.Parent, StartString)=(Parent, FieldValue);
		private string FinishInternalRender()
		{
			int ReplaceIndex=0;
			return
				GetLinks.Replace(
					StartString,
					Match => {
						string ID=Match.Groups[1].Value;
						string Text=Match.Groups[2].Value;
						Text=!string.IsNullOrEmpty(Text) ? Text[1..] : (GetItemTitleFromID(ID) ?? ID);
						return $"<LinkID={Parent.GetLinkID}>{(false ? $"<ATTR=RepIndx>{ReplaceIndex++}</ATTR>" : null)}<ATTR=ItemID>{ID}</ATTR><u>{Text}</u></LinkID>";
					}
				);
		}
		public override string ToString() => RenderedString;
		public string Render(string FieldTitle) => $"<b>{TSan(FieldTitle)}</b>: "+RenderedString;
	}

	public CategoryToggleState CurrentToggleState
	{
		get;
		set {
			if(value==CategoryToggleState.Unknown)
				return;
			field=value;
			MapIcon?.UpdateState(value);
		}
	} = CategoryToggleState.Unknown;
	public void SetStatusFlag(bool ForStarted, bool Value)
	{
		if(!ForStarted)
			IsFound=Value;
		else
			IsStarted=Value;
	}
	public bool IsStarted=false;
	public bool IsFound
	{
		get;
		set {
			if(field==value)
				return;
			MC.DS.Categories[CategoryID].CurrentCount+=(value ? 1 : -1);
			field=value;
			MapIcon?.SetIsFound(value);
		}
	} = false;

	public bool IsLinked
	{
		get;
		set {
			if(field==value)
				return;
			field=value;
			MapIcon?.SetIsLinked(value);
		}
	} = false;

	public MapIcon? MapIcon
	{
		get;
		set {
			field=value;
			field!.UpdateState(CurrentToggleState);
			field!.SetIsFound(IsFound);
			field!.SetIsLinked(IsLinked);
		}
	} = null!;

	public bool Visible =>
		   CurrentToggleState==CategoryToggleState.All
		|| (CurrentToggleState==CategoryToggleState.Incomplete && !IsFound);

	//JSON type conversion
	internal class CreateItem : Item
	{
		public new string? WhereAt	{ set => Misc.IFF(value!=null, () => base.WhereAt	=new RenderedField	(this, value!					)); }
		public new string? Notes	{ set => Misc.IFF(value!=null, () => base.Notes		=new RenderedField	(this, value!					)); }
		public new string? Effect	{ set => Misc.IFF(value!=null, () => base.Effect	=new RenderedField	(this, value!					)); }
		public new string? Tip		{ set => Misc.IFF(value!=null, () => base.Tip		=new RenderedField	(this, value!					)); }
		public new string? Reqs		{ set => Misc.IFF(value!=null, () => base.Reqs		=new ChainList		(this, value!, ChainType.Reqs	)); }
		public new string? Needs	{ set => Misc.IFF(value!=null, () => base.Needs		=new ChainList		(this, value!, ChainType.Needs	)); }
		public new string? Rewards	{ set => Misc.IFF(value!=null, () => base.Rewards	=new ChainList		(this, value!, ChainType.Rewards)); }

		//Store needs to be created separately since it is nested
		private new CreateStoreItems[]? Store=null; //Set via JSON
		private class CreateStoreItems { public string? Reqs=null; public string Needs=null!, Rewards=null!; }
		internal Item GetItem()
		{
			//If store is not set, nothing to do but return self
			if(Store==null)
				return this;

			//Fill in the store
			StoreItem[] Items=new StoreItem[Store.Length];
			foreach((int Index, CreateStoreItems Item) in Store.Entries)
				Items[Index]=new StoreItem(
					  Item.Reqs==null ? null
					: new ChainList(this, Item.Reqs		, ChainType.Reqs	),
					  new ChainList(this, Item.Needs	, ChainType.Needs	),
					  new ChainList(this, Item.Rewards	, ChainType.Rewards	)
				);
			base.Store=new StoreItems(Items);
			return this;
		}

		//Handle compacted Json data from CreateJSONs.php
		private int					C	{ set => CategoryID	=value; }
		private string				T	{ set => Title		=value; }
		private int					I	{ set => IconID		=value; }
		private string?				R	{ set => Reqs		=value; }
		private string?				A	{ set => WhereAt	=value; }
		private string?				N	{ set => Needs		=value; }
		private string?				W	{ set => Rewards	=value; }
		private string?				E	{ set => Effect		=value; }
		private string?				P	{ set => Tip		=value; }
		private string?				O	{ set => Notes		=value; }
		private string?				IGN { set => IgnPageName=value; }
		private CreateStoreItems[]? S	{ set => Store		=value; }
		private string[]?			U	{ set => ImageURLs	=value; }
	}

	//Store structures
	public class StoreItem(ChainList? Reqs, ChainList Needs, ChainList Rewards) {
		public readonly ChainList? Reqs=Reqs;
		public readonly ChainList Needs=Needs, Rewards=Rewards;
	}
	public class StoreItems(StoreItem[] Items)
	{
		public string RenderedString => FinishInternalRender(); //Cannot be cached due to changing item collection counts
		public StoreItem[] Items=Items;
		private string FinishInternalRender() =>
			string.Join(null, Items.Select(static I =>
				"\n- "+I.Rewards.RenderedString+TDef("STORE_FOR", " for ")+I.Needs.RenderedString+
				(I.Reqs!=null ? Tr.TDef("STORE_REQ", "ItemFields", " (Required: {0})", false, I.Reqs.RenderedString) : null)
			));
		public string Render(string FieldTitle) => $"<b>{TSan(FieldTitle)}</b>: "+RenderedString;
	}

	//Selected via a link
	public void Selected() => MC.SelectAndCenterItemI(ID);
}

public class StaticLink(string Name, int CategoryID, int[]? ItemIDs, int SpecialCount, FieldInfo? FI)
{
	public readonly string Name=Name;
	public readonly int CategoryID=CategoryID, SpecialCount=SpecialCount;
	public readonly int[]? ItemIDs=ItemIDs;
	public readonly FieldInfo? FI=FI; //Not kept as a Reflectors.RField as PlayerData.instance may change

	public const int MinID=501, MaxID=999;
	public static bool IDInRange(int ID) => ID is >=MinID and <=MaxID;
	private static DataStorage DS => field ??= MapControl.Self.DS;

	public int NumCollected =>
		  CategoryID!=-1		? DS.Categories[CategoryID].CurrentCount
		: ItemIDs!=null			? ItemIDs.Count(static I => DS.Items[I].IsFound)
		: FI!=null				? (int)FI.GetValue(PlayerData.instance)
		:						SpecialCount;

	//JSON type conversion
	public static Dictionary<int, StaticLink> Process(Dictionary<string, List<object>> StaticLinks, Dictionary<int, Item> Items, Dictionary<int, Category> Categories)
	{
		//Shortcut functions
		Dictionary<int, StaticLink> Out=[];
		string? CurName=null!, RemID;
		int CatID;
		void AddSL(int ID, int CategoryID=-1, int[]? ItemIDs=null, int SpecialCount=0, FieldInfo? FI=null, string? OverwriteName=null, string? ErrStr=null)
		{
			Out[ID]=new StaticLink(OverwriteName ?? CurName!, CategoryID, ItemIDs, SpecialCount, FI);
			if(ErrStr!=null)
				LineErr(ErrStr);
		}
		void LineErr(string Err, bool CompleteFail=false) => Log.Error($"Error on Static Link #{RemID}{(CompleteFail ? " [Skipped]" : null)}: {Err}");

		//Process the static links
		foreach((string ID, List<object> L) in StaticLinks)
			if     (!int.TryParse(RemID=ID, out int MyID)	)	LineErr("ID is not an int",					true);
			else if(!IDInRange(MyID)						)	LineErr("ID is not valid for a Static Link",true);
			else if(L.Count==0								)	LineErr("Array is empty",					true);
			else if((CurName=L[0] as string)==null			)	AddSL(MyID, OverwriteName:"???", ErrStr:"Name is not a string");//Invalid name
			else if(L.Count==1								)	AddSL(MyID, SpecialCount:1);									//Unlinked
			else if(L.Count==2 && (L[1] is string Special))																		//Special check
				if(int.TryParse(Special, out int SpecialInt))	AddSL(MyID, SpecialCount:SpecialInt);							//Special Count Success
				else try {																										//Special FieldInfo Check
					FieldInfo FI=new Reflectors.RField<PlayerData, int>(null, Special).FI;
					if(FI.FieldType!=typeof(int))				AddSL(MyID, ErrStr:"PlayerData field is not an int");			//Special FieldInfo failed (not int)
					else										AddSL(MyID, FI:FI);												//Special FieldInfo success
				} catch {										AddSL(MyID, ErrStr:$"Invalid value for special: {Special}"); }  //Special FieldInfo failed (doesn’t exist)
			else if(L.Count==2 && Category.IDInRange(CatID=(int)(L[1] is long CID ? CID : -1)))									//Category check
				if(Categories.ContainsKey(CatID))				AddSL(MyID, CategoryID:CatID);									//Category success
				else											AddSL(MyID, ErrStr:$"Invalid Category ID {CatID}");				//Category failed
			else
				AddSL(MyID, ItemIDs:[..																							//Item list
					L.Skip(1)
					.Select(I =>
						   I is not			 long	IVal  ? Misc.PassThru(() => LineErr($"ItemID is not a long: {I}"			), -1)
						: !Item.IDInRange	((int)	IVal) ? Misc.PassThru(() => LineErr($"ItemID is not a valid Item ID: {IVal}"), -1)
						: !Items.ContainsKey((int)	IVal) ? Misc.PassThru(() => LineErr($"ItemID is not a valid Item: {IVal}"	), -1)
						: (int)IVal
					).Where(static I => I!=-1)
				]);

		return Out;
	}

	//Selected via a link
	public void Selected() =>
		_=new SilkDev.Windows.PopupMessage(Config.C.Tr.Translate("Category selection is not yet supported", null, true));
}