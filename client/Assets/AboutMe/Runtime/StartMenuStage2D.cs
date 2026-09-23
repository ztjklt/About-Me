using System;
using UnityEngine;
using UnityEngine.UI;

namespace AboutMe
{
    /// <summary>Owns the lightweight motion and reveal state for the layered start-menu key art.</summary>
    public sealed class StartMenuStage2D : MonoBehaviour
    {
        struct BubbleMotion
        {
            public RectTransform rect;
            public Vector2 rest;
            public float phase;
        }

        struct LayerMotion
        {
            public RectTransform rect;
            public Vector2 rest;
            public float parallax,amplitude,rotation,period,phase;
        }

        CanvasGroup stageGroup;
        RectTransform hero;
        RectTransform[] buttons;
        Image heroOpen,heroBlink;
        LayerMotion[] layers=Array.Empty<LayerMotion>();
        BubbleMotion[] bubbles=Array.Empty<BubbleMotion>();
        Sprite[] bubbleSprites=Array.Empty<Sprite>();
        Vector2 pointer,pointerTarget;
        Vector2 heroRest;
        Quaternion heroRestRotation;
        Vector3 heroRestScale;
        float time,reveal,nextBlink,blinkUntil;
        bool configured,expanded,motionEnabled=true,compact;

        public bool Expanded=>expanded;
        public bool MotionEnabled=>motionEnabled;

        public void Configure(RectTransform titleRoot,RectTransform heroRoot,Image openEyes,Image closedEyes,RectTransform doodleRoot,RectTransform[] armRoots,
            RectTransform[] menuButtons)
        {
            hero=heroRoot;heroOpen=openEyes;heroBlink=closedEyes;buttons=menuButtons;
            heroRest=hero.anchoredPosition;heroRestRotation=hero.localRotation;heroRestScale=hero.localScale;
            stageGroup=gameObject.GetComponent<CanvasGroup>()??gameObject.AddComponent<CanvasGroup>();
            // Keep the mascot anchored to the background; its eyes still blink independently.
            layers=new LayerMotion[2+armRoots.Length];
            layers[0]=Motion(titleRoot,8,3,.35f,4.2f,.2f);
            layers[1]=Motion(doodleRoot,10,2,.25f,3.8f,2.4f);
            for(int i=0;i<armRoots.Length;i++)layers[i+2]=Motion(armRoots[i],18,4+i%3*2,1+(i%3)*.45f,3+(i%4)*.55f,i*.73f);
            // Four entry bubbles are direct children of the opening stage.
            bubbles=new BubbleMotion[buttons.Length];bubbleSprites=new Sprite[buttons.Length];
            string[] labels={"SINGLE\nPLAYER","MULTIPLAYER","INTRO\nPRESETS","SETTINGS"};
            Color[] colors={UITheme.Mint,UITheme.Yellow,UITheme.Purple,UITheme.Pink};
            for(int i=0;i<buttons.Length;i++)StyleBubble(i,labels[i],colors[i]);
            configured=true;SetCompact(false);ResetForInitialDisplay();
        }

        void StyleBubble(int index,string label,Color fill)
        {
            var rect=buttons[index];rect.SetParent(transform,false);rect.name="Bubble · "+label.Replace('\n',' ');
            bubbles[index]=new BubbleMotion{rect=rect,phase=index*1.55f};
            var style=rect.GetComponent<StickerButton>();
            if(!style)return;
            var surface=style.Surface;
            bubbleSprites[index]=CreateBubbleSprite(fill);surface.sprite=bubbleSprites[index];surface.type=Image.Type.Simple;
            surface.preserveAspect=true;surface.color=Color.white;surface.alphaHitTestMinimumThreshold=.12f;
            style.BaseColor=Color.white;
            var shadow=rect.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.08f,.12f,.22f,.42f);
            shadow.effectDistance=new Vector2(9,-12);shadow.useGraphicAlpha=true;
            var shine=new GameObject("Bubble shine",typeof(RectTransform),typeof(Image));shine.transform.SetParent(rect,false);
            var shineRect=(RectTransform)shine.transform;Place(shineRect,32,27,78,31);
            shineRect.localRotation=Quaternion.Euler(0,0,-23);
            var shineImage=shine.GetComponent<Image>();shineImage.sprite=UITheme.Circle;shineImage.color=new Color(1,1,1,.42f);shineImage.raycastTarget=false;
            shineRect.SetAsFirstSibling();
            var icon=style.Icon;Place(icon.rectTransform,73,43,42,42);icon.color=UITheme.Ink;
            var text=style.Label;text.text=label;text.fontSize=index==3?23:21;text.alignment=TextAnchor.MiddleCenter;
            Place(text.rectTransform,15,92,158,66);text.color=UITheme.Ink;
        }

