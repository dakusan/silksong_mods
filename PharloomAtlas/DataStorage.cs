using SilkDev;
using SilkDev.JSON;
using SilkDev.Textures;
using SilkDev.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using static PharloomAtlas.Item;

#if DEBUG
	using SafeTexture2D = SilkDev.Textures.SafeTexture2D;
#else
	using SafeTexture2D = UnityEngine.Texture2D;
#endif
using RTexture2D = UnityEngine.Texture2D;

namespace PharloomAtlas;

public class DataStorage
{
	public readonly CategoryGroup[] CategoryGroups;
	public readonly Dictionary<int, Category> Categories=[];
	public readonly Dictionary<int, Item> Items;
	public readonly Dictionary<int, StaticLink> StaticLinks=[];
	public readonly SafeTexture2D IconPicsTex;
	internal const int IconLenX=10, IconLenY=8, IconWidth=65, IconHeight=65, IconPadding=1;

	//Create icon sprites as needed
	public class IconSprites
	{
		private readonly Sprite?[] SpriteList=new Sprite?[IconLenX*IconLenY];
		private readonly RTexture2D IconPicsTex;
		private const int ErrorTexSize=54;
		internal IconSprites(RTexture2D IconPicsTex)
		{
			this.IconPicsTex=IconPicsTex;

			//Create the special error sprite (which is always the last square and is of size ErrorTexSize*ErrorTexSize)
			int LastSpriteID=IconLenX*IconLenY-1;
			SpriteList[LastSpriteID]=CreateSprite(GetIconRectByID(LastSpriteID).SetWidth(ErrorTexSize).SetHeight(ErrorTexSize));
		}

		public Sprite this[int IconID]
		{
			get {
				//Instead of dealing with errors, just use an error icon when out of range
				if(IconID is <0 or >=(IconLenX*IconLenY))
					IconID=IconLenX*IconLenY-1;

				//Return if already created
				if(SpriteList[IconID]!=null)
					return SpriteList[IconID]!;

				//Create the sprite
				return SpriteList[IconID]=CreateSprite(GetIconRectByID(IconID));
			}
		}

		internal static Rect GetIconRectByID(int IconID)
		{
			int x=IconID%IconLenX, y=IconID/IconLenX;
			return new(x*(IconWidth+IconPadding), (IconLenY-y-1)*(IconHeight+IconPadding), IconWidth, IconHeight);
		}
		private Sprite CreateSprite(Rect IconRect) =>
			Sprite.Create(IconPicsTex, IconRect, new Vector2(0.5f, 0.5f), 100f);
	}
	public readonly IconSprites MyIconSprites;

