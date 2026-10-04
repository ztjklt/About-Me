using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AboutMe
{
    [Serializable] public class IntroductionDraft { public string id,name,nickname,introduction; }
    [Serializable] public class IntroductionDrafts { public List<IntroductionDraft> items=new List<IntroductionDraft>(); public string selected; }
    public enum MenuPage { Main, Solo, Multiplayer, Presets }
    public partial class GameUI
    {
        MenuPage page;
        RectTransform mainMenu,soloMenu,multiplayerMenu,draftList,draftLibrary;
        StartMenuStage2D startMenuStage;
        Text soloSummary,multiSummary,draftHeading;
        InputField draftName;
        IntroductionDrafts drafts;
        Button soloStart,replay;
        Profile lastSoloProfile;
        // QA can inspect the plain default doll without changing saved wardrobes or profiles.
        static bool SessionDefaultActor=>Environment.GetCommandLineArgs().Contains("--qa-default-actor")||Application.absoluteURL.Contains("qa-default-actor");
        float cameraSensitivity=1;
        int touchMode;
        bool reduceMotion;
        void BuildMenus()
        {
            cameraSensitivity=PlayerPrefs.GetFloat("cameraSensitivity",1);touchMode=PlayerPrefs.GetInt("touchMode",0);reduceMotion=PlayerPrefs.GetInt("reduceMotion",0)==1;
            RefreshSettingLabels();
            BuildStartMenuStage();
            var soloCard=MenuCard("单人游戏","OFFLINE PRACTICE");soloMenu=(RectTransform)soloCard.parent;
            soloSummary=ArtText(soloCard,"",40,170,900,135,24,UITheme.Ink,FontStyle.Bold);
            ArtText(soloCard,"陪练 NPC · 离线可玩",40,320,600,48,22,UITheme.Muted);
            soloStart=ArtButton(soloCard,"当前角色开始",40,487,294,66,()=>StartSolo(false),UITheme.Mint,UITheme.Ink,"attack");
            ArtButton(soloCard,"练习道具（非 AI）",40,574,294,58,()=>StartSolo(true),UITheme.Yellow,UITheme.Ink);
            ArtButton(soloCard,"编辑自我介绍",356,574,242,58,()=>ShowPage(MenuPage.Presets),UITheme.Field,UITheme.Ink);
            ArtButton(soloCard,"性格能力试验台",624,574,294,58,OpenTraitLab,UITheme.Purple,UITheme.Ink);
            var multiplayerCard=MenuCard("多人游戏","PLAY WITH FRIENDS");multiplayerMenu=(RectTransform)multiplayerCard.parent;
            multiSummary=ArtText(multiplayerCard,"",40,170,900,98,23,UITheme.Ink,FontStyle.Bold);
            ArtButton(multiplayerCard,"创建房间",40,294,245,67,CreateRoom,UITheme.Mint,UITheme.Ink,"share");
            room.transform.SetParent(multiplayerCard,false);Place((RectTransform)room.transform,40,402,550,64);
            room.characterLimit=6;room.contentType=InputField.ContentType.Alphanumeric;
            ArtButton(multiplayerCard,"加入房间",615,402,325,64,JoinRoom,UITheme.Yellow,UITheme.Ink,"ready");
            ArtText(multiplayerCard,"2–6 人 · 两人即可开局",40,496,650,40,20,UITheme.Muted);
            ArtButton(multiplayerCard,"练习道具（非 AI）",40,600,230,56,LoadPracticePersona,UITheme.Yellow,UITheme.Ink);
            ArtButton(multiplayerCard,"自我介绍",292,600,210,56,()=>ShowPage(MenuPage.Presets),UITheme.Purple,UITheme.Ink);
            ArtButton(multiplayerCard,"设置",524,600,210,56,OpenSettings,UITheme.Field,UITheme.Ink,"settings");
            BuildDraftLibrary();ApplyAdaptiveLayout(true);
            LoadCachedProfile();ShowPage(MenuPage.Main);statusPill.SetAsLastSibling();
        }
        void BuildStartMenuStage()
        {
            mainMenu=ArtPanel(artboard,"Main menu · 2.5D stage",0,0,1600,900,Color.clear,false,false);
            var title=MenuSprite(mainMenu,"About Me clay title","StartMenu2D/title",400,24,800,267);

            var arms=new List<RectTransform>();
            arms.Add(MenuSprite(mainMenu,"Camera arm","StartMenu2D/arm-camera",-44,78,500,333));
            arms.Add(MenuSprite(mainMenu,"Pizza arm","StartMenu2D/arm-pizza",1144,92,500,333));
            arms.Add(MenuSprite(mainMenu,"Controller arm","StartMenu2D/arm-controller",-55,264,480,320));
            arms.Add(MenuSprite(mainMenu,"Stylus arm","StartMenu2D/arm-stylus",1160,278,480,320));

            var hero=ArtRect(mainMenu,"Central mascot",new Vector2(570,-238),new Vector2(460,690));
            var heroOpen=MenuSprite(hero,"Mascot · open","StartMenu2D/mascot-open",0,0,460,690).GetComponent<Image>();
            var heroBlink=MenuSprite(hero,"Mascot · blink","StartMenu2D/mascot-blink",0,0,460,690).GetComponent<Image>();heroBlink.enabled=false;

            arms.Add(MenuSprite(mainMenu,"Story arm","StartMenu2D/arm-book",-54,450,470,313));
            arms.Add(MenuSprite(mainMenu,"Cat arm","StartMenu2D/arm-cat",-52,640,430,287));
            arms.Add(MenuSprite(mainMenu,"Clover arm","StartMenu2D/arm-clover",1184,466,470,313));
            arms.Add(MenuSprite(mainMenu,"Photo arm","StartMenu2D/arm-photo",1170,650,450,300));

            var doodles=ArtRect(mainMenu,"Clay doodles",Vector2.zero,new Vector2(1600,900));
            ArtText(doodles,"?",760,202,80,92,72,UITheme.Paper,FontStyle.Bold,TextAnchor.MiddleCenter);
            Accent(doodles,497,373,38,9,-36,UITheme.Paper);Accent(doodles,478,414,34,9,-8,UITheme.Paper);Accent(doodles,1080,348,38,9,34,UITheme.Paper);
            Accent(doodles,1090,398,34,9,8,UITheme.Yellow);Accent(doodles,415,545,34,9,-28,UITheme.Yellow);Accent(doodles,1150,548,34,9,28,UITheme.Yellow);

            var menuButtons=new[]{
                ArtButton(mainMenu,"SINGLE PLAYER",418,306,188,188,()=>ShowPage(MenuPage.Solo),UITheme.Mint,UITheme.Ink,"attack"),
                ArtButton(mainMenu,"MULTIPLAYER",994,306,188,188,()=>ShowPage(MenuPage.Multiplayer),UITheme.Yellow,UITheme.Ink,"share"),
                ArtButton(mainMenu,"INTRO PRESETS",418,546,188,188,()=>ShowPage(MenuPage.Presets),UITheme.Purple,UITheme.Ink,"bag"),
                ArtButton(mainMenu,"SETTINGS",994,546,188,188,OpenSettings,UITheme.Field,UITheme.Ink,"settings")};

            startMenuStage=mainMenu.gameObject.AddComponent<StartMenuStage2D>();
            startMenuStage.Configure(title,hero,heroOpen,heroBlink,doodles,arms.ToArray(),menuButtons.Select(x=>(RectTransform)x.transform).ToArray());
            startMenuStage.SetMotionEnabled(!reduceMotion);
        }
        RectTransform MenuSprite(Transform parent,string name,string resource,float x,float y,float width,float height,bool preserve=true)
        {
            var sprite=Resources.Load<Sprite>(resource);if(!sprite)throw new InvalidOperationException("Missing UI sprite: "+resource);
            var rect=ArtRect(parent,name,new Vector2(x,-y),new Vector2(width,height));var image=rect.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.type=Image.Type.Simple;image.preserveAspect=preserve;image.raycastTarget=false;return rect;
        }
        void Accent(Transform parent,float x,float y,float width,float height,float rotation,Color color)
        {var mark=ArtPanel(parent,"Clay accent",x,y,width,height,color,false,false);mark.GetComponent<Image>().sprite=UITheme.Rounded;mark.localRotation=Quaternion.Euler(0,0,rotation);}
        RectTransform MenuCard(string title,string tape)
        {
            var stage=SelectionStage(title+" selection stage");
            var panel=ArtPanel(stage,title,310,66,980,720,UITheme.Paper,true,true);
            Tape(panel,tape,40,30,260,36,UITheme.Pink);ArtText(panel,title,40,82,750,90,38,UITheme.Ink,FontStyle.Bold);
            ArtButton(panel,"返回",812,34,126,52,()=>ShowPage(MenuPage.Main),UITheme.Field,UITheme.Ink,"exit");return panel;
        }
        void OpenSettings(){RefreshSettingLabels();optionsOpen=true;settings.gameObject.SetActive(true);settings.SetAsLastSibling();}
        void ShowPage(MenuPage next)
        {
            if(busy){net.Status="请等待当前操作完成";return;}
            SaveDraft(false);page=next;pendingRoom=false;
            if(wardrobe&&wardrobe.gameObject.activeSelf)CloseWardrobe();
            optionsOpen=false;settings.gameObject.SetActive(false);RefreshMenuSummary();UpdateMenuVisibility();
            net.Status="";
        }
        void UpdateMenuVisibility()
        {
            if(!mainMenu)return;bool show=net.State==null;
            bool covered=optionsOpen||(wardrobe&&wardrobe.gameObject.activeSelf);
            mainMenu.gameObject.SetActive(show&&!covered&&page==MenuPage.Main);
            soloMenu.gameObject.SetActive(show&&!covered&&page==MenuPage.Solo);
            multiplayerMenu.gameObject.SetActive(show&&!covered&&page==MenuPage.Multiplayer);
            home.gameObject.SetActive(show&&!covered&&page==MenuPage.Presets);
            if(world)world.SetMenuPreviewVisible(!show);
        }
        void RefreshMenuSummary()
        {
            string summary=profile?.personas?.Length>0?(net.Sample?"练习角色（非 AI）":"当前角色："+GameLocalization.Protect(profile.nickname))+"\nPersona × "+profile.personas.Length+"\n"+string.Join(" / ",profile.personas.Select(p=>GameLocalization.PersonaName(p))):"还没有 Persona。\n先填写自我介绍，或使用练习道具开始。";
            if(soloSummary)soloSummary.text=summary;if(multiSummary)multiSummary.text=summary;
            if(soloStart)soloStart.interactable=profile?.personas?.Length>0&&!busy;
        }
        void StartSolo(bool practice)
        {
            if(busy)return;
            var chosen=practice?GameNetwork.SampleProfile(Guid.NewGuid().ToString("N"),"练习玩家"):profile;
            if(chosen?.personas?.Length>0)
            {
                lastSoloProfile=JsonUtility.FromJson<Profile>(JsonUtility.ToJson(chosen));
                if(practice&&profile!=null&&!net.Sample&&SavedTokenForApi()!="")
                {lastSoloProfile.dsGirlUnlocked=profile.dsGirlUnlocked;lastSoloProfile.claudeGirlUnlocked=profile.claudeGirlUnlocked;lastSoloProfile.characterSkin=profile.characterSkin;}
                lastSoloProfile.appearance=SessionDefaultActor?new AppearanceData():CurrentAppearance;lastInventory="";lastResultVisual="";lastEvent=0;
                net.StartSolo(lastSoloProfile);RefreshTouchVisibility();
            }
            else {ShowPage(MenuPage.Presets);net.Status="请先生成 Persona，或使用练习道具。";}
        }
        void ReplaySolo(){if(!net.IsSolo||lastSoloProfile==null)return;lastInventory="";lastResultVisual="";lastEvent=0;net.StartSolo(lastSoloProfile);}
        void LoadCachedProfile()
        {
            try{var json=PlayerPrefs.GetString("cachedProfile","");if(json!=""){profile=JsonUtility.FromJson<Profile>(json);if(profile?.personas?.Length>0||profile?.generationTerms?.Length>0)DisplayProfile();}}
            catch(Exception){profile=null;}
        }
        void CacheProfile(){if(!net.Sample&&(profile?.personas?.Length>0||profile?.generationTerms?.Length>0)){PlayerPrefs.SetString("cachedProfile",JsonUtility.ToJson(profile));PlayerPrefs.Save();}RefreshMenuSummary();}
        void BuildDraftLibrary()
        {
            if(homeHero)homeHero.gameObject.SetActive(false);
            var library=draftLibrary=ArtPanel(home,"Introduction draft library",28,28,616,836,UITheme.Paper,true,true);
            Tape(library,"MY INTRODUCTIONS",28,24,260,38,UITheme.Yellow);
            ArtText(library,"自我介绍预设",28,84,550,48,31,UITheme.Ink,FontStyle.Bold);
            ArtText(library,"切换草稿不替换 Persona",28,142,550,30,17,UITheme.Muted);
            MenuSprite(library,"Persona drawer","IntroductionUI/mascot-drawer-r3",335,178,245,245);
            ArtText(library,"为每个自己留一个抽屉",28,204,288,70,24,UITheme.Ink,FontStyle.Bold);
            draftName=ArtField(library,28,304,288,48,"草稿名称",false);draftName.characterLimit=24;
            ArtButton(library,"新建",28,374,100,42,NewDraft,UITheme.Yellow,UITheme.Ink);
            ArtButton(library,"删除",146,374,100,42,DeleteDraft,UITheme.Field,UITheme.Ink);
            draftHeading=ArtText(library,"",28,454,550,28,17,UITheme.Muted);
            var viewport=ArtPanel(library,"Draft scroll",28,494,560,284,Color.clear,false,false);viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
            draftList=ArtRect(viewport,"Draft list",Vector2.zero,new Vector2(560,284));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=draftList;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            try{drafts=JsonUtility.FromJson<IntroductionDrafts>(PlayerPrefs.GetString("introductionDrafts",""));}catch(Exception){}
            if(drafts==null||drafts.items==null||drafts.items.Count==0)
            {drafts=new IntroductionDrafts();drafts.items.Add(new IntroductionDraft{id=Guid.NewGuid().ToString("N"),name="我的第一份介绍",nickname=nickname.text,introduction=introduction.text});drafts.selected=drafts.items[0].id;}
            SelectDraft(drafts.items.Any(d=>d.id==drafts.selected)?drafts.selected:drafts.items[0].id,false);
            nickname.characterLimit=24;

        }
        void PersistDrafts(){if(Environment.GetCommandLineArgs().Contains("--introduction-ui-audit"))return;PlayerPrefs.SetString("introductionDrafts",JsonUtility.ToJson(drafts));PlayerPrefs.Save();}
        void SaveDraft(bool notify)
        {
            if(Environment.GetCommandLineArgs().Contains("--introduction-ui-audit"))return;
            if(drafts==null)return;var draft=drafts.items.Find(d=>d.id==drafts.selected);if(draft==null)return;
            draft.name=string.IsNullOrWhiteSpace(draftName.text)?"未命名介绍":draftName.text.Trim();draft.nickname=nickname.text;draft.introduction=introduction.text;PersistDrafts();RefreshDrafts();
            if(notify)net.Status="草稿已保存；当前 Persona 未改变。";
        }
        void SelectDraft(string id,bool save=true)
        {
            if(save)SaveDraft(false);var draft=drafts.items.Find(d=>d.id==id);if(draft==null)return;
            drafts.selected=id;draftName.text=draft.name;nickname.text=draft.nickname;introduction.text=draft.introduction;PersistDrafts();RefreshDrafts();
        }
        void NewDraft()
        {
            SaveDraft(false);var draft=new IntroductionDraft{id=Guid.NewGuid().ToString("N"),name="介绍 "+(drafts.items.Count+1),nickname=nickname.text,introduction=""};drafts.items.Add(draft);SelectDraft(draft.id,false);
        }
        void DeleteDraft()
        {
            drafts.items.RemoveAll(d=>d.id==drafts.selected);
            if(drafts.items.Count==0)drafts.items.Add(new IntroductionDraft{id=Guid.NewGuid().ToString("N"),name="我的介绍",nickname=nickname.text,introduction=""});
            SelectDraft(drafts.items[0].id,false);net.Status="已删除介绍草稿；当前角色不受影响。";
        }
        void RefreshDrafts()
        {
            foreach(Transform child in draftList)Destroy(child.gameObject);
            int i=0;foreach(var d in drafts.items){string id=d.id;ArtButton(draftList,(d.id==drafts.selected?"●\n":"▰\n")+d.name,8+(i%3)*184,8+(i/3)*134,172,116,()=>{if(!busy)SelectDraft(id);},d.id==drafts.selected?UITheme.Mint:UITheme.Paper,UITheme.Ink);i++;}
            draftList.sizeDelta=new Vector2(560,Mathf.Max(284,16+((i+2)/3)*134));draftHeading.text="已保存 "+drafts.items.Count+" 份 · 点击切换";

        }
        static string ShortPersonaName(string name)=>string.IsNullOrEmpty(name)?"—":name.Length<=(GameLocalization.English?16:4)?name:name.Substring(0,GameLocalization.English?15:3)+"…";
        void OnApplicationPause(bool paused){if(paused){SaveDraft(false);ResetMovement();if(net)Send("cancel");}}
        void OnApplicationQuit(){SaveDraft(false);}
    }
}
