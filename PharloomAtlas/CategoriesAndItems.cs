using SilkDev;
using SilkDev.Textures;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Misc=SilkDev.Misc;

namespace PharloomAtlas;

public enum CategoryToggleState
{
	All=0, Incomplete, None, Unknown //Unknown must be last
}

//Category groups (title and list of categories)
public class CategoryGroup : Dictionary<int, Category>
{
	public string Title=Misc.Empty;
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
	public string Title=Misc.Empty;
	public Sprite Sprite	{ get; internal set; } = null!;
	public CategoryToggleState ToggleState=CategoryToggleState.Unknown;
//		internal Category() {} //Not yet ready for other people to make these. Would need some work.
}

//Items (icons)
public class Item
{
	public int ID			{ get; internal set; }
	public int CategoryID	{ get; internal set; } //Locking down CategoryID to make sure only registered categories are used
	public Sprite Sprite	{ get; internal set; } = null!;
	public int IconID=-1;
	public string Title=Misc.Empty;
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

	//Render the description
	public string Description => ToString();
	public override string ToString() =>
		string.Join(Misc.NewLine, (new string?[] {
			WhereAt	?.Render("Where"		),
			Notes	?.Render("Notes"		),
			Effect	?.Render("Effect"		),
			Tip		?.Render("Tip"			),
			Reqs	?.Render("Requirements"	),
			Needs	?.Render("Needs"		),
			Rewards	?.Render("Rewards"		),
			Store	?.Render("Store"		),
		}).Where(static V => V!=null));

	//Get the title from the item ID (cannot be ran until after all Items are loaded, which is why below objects have delayed string rendering)
	private static string GetItemTitleFromID(string ID) =>
		  !int.TryParse(ID, out int i) ? ID
		: i<1000 ? (MapControl.Self?.DS.StaticLinks.Get(i)?.Name ?? ID)
		: (MapControl.Self?.DS.Items.Get(i)?.Title ?? ID);

	//A full chain list for a single field
	public class ChainList
	{
		public readonly Item Parent;
		public readonly string StartString;
		public string RenderedString => field ??= FinishInternalRender();
		public readonly RenderedField? ExtraStr;
		public readonly ChainItem[][]? Items;
		internal ChainList(Item Parent, string ItemList)
		{
			(this.Parent, StartString)=(Parent, ItemList);

			//Get and remove the extra string part
			int ExtraStrPos=ItemList.IndexOf('^');
			if(ExtraStrPos!=-1) {
				ExtraStr=new RenderedField(Parent, ItemList[(ExtraStrPos+1)..]);
				ItemList=ItemList[0..ExtraStrPos];
			}

			//Parse the list
			if(ItemList!=Misc.Empty)
				Items=[..ItemList.Split('|').Select((OrStr, GroupIndex) =>
					OrStr.Split('`').Select((ItemStr, ItemIndex) =>
						new ChainItem(this, ItemStr, GroupIndex, ItemIndex)
					).ToArray()
				)];
		}
		private string FinishInternalRender()
		{
			//If no list, just use the extra string
			if(Items==null)
				return ExtraStr?.ToString() ?? Misc.Empty;

			//Reformat the list
			string Ret=string.Join(" <b><color=purple>OR</color></b> ", Items.Select(static ItemList =>
				string.Join(", ", ItemList.Select(static I => I.RenderedString))
			));

			//Combine the list and the extra string
			return
				  ExtraStr==null ? Ret
				: $"{Ret}; {ExtraStr}";
		}

		public string Render(string FieldTitle) => $"<b>{Misc.SanitizeRichString(FieldTitle)}</b>: "+RenderedString;
	}

	//A single item in a ChainList
	public class ChainItem
	{
		public readonly ChainList Parent;
		public readonly string StartString;
		public string RenderedString => field ??= FinishInternalRender();
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
		private string FinishInternalRender()
		{
			//Add flags back
			List<string> Parts=[];
			if(FlagNot		) Parts.Add("<i>NOT</i> "			);
			if(FlagStarted	) Parts.Add("<i>STARTED</i> "		);
			if(FlagRecommend) Parts.Add("<i>RECOMMENDED</i> "	);
			if(FlagAmount!=1) Parts.Add($"{FlagAmount}*"		);

			//Render as a linked item if item is found
			string ItemValue=Name, NewItemValue;
			if(!FlagUnlinked && (NewItemValue=GetItemTitleFromID(ItemValue))!=ItemValue)
			{
				Name=NewItemValue;
				LinkID=int.Parse(ItemValue);
				return $"<LinkID={Parent.Parent.GetLinkID}><ATTR=GroupID>{GroupID}</ATTR><ATTR=GroupIndex>{GroupIndex}</ATTR><ATTR=ItemID>{LinkID}</ATTR><u>"+string.Join(Misc.Empty, [.. Parts, NewItemValue])+"</u></LinkID>";
			}

			//If unlinked or linking failed do do not make it a real link
			Name=ItemValue;
			return "<u>"+string.Join(Misc.Empty, [.. Parts, ItemValue])+"</u>";
		}
	}

