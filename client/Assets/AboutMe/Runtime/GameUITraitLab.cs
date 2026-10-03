using System;
using UnityEngine;
using UnityEngine.UI;

namespace AboutMe
{
    public partial class GameUI
    {
        RectTransform traitLab;
        TraitToyEffects traitEffects;
        GameObject traitScene;
        Camera traitCamera;
        RenderTexture traitRender;
        Text traitDetail,traitMetric,traitMbti;
        Text traitAutoLabel;
        readonly System.Collections.Generic.List<StickerButton> traitButtons=new System.Collections.Generic.List<StickerButton>();
        bool traitAuto=true;float traitNext;
        int traitChoice,mbtiChoice;
        static readonly string[] TraitNames={"热烈 · 喷火","冷静 · 冰霜","坚定 · 护罩","好奇 · 探照","调皮 · 弹跳","温柔 · 泡泡"};
        static readonly string[] TraitDescriptions={
            "热情似火 → 喷出玩具火焰\n建议：前摇 0.3 秒 / 持续 0.9 秒\n范围 4 米 / 冷却 9 秒\n反制：绕侧、贴身抢夺、击断前摇",
            "从容冷静 → 冰霜足迹\n建议：地面减速区持续 2 秒\n范围 3 米 / 冷却 12 秒\n反制：绕开区域、跳跃越过",
            "坚持原则 → 泡棉护罩\n建议：挡住一次正面冲击\n持续 2.5 秒 / 冷却 12 秒\n反制：绕后、等待护罩消失",
            "好奇探索 → 发现光圈\n建议：标出附近地面 Persona\n范围 6 米 / 冷却 14 秒\n只提示已公开的物件，不窥探背包",
            "爱开玩笑 → 弹簧惊喜\n建议：短暂前摇的弹跳陷阱\n场上最多一个 / 冷却 12 秒\n反制：看见标记后绕开，也会弹自己",
            "体贴温柔 → 安抚泡泡\n建议：安静 2 秒后恢复部分韧性\n受击即中断 / 冷却 16 秒\n不增加 Persona 数量，不复活"};
        static readonly string[] MbtiNames={"自选 MBTI","ENFP","INFP","ENFJ","INFJ","ENTP","INTP","ENTJ","INTJ","ESFP","ISFP","ESFJ","ISFJ","ESTP","ISTP","ESTJ","ISTJ"};
        static readonly int[] MbtiSuggestions={0,0,5,5,3,4,3,2,1,0,5,5,2,4,1,2,1};

