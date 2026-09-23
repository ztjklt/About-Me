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
        Text soloSummary,multiSummary,draftHeading;
        InputField draftName;
        IntroductionDrafts drafts;
        Button soloStart,replay;
        Profile lastSoloProfile;
        float cameraSensitivity=1;
        int touchMode;
        bool reduceMotion;
        void BuildMenus()
        {
            cameraSensitivity=PlayerPrefs.GetFloat("cameraSensitivity",1);touchMode=PlayerPrefs.GetInt("touchMode",0);reduceMotion=PlayerPrefs.GetInt("reduceMotion",0)==1;
            RefreshSettingLabels();
            mainMenu=ArtPanel(artboard,"Main menu",0,0,1600,900,Color.clear,false,false);
            Tape(mainMenu,"YOUR STORY. YOUR PARTY.",65,55,285,40,UITheme.Yellow);
            ArtText(mainMenu,"About Me",65,100,820,180,94,UITheme.Ink,FontStyle.Bold);
            ArtText(mainMenu,"你的自我，既是武器，也是生命。",70,279,780,48,28,UITheme.Ink,FontStyle.Bold);
            ArtText(mainMenu,"介绍自己 · 拿自己打架 · 保住最后的自我",70,340,760,42,21,UITheme.Muted);
            ArtFrame(mainMenu,"Your party stage",40,453,830,345);
            var card=ArtPanel(mainMenu,"Start menu",960,185,535,560,UITheme.Paper,true,true);
            Tape(card,"LET'S PLAY",30,28,180,38,UITheme.Pink);
            ArtButton(card,"单人游戏",30,102,475,82,()=>ShowPage(MenuPage.Solo),UITheme.Mint,UITheme.Ink,"attack");
            ArtButton(card,"多人游戏",30,206,475,82,()=>ShowPage(MenuPage.Multiplayer),UITheme.Yellow,UITheme.Ink,"share");
            ArtButton(card,"自我介绍预设",30,310,475,82,()=>ShowPage(MenuPage.Presets),UITheme.Purple,UITheme.Ink,"bag");
            ArtButton(card,"设置",30,414,475,82,OpenSettings,UITheme.Field,UITheme.Ink,"settings");
            ArtText(mainMenu,"每 2:30 大手点名  ·  7:30 到时结算",985,778,560,38,19,UITheme.Ink);
            soloMenu=MenuCard("单人游戏","OFFLINE PRACTICE");
            soloSummary=ArtText(soloMenu,"",40,170,900,135,24,UITheme.Ink,FontStyle.Bold);
            ArtText(soloMenu,"对手：陪练 NPC\n会追击、争夺、吞噬和回收；不需要服务器。\n吞噬数优先，同分比较剩余 Persona。",40,320,900,135,22,UITheme.Muted);
            soloStart=ArtButton(soloMenu,"使用当前角色开始",40,487,900,66,()=>StartSolo(false),UITheme.Mint,UITheme.Ink,"attack");
            ArtButton(soloMenu,"使用练习道具开始（非 AI）",40,574,438,58,()=>StartSolo(true),UITheme.Yellow,UITheme.Ink);
            ArtButton(soloMenu,"编辑自我介绍",500,574,440,58,()=>ShowPage(MenuPage.Presets),UITheme.Field,UITheme.Ink);
            multiplayerMenu=MenuCard("多人游戏","PLAY WITH FRIENDS");
            multiSummary=ArtText(multiplayerMenu,"",40,170,900,98,23,UITheme.Ink,FontStyle.Bold);
            ArtButton(multiplayerMenu,"创建房间",40,294,900,67,()=>{room.text="";Enter();},UITheme.Mint,UITheme.Ink,"share");
            room.transform.SetParent(multiplayerMenu,false);Place((RectTransform)room.transform,40,402,550,64);
            room.characterLimit=6;room.contentType=InputField.ContentType.Alphanumeric;
            ArtButton(multiplayerMenu,"加入房间",615,402,325,64,()=>{if(room.text.Trim().Length!=6){net.Status="请输入 6 位房间码";return;}Enter();},UITheme.Yellow,UITheme.Ink,"ready");
            ArtText(multiplayerMenu,"2–6 人 · 两人即可开局 · 房主点击开始\n需要在线游戏服务；断线后可使用原角色重连。",40,496,900,80,20,UITheme.Muted);
            ArtButton(multiplayerMenu,"自我介绍与角色",40,600,438,56,()=>ShowPage(MenuPage.Presets),UITheme.Purple,UITheme.Ink);
            ArtButton(multiplayerMenu,"连接设置",500,600,440,56,OpenSettings,UITheme.Field,UITheme.Ink,"settings");
            BuildDraftLibrary();ApplyAdaptiveLayout(true);
            LoadCachedProfile();ShowPage(MenuPage.Main);statusPill.SetAsLastSibling();
        }
        RectTransform MenuCard(string title,string tape)
        {
            var panel=ArtPanel(artboard,title,310,66,980,720,UITheme.Paper,true,true);
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
            mainMenu.gameObject.SetActive(show&&page==MenuPage.Main);
            soloMenu.gameObject.SetActive(show&&page==MenuPage.Solo);
            multiplayerMenu.gameObject.SetActive(show&&page==MenuPage.Multiplayer);
            home.gameObject.SetActive(show&&page==MenuPage.Presets);
        }
        void RefreshMenuSummary()
        {
            string summary=profile?.personas?.Length>0?(net.Sample?"练习角色（非 AI）":"当前角色："+profile.nickname)+"\nPersona × "+profile.personas.Length+"\n"+string.Join(" / ",profile.personas.Select(p=>p.name)):"还没有 Persona。\n先填写自我介绍，或使用练习道具开始。";
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
                lastSoloProfile.appearance=CurrentAppearance;lastInventory="";lastResultVisual="";lastEvent=0;
                net.StartSolo(lastSoloProfile);RefreshTouchVisibility();
            }
            else {ShowPage(MenuPage.Presets);net.Status="请先生成 Persona，或使用练习道具。";}
        }
        void ReplaySolo(){if(!net.IsSolo||lastSoloProfile==null)return;lastInventory="";lastResultVisual="";lastEvent=0;net.StartSolo(lastSoloProfile);}
        void LoadCachedProfile()
        {
            try{var json=PlayerPrefs.GetString("cachedProfile","");if(json!=""){profile=JsonUtility.FromJson<Profile>(json);if(profile?.personas?.Length>0)DisplayProfile();}}
            catch(Exception){profile=null;}
        }
        void CacheProfile(){if(!net.Sample&&profile?.personas?.Length>0){PlayerPrefs.SetString("cachedProfile",JsonUtility.ToJson(profile));PlayerPrefs.Save();}RefreshMenuSummary();}
        void BuildDraftLibrary()
        {
            homeHero.gameObject.SetActive(false);
            var library=draftLibrary=ArtPanel(home,"Introduction draft library",45,34,800,820,UITheme.Paper,true,true);
            Tape(library,"MY INTRODUCTIONS",30,24,260,38,UITheme.Yellow);
            ArtText(library,"自我介绍预设",30,75,660,84,34,UITheme.Ink,FontStyle.Bold);
            ArtButton(library,"返回菜单",30,735,720,55,()=>ShowPage(MenuPage.Main),UITheme.Field,UITheme.Ink,"exit");
            ArtText(library,"切换草稿不会自动生成或替换 Persona。",30,146,730,44,20,UITheme.Muted);
            draftName=ArtField(library,30,212,500,55,"草稿名称",false);draftName.characterLimit=24;
            ArtButton(library,"保存草稿",551,212,199,55,()=>SaveDraft(true),UITheme.Mint,UITheme.Ink);
            ArtButton(library,"新建草稿",30,287,345,52,NewDraft,UITheme.Yellow,UITheme.Ink);
            ArtButton(library,"删除当前草稿",400,287,350,52,DeleteDraft,UITheme.Field,UITheme.Ink);
            draftHeading=ArtText(library,"",30,360,720,40,18,UITheme.Muted);
            var viewport=ArtPanel(library,"Draft scroll",30,410,720,300,UITheme.Field,false,false);viewport.gameObject.AddComponent<RectMask2D>();
            draftList=ArtRect(viewport,"Draft list",Vector2.zero,new Vector2(710,300));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=draftList;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            try{drafts=JsonUtility.FromJson<IntroductionDrafts>(PlayerPrefs.GetString("introductionDrafts",""));}catch(Exception){}
            if(drafts==null||drafts.items==null||drafts.items.Count==0)
            {drafts=new IntroductionDrafts();drafts.items.Add(new IntroductionDraft{id=Guid.NewGuid().ToString("N"),name="我的第一份介绍",nickname=nickname.text,introduction=introduction.text});drafts.selected=drafts.items[0].id;}
            SelectDraft(drafts.items.Any(d=>d.id==drafts.selected)?drafts.selected:drafts.items[0].id,false);
            // This is now a dedicated editor, not an implicit room entry.
            join.GetComponentInChildren<Text>().text="保存草稿，返回菜单";join.onClick.RemoveAllListeners();join.onClick.AddListener(()=>ShowPage(MenuPage.Main));
            generate.GetComponentInChildren<Text>().text="生成并替换当前 Persona";
            nickname.characterLimit=24;
            foreach(Transform child in home)if(child.name=="Button · 连接设置")child.gameObject.SetActive(false);
            var roomLabel=studioCard.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.text=="房间");if(roomLabel)roomLabel.gameObject.SetActive(false);
            Place((RectTransform)nickname.transform,30,195,590,50);foreach(var text in nickname.GetComponentsInChildren<Text>())text.rectTransform.sizeDelta=new Vector2(562,40);
        }
        void PersistDrafts(){PlayerPrefs.SetString("introductionDrafts",JsonUtility.ToJson(drafts));PlayerPrefs.Save();}
        void SaveDraft(bool notify)
        {
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
            int i=0;foreach(var d in drafts.items){string id=d.id;ArtButton(draftList,(d.id==drafts.selected?"●  ":"")+d.name,8,8+i++*66,690,54,()=>{if(!busy)SelectDraft(id);},d.id==drafts.selected?UITheme.Mint:UITheme.Paper,UITheme.Ink);}
            draftList.sizeDelta=new Vector2(710,Mathf.Max(300,16+i*66));draftHeading.text="已保存 "+drafts.items.Count+" 份 · 点击切换";
        }
        static string ShortPersonaName(string name)=>string.IsNullOrEmpty(name)?"—":name.Length<=4?name:name.Substring(0,3)+"…";
        void OnApplicationPause(bool paused){if(paused){SaveDraft(false);ResetMovement();if(net)Send("cancel");}}
        void OnApplicationQuit(){SaveDraft(false);}
    }
}
