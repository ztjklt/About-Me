Shader "AboutMe/KimiGirlPalette" {
 SubShader { Tags {"RenderType"="Opaque"} Cull Off
  Pass { Tags {"LightMode"="ForwardBase"}
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;fixed4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1)};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
   fixed4 frag(v2f i,fixed facing:VFACE):SV_Target {float3 n=normalize(i.normal)*sign(facing);float lit=saturate(dot(n,normalize(_WorldSpaceLightPos0.xyz)));fixed4 c=fixed4(i.color.rgb*(.70+.30*lit),1);UNITY_APPLY_FOG(i.fogCoord,c);return c;}
   ENDCG
  }
  UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
 }
 FallBack "Diffuse"
}
