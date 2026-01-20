Shader "Custom/SpriteHSVA"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Hue	("Hue Shift",		Range(-0.5, 0.5	)) = 0
		_Sat	("Saturation",		Range(0, 2		)) = 1
		_Val	("Value",			Range(0, 2		)) = 1
		_Alpha	("Alpha Multiplier",Range(0, 2		)) = 1
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 vertex: POSITION;
				fixed4 color : COLOR;
				float2 uv	 : TEXCOORD0;
			};

			sampler2D _MainTex;
			float _Hue, _Sat, _Val, _Alpha;

			v2f vert(v2f v)
			{
				v.vertex = UnityObjectToClipPos(v.vertex);
				return v;
			}

			half3 rgb2hsv(half3 c)
			{
				half4 K=half4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
				half4 p=c.g<c.b ? half4(c.bg, K.wz) : half4(c.gb, K.xy);
				half4 q=c.r<p.x ? half4(p.xyw, c.r) : half4(c.r, p.yzx);
				half d =q.x-min(q.w, q.y);
				half e =1e-10;
				return half3(abs(q.z+(q.w-q.y)/(6.0*d+e)), d/(q.x+e), q.x);
			}

			half3 hsv2rgb(half3 c)
			{
				half3 K=half3(1.0, 2.0/3.0, 1.0/3.0);
				half3 p=abs(frac(c.xxx+K.xyz)*6.0-3.0);
				return c.z*lerp(1.0, clamp(p-1.0, 0.0, 1.0), c.y);
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				fixed4 c	 = IN.color*tex2D(_MainTex, IN.uv);
				half3 hsv	 = rgb2hsv(c.rgb);
				hsv.x		 = frac(hsv.x + _Hue);
				hsv.y		*= _Sat;
				hsv.z		*= _Val;
				c.rgb		 = hsv2rgb(hsv);
				c.a			*= _Alpha;
				return c;
			}
			ENDCG
		}
	}
}