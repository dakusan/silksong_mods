using System.Collections.Generic;
using UnityEngine;

namespace PharloomAtlas;

public enum CategoryToggleState
{
	All=0, Incomplete, None, Unknown //Unknown must be last
}

//Category groups (title and list of categories)
public class CategoryGroup : Dictionary<int, Category>
{
	public string Title="";
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
	public string Title="";
	public string? Info;
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
	public string Title="";
	public string? Description;
	public float x, y;
	public string[]? ImageURLs;
	public string? IgnPageName;
	public Vector2 Pos => new(x, y);
//		internal Item() {} //Not yet ready for other people to make these. Would need some work.

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
}