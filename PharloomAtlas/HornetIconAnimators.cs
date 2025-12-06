using SilkDev;
using SilkDev.Hooks;
using System;
using System.Reflection;
using UnityEngine;
using HHT=PharloomAtlas.HornetIconAnimators.HornetHighlightTypes;

namespace PharloomAtlas;

internal class HornetIconAnimators
{
	private static HornetIconAnimators _Self=null!; public static HornetIconAnimators HIA => _Self; //Singleton
	private static MapControl MC => MapControl.Self;
	private static float HighlightSpeed => Conf.HornetHighlightSpeed;
	private static Config Conf => Config.C;
	public enum HornetHighlightTypes { None, Revolve, Growing, Spin, Rainbow1, Rainbow2 }
	private HornetIconAnimator Current {
		get;
		set {
			field.Close();
			field=value;
		}
	} = new AnimatorNone();

	public HornetIconAnimators()
	{
		Misc.InitSingleton(this, ref _Self);
		Conf.HornetHighlights.SettingChanged	+= (_, _) => SettingUpdate();
		Conf.ForceDisplayCompass.SettingChanged	+= (_, _) => SilkDev.Windows.Window.OnNextFrame(SettingUpdate);
	}
	private void SettingUpdate() =>
		SetAnimator(
			MC?.MapState==MapControl.MapStateEnum.Open && MC.DisplayingCompassI
				? Conf.HornetHighlights
				: HHT.None
		);

	private void SetAnimator(HHT NewType) => Misc.IFF(
		Current.Type!=NewType,
		() => Current=NewType switch {
			HHT.None	=> new AnimatorNone			(),
			HHT.Revolve	=> new AnimatorRevolution	(),
			HHT.Growing	=> new AnimatorScaling		(),
			HHT.Spin	=> new AnimatorRotation		(),
			HHT.Rainbow1=> new AnimatorRainbow1		(),
			HHT.Rainbow2=> new AnimatorRainbow2		(),
			_			=> throw new NotImplementedException(),
		}
	);

	public void Init()	=> SetAnimator(Conf.HornetHighlights);
	public void Run()	=> Current.Run();
	public void Close()	=> SetAnimator(HHT.None);

	//Animator abstract class
	private abstract class HornetIconAnimator
	{
		public abstract HHT Type { get; }
		public abstract void Run();
		public abstract void Close();

		protected GameObject? OSprite =>
			field!.NullSafe ?? (field=MC.GameMap.transform.Find("Compass Icon").gameObject);
		protected DateTime StartAt=DateTime.Now;
		protected float TimeSpan() => (DateTime.Now.Ticks-StartAt.Ticks)%10000000000L/10000000f;
	}

	private class AnimatorNone : HornetIconAnimator
	{
		public override HHT Type => HHT.None;
		public override void Run	() { }
		public override void Close	() { }
	}
	private class AnimatorRainbow1 : HornetIconAnimator
	{
		private static readonly Material? MyMaterial;
		private readonly Material PrevMaterial;
		private const float BaseSpeed=0.25f;
		private const string BundleFile="PharloomAtlas.bundle", ShaderFile="tk2d_OverlayBlend.shader";

		public override HHT Type => HHT.Rainbow1;

		static AnimatorRainbow1()
		{
			//Create a material from the new shader
			try {
				using TypedDisposer<AssetBundle> Bundle=new(
					AssetBundle.LoadFromStream(FileOps.LoadEmbeddedResource(BundleFile)),
					static Target => Target.Unload(false)
				);
				MyMaterial=new Material(Bundle.Target.LoadAsset<Shader>(ShaderFile));
			} catch(Exception e) {
				Log.Info($"Could not load shader: {e.Message}");
				return;
			}

			//Create and set the rainbow texture
			const int NumPixels=256;
			Texture2D RainbowTexture=new(1, NumPixels, TextureFormat.ARGB32, false);
			Color[] Pixels=new Color[NumPixels];
			for(int i=0; i<NumPixels; i++) {
				Color NewColor=Color.HSVToRGB((float)i/NumPixels, 1f, 1f);
				NewColor.a=0.5f;
				Pixels[i]=NewColor;
			}
			RainbowTexture.SetPixels(Pixels);
			RainbowTexture.Apply();
			RainbowTexture.wrapMode = TextureWrapMode.Repeat;
			MyMaterial.SetTexture("_OverlayTex", RainbowTexture);

			//Set up config changes
			static void UpdateScale() => MyMaterial!.SetTextureScale("_OverlayTex", new Vector2(1f, Conf.HornetRainbow1Scale));
			Conf.HornetRainbow1Scale.SettingChanged += static (_, _) => UpdateScale();
			UpdateScale();
		}

		public AnimatorRainbow1()
		{
			//Store the old material
			Renderer SRend=OSprite!.GetComponent<Renderer>();
			PrevMaterial=SRend.material;

			//Swap in the new material if available
			if(MyMaterial==null)
				return;
			MyMaterial.mainTexture=PrevMaterial.mainTexture;
			SRend.material=MyMaterial;
		}