        void OpenTraitLab()
        {
            if(traitLab){traitLab.gameObject.SetActive(true);return;}
            traitChoice=mbtiChoice=0;traitAuto=true;traitButtons.Clear();
            traitLab=ArtPanel(artboard,"Personality ability laboratory",20,16,1560,864,UITheme.Paper,true,true);
            statusPill.gameObject.SetActive(false);
            traitLab.SetAsLastSibling();
            ArtText(traitLab,"PERSONA · 性格能力试验台",30,22,1280,50,32,UITheme.Ink,FontStyle.Bold);
            ArtText(traitLab,"先看代码特效，再确认正式玩法。建议数值尚未接入对战。",30,79,1390,35,18,UITheme.Muted);
            ArtButton(traitLab,"返回",1382,24,140,52,CloseTraitLab,UITheme.Field,UITheme.Ink,"exit");
            var view=ArtPanel(traitLab,"Toy effect viewport",30,133,890,625,UITheme.Field,true,false);
            var raw=ArtRect(view,"Native 3D effect",new Vector2(8,-8),new Vector2(874,609)).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;
            traitRender=new RenderTexture(1160,810,24);raw.texture=traitRender;
            traitScene=new GameObject("Trait preview scene");
            var actor=new ToyActor("trait-preview","#77C8BD",0,traitScene.transform,AppearanceData.Demo(0));
            actor.DrawAt(new PublicPlayer{id="trait-preview",nickname="",remaining=1,connected=true,stability=100,color="#77C8BD",position=new Vector3(0,1.05f,0),yaw=0,appearance=AppearanceData.Demo(0),interaction="",publicProp=""},null,.02f,0);
            ToyFactory.Shape("Toy stage",PrimitiveType.Cylinder,traitScene.transform,new Vector3(0,-.09f,1.3f),new Vector3(5,.1f,5),"#E9DBC8");
            ToyFactory.Shape("Practice target",PrimitiveType.Cube,traitScene.transform,new Vector3(0,.6f,3.7f),new Vector3(.9f,1.2f,.9f),"#E2A867");
            var emitter=new GameObject("Code trait VFX");emitter.transform.SetParent(traitScene.transform,false);
            emitter.transform.position=(actor.mouth?actor.mouth.position:new Vector3(0,1.35f,0))+Vector3.forward*.12f;
            emitter.transform.rotation=Quaternion.LookRotation(new Vector3(0,.9f,3.7f)-emitter.transform.position);
            foreach(var t in traitScene.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
            traitEffects=emitter.AddComponent<TraitToyEffects>();traitEffects.Select(0);
            var cameraGo=new GameObject("Trait preview camera");cameraGo.transform.SetParent(traitScene.transform,false);traitCamera=cameraGo.AddComponent<Camera>();
            traitCamera.cullingMask=1<<29;traitCamera.clearFlags=CameraClearFlags.SolidColor;traitCamera.backgroundColor=ToyFactory.ColorOf("#EBE1D0");
            traitCamera.targetTexture=traitRender;traitCamera.fieldOfView=43;traitCamera.transform.position=new Vector3(4.7f,3.2f,6.2f);traitCamera.transform.LookAt(new Vector3(0,1.15f,1.45f));
            for(int i=0;i<TraitNames.Length;i++){int choice=i;traitButtons.Add(ArtButton(traitLab,TraitNames[i],954+(i%2)*282,137+(i/2)*70,263,55,()=>ChooseTrait(choice),UITheme.Mint,UITheme.Ink).GetComponent<StickerButton>());}
            traitButtons[0].SetState(UIVisualState.Selected);
            traitDetail=ArtText(traitLab,TraitDescriptions[0],954,363,558,211,22,UITheme.Ink);
            ArtText(traitLab,"MBTI 只作自选风格预设\n可选任意特性，不决定强弱，也不作心理判断。",954,591,555,66,17,UITheme.Muted);
            var mbti=ArtButton(traitLab,"",954,665,267,53,()=>{mbtiChoice=(mbtiChoice+1)%MbtiNames.Length;traitMbti.text=MbtiNames[mbtiChoice];if(mbtiChoice>0)ChooseTrait(MbtiSuggestions[mbtiChoice]);},UITheme.Purple,UITheme.Ink);
            traitMbti=mbti.GetComponentInChildren<Text>();traitMbti.text=MbtiNames[0];
            ArtButton(traitLab,"释放一次（F）",1238,665,274,53,()=>traitEffects.Play(),UITheme.Yellow,UITheme.Ink,"attack");
            var autoButton=ArtButton(traitLab,"自动演示 · 开启",954,734,558,46,()=>{traitAuto=!traitAuto;traitNext=traitAuto?0:float.PositiveInfinity;traitAutoLabel.text="自动演示 · "+(traitAuto?"开启":"关闭");},UITheme.Field,UITheme.Ink);
            traitAutoLabel=autoButton.GetComponentInChildren<Text>();
            traitMetric=ArtText(traitLab,"",30,783,1470,40,18,UITheme.Muted);
            traitNext=Time.unscaledTime+2.2f;StartCoroutine(TraitLabTick());
        }
        void ChooseTrait(int choice){traitChoice=choice;traitDetail.text=TraitDescriptions[choice];traitEffects.Select(choice);traitNext=Time.unscaledTime+2.2f;for(int i=0;i<traitButtons.Count;i++)traitButtons[i].SetState(i==choice?UIVisualState.Selected:UIVisualState.Normal);}
        System.Collections.IEnumerator TraitLabTick()
        {
            while(traitLab&&traitLab.gameObject.activeSelf)
            {
                if(Input.GetKeyDown(KeyCode.F))traitEffects.Play();
                if(traitAuto&&Time.unscaledTime>=traitNext){traitEffects.Play();traitNext=Time.unscaledTime+2.2f;}
                traitMetric.text=$"程序纹理 + Unity 粒子 · 当前 {TraitNames[traitChoice]} · 活跃粒子 {traitEffects.ParticleCount} / 上限 {TraitToyEffects.MaxParticleBudget} · 视觉预览，无伤害判定";
                yield return null;
            }
        }
        void CloseTraitLab()
        {
            if(traitScene)Destroy(traitScene);if(traitRender){traitRender.Release();Destroy(traitRender);}
            if(traitLab)Destroy(traitLab.gameObject);traitLab=null;traitEffects=null;statusPill.gameObject.SetActive(true);ShowPage(MenuPage.Main);
        }
    }
}