        static Sprite CreateBubbleSprite(Color fill)
        {
            const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true)
            {name="Start menu bubble",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float nx=(x-(size-1)*.5f)/(size*.5f-2),ny=(y-(size-1)*.5f)/(size*.5f-2);
                float radius=Mathf.Sqrt(nx*nx+ny*ny);
                float edge=Mathf.Clamp01((radius-.94f)/.06f);
                float alpha=1-edge*edge*(3-2*edge);
                float light=Mathf.Clamp(.94f+.12f*(ny-nx)-.14f*radius*radius,.68f,1.12f);
                float glint=Mathf.Exp(-((nx+.37f)*(nx+.37f)*11+(ny-.46f)*(ny-.46f)*26));
                var color=Color.Lerp(fill*light,Color.white,.08f*(1-Mathf.Clamp01(radius))+.36f*glint);
                color.a=alpha;pixels[y*size+x]=color;
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
            return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        }

        static LayerMotion Motion(RectTransform rect,float parallax,float amplitude,float rotation,float period,float phase)
        {return new LayerMotion{rect=rect,rest=rect.anchoredPosition,parallax=parallax,amplitude=amplitude,rotation=rotation,period=period,phase=phase};}

        public void ResetForInitialDisplay()
        {
            expanded=true;reveal=0;time=0;pointer=pointerTarget=Vector2.zero;
            nextBlink=Time.unscaledTime+4.2f;blinkUntil=0;
            if(stageGroup){stageGroup.alpha=0;stageGroup.interactable=false;}
            ApplyRestPose();
        }

        public void SetExpanded(bool value,bool immediate=false)
        {
            // The four destinations are always on the opening screen.
            expanded=true;
        }

        public void ToggleExpanded(){SetExpanded(true);}

        public void SetMotionEnabled(bool value)
        {
            motionEnabled=value;
            if(!motionEnabled)
            {
                pointer=pointerTarget=Vector2.zero;ApplyRestPose();SetBlink(false);reveal=1;
                if(stageGroup){stageGroup.alpha=1;stageGroup.interactable=true;}
            }
        }

        public void SetCompact(bool value)
        {
            compact=value;
            if(buttons==null||buttons.Length<4)return;
            float left=compact?424:418,right=compact?988:994;
            float upper=compact?310:306,lower=compact?548:546;
            Place(buttons[0],left,upper,188,188);Place(buttons[1],right,upper,188,188);
            Place(buttons[2],left,lower,188,188);Place(buttons[3],right,lower,188,188);
            for(int i=0;i<bubbles.Length;i++)bubbles[i].rest=buttons[i].anchoredPosition;
            ApplyRestPose();
        }

        static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}

        void Update()
        {
            if(!configured)return;
            float dt=Time.unscaledDeltaTime;
            if(reveal<1){reveal=Mathf.MoveTowards(reveal,1,dt/.4f);stageGroup.alpha=1-Mathf.Pow(1-reveal,3);stageGroup.interactable=reveal>.96f;}
            if(!motionEnabled)return;

            if(Application.isMobilePlatform)
                pointerTarget=Vector2.ClampMagnitude(new Vector2(Input.acceleration.x,Input.acceleration.y)*1.8f,1);
            else if(Screen.width>0&&Screen.height>0)
                pointerTarget=Vector2.ClampMagnitude(new Vector2(Input.mousePosition.x/Screen.width-.5f,Input.mousePosition.y/Screen.height-.5f)*2,1);
            pointer=Vector2.Lerp(pointer,pointerTarget,1-Mathf.Exp(-dt*5));time+=dt;
            for(int i=0;i<layers.Length;i++)
            {
                var layer=layers[i];float wave=Mathf.Sin(time*Mathf.PI*2/layer.period+layer.phase);
                layer.rect.anchoredPosition=layer.rest+pointer*layer.parallax+new Vector2(wave*layer.amplitude*.28f,wave*layer.amplitude);
                layer.rect.localRotation=Quaternion.Euler(0,0,wave*layer.rotation);
            }
            for(int i=0;i<bubbles.Length;i++)
            {
                var bubble=bubbles[i];float wave=Mathf.Sin(time*Mathf.PI*2/3.8f+bubble.phase);
                bubble.rect.anchoredPosition=bubble.rest+Vector2.up*wave*5;
                bubble.rect.localRotation=Quaternion.Euler(0,0,wave*.7f);
            }
            if(Time.unscaledTime>=nextBlink){blinkUntil=Time.unscaledTime+.12f;nextBlink=blinkUntil+UnityEngine.Random.Range(3f,6f);}
            SetBlink(Time.unscaledTime<blinkUntil);
        }

        void ApplyRestPose()
        {
            for(int i=0;i<layers.Length;i++)
            {layers[i].rect.anchoredPosition=layers[i].rest;layers[i].rect.localRotation=Quaternion.identity;}
            for(int i=0;i<bubbles.Length;i++)
            {bubbles[i].rect.anchoredPosition=bubbles[i].rest;bubbles[i].rect.localRotation=Quaternion.identity;}
            if(hero){hero.anchoredPosition=heroRest;hero.localRotation=heroRestRotation;hero.localScale=heroRestScale;}
        }

        void OnDestroy()
        {foreach(var sprite in bubbleSprites)if(sprite){var texture=sprite.texture;Destroy(sprite);Destroy(texture);}}

        void SetBlink(bool blinking)
        {if(heroOpen)heroOpen.enabled=!blinking;if(heroBlink)heroBlink.enabled=blinking;}
    }
}