	//Load the data from the files
	internal DataStorage()
	{
		//Utility functions
		static string GetPluginFile(string FileName) => FileOps.PathCombine(FileOps.GetPluginPath, FileName);
		static T LoadJSON<T, T2>(string FileName) where T2: class =>
			JsonUtils.Deserialize_FPC<T, T2>(FileOps.ReadFile(GetPluginFile(FileName)))!;

		//Load the categories
		Dictionary<string, CategoryGroup> CategoryGroupsDict;
		try {
			CategoryGroupsDict=LoadJSON<Dictionary<string, CategoryGroup>, Category>("categories.json") ??
				throw new Exception("Categories is null");
		} catch(Exception e) {
			throw new Exception($"Could not load categories, failing out: {e.Message}");
		}
		if(CategoryGroupsDict.Count==0)
			throw new Exception("Categories cannot be empty");

		//Sort, turn into arrays and dicts, and add IDs/Titles
		int i=0;
		CategoryGroups=new CategoryGroup[CategoryGroupsDict.Count];
		foreach((string GroupName, CategoryGroup Groups) in CategoryGroupsDict) {
			Groups.Title=GroupName;
			CategoryGroups[i]=Groups;
			Groups.Order=i++;
			foreach((int CatID, Category CatData) in Groups) {
				CatData.ID=CatID;
				Categories[CatID]=CatData;
			}
		}

		//Load the items
		try {
			Items=(LoadJSON<Dictionary<int, CreateItem>, CreateItem>("items.json") ?? throw new Exception("Items is null"))
				.ToDictionary(static Pair => Pair.Key, static Pair => Pair.Value.GetItem());
		} catch(Exception e) {
			throw new Exception($"Could not load items, failing out: "+FileOps.Ser(e)); //The exceptions in this can get pretty deep, so just output the entire exception chain
		}
		var MatchedIcons=MonitorSaveValues.Self.GetMatchedIcons;
		foreach((int ItemID, Item ItemData) in Items) {
			ItemData.ID=ItemID;
			ItemData.IsLinked=MatchedIcons.ContainsKey(new(ItemID, false));
			if(Categories.ContainsKey(ItemData.CategoryID))
				continue;
			Log.Error($"Invalid CategoryID[#{ItemData.CategoryID}] on Item[#{ItemID}]");
			ItemData.CategoryID=Categories.First().Key;
		}
		foreach(Item Item in Items.Values)
			Categories[Item.CategoryID].TotalCount++;

		//Load the static links and Misc
		try {
			(LoadJSON<LoadMisc, LoadMisc>("Misc.json") ?? throw new Exception("Misc is null")).Process(this);
		} catch(Exception e) {
			Catcher.OutputException("Loading static links", e);
			throw new Exception($"Could not load misc/static links, failing out: {e.Message}");
		}

		//Create and update the sprite texture
		IconPicsTex=SafeTexture2D.New();
		if(!IconPicsTex.LoadImage(FileOps.LoadLocalFileOrResource(Config.C.IconSet.Value).ReadAllAndCloseB()))
			throw new Exception("Could not load icons texture, failing out");
		Config.C.IconSet.SettingChanged += (_, _) => Misc.IFF(
			!IconPicsTex.LoadImage(FileOps.LoadLocalFileOrResource(Config.C.IconSet.Value).ReadAllAndCloseB()),
			() => throw new Exception("Could not load icons texture, failing out")
		);

		//Create the sprites
		MyIconSprites=new IconSprites(IconPicsTex);
		foreach(Category Category in Categories.Values)
			Category.Sprite=MyIconSprites[Category.IconID];

		LoadCategoryToggleStates(true);
	}
	private static bool ItemIDInRange(int ID) => IDInRange(ID); //Alleviate some naming confusion

	//Distribute chain system items
	internal void CompleteInit()
	{
		//Static helpers
		static IEnumerable<ChainList> GetNonEmptyLists(params ChainList?[] CL) => CL.Where(static CLi => CLi?.Items?.Length>0)!;
		static IEnumerable<ChainItem> GetListItems				(ChainList CL) => CL.Items.SelectMany(static Arr => Arr);
		static void AddReqOrNeedToReward(Item RewardItem, ChainList ReqOrNeedList, Dictionary<int, Item> Items)
		{
			//Add the Req/Need ChainList to the Reward
			string? Error=RewardItem.AddStoreChainList(ReqOrNeedList);
			if(Error!=null)
				Log.Error($"Error adding {ReqOrNeedList.Parent.ID}.Store.{ReqOrNeedList.Type} to reward {RewardItem.ID}: {Error}");

			//For Req/Needs items sets “Unlocks” to the reward
			foreach(ChainItem CI in GetListItems(ReqOrNeedList))
				if(ItemIDInRange(CI.LinkID))
					Items[CI.LinkID].Unlocks!.Add(RewardItem);
		}

		ChainItem.Process_NeedsIDAndName();

		//Distribute links from each item
		foreach(Item ItemData in Items.Values) {
			//Fills in Item.{Unlocks, AQFrom} for items linked from this item
			foreach(ChainList CL in GetNonEmptyLists(ItemData.Reqs, ItemData.Needs, ItemData.Rewards))
				foreach(ChainItem CI in GetListItems(CL))
					if(ItemIDInRange(CI.LinkID))
						(CL==ItemData.Rewards ? Items[CI.LinkID].AQFrom! : Items[CI.LinkID].Unlocks!).Add(ItemData);

			//Distribute store reward related items: Fills in Item.{Unlocks, AQFrom, Reqs, Needs} for items linked from this item’s store
			foreach(StoreItem SI in ItemData.Store?.Items ?? [])
				if(SI.Rewards.Items!=null)
					foreach(ChainItem RWCI in GetListItems(SI.Rewards))
						if(ItemIDInRange(RWCI.LinkID)) {
							Items[RWCI.LinkID].AQFrom!.Add(ItemData); //Set reward’s AQFrom to the vendor
							foreach(ChainList CL in GetNonEmptyLists(SI.Reqs, SI.Needs))
								AddReqOrNeedToReward(Items[RWCI.LinkID], CL, Items);
						}
		}

		//Remove unused Unlocks/AQFrom
		foreach(Item ItemData in Items.Values) {
			if(ItemData.Unlocks!.GetItems.Count==0)
				ItemData.Unlocks=null;
			if(ItemData.AQFrom!.GetItems.Count==0)
				ItemData.AQFrom=null;
		}

		//Write out to a file for testing
		#if WRITE_JSON
			DateTime Start=DateTime.Now;
			FileOps.WriteFile(FileOps.PathCombine(FileOps.GetPluginPath, "ItemsAndCategoriesExport.json"), JsonUtils.Serialize_Exporter(
				new Dictionary<string, object?> { { "Categories", Categories }, { "Items", Items } },
				TrailingCommas:true
			));
			Log.Error("Time to export: "+(DateTime.Now-Start).TotalSeconds);
		#endif
	}

