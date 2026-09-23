Shader "AboutMe/MemoryDesk"
{
    Properties
    {
        _Color ("Pastel pigment", Color) = (1,1,1,1)
        _Wood ("Wood grain", Range(0,1)) = 0
        _Sky ("Window daylight", Range(0,1)) = 0
        _DeskShadow ("Baked tabletop shadows (RG)", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Wood, _Sky;
            sampler2D _DeskShadow;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal); UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal);
                // Fixed north-window key and cool room fill: identical in the preview and the game.
                float diffuse=saturate(dot(n,normalize(float3(.439,.766,.470))));
                float hemi=saturate(n.y*.5+.5);
                float3 light=lerp(float3(.80,.80,.79),float3(.92,.91,.87),hemi)+float3(.20,.15,.085)*diffuse;
                float3 color=_Color.rgb*light;
                float grain=sin(i.world.z*.72+noise(i.world.xz*float2(.026,.073))*9);
                float fine=noise(i.world.xz*float2(.09,1.1))-.5;
                float board=floor((i.world.z+118)/19.7);
                color*=1+_Wood*(grain*.018+fine*.02+sin(board*3.17)*.014);
                // Only the horizontal desk top receives the offline projected soft shadows.
                float tabletop=(1-smoothstep(.04,.3,abs(i.world.y+14)))*smoothstep(.8,.98,n.y);
                float2 shade=tex2D(_DeskShadow,float2(i.world.x/282+.5,i.world.z/236+.5)).rg;
                color*=1-tabletop*saturate(shade.r*.24+shade.g*.24);
                // A little warm bounce on the model plinth replaces its former grey cliff appearance.
                color+=_Color.rgb*float3(.08,.044,.015)*(1-abs(n.y))*saturate((-i.world.y+2)/18);
                float skyHeight=saturate((i.world.y-12)/82);
                float3 sky=lerp(float3(.88,.94,.94),float3(.54,.76,.87),skyHeight);
                float cloud=noise(i.world.xy*float2(.035,.065))*.7+noise(i.world.xy*.12)*.3;
                sky=lerp(sky,float3(.99,.985,.96),smoothstep(.56,.76,cloud)*smoothstep(.3,.6,skyHeight)*.85);
                color=lerp(color,sky,_Sky);
                fixed4 result=fixed4(color,1); UNITY_APPLY_FOG(i.fogCoord,result); return result;
            }
            ENDCG
        }
    }
}
