using SilkDev;
using UnityEngine;

namespace PharloomAtlas;

//The physical Unity GameObject map icons
public class MapIcon
{
	//Basic instance members
	public GameObject? IconGO => MyGO!.NullSafe;
	private readonly GameObject? MyGO;
	private static float CurrentZ=1.9f;

	public MapIcon(Item Item, Sprite MySprite)
	{
		//Create the new GameObject and set its location
		MyGO=new GameObject($"Pin - {Item.Title} [{Item.ID}]");
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

	public CategoryToggleState CTS
	{
		private get;
		set {
			if(field==value || value==CategoryToggleState.Unknown)
				return;
			field=value;
			UpdateActive(true);
		}
	} = CategoryToggleState.Unknown;
	private void UpdateActive(bool _)
	{
		IconGO?.SetActive(
			    CTS==CategoryToggleState.All
			|| (CTS==CategoryToggleState.Incomplete && !IsFound)
		);
		SetIconColor();
	}

	public	bool IsFound	{ get;			set => Misc.IFF(field!=value, () => UpdateActive(field=value)); } = false;
	public	bool IsHovered	{ get; internal	set => Misc.IFF(field!=value, () => SetIconColor(field=value)); } = false;
	public	bool IsSelected	{ get;			set => Misc.IFF(field!=value, () => SetIconColor(field=value)); } = false;
	public	bool IsLinked	{ get;			set => Misc.IFF(field!=value, () => SetIconColor(field=value)); } = false;
	private void SetIconColor(bool _) => SetIconColor();

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
			Log.Error($"Could not load icon shader: {e.Message}");
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