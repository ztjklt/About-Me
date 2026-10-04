using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AboutMe
{
    public enum UIVisualState { Normal, Selected, Loading, Recording, Success, Error }

    public interface IUIStateful
    {
        void SetState(UIVisualState state);
    }

    public static class UITheme
    {
        public static readonly Color Ink=Hex("#202A3D"), Cream=Hex("#F5EFE3"), Paper=Hex("#FFF9EF");
        public static readonly Color Mint=Hex("#79CFC5"), Pink=Hex("#F3AFC2"), Yellow=Hex("#FFD773"), Purple=Hex("#9FAFF2");
        public static readonly Color Orange=Hex("#F49A6B"), Green=Hex("#78BE8C"), Red=Hex("#E77770"), Muted=Hex("#8D928E");
        public static readonly Color Field=Hex("#ECECE5"), Shadow=Hex("#202A3D66"), White=Color.white;
        static Sprite rounded, circle;
        static readonly Dictionary<string,Sprite> icons=new Dictionary<string,Sprite>();
        static Texture2D iconAtlas;

        public static Color Hex(string value)
        { ColorUtility.TryParseHtmlString(value,out var color);return color; }

        public static Sprite Rounded
        {
            get
            {
                if(rounded)return rounded;
                rounded=CreateRounded(64,14,new Vector4(18,18,18,18));rounded.name="UI Rounded 9 Slice";return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if(circle)return circle;
                circle=CreateRounded(64,31,Vector4.zero);circle.name="UI Circle";return circle;
            }
        }

        static Sprite CreateRounded(int size,float radius,Vector4 border)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true){name="About Me UI Shape",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[size*size];float edge=radius-.75f;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float px=Mathf.Abs(x-(size-1)*.5f)-(size*.5f-radius);float py=Mathf.Abs(y-(size-1)*.5f)-(size*.5f-radius);
                float distance=Mathf.Sqrt(Mathf.Max(px,0)*Mathf.Max(px,0)+Mathf.Max(py,0)*Mathf.Max(py,0))+Mathf.Min(Mathf.Max(px,py),0)-radius;
                byte alpha=(byte)Mathf.RoundToInt(Mathf.Clamp01(.5f-distance)*255);pixels[y*size+x]=new Color32(255,255,255,alpha);
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);
        }

        public static Sprite Icon(string name)
        {
            if(iconAtlas==null)BuildIconAtlas();
            return icons.TryGetValue(name,out var sprite)?sprite:icons["status"];
        }

        static void BuildIconAtlas()
        {
            const int cell=64, columns=4, rows=3;
            iconAtlas=new Texture2D(cell*columns,cell*rows,TextureFormat.RGBA32,false,true){name="About Me UI Icon Atlas",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var clear=new Color32[cell*columns*cell*rows];iconAtlas.SetPixels32(clear);
            string[] names={"mic","settings","share","bag","exit","attack","grab","jump","camera","status","ready","crown"};
            for(int i=0;i<names.Length;i++)
            {
                int ox=(i%columns)*cell,oy=(rows-1-i/columns)*cell;DrawIcon(names[i],ox,oy,cell);
                var sprite=Sprite.Create(iconAtlas,new Rect(ox,oy,cell,cell),new Vector2(.5f,.5f),64,0,SpriteMeshType.FullRect);sprite.name="UI Icon "+names[i];icons[names[i]]=sprite;
            }
            iconAtlas.Apply(false,true);
        }

        static void Pixel(int x,int y,int ox,int oy,int size)
        {if(x>=0&&y>=0&&x<size&&y<size)iconAtlas.SetPixel(ox+x,oy+y,Color.white);}
        static void Dot(int cx,int cy,int radius,int ox,int oy,int size,bool fill=true)
        {
            for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++)
            {float d=x*x+y*y;if(fill?d<=radius*radius:d<=radius*radius&&d>=(radius-3)*(radius-3))Pixel(cx+x,cy+y,ox,oy,size);}
        }
        static void Line(int x0,int y0,int x1,int y1,int thickness,int ox,int oy,int size)
        {
            int steps=Mathf.Max(Mathf.Abs(x1-x0),Mathf.Abs(y1-y0));
            for(int i=0;i<=steps;i++){float t=steps==0?0:i/(float)steps;Dot(Mathf.RoundToInt(Mathf.Lerp(x0,x1,t)),Mathf.RoundToInt(Mathf.Lerp(y0,y1,t)),thickness,ox,oy,size);}
        }
        static void RectLine(int x,int y,int w,int h,int thickness,int ox,int oy,int size)
        {Line(x,y,x+w,y,thickness,ox,oy,size);Line(x+w,y,x+w,y+h,thickness,ox,oy,size);Line(x+w,y+h,x,y+h,thickness,ox,oy,size);Line(x,y+h,x,y,thickness,ox,oy,size);}
        static void DrawIcon(string name,int ox,int oy,int size)
        {
            switch(name)
            {
                case "mic": RectLine(25,22,14,24,3,ox,oy,size);Dot(32,45,7,ox,oy,size,true);Line(19,34,19,41,2,ox,oy,size);Line(45,34,45,41,2,ox,oy,size);Line(19,41,25,48,2,ox,oy,size);Line(45,41,39,48,2,ox,oy,size);Line(32,49,32,57,2,ox,oy,size);break;
                case "settings": Dot(32,32,17,ox,oy,size,false);Dot(32,32,6,ox,oy,size,false);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(32+Mathf.RoundToInt(Mathf.Cos(a)*17),32+Mathf.RoundToInt(Mathf.Sin(a)*17),32+Mathf.RoundToInt(Mathf.Cos(a)*24),32+Mathf.RoundToInt(Mathf.Sin(a)*24),3,ox,oy,size);}break;
                case "share": Dot(19,31,6,ox,oy,size);Dot(45,17,6,ox,oy,size);Dot(45,47,6,ox,oy,size);Line(24,28,40,20,2,ox,oy,size);Line(24,34,40,44,2,ox,oy,size);break;
                case "bag": RectLine(12,22,40,32,3,ox,oy,size);Line(23,22,23,14,3,ox,oy,size);Line(23,14,41,14,3,ox,oy,size);Line(41,14,41,22,3,ox,oy,size);break;
                case "exit": RectLine(10,12,27,40,3,ox,oy,size);Line(28,32,55,32,3,ox,oy,size);Line(46,23,55,32,3,ox,oy,size);Line(46,41,55,32,3,ox,oy,size);break;
                case "attack": Line(15,49,46,18,4,ox,oy,size);Line(38,14,51,14,3,ox,oy,size);Line(51,14,51,27,3,ox,oy,size);Line(13,51,21,51,4,ox,oy,size);break;
                case "grab": Dot(22,37,7,ox,oy,size);Dot(30,26,6,ox,oy,size);Dot(39,24,6,ox,oy,size);Dot(47,28,6,ox,oy,size);Line(18,40,27,52,5,ox,oy,size);Line(27,52,43,50,5,ox,oy,size);break;
                case "jump": Line(15,48,32,19,4,ox,oy,size);Line(32,19,49,48,4,ox,oy,size);Line(32,19,32,54,3,ox,oy,size);break;
                case "camera": RectLine(10,21,44,30,3,ox,oy,size);RectLine(18,14,13,7,2,ox,oy,size);Dot(34,36,11,ox,oy,size,false);Dot(48,27,3,ox,oy,size);break;
                case "ready": Line(13,33,26,47,4,ox,oy,size);Line(26,47,52,18,4,ox,oy,size);break;
                case "crown": Line(10,22,18,48,3,ox,oy,size);Line(18,48,48,48,3,ox,oy,size);Line(48,48,55,22,3,ox,oy,size);Line(10,22,25,35,3,ox,oy,size);Line(25,35,32,18,3,ox,oy,size);Line(32,18,41,35,3,ox,oy,size);Line(41,35,55,22,3,ox,oy,size);break;
                default: Dot(32,32,19,ox,oy,size,false);Dot(32,22,3,ox,oy,size);Line(32,31,32,45,3,ox,oy,size);break;
            }
        }

        public static void Surface(Image image,Color color,bool outline=true,bool shadow=false)
        {
            image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=color;
            if(outline){var edge=image.gameObject.AddComponent<Outline>();edge.effectColor=Ink;edge.effectDistance=new Vector2(3,-3);edge.useGraphicAlpha=true;}
            if(shadow){var shade=image.gameObject.AddComponent<Shadow>();shade.effectColor=Shadow;shade.effectDistance=new Vector2(8,-9);shade.useGraphicAlpha=true;}
        }
    }

    public class StickerButton : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,IPointerClickHandler,IUIStateful
    {
        public Image Surface;public Text Label;public Image Icon;public Button Button;public Color BaseColor=UITheme.Paper;public float HoverScale=1;
        public UIVisualState State;
        bool hovered,pressed,lastEnabled=true;Vector3 targetScale=Vector3.one;float rebound,clickFlash;
        public void Configure(Button button,Image surface,Text label,Color color)
        {Button=button;Surface=surface;Label=label;BaseColor=color;button.transition=Selectable.Transition.None;Apply();}
        public void SetState(UIVisualState state){State=state;if(!pressed)targetScale=state==UIVisualState.Selected&&Label==null?Vector3.one*1.12f:Vector3.one;Apply();}
        public void OnPointerEnter(PointerEventData eventData){hovered=true;if(!pressed&&rebound<=0)targetScale=Vector3.one*HoverScale;Apply();}
        public void OnPointerExit(PointerEventData eventData){hovered=false;pressed=false;targetScale=Vector3.one;Apply();}
        public void OnPointerDown(PointerEventData eventData){if(Button&&Button.interactable){pressed=true;rebound=0;targetScale=Vector3.one*.94f;Apply();}}
        public void OnPointerUp(PointerEventData eventData){pressed=false;targetScale=Vector3.one*(hovered?HoverScale:1);Apply();}
        public void OnPointerClick(PointerEventData eventData)
        {if(Button&&Button.interactable){rebound=.09f;clickFlash=1;targetScale=Vector3.one*1.045f;Apply();}}
        void Update()
        {
            float dt=Time.unscaledDeltaTime;
            if(rebound>0){rebound-=dt;if(rebound<=0)targetScale=Vector3.one*(hovered?HoverScale:1);}
            if(clickFlash>0){clickFlash=Mathf.Max(0,clickFlash-dt*8);Apply();}
            transform.localScale=PlayerPrefs.GetInt("reduceMotion",0)==1?Vector3.one:Vector3.Lerp(transform.localScale,targetScale,1-Mathf.Exp(-dt*25));
            bool enabled=Button&&Button.interactable;if(enabled!=lastEnabled){lastEnabled=enabled;Apply();}
        }
        void Apply()
        {
            if(!Surface)return;Color color=BaseColor;
            if(Button&&!Button.interactable)color=Color.Lerp(UITheme.Field,UITheme.Muted,.18f);
            else if(State==UIVisualState.Selected)color=Label==null?BaseColor:UITheme.Yellow;
            else if(State==UIVisualState.Loading)color=UITheme.Purple;
            else if(State==UIVisualState.Recording)color=UITheme.Pink;
            else if(State==UIVisualState.Success)color=UITheme.Green;
            else if(State==UIVisualState.Error)color=UITheme.Red;
            else if(pressed)color=Color.Lerp(BaseColor,UITheme.Ink,.15f);
            else if(hovered)color=Color.Lerp(BaseColor,Color.white,.25f);
            Surface.color=Color.Lerp(color,Color.white,clickFlash*.25f);
            if(Label)Label.color=(State==UIVisualState.Error||BaseColor==UITheme.Ink)?UITheme.Paper:UITheme.Ink;
        }
    }

    public class UIReveal : MonoBehaviour
    {
        CanvasGroup group;float time;bool running;
        void Awake(){group=gameObject.GetComponent<CanvasGroup>()??gameObject.AddComponent<CanvasGroup>();}
        void OnEnable(){if(!group)Awake();time=0;running=true;group.alpha=0;transform.localScale=Vector3.one*.94f;}
        void Update()
        {
            if(!running)return;if(PlayerPrefs.GetInt("reduceMotion",0)==1){group.alpha=1;transform.localScale=Vector3.one;running=false;return;}time+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(time/.28f);float eased=1-Mathf.Pow(1-t,3);group.alpha=eased;transform.localScale=Vector3.one*(.94f+.06f*(eased+Mathf.Sin(t*Mathf.PI)*.08f));if(t>=1){running=false;transform.localScale=Vector3.one;}
        }
    }

    public class UIPulse : MonoBehaviour
    {
        float time;
        public void Play(){if(PlayerPrefs.GetInt("reduceMotion",0)==1)return;time=.42f;transform.localScale=Vector3.one*.88f;}
        void Update(){if(PlayerPrefs.GetInt("reduceMotion",0)==1){time=0;transform.localScale=Vector3.one;return;}if(time<=0)return;time-=Time.unscaledDeltaTime;float t=1-Mathf.Clamp01(time/.42f);transform.localScale=Vector3.one*(1+Mathf.Sin(t*Mathf.PI)*.09f);if(time<=0)transform.localScale=Vector3.one;}
    }

    public class UISafeArtboard : MonoBehaviour
    {
        public RectTransform SafeRoot,Artboard;Rect lastSafe;int lastW,lastH;float lastScale;
        void Start(){Apply();}
        void LateUpdate(){var canvas=GetComponent<Canvas>();if(lastW!=Screen.width||lastH!=Screen.height||lastSafe!=Screen.safeArea||!Mathf.Approximately(lastScale,canvas.scaleFactor))Apply();}
        public void Apply()
        {
            if(!SafeRoot||!Artboard||Screen.width<=0||Screen.height<=0)return;
            lastW=Screen.width;lastH=Screen.height;lastSafe=Screen.safeArea;
            SafeRoot.anchorMin=new Vector2(lastSafe.xMin/lastW,lastSafe.yMin/lastH);SafeRoot.anchorMax=new Vector2(lastSafe.xMax/lastW,lastSafe.yMax/lastH);SafeRoot.offsetMin=SafeRoot.offsetMax=Vector2.zero;
            // Convert safe-area pixels to canvas units once; also letterbox 4:3 / ultrawide.
            lastScale=GetComponent<Canvas>().scaleFactor;
            float fit=Mathf.Min(lastSafe.width/(1600*Mathf.Max(.001f,lastScale)),lastSafe.height/(900*Mathf.Max(.001f,lastScale)));
            Artboard.sizeDelta=new Vector2(1600,900);Artboard.anchoredPosition=Vector2.zero;Artboard.localScale=Vector3.one*fit;
        }
    }
}
