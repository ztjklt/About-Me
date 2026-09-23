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
            public float phase,tilt;
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
            // Four 3D letter entries are direct children of the opening stage.
            bubbles=new BubbleMotion[buttons.Length];
            string[] labels={"SINGLE\nPLAYER","MULTIPLAYER","INTRO\nPRESETS","SETTINGS"};
            string[] assets={"solo","multi","intro","settings"};
            for(int i=0;i<buttons.Length;i++)StyleBubble(i,labels[i],assets[i]);
            configured=true;SetCompact(false);ResetForInitialDisplay();
        }

        void StyleBubble(int index,string label,string asset)
        {
            var rect=buttons[index];rect.SetParent(transform,false);rect.name="Menu title · "+label.Replace('\n',' ');
            bubbles[index]=new BubbleMotion{rect=rect,phase=index*1.55f,tilt=index%2==0?6.5f:-6.5f};
            var style=rect.GetComponent<StickerButton>();
            if(!style)return;
            var surface=style.Surface;
            var sprite=Resources.Load<Sprite>("StartMenu2D/button-"+asset);
            if(!sprite)throw new InvalidOperationException("Missing menu title: "+asset);
            surface.sprite=sprite;surface.type=Image.Type.Simple;surface.preserveAspect=true;
            surface.color=Color.white;
            // Keep Image's default alpha hit threshold (zero): the full image
            // rectangle remains clickable even where the artwork is transparent.
            var oldOutline=rect.GetComponent<Outline>();
            if(oldOutline)Destroy(oldOutline);
            style.BaseColor=Color.white;style.HoverScale=1.055f;
            if(style.Icon)style.Icon.gameObject.SetActive(false);
            if(style.Label)style.Label.gameObject.SetActive(false);
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
            float left=compact?398:392,right=compact?962:968;
            float upper=compact?329:325,lower=compact?567:565;
            Place(buttons[0],left,upper,240,150);Place(buttons[1],right,upper,240,150);
            Place(buttons[2],left,lower,240,150);Place(buttons[3],right,lower,240,150);
            for(int i=0;i<bubbles.Length;i++)bubbles[i].rest=buttons[i].anchoredPosition;
            ApplyRestPose();
        }

        static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x+width*.5f,-y-height*.5f);rect.sizeDelta=new Vector2(width,height);}

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
                bubble.rect.localRotation=Quaternion.Euler(0,0,bubble.tilt+wave*.7f);
            }
            if(Time.unscaledTime>=nextBlink){blinkUntil=Time.unscaledTime+.12f;nextBlink=blinkUntil+UnityEngine.Random.Range(3f,6f);}
            SetBlink(Time.unscaledTime<blinkUntil);
        }

        void ApplyRestPose()
        {
            for(int i=0;i<layers.Length;i++)
            {layers[i].rect.anchoredPosition=layers[i].rest;layers[i].rect.localRotation=Quaternion.identity;}
            for(int i=0;i<bubbles.Length;i++)
            {bubbles[i].rect.anchoredPosition=bubbles[i].rest;bubbles[i].rect.localRotation=Quaternion.Euler(0,0,bubbles[i].tilt);}
            if(hero){hero.anchoredPosition=heroRest;hero.localRotation=heroRestRotation;hero.localScale=heroRestScale;}
        }

        void SetBlink(bool blinking)
        {if(heroOpen)heroOpen.enabled=!blinking;if(heroBlink)heroBlink.enabled=blinking;}
    }
}
