Shader "AboutMe/DollPalette"
{
    Properties { _PatternTex ("Personality pattern", 2D) = "white" {} _SkinAccent ("Skin accent", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            sampler2D _PatternTex;fixed4 _SkinAccent;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; fixed4 color:COLOR; float3 pattern:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; fixed4 color:COLOR; SHADOW_COORDS(1) UNITY_FOG_COORDS(2) float3 pattern:TEXCOORD3; };
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.color=v.color;o.pattern=v.pattern;TRANSFER_SHADOW(o);UNITY_TRANSFER_FOG(o,o.pos);return o;}
            fixed4 frag(v2f i):SV_Target {half diffuse=saturate(dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz)));fixed3 tint=lerp(i.color.rgb,_SkinAccent.rgb,tex2D(_PatternTex,i.pattern.xy).a*saturate(i.pattern.z));fixed4 c=fixed4(tint*(.68+.32*diffuse),1);UNITY_APPLY_FOG(i.fogCoord,c);return c;}
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}