	//Link colors. Do not add any other public properties unless they are colors
	public sealed class LinkColorsT : ColorsSet
	{
		//The following of these are set statically so realtime changing is not supported (for now): Flag_{NOT,STARTED,RECOMMENDED}, Sep_{OR,AND}
		public StringColor Default			{ get; set => field=SetLinkColor(value, field, "cyan"		); } //Default link color
		public StringColor LinkHover		{ get; set => field=SetLinkColor(value, field, "yellow"		); } //Color when a link has the mouse over it
		public StringColor LabelHover		{ get; set => field=SetLinkColor(value, field, "#4678C880"	); } //Box color for the entire label when mouse over (in the search box); Desaturated, mid-luminance blue goes well with: red, teal, plum, yellow, cyan, white, black, green
		public StringColor Flag_NOT			{ get; set => field=SetLinkColor(value, field, "red"		); } //Flag color (precedence=0) for NOT
		public StringColor Flag_STARTED		{ get; set => field=SetLinkColor(value, field, "teal"		); } //Flag color (precedence=1) for STARTED
		public StringColor Flag_RECOMMENDED	{ get; set => field=SetLinkColor(value, field, "#dda0dd"	); } //Flag color (precedence=2) for RECOMMENDED [#=plum]
		public StringColor Sep_OR			{ get; set => field=SetLinkColor(value, field, "purple"		); } //Separator for boolean OR “ OR ”
		public StringColor Sep_AND			{ get; set => field=SetLinkColor(value, field, "white"		); } //Separator for boolean AND “, ”
		public StringColor Strike_Found		{ get; set => field=SetLinkColor(value, field, "white"		); } //Straight line through link when item has been found
		public StringColor Strike_Started	{ get; set => field=SetLinkColor(value, field, "silver"		); } //Wavy line through link when item has been started (and not found)
		public StringColor Search_Highlight	{ get; set => field=SetLinkColor(value, field, "green"		); } //Highlighting searched string
		public StringColor CollectedCounts	{ get; set => field=SetLinkColor(value, field, "grey"		); } //Amounts the player has and needs to finish an item
	}

