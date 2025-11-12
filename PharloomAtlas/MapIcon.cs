using SilkDev;
using UnityEngine;

namespace PharloomAtlas;

//The physical Unity GameObject map icons
public class MapIcon
{
	public GameObject? IconGO => MyGO.NullSafe();
	private readonly GameObject? MyGO;
	public bool IsFound		{ get; private set; } = false;
	public bool IsHovered	{ get; private set; } = false;
	public bool IsSelected	{ get; private set; } = false;
	public bool IsLinked	{ get; private set; } = false;
	private CategoryToggleState CTS=CategoryToggleState.Unknown;

	public MapIcon(Item Item, Sprite MySprite)
	{
		//Create the new GameObject and set its location
		MyGO=new GameObject("Pin - "+Item.Title);
		MyGO.transform.SetLocalPosition2D(Item.Pos);
		MyGO.layer=5;

		//Turn it into a Sprite
		SpriteRenderer IconSprite=MyGO.AddComponent<SpriteRenderer>();
		IconSprite.sprite=MySprite;
		IconSprite.sortingLayerName="HUD";

		//Add it to the heiarchy
		MyGO.transform.SetParent(MapControl.Self.AllIcons, false);
	}

	public void UpdateState(CategoryToggleState NewState)
	{
		if(CTS==NewState || NewState==CategoryToggleState.Unknown)
			return;

		CTS=NewState;
		SetIconColor();
		SetIsFound(IsFound);
	}

	public void SetIsFound(bool IsFound)
	{
		this.IsFound=IsFound;
		IconGO?.SetActive(CTS==CategoryToggleState.All || (CTS==CategoryToggleState.Incomplete && !IsFound));
		SetIconColor();
	}

	internal void SetHovered  (bool State) => SetIconColor(IsHovered=State);
	public   void SetSelected (bool State) => SetIconColor(IsSelected=State);
	public   void SetIsLinked (bool State) => SetIconColor(IsLinked=State);
	private  void SetIconColor(bool _	 ) => SetIconColor();

	internal void SetIconColor() =>
		IconGO?.GetComponent<SpriteRenderer>().color=
			  IsSelected ? Color.green
			: IsHovered  ? Color.blue
			: MapControl.Self.ShowLinkedStatus && !IsLinked ? Color.red
			: CTS==CategoryToggleState.All && IsFound ? new Color(.5f, .5f, .5f, .85f)
			: Color.white;

	public void UpdateSize(float IconSize) =>
		IconGO?.transform.localScale=new Vector3(IconSize, IconSize, 1f);
}