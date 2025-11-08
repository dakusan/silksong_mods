Shader "tk2d/OverlayBlend"
{
    Properties
    {
        _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
        _OverlayTex ("Overlay Texture", 2D) = "white" {}
        _OverlayTex_ST ("Overlay ST", Vector) = (1,1,0,0) // For scale (xy) and offset (zw)
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
            #pragma fragmentoption ARB_precision_hint_fastest

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _OverlayTex;
            float4 _MainTex_ST;
            float4 _OverlayTex_ST; // Scale (xy) and offset (zw)

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
                return o;
            }

            fixed4 frag (v2f IN) : COLOR
            {
                fixed4 col = tex2D(_MainTex, IN.texcoord) * IN.color;
                float2 overlayUV = IN.texcoord * _OverlayTex_ST.xy + _OverlayTex_ST.zw; // Apply scale and offset
                fixed4 overlay = tex2D(_OverlayTex, overlayUV);
                if (col.a > 0) col.rgb = lerp(col.rgb, overlay.rgb, overlay.a);
                return col;
            }
            ENDCG
        }
    }
}