	//In case I decide to store more colors like this, I decided to make it an abstract class
	//Note: Do not add public properties to subclass unless they are colors
	public abstract class ColorsSet
	{
		//Color type that stores it HTML string and UnityEngine.Color
		public readonly record struct StringColor(string AsString, Color AsColor)
		{
			public readonly string AsString=AsString;
			public readonly Color AsColor=AsColor;
			public StringColor(string Value) : this(Value, Color.black) { }			//This is only used as a pass through and is not considered a legitimate value
			public static implicit operator StringColor(string Value) => new(Value);//This is only used as a pass through and is not considered a legitimate value
			public static implicit operator string(StringColor Value) => Value.AsString;
			public static implicit operator Color (StringColor Value) => Value.AsColor;
			public override string ToString() => AsString;
		}

		//Set the colors
		public readonly string[] ColorNames;
		protected StringColor SetLinkColor(string RequestedValue, StringColor PreviousValue, string Default, [System.Runtime.CompilerServices.CallerMemberName] string? ColorName=null)
		{
			//Store new values
			bool IsValid=ColorUtility.TryParseHtmlString(RequestedValue, out Color NewColor);
			StringColor SetVal=new(
				IsValid ? RequestedValue : Default,
				  IsValid ? NewColor
				: ColorUtility.TryParseHtmlString(Default, out Color DefaultColor) ? NewColor=DefaultColor
				: throw new InvalidOperationException("Could not parse default color: "+Default)
			);

			//Run callbacks and set new value
			_=ColorSetCallbacks.Run(
				ColorName!,
				CB => CB(SetVal, ColorName!, PreviousValue, RequestedValue)
			);
			return SetVal;
		}
		public ColorsSet()
		{
			Type SubclassType=GetType();
			ColorNames=[.. SubclassType.GetProperties().Where(P => P.DeclaringType==SubclassType).Select(static P => P.Name)];
			ColorNames.ForEach(CName => SubclassType.GetProperty(CName).SetValue(this, new StringColor(string.Empty)));
		}

		//Callbacks
		public delegate void ColorCallback(StringColor NewValue, string ColorName, StringColor PreviousValue, string RequestedValue);
		private readonly SilkDev.Events.EventRegister<string, ColorCallback> ColorSetCallbacks=new("Set color callback");
		public void AddCallback(string ColorName, ColorCallback CB) => Misc.IFF(
			ColorNames.Contains(ColorName),
			() => ColorSetCallbacks.Add(ColorName, CB)
		);
		public void RemoveCallback(string ColorName, ColorCallback CB) => Misc.IFF(
			ColorNames.Contains(ColorName),
			() => ColorSetCallbacks.Remove(ColorName, CB)
		);
	}
	public LinkColorsT LinkColors=new();

	private class LoadMisc
	{
		private readonly Dictionary<string, List<object>> StaticLinks=[];
		private readonly Dictionary<string, string> LinkColors=[], ImagePrefix=[], OtherLinkPrefix=[];

