using SilkDev;
using UnityEngine;

namespace PharloomAtlas;

//The physical Unity GameObject map icons
public class MapIcon
{
	//Basic instance members
	public GameObject? IconGO => MyGO!.NullSafe;
	private readonly GameObject? MyGO;
	public bool IsFound		{ get; private set; } = false;
	public bool IsHovered	{ get; private set; } = false;
	public bool IsSelected	{ get; private set; } = false;
	public bool IsLinked	{ get; private set; } = false;
	private CategoryToggleState CTS=CategoryToggleState.Unknown;
	private static float CurrentZ=1.9f;

	public MapIcon(Item Item, Sprite MySprite)
	{
		//Create the new GameObject and set its location
		MyGO=new GameObject("Pin - "+Item.Title);
		MyGO.transform.SetLocalPosition2D(Item.Pos);
		MyGO.transform.SetLocalPositionZ(CurrentZ);
		MyGO.layer=5;

		//Turn it into a Sprite
		SpriteRenderer IconSprite=MyGO.AddComponent<SpriteRenderer>();
		IconSprite.sprite=MySprite;
		IconSprite.sortingLayerName="HUD";
		OriginalMaterial=IconSprite.material;

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

	internal void SetIconColor()
	{
		SpriteRenderer? SR=IconGO?.GetComponent<SpriteRenderer>();
		if(SR==null)
			return;

		Color NewColor=SR.color=
			  IsSelected ? Color.green
			: IsHovered  ? Color.blue
			: MapControl.Self.ShowLinkedStatus && !IsLinked ? Color.red
			: CTS==CategoryToggleState.All && IsFound && !HasMaterial ? Config.C.Color_FoundIcon
			: Color.white;

		//Update the IsFound shader
		bool UseFoundShader=IsFound && HasMaterial && NewColor==Color.white;
		if(UseFoundShader!=IsUsingNewMaterial)
			SR.material=(IsUsingNewMaterial=UseFoundShader) ? NewMaterial : OriginalMaterial;
	}

	public void UpdateSize(float IconSize) =>
		IconGO?.transform.localScale=new Vector3(IconSize, IconSize, 1f);

	//Material shader for HSV conversion
	private const string BundleFile="PharloomAtlas.bundle", ShaderFile="SpriteHSVA.shader";
	private static readonly Material? NewMaterial;
	public static bool HasMaterial => NewMaterial!=null;
	private readonly Material OriginalMaterial;
	private bool IsUsingNewMaterial=false;
	static MapIcon()
	{
		try {
			using TypedDisposer<AssetBundle> Bundle=new(
				AssetBundle.LoadFromStream(FileOps.LoadLocalFileOrResource(BundleFile)),
				static Target => Target.Unload(false)
			);
			NewMaterial=new Material(Bundle.Target.LoadAsset<Shader>(ShaderFile));
		} catch(System.Exception e) {
			Log.Info($"Could not load icon shader: {e.Message}");
		}

		if(!HasMaterial)
			return;
		UpdateShaderColor();
		Config.C.Color_FoundIcon.SettingChanged += static (Obj, _) => UpdateShaderColor();
	}
	private static void UpdateShaderColor()
	{
		Color C=Config.C.Color_FoundIcon;
		NewMaterial!.SetFloat("_Hue"  , C.r-0.5f);
		NewMaterial .SetFloat("_Sat"  , C.g*2	);
		NewMaterial .SetFloat("_Val"  , C.b*2	);
		NewMaterial .SetFloat("_Alpha", C.a		);
	}

	public void BringToFront() => MyGO?.transform.SetLocalPositionZ(CurrentZ-=.001f);
}