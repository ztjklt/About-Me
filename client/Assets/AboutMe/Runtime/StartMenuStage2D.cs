using System;
using UnityEngine;
using UnityEngine.UI;

namespace AboutMe
{
    /// <summary>Owns the lightweight motion and reveal state for the layered start-menu key art.</summary>
    public sealed class StartMenuStage2D : MonoBehaviour
    {
        struct LayerMotion
        {
            public RectTransform rect;
            public Vector2 rest;
            public float parallax,amplitude,rotation,period,phase;
        }

        CanvasGroup stageGroup,menuGroup,hintGroup;
        RectTransform title,hero,doodles,menuSheet;
        RectTransform[] buttons;
        Image heroOpen,heroBlink,dimmer;
        LayerMotion[] layers=Array.Empty<LayerMotion>();
        Vector2 pointer,pointerTarget,sheetRest;
        Vector2 heroRest;
        Quaternion heroRestRotation;
        Vector3 heroRestScale;
        float time,reveal,expansion,nextBlink,blinkUntil;
        bool configured,expanded,motionEnabled=true,compact;

        public bool Expanded=>expanded;
        public bool MotionEnabled=>motionEnabled;

        public void Configure(RectTransform titleRoot,RectTransform heroRoot,Image openEyes,Image closedEyes,RectTransform doodleRoot,RectTransform[] armRoots,
            RectTransform sheet,CanvasGroup sheetGroup,CanvasGroup enterHint,Image backdropDimmer,RectTransform[] menuButtons)
        {
            title=titleRoot;hero=heroRoot;heroOpen=openEyes;heroBlink=closedEyes;doodles=doodleRoot;
            heroRest=hero.anchoredPosition;heroRestRotation=hero.localRotation;heroRestScale=hero.localScale;
            menuSheet=sheet;menuGroup=sheetGroup;hintGroup=enterHint;dimmer=backdropDimmer;buttons=menuButtons;
            stageGroup=gameObject.GetComponent<CanvasGroup>()??gameObject.AddComponent<CanvasGroup>();
            // Keep the mascot anchored to the background; its eyes still blink independently.
            layers=new LayerMotion[2+armRoots.Length];
            layers[0]=Motion(title,8,3,.35f,4.2f,.2f);
            layers[1]=Motion(doodles,10,2,.25f,3.8f,2.4f);
            for(int i=0;i<armRoots.Length;i++)layers[i+2]=Motion(armRoots[i],18,4+i%3*2,1+(i%3)*.45f,3+(i%4)*.55f,i*.73f);
            configured=true;SetCompact(false);ResetForInitialDisplay();
        }

        static LayerMotion Motion(RectTransform rect,float parallax,float amplitude,float rotation,float period,float phase)
        {return new LayerMotion{rect=rect,rest=rect.anchoredPosition,parallax=parallax,amplitude=amplitude,rotation=rotation,period=period,phase=phase};}

        public void ResetForInitialDisplay()
        {
            expanded=false;expansion=0;reveal=0;time=0;pointer=pointerTarget=Vector2.zero;
            nextBlink=Time.unscaledTime+4.2f;blinkUntil=0;
            if(stageGroup)stageGroup.alpha=0;
            ApplyMenu(0);ApplyRestPose();
        }

        public void SetExpanded(bool value,bool immediate=false)
        {
            expanded=value;
            if(immediate){expansion=value?1:0;ApplyMenu(expansion);}
        }

        public void ToggleExpanded(){SetExpanded(!expanded);}

        public void SetMotionEnabled(bool value)
        {
            motionEnabled=value;
            if(!motionEnabled){pointer=pointerTarget=Vector2.zero;ApplyRestPose();SetBlink(false);}
        }

        public void SetCompact(bool value)
        {
            compact=value;
            if(!menuSheet||buttons==null||buttons.Length<4)return;
            if(compact)
            {
                Place(menuSheet,340,520,920,344);
                Place(buttons[0],34,82,410,82);Place(buttons[1],476,82,410,82);
                Place(buttons[2],34,190,410,82);Place(buttons[3],476,190,410,82);
            }
            else
            {
                Place(menuSheet,82,650,1436,210);
                for(int i=0;i<4;i++)Place(buttons[i],30+i*352,82,330,78);
            }
            sheetRest=menuSheet.anchoredPosition;ApplyMenu(expansion);
        }

        static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}

        void Update()
        {
            if(!configured)return;
            float dt=Time.unscaledDeltaTime;
            if(reveal<1){reveal=Mathf.MoveTowards(reveal,1,dt/.4f);stageGroup.alpha=1-Mathf.Pow(1-reveal,3);}
            float target=expanded?1:0;
            expansion=motionEnabled?Mathf.MoveTowards(expansion,target,dt*4.8f):target;ApplyMenu(expansion);
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
            if(Time.unscaledTime>=nextBlink){blinkUntil=Time.unscaledTime+.12f;nextBlink=blinkUntil+UnityEngine.Random.Range(3f,6f);}
            SetBlink(Time.unscaledTime<blinkUntil);
        }

        void ApplyMenu(float value)
        {
            if(!menuSheet)return;
            float eased=1-Mathf.Pow(1-Mathf.Clamp01(value),3);
            menuSheet.anchoredPosition=sheetRest+Vector2.down*(1-eased)*(compact?390:250);
            menuGroup.alpha=eased;menuGroup.interactable=value>.98f;menuGroup.blocksRaycasts=value>.98f;
            hintGroup.alpha=1-eased;
            var c=dimmer.color;c.a=.16f*eased;dimmer.color=c;
        }

        void ApplyRestPose()
        {
            for(int i=0;i<layers.Length;i++)
            {layers[i].rect.anchoredPosition=layers[i].rest;layers[i].rect.localRotation=Quaternion.identity;}
            if(hero){hero.anchoredPosition=heroRest;hero.localRotation=heroRestRotation;hero.localScale=heroRestScale;}
        }

        void SetBlink(bool blinking)
        {if(heroOpen)heroOpen.enabled=!blinking;if(heroBlink)heroBlink.enabled=blinking;}
    }
}