		/*
		Rewrite Item’s ImageURLs/OtherLinks entries based on per-prefix regex rules.

		For each string in ModifyList:
		- If it starts with a rule’s PrefixSymbol (PrefixList.Key), remove the prefix and apply the rule’s regex rewrite.
		- The rewrite specification is PrefixList.Value in the form: <D><SEARCH><D><REPLACE> (e.g., “~SEARCH~REPLACE”) where <D> is a single UTF-16 code unit delimiter.
		- The SEARCH regex has no flags inherently. This means RegexOptions.CultureInvariant IS NOT turned on. Meaning \d matches more than [0-9].
		- The delimiter must appear exactly twice (at the start and between SEARCH and REPLACE) and must not appear inside SEARCH or REPLACE.

		If FinishProcessing is provided, it is run on every final value after all rewrites have been applied.
		*/
		private static void RewriteList(Dictionary<string, string> PrefixList, IEnumerable<(Item, string[])> ModifyList, FinishProcessingFunc? FinishProcessing=null)
		{
			//Get the regular expression rewrites
			List<(Regex SearchRegEx, string PrefixSymbol, string ReplaceWith)> Rewrites=new(PrefixList.Count);
			foreach((string PrefixSymbol, string RegExStr) in PrefixList)
				try {
					if(PrefixSymbol.Length==0)
						throw new("PrefixSymbol cannot be blank");
					else if(RegExStr.Length<4)
						throw new("RegEx must have at least 4 characters");
					else if(char.IsSurrogate(RegExStr[0]))
						throw new("RegEx split character must fit within a UTF16 code unit");
					string[] RegExParts=RegExStr[1..].Split(RegExStr[0]);
					if(RegExParts.Length!=2)
						throw new($"Must contain first ({RegExStr[0]}) character exactly once more to split SEARCH and REPLACE");
					else if(RegExParts[0].Length==0)
						throw new($"SEARCH cannot be blank");
					else if(RegExParts[1].Length==0)
						throw new($"REPLACE cannot be blank");
					Rewrites.Add((new(RegExParts[0], RegexOptions.Compiled, TimeSpan.FromSeconds(1)), PrefixSymbol, RegExParts[1]));
				} catch(Exception e) {
					Log.Error($"Error parsing Rewrite RegEx “{RegExStr}” for “{PrefixSymbol}”: {e.Message}");
				}

			//Rewrite entries
			try {
				foreach((Item I, string[] ItemList) in ModifyList)
					foreach((int Index, string ModifyItem) in ItemList.Entries) {
						string FinalVal=ModifyItem ?? string.Empty;
						foreach((Regex SearchRegEx, string PrefixSymbol, string ReplaceWith) in Rewrites)
							if(FinalVal.StartsWith(PrefixSymbol, StringComparison.Ordinal))
								FinalVal=SearchRegEx.Replace(FinalVal[PrefixSymbol.Length..], ReplaceWith);
						if(FinishProcessing!=null)
							FinalVal=FinishProcessing(FinalVal, I.ID, Index);
						ItemList[Index]=FinalVal;
					}
			} catch(Exception e) { //This should only happen with a RegexMatchTimeoutException
				Log.Error($"Aborted RewriteList: {e.Message}");
			}
		}
		private delegate string FinishProcessingFunc(string Str, int ItemID, int Index);

		public void Process(DataStorage DS)
		{
			//StaticLinks
			DS.StaticLinks.AddRange(StaticLink.Process(StaticLinks, DS.Items, DS.Categories));

			//LinkColors
			foreach(string ColorName in DS.LinkColors.ColorNames)
				if(LinkColors.TryGetValue(ColorName, out string ColorValue))
					DS.LinkColors.GetType().GetProperty(ColorName).SetValue(DS.LinkColors, new ColorsSet.StringColor(ColorValue));

			//Rewrite from Image and OtherLink prefixes
			RewriteList(ImagePrefix		, DS.Items.Values.Where(static I => I.ImageURLs ?.Length>0).Select(static I => (I, I.ImageURLs !)));
			RewriteList(OtherLinkPrefix	, DS.Items.Values.Where(static I => I.OtherLinks?.Length>0).Select(static I => (I, I.OtherLinks!)),
				//URL can be followed by an optional link name (URL escape not necessary) prefixed with a pipe “|”. If not given, the URL will be the link name. The Link name will have UrlDecode() ran on it for display.
				static (Str, ItemID, Index) => {
					string[] Parts=Str.Split('|', 2);
					(string URL, string Name)=(Parts.Length==2 ? (Parts[0], Parts[1]) : (Str, Str));
					return $"<LinkID=OL-{ItemID}-{Index}><ATTR=href>{URL.Replace("<", "%3C")}</ATTR>{DevStrings.SafeRich(System.Net.WebUtility.UrlDecode(Name))}</LinkID>";
				}
			);
		}
	}

	//Load the category toggle states
	internal void LoadCategoryToggleStates(bool FirstRun)
	{
		try {
			//After deserialization works on the lists, set all categories to Incomplete by default
			int[][] CatIDsLists=JsonUtils.Deserialize<int[][]>(Config.C.CategoryToggleStates) ?? throw new Exception("Conversion failed");
			foreach(Category Cat in Categories.Values)
				Cat.ToggleState=CategoryToggleState.Incomplete;

			//Load the categories from the settings
			foreach((int CatToggleState, int[] CatIDs) in CatIDsLists.Take((int)CategoryToggleState.Unknown).Entries)
				foreach(int CatID in CatIDs)
					_=Categories.Get(CatID)?.ToggleState=(CategoryToggleState)CatToggleState;

			//Resave in case there were errors or changes
			if(FirstRun)
				SaveAndUpdateAllCategoryToggleStates();
		} catch(Exception) {
			if(FirstRun)
				SetCategoriesStatesFor100Percent();
		}
	}

