Shader "tk2d/OverlayBlend"
{
	Properties
	{
		_MainTex		 ("Main Texture", 2D)			= "white" {}
		_OverlayTex		 ("Overlay Texture", 2D)		= "white" {}
		_LineOffsetScale ("Line Offset Scale", Vector)	= (0,0,0,0) //Scales each line successive line (uv.xy+=pos.yx*this.xy)
	}

	SubShader
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		LOD 110
		ZWrite off
		Fog { Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 vertex: POSITION;
				fixed4 color : COLOR;
				float2 uv	 : TEXCOORD0;
			};

			sampler2D _MainTex, _OverlayTex;
			float4 _OverlayTex_ST;
			float2 _LineOffsetScale;

			v2f vert(v2f v)
			{
				v.vertex=UnityObjectToClipPos(v.vertex);
				return v;
			}

			fixed4 frag(v2f IN) : COLOR
			{
				fixed4 Col		=tex2D(_MainTex, IN.uv)*IN.color;
				fixed2 OverlayUV=IN.uv*_OverlayTex_ST.xy + IN.uv.yx*_LineOffsetScale + _OverlayTex_ST.zw;
				fixed4 Overlay	=tex2D(_OverlayTex, OverlayUV);
				return fixed4(lerp(Col.rgb, Overlay.rgb, Overlay.a), Col.a);
			}
			ENDCG
		}
	}
}