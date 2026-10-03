using UnityEngine;

namespace AboutMe
{
    // One small, reproducible texture per actor; AI supplies only validated style data.
    public sealed class PersonalitySkinSurface : MonoBehaviour
    {
        public bool Active {get;private set;}
        Material material;Texture2D texture;string key;
        public void Configure(AppearanceData a)
        {
            Active=a.character=="normal"&&a.skinPattern!="plain";
            string next=Active?a.skinPattern+"|"+a.skinSeed+"|"+a.accessoryColor:"plain";
            if(next==key)return;key=next;Release();if(!Active)return;
            texture=CreateTexture(a.skinPattern,a.skinSeed);
            var basis=Resources.Load<Material>("CampusDoll/Palette");
            material=basis?new Material(basis):new Material(Shader.Find("AboutMe/DollPalette"));
            material.name="Personality skin · "+a.skinPattern;material.SetTexture("_PatternTex",texture);material.SetColor("_SkinAccent",ToyFactory.ColorOf(a.accessoryColor));
        }
        public Material Material=>material;
        public static Texture2D CreateTexture(string pattern,int seed)
        {
            const int size=128;var pixels=new Color[size*size];float shift=(seed%97)/97f;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size,mask=0;
                float px=Mathf.Repeat(u*3+shift,1)-.5f,py=Mathf.Repeat(v*3+shift*.3f,1)-.5f;
                switch(pattern)
                {
                    case "flame":float width=.27f*(1-(py+.5f))+.04f;mask=Mathf.Abs(px+.065f*Mathf.Sin(py*11))<width&&py>-.35f&&py<.40f?1:0;break;
                    case "wave":mask=Mathf.Abs(Mathf.Sin(v*28+Mathf.Sin(u*14+shift*6)*1.2f))<.25f?1:0;break;
                    case "stars":float angle=Mathf.Atan2(py,px);float radius=.16f+.055f*Mathf.Cos(angle*5);mask=px*px+py*py<radius*radius?1:0;break;
                    case "leaf":float lx=(px+py)*.707f,ly=(py-px)*.707f;mask=lx*lx/.065f+ly*ly/.017f<1?1:0;break;
                    case "circuit":mask=(Mathf.Abs(px)<.025f&&py<.25f)||(Mathf.Abs(py-.23f)<.025f&&px>0)||(px*px+(py+.27f)*(py+.27f)<.008f)?1:0;break;
                    case "lightning":float bolt=py>0?-.11f+py*.5f:.11f+py*.5f;mask=Mathf.Abs(px-bolt)<.07f&&Mathf.Abs(py)<.38f?1:0;break;
                    case "grid":mask=Mathf.Abs(px)<.035f||Mathf.Abs(py)<.035f?1:0;break;
                    case "stripe":mask=Mathf.Repeat((u+v)*5+shift,1)<.20f?1:0;break;
                }
                pixels[y*size+x]=new Color(1,1,1,mask*.88f);
            }
            var result=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Personality pattern · "+pattern,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
            result.SetPixels(pixels);result.Apply(false,false);return result;
        }
        void Release(){if(material)DestroyOwned(material);if(texture)DestroyOwned(texture);material=null;texture=null;}
        static void DestroyOwned(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void OnDestroy(){Release();}
    }
}