	//Create all the icons
	internal void LoadIcons() =>
		Items.Values.ForEach(Item =>
			Item.MapIcon=new MapIcon(
				Item,
				MyIconSprites[Item.IconID!=-1 ? Item.IconID : Categories[Item.CategoryID].IconID]
			)
		);

	//Category state updating functions
	public void CycleGroupCategoryState(CategoryGroup CG)
	{
		CategoryToggleState ConfirmState=CG.FirstOrDefault().Value?.ToggleState ?? CategoryToggleState.None;
		foreach(Category Cat in CG.Values)
			if(Cat.ToggleState!=ConfirmState) {
				ConfirmState=CategoryToggleState.None;
				break;
			}
		ConfirmState=GetNextToggleState(ConfirmState);
		foreach(Category Cat in CG.Values)
			Cat.ToggleState=ConfirmState;
		SaveAndUpdateAllCategoryToggleStates();
	}

	public void SetAllCategoriesStates(CategoryToggleState NewState)
	{
		if(NewState==CategoryToggleState.Unknown)
			return;
		foreach(Category Category in Categories.Values)
			Category.ToggleState=NewState;
		SaveAndUpdateAllCategoryToggleStates();
	}

	public void SetCategoryState(Category TheCat, CategoryToggleState NewState)
	{
		if(NewState==CategoryToggleState.Unknown)
			return;
		TheCat.ToggleState=NewState;
		SaveAndUpdateAllCategoryToggleStates();
	}

	public void SetCategoriesStatesFor100Percent()
	{
		string[] RequiredCategories=["Mask Shard", "Spool Fragment", "Silk Heart", "Kit/Pouch Update"];
		foreach(Category Cat in Categories.Values)
			Cat.ToggleState=RequiredCategories.Contains(Cat.Title) ? CategoryToggleState.Incomplete : CategoryToggleState.None;
		SaveAndUpdateAllCategoryToggleStates();
	}

	public static CategoryToggleState GetNextToggleState(CategoryToggleState TS) =>
		TS switch {
			CategoryToggleState.None => CategoryToggleState.All,
			CategoryToggleState.All => CategoryToggleState.Incomplete,
			CategoryToggleState.Incomplete => CategoryToggleState.None,
			_ => CategoryToggleState.All
		};

	private void SaveAndUpdateAllCategoryToggleStates()
	{
		List<int>[] SaveLists=[[], [], []];
		foreach(Category Cat in Categories.Values)
			SaveLists[(int)Cat.ToggleState].Add(Cat.ID);
		Config.C.CategoryToggleStates.V=JsonUtils.Serialize(SaveLists, Compact:true);

		//I originally had this optimized to only run the update on changed items, but I decided it wasn’t worth debugging. It’s not that much compute.
		foreach(Item Item in Items.Values)
			Item.CurrentToggleState=Categories[Item.CategoryID].ToggleState;
	}

	public void LinkSelected(int ID)
	{
		if(StaticLink.IDInRange(ID))
			if(StaticLinks.TryGetValue(ID, out StaticLink SL))
				SL.Selected();
			else
				_=new PopupMessage("Invalid Static Link ID");
		else if(ItemIDInRange(ID))
			if(Items.TryGetValue(ID, out Item I))
				I.Selected();
			else
				_=new PopupMessage("Invalid Item ID");
		else
			_=new PopupMessage("Invalid ID");
	}
	public void LinkSelected(string StrID) =>
		Misc.IFF(int.TryParse(StrID, out int ID), () => LinkSelected(ID));
}