using SilkDev;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PharloomAtlas;

public class DataStorage
{
	public readonly CategoryGroup[] CategoryGroups;
	public readonly Dictionary<int, Category> Categories=[];
	public readonly Dictionary<int, Item> Items;
	public readonly Texture2D IconPicsTex;
	private const int IconLenX=10, IconLenY=8, IconWidth=65, IconHeight=65, IconPadding=1;
	private const string IconFile="Icons.png";

	//Load the data from the files
	internal DataStorage()
	{
		//Utility functions
		static string GetPluginFile(string FileName) => FileOps.PathCombine(Misc.GetPluginPath, FileName);
		static T LoadJSON<T, T2>(string FileName) where T2: class =>
			FileOps.DeserializeJson<T, T2>(FileOps.ReadFile(GetPluginFile(FileName)))!;

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
			Items=LoadJSON<Dictionary<int, Item>, Item>("items.json") ?? throw new Exception("Items is null");
		} catch(Exception e) {
			throw new Exception($"Could not load items, failing out: {e.Message}");
		}
		var MatchedIcons=MonitorSaveValues.Self.GetMatchedIcons;
		foreach((int ItemID, Item ItemData) in Items) {
			ItemData.ID=ItemID;
			ItemData.IsLinked=MatchedIcons.ContainsKey(ItemID);
			if(Categories.ContainsKey(ItemData.CategoryID))
				continue;
			Log.Error($"Invalid CategoryID[#{ItemData.CategoryID}] on Item[#{ItemID}]");
			ItemData.CategoryID=Categories.First().Key;
		}

		//Create the texture and sprites
		IconPicsTex=new Texture2D(2, 2, TextureFormat.ARGB32, false);
		if(!IconPicsTex.LoadImage(FileOps.LoadLocalFileOrResource(IconFile).ReadAllAndCloseB()))
			throw new Exception($"Could not load icons texture, failing out");
		IconPicsTex.Apply();
		foreach(Category Category in Categories.Values) {
			int x=Category.IconID%IconLenX, y=Category.IconID/IconLenX;
			if(y>IconLenY)
				throw new Exception($"Invalid Icon ID for Category \"{Category.Title}\": {Category.IconID}>{IconLenX*IconLenY}");
			Rect IconRect=new(x*(IconWidth+IconPadding), (IconLenY-y-1)*(IconHeight+IconPadding), IconWidth, IconHeight);
			Category.Sprite=Sprite.Create(IconPicsTex, IconRect, new Vector2(0.5f, 0.5f), 100f);
		}

		LoadCategoryToggleStates(true);
	}

	//Load the category toggle states
	internal void LoadCategoryToggleStates(bool FirstRun)
	{
		try {
			//After deserialization works on the lists, set all categories to Incomplete by default
			int[][] CatIDsLists=FileOps.DeserializeJson<int[][]>(Config.C.CategoryToggleStates) ?? throw new Exception("Conversion failed");
			foreach(Category Cat in Categories.Values)
				Cat.ToggleState=CategoryToggleState.Incomplete;

			//Load the categories from the settings
			foreach((int CatToggleState, int[] CatIDs) in CatIDsLists.Take((int)CategoryToggleState.Unknown).Entries())
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

	internal void LoadIcons()
	{
		//Create all the icons
		foreach(Item Item in Items.Values) {
			Category MyCategory=Categories[Item.CategoryID];
			MyCategory.TotalCount++;
			MyCategory.CurrentCount++;
			Item.MapIcon=new MapIcon(Item, Categories[Item.CategoryID].Sprite);
		}
	}

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
		Config.C.CategoryToggleStates.V=FileOps.SerializeToJSON(SaveLists, true);

		//I originally had this optimized to only run the update on changed items, but I decided it wasn’t worth debugging. It’s not that much compute.
		foreach(Item Item in Items.Values)
			Item.CurrentToggleState=Categories[Item.CategoryID].ToggleState;
	}
}