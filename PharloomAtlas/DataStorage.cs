using SilkDev;
using SilkDev.JSON;
using SilkDev.Textures;
using SilkDev.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

	//Create icon sprites when needed
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
			CategoryGroupsDict=LoadJSON<Dictionary<string, CategoryGroup>, Category>("categories.json") ?? //RegEx adds ID
				throw new Exception("Categories is null");
		} catch(Exception e) {
			throw new Exception($"Could not load categories, failing out: {e.Message}");
		}

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
			Items=(LoadJSON<Dictionary<int, Item.CreateItem>, Item.CreateItem>("items.json") ?? throw new Exception("Items is null"))
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

		//Load the static links
		try {
			(LoadJSON<LoadMisc, LoadMisc>("Misc.json") ?? throw new Exception("Misc is null")).Process(this);
		} catch(Exception e) {
			Catcher.OutputException("Loading static links", e);
			throw new Exception($"Could not load static links, failing out: {e.Message}");
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

	//Link colors. Do not add any other public properties unless they are colors
	public sealed class LinkColorsT : ColorsSet
	{
		//The following of these are set statically so realtime changing is not supported (for now): Flag_{NOT,STARTED,RECOMMENDED}, Sep_{OR,AND}
		public string Default			{ get; set => field=SetLinkColor(value, field, "cyan"		); } = null!; //Default link color
		public string LinkHover			{ get; set => field=SetLinkColor(value, field, "yellow"		); } = null!; //Color when a link has the mouse over it
		public string LabelHover		{ get; set => field=SetLinkColor(value, field, "#4678C880"	); } = null!; //Box color for the entire label when mouse over (in the search box); Desaturated, mid-luminance blue goes well with: red, teal, plum, yellow, cyan, white, black, green
		public string Flag_NOT			{ get; set => field=SetLinkColor(value, field, "red"		); } = null!; //Flag color (precedence=0) for NOT
		public string Flag_STARTED		{ get; set => field=SetLinkColor(value, field, "teal"		); } = null!; //Flag color (precedence=1) for STARTED
		public string Flag_RECOMMENDED	{ get; set => field=SetLinkColor(value, field, "#dda0dd"	); } = null!; //Flag color (precedence=2) for RECOMMENDED [#=plum]
		public string Sep_OR			{ get; set => field=SetLinkColor(value, field, "purple"		); } = null!; //Separator for boolean OR “ OR ”
		public string Sep_AND			{ get; set => field=SetLinkColor(value, field, "white"		); } = null!; //Separator for boolean AND “, ”
		public string Strike_Found		{ get; set => field=SetLinkColor(value, field, "white"		); } = null!; //Straight line through link when item has been found
		public string Strike_Started	{ get; set => field=SetLinkColor(value, field, "silver"		); } = null!; //Wavy line through link when item has been started (and not found)
		public string Search_Highlight	{ get; set => field=SetLinkColor(value, field, "green"		); } = null!; //Highlighting searched string
	}

	//In case I decide to store more colors like this, I decided to make it an abstract class
	//Note: Do not add public properties to subclass unless they are colors
	public abstract class ColorsSet
	{
		//Set the colors
		public readonly string[] ColorNames;
		private readonly Dictionary<string, Color> ColorsFromName;
		protected string SetLinkColor(string RequestedValue, string PreviousValue, string Default, [System.Runtime.CompilerServices.CallerMemberName] string? ColorName=null)
		{
			//Store new values
			bool IsValid=ColorUtility.TryParseHtmlString(RequestedValue, out Color NewColor);
			string SetVal=IsValid ? RequestedValue : Default;
			ColorsFromName[ColorName!]=
				  IsValid ? NewColor
				: ColorUtility.TryParseHtmlString(Default, out Color DefaultColor) ? NewColor=DefaultColor
				: throw new InvalidOperationException("Could not parse default color: "+Default);

			//Run callbacks and set new value
			_=ColorSetCallbacks.Run(
				ColorName!,
				CB => CB(SetVal, NewColor, ColorName!, PreviousValue, RequestedValue)
			);
			return SetVal;
		}
		public ColorsSet()
		{
			Type SubclassType=GetType();
			ColorNames=[.. SubclassType.GetProperties().Where(P => P.DeclaringType==SubclassType).Select(static P => P.Name)];
			ColorsFromName=new(ColorNames.Length);
			ColorNames.ForEach(CName => {
				ColorsFromName[CName]=Color.black; //This will be immediately overwritten
				SubclassType.GetProperty(CName).SetValue(this, string.Empty);
			});
		}

		//Callbacks
		public delegate void ColorCallback(string NewValue, Color NewValueColor, string ColorName, string PreviousValue, string RequestedValue);
		private readonly SilkDev.Events.EventRegister<string, ColorCallback> ColorSetCallbacks=new("Set color callback");
		public void AddCallback(string ColorName, ColorCallback CB) => Misc.IFF(
			ColorsFromName.ContainsKey(ColorName),
			() => ColorSetCallbacks.Add(ColorName, CB)
		);
		public void RemoveCallback(string ColorName, ColorCallback CB) => Misc.IFF(
			ColorsFromName.ContainsKey(ColorName),
			() => ColorSetCallbacks.Remove(ColorName, CB)
		);

		//Get as Color
		public Color FromName(string LinkColorName) =>
			ColorsFromName.TryGetValue(LinkColorName, out Color C) ? C : throw new ArgumentException("Invalid link color name", nameof(LinkColorName));
	}
	public LinkColorsT LinkColors=new();

	private class LoadMisc
	{
		private readonly Dictionary<string, List<object>> StaticLinks=[];
		private readonly Dictionary<string, string> LinkColors=[];

		public void Process(DataStorage DS)
		{
			DS.StaticLinks.AddRange(StaticLink.Process(StaticLinks, DS.Items, DS.Categories));
			foreach(string ColorName in DS.LinkColors.ColorNames)
				if(LinkColors.TryGetValue(ColorName, out string ColorValue))
					DS.LinkColors.GetType().GetProperty(ColorName).SetValue(DS.LinkColors, ColorValue);
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
		CategoryToggleState ConfirmState=CG.First().Value.ToggleState;
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
		int[] RequiredCategoryIDs=new int[RequiredCategories.Length];
		List<int> ChangedCategories=[.. Categories.Values.Where(Cat => {
			CategoryToggleState Expected=RequiredCategories.Contains(Cat.Title) ? CategoryToggleState.Incomplete : CategoryToggleState.None;
			if(Cat.ToggleState==Expected)
				return false;
			Cat.ToggleState=Expected;
			return true;
		}).Select(static Cat => Cat.ID)];
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
		else if(Item.IDInRange(ID))
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