	//A string with item links inside square brackets rendered as actual links
	public class RenderedField
	{
		//Turn item links in a string into actual links
		private static readonly Regex GetLinks=new(@"\[(\d+)(~[^^|`\]]+)?]");

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
						Text=!string.IsNullOrEmpty(Text) ? Text[1..] : GetItemTitleFromID(ID);
						return $"<LinkID={Parent.GetLinkID}><ATTR=RepIndx>{ReplaceIndex++}</ATTR><ATTR=ItemID>{ID}</ATTR><u>{Text}</u></LinkID>";
					}
				);
		}
		public override string ToString() => RenderedString;
		public string Render(string FieldTitle) => $"<b>{Misc.SanitizeRichString(FieldTitle)}</b>: "+RenderedString;
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

	public bool IsFound
	{
		get;
		set {
			if(field==value)
				return;
			MapControl.Self.DS.Categories[CategoryID].CurrentCount+=(value ? 1 : -1);
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
		public new string? WhereAt	{ set => Misc.IFF(value!=null, () => base.WhereAt	=new RenderedField	(this, value!)); }
		public new string? Notes	{ set => Misc.IFF(value!=null, () => base.Notes		=new RenderedField	(this, value!)); }
		public new string? Effect	{ set => Misc.IFF(value!=null, () => base.Effect	=new RenderedField	(this, value!)); }
		public new string? Tip		{ set => Misc.IFF(value!=null, () => base.Tip		=new RenderedField	(this, value!)); }
		public new string? Reqs		{ set => Misc.IFF(value!=null, () => base.Reqs		=new ChainList		(this, value!)); }
		public new string? Needs	{ set => Misc.IFF(value!=null, () => base.Needs		=new ChainList		(this, value!)); }
		public new string? Rewards	{ set => Misc.IFF(value!=null, () => base.Rewards	=new ChainList		(this, value!)); }

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
					: new ChainList(this, Item.Reqs		),
					  new ChainList(this, Item.Needs	),
					  new ChainList(this, Item.Rewards	)
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
		public string RenderedString => field ??= FinishInternalRender();
		public StoreItem[] Items=Items;
		private string FinishInternalRender() =>
			string.Join(Misc.Empty, Items.Select(static I =>
				"\n- "+I.Rewards.RenderedString+" for "+I.Needs.RenderedString+
				(I.Reqs!=null ? $" (Required: {I.Reqs.RenderedString})" : Misc.Empty)
			));
		public string Render(string FieldTitle) => $"<b>{Misc.SanitizeRichString(FieldTitle)}</b>: "+RenderedString;
	}

	//TODO: Temporarily remove links until ClickableLabel class is ready
	static Item()
	{
		CurrentLinkColor=Config.C.Color_Link.V.Hex;
		Config.C.Color_Link.SettingChanged += (_, _) => CurrentLinkColor=Config.C.Color_Link.V.Hex;
	}
	private static string CurrentLinkColor=null!;
	private static readonly Regex ReplaceLinkIDs=new(@"<LinkID=[^>]+>(.*?)</LinkID>", RegexOptions.IgnoreCase);
	private static readonly Regex RemoveAttrs=new(@"<ATTR\s*=([^>\n]+)>(.*?)</ATTR>", RegexOptions.IgnoreCase);
	public static string StripLinkIDTags(string Str) =>
		ReplaceLinkIDs.Replace(RemoveAttrs.Replace(Str, Misc.Empty), $"<color=#{CurrentLinkColor}>$1</color>");

	//Selected via a link
	public void Selected() => MapControl.Self.SelectAndCenterItemI(ID);
}

public class StaticLink(string Name, int CategoryID, int[]? ItemIDs)
{
	public string Name=Name;
	public int CategoryID=CategoryID;
	public int[]? ItemIDs=ItemIDs;

	//JSON type conversion
	internal class CreateStaticLinks
	{
		public Dictionary<string, List<object>> StaticLinks=[];
		public Dictionary<int, StaticLink> Process()
		{
			Dictionary<int, StaticLink> Out=[];
			foreach((string ID, List<object> L) in StaticLinks)
				try {
					//Unlinked
					if(L.Count==1) {
						Out[int.Parse(ID)]=new StaticLink((string)L[0], -1, null);
						continue;
					}

					//Category or item lists
					bool IsCategory=(L.Count==2 && (int)(long)L[1]<1000);
					Out[int.Parse(ID)]=new StaticLink(
						(string)L[0],
						IsCategory ? (int)(long)L[1] : -1,
						IsCategory ? null : [..L.Skip(1).Select(static I => (int)(long)I)]
					);
				} catch(System.Exception e) {
					Log.Error($"Error parsing Static Link {ID}: {e.Message}", L, L[1].GetType().Name);
				}
			return Out;
		}
	}

	//Selected via a link
	public void Selected() =>
		_=new SilkDev.Windows.PopupMessage("Category selection is not yet supported");
}