		public override void Close() =>
			OSprite!.GetComponent<Renderer>().material=PrevMaterial;
		public override void Run() =>
			MyMaterial?.SetTextureOffset("_OverlayTex", new Vector2(0f, -Mathf.Repeat(TimeSpan()*BaseSpeed*HighlightSpeed, 1f)));
	}

	private class AnimatorRainbow2 : HornetIconAnimator
	{
		private static readonly Type TSprite=DynamicHook.FindType("tk2dSprite")!;
		private static readonly PropertyInfo? TColor;
		private const float BaseSpeed=1.8f;
		private bool HasStopped=true;

		static AnimatorRainbow2() =>
			TColor=TSprite?.GetProperty("color");
		public override HHT Type => HHT.Rainbow2;
		public AnimatorRainbow2() => StartAt=DateTime.MinValue;

		public override void Run()
		{
			if(TColor==null)
				return;
			if((DateTime.Now-StartAt).TotalSeconds>=Conf.HornetRainbow2RunTime+Conf.HornetRainbow2WaitTime)
				(StartAt, HasStopped)=(DateTime.Now, false);
			if((DateTime.Now-StartAt).TotalSeconds>=Conf.HornetRainbow2RunTime) {
				if(!HasStopped) {
					HasStopped=true;
					SetColor(Color.white);
				}
				return;
			}
			SetColor(Color.HSVToRGB(Mathf.Repeat(TimeSpan()*BaseSpeed*HighlightSpeed, 1f), 1f, 1f));
		}

		public override void Close()
		{
			SetColor(Color.white);
			HasStopped=true;
		}

		private void SetColor(Color C) => Misc.IFF(
			OSprite!=null,
			() => TColor?.SetValue(OSprite!.GetComponent(TSprite), C)
		);
	}

	private class AnimatorScaling : HornetIconAnimator
	{
		public override HHT Type => HHT.Growing;
		private const float BaseSpeed=0.5f;
		public override void Run() => SetScale(Mathf.Repeat(TimeSpan()*BaseSpeed*HighlightSpeed, 1f));
		public override void Close() => SetScale(0);
		private void SetScale(float ScaleAmount) {
			if(OSprite==null)
				return;
			float ScaleSize=Conf.IconSize;
			ScaleSize=(Conf.IconSizeScalesWithZoom ? MC.ToZoomOut(ScaleSize) : ScaleSize);
			ScaleSize*=1+Conf.HornetGrowingMax*(ScaleAmount<0.5 ? ScaleAmount*2 : 1-(ScaleAmount-0.5f)*2);
			OSprite!.transform.localScale=new Vector3(ScaleSize, ScaleSize, 1);
		}
	}

	private class AnimatorRotation : HornetIconAnimator
	{
		public override HHT Type => HHT.Spin;
		private const float BaseSpeed=0.5f;
		public override void Run() => SetRotation(Mathf.Repeat(TimeSpan()*BaseSpeed*HighlightSpeed, 1f));
		public override void Close() => SetRotation(0);
		private void SetRotation(float Rot) => Misc.IFF(
			OSprite!=null,
			() => OSprite!.transform.localRotation=Quaternion.Euler(0, 0, (Conf.HornetSpinningClockwise ? (1-Rot) : Rot)*360)
		);
	}

	private class AnimatorRevolution : HornetIconAnimator
	{
		public override HHT Type => HHT.Revolve;
		private readonly Vector2 CharLoc=MC.CharacterPositionI;
		private const float BaseSpeed=0.8f;
		private readonly GameMap_OnUpdate GMOU=new() { IsEnabled=true };

		public override void Run()
		{
			float Angle=Mathf.Repeat(TimeSpan()*BaseSpeed*HighlightSpeed, 1f);
			Angle=(Conf.HornetRevolvingClockwise ? (1-Angle) : Angle);
			SetRevolution(Angle*2*Mathf.PI);
		}
		public override void Close()
		{
			SetLocation(CharLoc);
			GMOU.IsEnabled=false;
		}
		private void SetRevolution(float Angle) =>
			SetLocation(CharLoc+MC.ToZoomOut(new Vector2(MathF.Cos(Angle), MathF.Sin(Angle))*Conf.HornetRevolvingDist));
		private void SetLocation(Vector2 Pos) => Misc.IFF(
			OSprite!=null,
			() => OSprite!.transform.SetLocalPosition2D(Pos)
		);

		private class GameMap_OnUpdate() : LiveHook(
			new("temp.patcher.dakusan.HIC_GameMap_OnUpdate"),
			typeof(GameMap).GetMethod("Update", BindingFlags.Instance|BindingFlags.NonPublic),
			PostfixMethod: typeof(GameMap_OnUpdate).GetMethod(nameof(FixPositionStatic), BindingFlags.Static|BindingFlags.NonPublic)
		) {
			private static void FixPositionStatic()
			{
				if(HIA.Current is AnimatorRevolution AR)
					AR.Run();
			}
		}
	}
}