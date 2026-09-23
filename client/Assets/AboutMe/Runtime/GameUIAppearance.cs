using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AboutMe
{
    [Serializable] class AppearanceSaveRequest {public AppearanceData appearance;public int expectedRevision;public string characterSkin,introduction;}
    [Serializable] class AppearanceRecommendRequest {public AppearanceData appearance;public string introduction;}
    [Serializable] class AppearanceRecommendation {public AppearanceData appearance;public string reason;}
    public partial class GameUI
    {
        AppearanceData appearanceDraft=new AppearanceData();bool appearanceDirty;string skinDraft="normal";
        RectTransform wardrobe;Camera wardrobeCamera;ToyActor wardrobeActor;RenderTexture wardrobeTexture;
        GameObject wardrobeLight;Text wardrobeNote,skinButtonLabel;Button skinButton,recommendButton;readonly Dictionary<string,Text> wardrobeValues=new Dictionary<string,Text>();
        readonly List<Button> wardrobeButtons=new List<Button>();
        UILiquidTabIndicator wardrobeViewIndicator;readonly Text[] wardrobeViewLabels=new Text[3];
        public AppearanceData CurrentAppearance=>appearanceDraft.Copy();
        void BuildWardrobe()
        {
            wardrobe=ArtPanel(artboard,"Wardrobe",0,0,1600,900,new Color(.08f,.10f,.15f,.84f),false,false);
            var card=ArtPanel(wardrobe,"Campus wardrobe",115,28,1370,836,UITheme.Paper,true,true);
            ArtText(card,"我的校园穿搭",32,28,600,52,34,UITheme.Ink,FontStyle.Bold);
            ArtText(card,"自由搭配 · 不影响战斗属性或 Persona",34,84,810,32,18,UITheme.Muted);
            wardrobeButtons.Add(ArtButton(card,"关闭",1220,28,116,46,CloseWardrobe,UITheme.Field,UITheme.Ink));
            skinButton=ArtButton(card,"DS 娘 · 未解锁",965,82,371,42,ToggleCharacterSkin,UITheme.Mint,UITheme.Ink);
            skinButtonLabel=skinButton.GetComponentInChildren<Text>();wardrobeButtons.Add(skinButton);
            var preview=ArtPanel(card,"Doll preview",32,137,435,484,UITheme.Field,true,false);var viewport=ArtRect(preview,"Preview image",new Vector2(5,-5),new Vector2(425,474));var raw=viewport.gameObject.AddComponent<RawImage>();
            wardrobeTexture=new RenderTexture(640,740,24){antiAliasing=4};raw.texture=wardrobeTexture;raw.raycastTarget=false;
            var cameraObject=new GameObject("Wardrobe camera");wardrobeCamera=cameraObject.AddComponent<Camera>();wardrobeCamera.orthographic=true;wardrobeCamera.orthographicSize=1.5f;wardrobeCamera.clearFlags=CameraClearFlags.SolidColor;wardrobeCamera.backgroundColor=UITheme.Field;wardrobeCamera.cullingMask=1<<30;wardrobeCamera.targetTexture=wardrobeTexture;wardrobeCamera.enabled=false;
            wardrobeLight=new GameObject("Wardrobe light");var light=wardrobeLight.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(35,155,0);
            var viewTabs=ArtRect(card,"Wardrobe preview views",new Vector2(32,-640),new Vector2(435,45));
            ArtPanel(viewTabs,"View tab track",0,0,435,45,UITheme.Field,true,false).GetComponent<Image>().raycastTarget=false;
            var activeView=ArtPanel(viewTabs,"Active view indicator",0,0,137,45,UITheme.Mint,true,false);activeView.GetComponent<Image>().raycastTarget=false;
            var tabRects=new RectTransform[3];
            for(int i=0;i<3;i++)
            {
                int view=i;var tab=tabRects[i]=ArtRect(viewTabs,"View · "+new[]{"正面","侧面","背面"}[i],new Vector2(i*149,0),new Vector2(137,45));
                var hit=tab.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
                var button=tab.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>SetWardrobeView(view));wardrobeButtons.Add(button);
                wardrobeViewLabels[i]=ArtText(tab,new[]{"正面","侧面","背面"}[i],0,0,137,45,18,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            }
            wardrobeViewIndicator=viewTabs.gameObject.AddComponent<UILiquidTabIndicator>();wardrobeViewIndicator.Configure(activeView,tabRects);
            wardrobeNote=ArtText(card,"根据介绍推荐一套，再按你的喜好调整。",32,706,435,83,18,UITheme.Ink);
            var labels=new Dictionary<string,string>{{"expression","表情"},{"hair","发型"},{"top","上衣"},{"bottom","下装"},{"shoes","鞋子"},{"headAccessory","头部配饰"},{"bag","包"},{"skinColor","肤色"},{"hairColor","发色"},{"topColor","上衣配色"},{"bottomColor","下装配色"},{"shoeColor","鞋子配色"},{"accessoryColor","配饰配色"}};
            int row=0;foreach(var slot in AppearanceCatalog.Data.slots)
            {
                string id=slot.id;float y=131+row++*42;
                ArtText(card,labels[id],510,y+8,130,30,18,UITheme.Ink,FontStyle.Bold);
                wardrobeButtons.Add(ArtButton(card,"‹",650,y,46,37,()=>CycleAppearance(id,-1),UITheme.Field,UITheme.Ink));
                var value=ArtPanel(card,"Choice "+id,711,y,477,37,UITheme.Field,true,false);
                wardrobeValues[id]=ArtText(value,"",9,3,459,31,18,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
                wardrobeButtons.Add(ArtButton(card,"›",1202,y,46,37,()=>CycleAppearance(id,1),UITheme.Field,UITheme.Ink));
            }
            recommendButton=ArtButton(card,"根据介绍 AI 推荐",510,704,357,52,()=>StartCoroutine(RecommendAppearance()),UITheme.Yellow,UITheme.Ink);wardrobeButtons.Add(recommendButton);
            wardrobeButtons.Add(ArtButton(card,"保存穿搭",885,704,363,52,()=>StartCoroutine(SaveAppearance()),UITheme.Mint,UITheme.Ink));
            wardrobeButtons.Add(ArtButton(card,"撤销未保存修改",510,771,357,37,()=>{appearanceDirty=false;LoadAppearance();wardrobeNote.text="已恢复上次保存的穿搭。";},UITheme.Field,UITheme.Ink));
            wardrobeButtons.Add(ArtButton(card,"重新载入角色",885,771,363,37,()=>{if(net.Sample){appearanceDirty=false;LoadAppearance();}else StartCoroutine(ReloadAppearance());},UITheme.Field,UITheme.Ink));
            wardrobe.gameObject.SetActive(false);
        }
        void OpenWardrobe()
        {
            if(busy)return;if(!wardrobe)BuildWardrobe();wardrobe.gameObject.SetActive(true);wardrobe.SetAsLastSibling();RefreshAppearancePreview();SetWardrobeView(0,true);wardrobeCamera.enabled=true;
            if(profile==null||!profile.dsGirlUnlocked)wardrobeNote.text="介绍里写喜欢 DeepSeek，保存穿搭后可解锁 DS 娘。";
            else if(skinDraft=="ds-girl")wardrobeNote.text="DS 娘使用固定套装；原校园穿搭仍保留。";
        }
        void CloseWardrobe(){wardrobe.gameObject.SetActive(false);wardrobeCamera.enabled=false;}
        void SetWardrobeView(int view,bool immediate=false)
        {
            wardrobeViewIndicator.SetSelected(view,immediate);
            for(int i=0;i<wardrobeViewLabels.Length;i++)wardrobeViewLabels[i].color=i==view?UITheme.Ink:UITheme.Muted;
            var origin=new Vector3(1000,1.2f,0);wardrobeCamera.transform.position=origin+new[]{new Vector3(0,.1f,5),new Vector3(5,.1f,0),new Vector3(0,.1f,-5)}[view];wardrobeCamera.transform.LookAt(origin);
        }
        void CycleAppearance(string id,int step)
        {
            if(busy)return;var slot=AppearanceCatalog.Data.slots.First(x=>x.id==id);int index=Array.FindIndex(slot.options,x=>x.id==appearanceDraft.Get(id));appearanceDraft.Set(id,slot.options[(index+step+slot.options.Length)%slot.options.Length].id);appearanceDirty=true;RefreshAppearancePreview();
        }
        void SetDraftColor(string color){appearanceDraft.topColor=color;appearanceDirty=true;RefreshAppearancePreview();}
        void ToggleCharacterSkin()
        {
            if(busy||profile==null||!profile.dsGirlUnlocked)return;
            skinDraft=skinDraft=="ds-girl"?"normal":"ds-girl";appearanceDirty=true;
            wardrobeNote.text=skinDraft=="ds-girl"?"DS 娘使用固定套装；原穿搭保留。保存后生效。":"已切回校园玩偶；保存后生效。";RefreshAppearancePreview();
        }
        void LoadAppearance()
        {
            if(appearanceDirty)
            {if(profile==null||!profile.dsGirlUnlocked)skinDraft="normal";RefreshAppearancePreview();return;}
            skinDraft=profile!=null&&profile.dsGirlUnlocked&&profile.characterSkin=="ds-girl"?"ds-girl":"normal";
            if(net.Sample)
            {
                var saved=PlayerPrefs.GetString("sampleAppearance","");appearanceDraft=AppearanceData.Resolve(saved==""?profile?.appearance:JsonUtility.FromJson<AppearanceData>(saved));
                if(profile!=null)profile.appearance=appearanceDraft.Copy();
            }
            else appearanceDraft=AppearanceData.Resolve(profile?.appearance,profile?.color??selectedColor,profile?.outfit??0);
            RefreshAppearancePreview();
        }
        void RefreshAppearancePreview()
        {
            appearanceDraft=AppearanceData.Resolve(appearanceDraft);appearanceDraft.character=profile!=null&&profile.dsGirlUnlocked&&skinDraft=="ds-girl"?"ds":"normal";
            selectedColor=appearanceDraft.topColor;world.PreviewAppearance=appearanceDraft.Copy();net.SampleAppearance=appearanceDraft.Copy();RefreshColorSelection();RefreshExpressionChoices();
            if(!wardrobe)return;
            skinButton.interactable=profile!=null&&profile.dsGirlUnlocked&&!busy;
            skinButtonLabel.text=profile!=null&&profile.dsGirlUnlocked?(skinDraft=="ds-girl"?"切换为校园玩偶":"切换为 DS 娘"):"DS 娘 · 未解锁";
            recommendButton.interactable=skinDraft!="ds-girl"&&!busy;
            foreach(var slot in AppearanceCatalog.Data.slots)
            {
                wardrobeValues[slot.id].text=slot.options.First(x=>x.id==appearanceDraft.Get(slot.id)).label;
                if(slot.id.EndsWith("Color"))wardrobeValues[slot.id].transform.parent.GetComponent<Image>().color=ToyFactory.ColorOf(appearanceDraft.Get(slot.id));
                wardrobeValues[slot.id].color=slot.id.EndsWith("Color")&&ToyFactory.ColorOf(appearanceDraft.Get(slot.id)).grayscale<.45f?UITheme.Paper:UITheme.Ink;
            }
            if(wardrobeActor==null){wardrobeActor=new ToyActor("My campus doll",selectedColor,0,null,appearanceDraft);wardrobeActor.root.transform.position=new Vector3(1000,1,0);wardrobeActor.label.text="";}
            wardrobeActor.ApplyAppearance(appearanceDraft);
            foreach(var t in wardrobeActor.root.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
        }
        IEnumerator ReloadAppearance()
        {
            if(busy)yield break;busy=true;
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/profiles/me",null,net.Token))
            {
                yield return req.SendWebRequest();
                if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);appearanceDirty=false;LoadAppearance();wardrobeNote.text="已载入保存的穿搭。";
            }
            busy=false;
        }
        IEnumerator EnsureAppearanceSession()
        {
            net.ApiUrl=api.text.Trim().TrimEnd('/');
            if(net.ApiUrl==""){busy=false;net.Status="请先在连接设置中填写角色服务地址。";yield break;}
            net.Token=SavedTokenForApi();
            if(!string.IsNullOrEmpty(net.Token))yield break;
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/sessions",JsonUtility.ToJson(new SessionRequest{nickname=nickname.text})))
            {
                yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req,"建立角色会话");yield break;}
                var response=JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text);net.Token=response.token;profile=response.profile;SaveTokenForApi(net.Token);
            }
        }
        IEnumerator UnlockDSGirl()
        {
            if(busy)yield break;
            if(net.Sample){net.Status="请先退出本地样例，再用真实介绍解锁 DS 娘。";yield break;}
            if(string.IsNullOrWhiteSpace(introduction.text)||introduction.text.Trim().Length<3)
            {net.Status="先写至少 3 个字的介绍，例如“我喜欢 DeepSeek”。";yield break;}
            yield return SaveAppearance(true);
        }
        IEnumerator RecommendAppearance()
        {
            if(busy)yield break;
            if(skinDraft=="ds-girl"){wardrobeNote.text="DS 娘套装固定；切回校园玩偶后可推荐穿搭。";yield break;}
            if(net.Sample){net.Status="样例模式可手动搭配；真实 AI 推荐请使用真实角色。";yield break;}
            if(string.IsNullOrWhiteSpace(introduction.text)||introduction.text.Trim().Length<3){net.Status="先写一段介绍，再让 AI 推荐穿搭。";yield break;}
            busy=true;wardrobeNote.text="正在根据你的介绍推荐穿搭…";yield return EnsureAppearanceSession();if(!busy||string.IsNullOrEmpty(net.Token))yield break;
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/appearance/recommend",JsonUtility.ToJson(new AppearanceRecommendRequest{introduction=introduction.text,appearance=appearanceDraft}),net.Token))
            {
                yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){wardrobeNote.text="推荐未完成，保留当前穿搭；可继续手动搭配。";Fail(req);yield break;}
                var result=JsonUtility.FromJson<AppearanceRecommendation>(req.downloadHandler.text);appearanceDraft=AppearanceData.Resolve(result.appearance);appearanceDirty=true;wardrobeNote.text=result.reason+"\n可继续调整，满意后保存。";RefreshAppearancePreview();
            }
            busy=false;
        }
        IEnumerator SaveAppearance(bool openWardrobeOnUnlock=false)
        {
            if(busy)yield break;
            if(net.Sample)
            {
                if(profile!=null)profile.appearance=appearanceDraft.Copy();net.SampleAppearance=appearanceDraft.Copy();PlayerPrefs.SetString("sampleAppearance",JsonUtility.ToJson(appearanceDraft));PlayerPrefs.Save();appearanceDirty=false;net.Status="样例穿搭已保存到本机。";yield break;
            }
            busy=true;net.ApiUrl=api.text.Trim().TrimEnd('/');yield return EnsureAppearanceSession();if(!busy||string.IsNullOrEmpty(net.Token))yield break;
            string intro=string.IsNullOrWhiteSpace(introduction.text)?null:introduction.text.Trim();
            for(int attempt=0;attempt<2;attempt++)
            {
                using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/profiles/me/appearance",JsonUtility.ToJson(new AppearanceSaveRequest{appearance=appearanceDraft,expectedRevision=profile?.appearanceRevision??0,characterSkin=skinDraft,introduction=intro!=null&&intro.Length>=3?intro:null}),net.Token))
                {
                    yield return req.SendWebRequest();
                    if(req.responseCode==401&&attempt==0)
                    {net.Token="";SaveTokenForApi("");yield return EnsureAppearanceSession();if(!busy||string.IsNullOrEmpty(net.Token))yield break;continue;}
                    if(req.result!=UnityWebRequest.Result.Success){Fail(req,"保存穿搭与 DS 娘解锁");if(wardrobeNote)wardrobeNote.text=net.Status;yield break;}
                    bool wasUnlocked=profile!=null&&profile.dsGirlUnlocked;
                    profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);appearanceDirty=false;DisplayProfile();
                    PlayerPrefs.SetString("introduction",introduction.text);PlayerPrefs.SetString("nickname",nickname.text);PlayerPrefs.Save();
                    busy=false;
                    if(openWardrobeOnUnlock)
                    {
                        if(profile.dsGirlUnlocked){OpenWardrobe();net.Status="DS 娘已解锁。请在衣橱中切换并保存。";wardrobeNote.text="已解锁 DS 娘。点击右上角切换，再保存即可使用。";}
                        else net.Status="尚未解锁：请在介绍中写明你喜欢 DeepSeek，然后重试。";
                    }
                    else
                    {
                        net.Status="穿搭已保存，Persona 没有改变。";
                        if(wardrobeNote)wardrobeNote.text=!wasUnlocked&&profile.dsGirlUnlocked?"已解锁 DS 娘。点击右上角切换，再保存即可使用。":"已保存。你可以直接带着这套穿搭进入房间。";
                    }
                    yield break;
                }
            }
            busy=false;
        }
        IEnumerator EnterWithAppearance()
        {
            if(busy)yield break;
            if(appearanceDirty){yield return SaveAppearance();if(appearanceDirty)yield break;}
            Enter();
        }
        void OnDestroy()
        {
            ReleaseCursor();
            if(wardrobeLight)Destroy(wardrobeLight);if(wardrobeActor!=null)Destroy(wardrobeActor.root);if(wardrobeCamera)Destroy(wardrobeCamera.gameObject);if(wardrobeTexture){wardrobeTexture.Release();Destroy(wardrobeTexture);}
        }
    }
}
