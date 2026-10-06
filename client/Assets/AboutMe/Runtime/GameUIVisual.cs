using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AboutMe
{
    public partial class GameUI
    {
        RectTransform safeRoot,artboard,selectionBleed,studioCard,homeHero,lobbyCard,lobbyGrid,hudCard,hudToast,hudProgress,hotbar,resultContent,resultEventLine,keyboardHint;
        Image selectionBleedImage,selectionBleedWash;
        AspectRatioFitter selectionBleedFit;
        Sprite selectionCampusSprite,selectionStudioSprite;
        ScrollRect inventoryScroll;
        Button keyboardHelpButton;
        Text mechanicHint;
        readonly List<RectTransform> inventoryCards=new List<RectTransform>();
        readonly List<Image> slotThumbnails=new List<Image>();
        readonly Dictionary<string,Sprite> personaThumbnails=new Dictionary<string,Sprite>();
        RectTransform[] playerCards=new RectTransform[6];
        Text[] playerNames=new Text[6],playerMeta=new Text[6];
        Image[] playerAvatars=new Image[6],playerReadyMarks=new Image[6];
        Text roomTitle,roomCount,phaseText,timerText,stabilityText,personaCountText,progressText,winnerText,outfitText,aiServiceText,introCounter;
        Image stabilityFill,progressFill,statusSurface,aiServiceSurface;
        RectTransform statusPill;
        StickerButton generateStyle,recordStyle,readyStyle,startStyle;
        readonly List<StickerButton> slotStyles=new List<StickerButton>(),colorStyles=new List<StickerButton>();
        UIPulse toastPulse;
        Button reconnectButton;
        string lastStatus="",lastLobbyVisual="",lastResultVisual="",previewKind="";
        int lastScreenWidth,lastScreenHeight;
        bool compactLayout,inventoryHasBeenBuilt,inventoryWasOpened;
        public bool CompactLayout=>compactLayout;

        void BuildArtUI()
        {
            var canvasObject=new GameObject("About Me UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var screenCanvas=canvasObject.GetComponent<Canvas>();screenCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas=(RectTransform)canvasObject.transform;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            if(!FindObjectOfType<EventSystem>())new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));

            var backdrop=ArtRect(canvas,"Camera passthrough",Vector2.zero,Vector2.zero);Stretch(backdrop);var backdropImage=backdrop.gameObject.AddComponent<Image>();backdropImage.color=Color.clear;backdropImage.raycastTarget=false;
            selectionBleed=ArtRect(canvas,"Selection background · full screen",Vector2.zero,Vector2.zero);Stretch(selectionBleed);
            selectionBleed.gameObject.AddComponent<RectMask2D>();
            var bleedRect=ArtRect(selectionBleed,"Campus background · full screen",Vector2.zero,new Vector2(1600,900));
            bleedRect.anchorMin=bleedRect.anchorMax=bleedRect.pivot=new Vector2(.5f,.5f);
            selectionBleedImage=bleedRect.gameObject.AddComponent<Image>();
            selectionCampusSprite=Resources.Load<Sprite>(CampusSelectionArt);
            if(!selectionCampusSprite)throw new InvalidOperationException("Missing selection background");
            selectionStudioSprite=Resources.Load<Sprite>(StudioSelectionArt);
            if(!selectionStudioSprite)throw new InvalidOperationException("Missing studio selection background");
            selectionBleedImage.sprite=selectionCampusSprite;
            selectionBleedImage.raycastTarget=false;
            selectionBleedFit=selectionBleedImage.gameObject.AddComponent<AspectRatioFitter>();
            selectionBleedFit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            selectionBleedFit.aspectRatio=selectionCampusSprite.rect.width/selectionCampusSprite.rect.height;
            var washRect=ArtRect(selectionBleed,"Selection paper wash · full screen",Vector2.zero,Vector2.zero);Stretch(washRect);
            selectionBleedWash=washRect.gameObject.AddComponent<Image>();selectionBleedWash.raycastTarget=false;
            safeRoot=ArtRect(canvas,"Safe area",Vector2.zero,Vector2.zero);Stretch(safeRoot);
            artboard=ArtRect(safeRoot,"1600 × 900 artboard",Vector2.zero,new Vector2(1600,900));artboard.anchorMin=artboard.anchorMax=artboard.pivot=new Vector2(.5f,.5f);
            artboard.gameObject.AddComponent<RectMask2D>();
            var safe=canvasObject.AddComponent<UISafeArtboard>();safe.SafeRoot=safeRoot;safe.Artboard=artboard;

            BuildHome();BuildSettings();BuildLobby();BuildHud();BuildBag();BuildResults();BuildArtTouch();BuildStatus();
            home.gameObject.SetActive(true);lobby.gameObject.SetActive(false);hud.gameObject.SetActive(false);results.gameObject.SetActive(false);settings.gameObject.SetActive(false);bag.gameObject.SetActive(false);
            ApplyAdaptiveLayout(true);
        }

        void UpdateSelectionBackground(Snapshot state)
        {
            bool wardrobeOpen=wardrobe&&wardrobe.gameObject.activeSelf;
            bool visible=state==null||state.phase==MatchPhase.Lobby||state.phase==MatchPhase.Finished||optionsOpen||wardrobeOpen;
            if(selectionBleed.gameObject.activeSelf!=visible)selectionBleed.gameObject.SetActive(visible);
            if(!visible)return;
            var sprite=optionsOpen||wardrobeOpen?selectionStudioSprite:selectionCampusSprite;
            if(selectionBleedImage.sprite!=sprite)
            {
                selectionBleedImage.sprite=sprite;
                selectionBleedFit.aspectRatio=sprite.rect.width/sprite.rect.height;
            }
            selectionBleedWash.color=wardrobeOpen?new Color(1f,.985f,.95f,.22f):
                optionsOpen?new Color(1f,.985f,.95f,.27f):
                state==null&&page==MenuPage.Presets?new Color(1f,.98f,.93f,.55f):
                state==null&&page!=MenuPage.Main?new Color(1f,.985f,.95f,.18f):
                state!=null&&state.phase==MatchPhase.Lobby?new Color(.96f,.99f,1f,.24f):Color.clear;
        }

        void BuildHome()
        {
            home=ArtPanel(artboard,"Introduction",0,0,1600,900,Color.clear,false,false);
            studioCard=ArtPanel(home,"Persona studio",672,28,900,836,UITheme.Paper,true,true);
            Tape(studioCard,"PERSONA WORKSHOP",28,24,216,38,UITheme.Mint);
            ArtText(studioCard,"先介绍一下，你是谁？",28,78,830,48,31,UITheme.Ink,FontStyle.Bold);
            ArtText(studioCard,"昵称",28,143,180,24,15,UITheme.Muted,FontStyle.Bold);
            nickname=ArtField(studioCard,28,176,400,50,"你的名字",false);nickname.text=PlayerPrefs.GetString("nickname","玩家");
            room=ArtField(studioCard,28,176,400,50,"房间码 · 留空创建",false);
            ArtText(studioCard,"自我介绍",28,248,230,24,15,UITheme.Muted,FontStyle.Bold);
            introCounter=ArtText(studioCard,"0 / 1200",278,248,150,24,14,UITheme.Muted,FontStyle.Bold,TextAnchor.UpperRight);
            introduction=ArtScrollableField(studioCard,28,283,400,250,"我来自哪里、喜欢什么、有什么特别经历……");
            introduction.characterLimit=1200;introduction.text=PlayerPrefs.GetString("introduction","");
            introduction.onValueChanged.AddListener(value=>introCounter.text=value.Length+" / 1200");introCounter.text=introduction.text.Length+" / 1200";
            ArtRaisedButton(studioCard,"选择题模式",464,152,184,154,OpenIntroductionQuestionnaire,UITheme.Yellow,UITheme.Ink);
            var outfitButton=ArtRaisedButton(studioCard,"我的衣橱",674,152,184,154,OpenWardrobe,UITheme.Mint,UITheme.Ink);outfitText=outfitButton.GetComponentInChildren<Text>();
            BuildSavedCharacterCard();
            BuildPersonaResults(ArtPanel(studioCard,"Persona result",28,565,830,174,UITheme.Field,true,false));
            generate=ArtButton(studioCard,"AI生成",28,763,156,46,()=>StartCoroutine(Generate()),UITheme.Ink,UITheme.Paper);generateStyle=generate.GetComponent<StickerButton>();
            ArtButton(studioCard,"本地样例",200,763,140,46,LoadPracticePersona,UITheme.Yellow,UITheme.Ink);
            join=ArtButton(studioCard,"保存",634,763,102,46,()=>SaveDraft(true),UITheme.Mint,UITheme.Ink);
            ArtButton(studioCard,"返回",752,763,106,46,()=>ShowPage(MenuPage.Main),UITheme.Field,UITheme.Ink);
        }

        void BuildSettings()
        {
            settings=ArtPanel(artboard,"Settings modal",0,0,1600,900,new Color(.05f,.07f,.12f,.52f),false,false);Stretch(settings);settings.GetComponent<Image>().raycastTarget=true;
            var card=ArtPanel(settings,"Player settings card",350,90,900,720,UITheme.Paper,true,true);card.gameObject.AddComponent<UIReveal>();
            ArtText(card,"游戏设置",36,28,650,60,34,UITheme.Ink,FontStyle.Bold);
            ArtButton(card,"关闭设置",730,28,134,50,()=>{optionsOpen=false;settings.gameObject.SetActive(false);PlayerPrefs.Save();},UITheme.Field,UITheme.Ink,"exit");
            ArtText(card,"画面与操作",36,108,820,32,19,UITheme.Muted,FontStyle.Bold);
            var sensitivity=ArtButton(card,"",36,154,828,60,()=>{cameraSensitivity=cameraSensitivity>=2?.5f:cameraSensitivity+.5f;PlayerPrefs.SetFloat("cameraSensitivity",cameraSensitivity);SavePlayerSettings();},UITheme.Field,UITheme.Ink);sensitivityLabel=sensitivity.GetComponentInChildren<Text>();
            var touchButton=ArtButton(card,"",36,230,828,60,()=>{touchMode=(touchMode+1)%3;PlayerPrefs.SetInt("touchMode",touchMode);RefreshTouchVisibility();SavePlayerSettings();},UITheme.Field,UITheme.Ink);touchSettingLabel=touchButton.GetComponentInChildren<Text>();
            var motion=ArtButton(card,"",36,306,828,60,()=>{reduceMotion=!reduceMotion;PlayerPrefs.SetInt("reduceMotion",reduceMotion?1:0);SavePlayerSettings();},UITheme.Field,UITheme.Ink);motionSettingLabel=motion.GetComponentInChildren<Text>();
            ArtText(card,"音效与反馈",36,388,820,32,19,UITheme.Muted,FontStyle.Bold);
            var music=ArtButton(card,"",36,434,402,60,()=>{PlayerPrefs.SetFloat("battleMusicVolume",BattleMusic.SavedVolume<.05f?.30f:0);SavePlayerSettings();},UITheme.Field,UITheme.Ink);musicSettingLabel=music.GetComponentInChildren<Text>();
            var shake=ArtButton(card,"",462,434,402,60,()=>{float v=PlayerPrefs.GetFloat("hitShake",.3f);PlayerPrefs.SetFloat("hitShake",v<.1f?.3f:v<.6f?1:0);SavePlayerSettings();},UITheme.Field,UITheme.Ink);shakeSettingLabel=shake.GetComponentInChildren<Text>();
            ArtText(card,"语言",36,532,230,50,22,UITheme.Ink,FontStyle.Bold);
            chineseLanguageButton=ArtButton(card,"中文",300,524,270,60,()=>SetGameLanguage(false),UITheme.Field,UITheme.Ink);
            englishLanguageButton=ArtButton(card,"English",594,524,270,60,()=>SetGameLanguage(true),UITheme.Field,UITheme.Ink);
            ArtText(card,"设置即时生效，自动保存。",36,628,828,40,17,UITheme.Muted);
        }

        Text sensitivityLabel,touchSettingLabel,musicSettingLabel,motionSettingLabel,shakeSettingLabel;
        Button chineseLanguageButton,englishLanguageButton;
        void SavePlayerSettings(){PlayerPrefs.Save();RefreshSettingLabels();}
        void SetGameLanguage(bool english)
        {
            GameLocalization.SetEnglish(english);if(startMenuStage)startMenuStage.ApplyLanguage();RefreshSettingLabels();
            lastInventory="";lastLobbyVisual="";lastResultVisual="";
            RefreshMenuSummary();RefreshQuestionnaireOptions();RefreshSavedCharacterCard();if(displayedGenerationTerms.Length>0)DisplayGenerationTerms(displayedGenerationTerms);
        }
        void RefreshSettingLabels()
        {
            sensitivityLabel.text="镜头灵敏度 · "+cameraSensitivity.ToString("0.0")+" 倍";
            touchSettingLabel.text="触控按钮 · "+new[]{"自动","显示","隐藏"}[touchMode];
            musicSettingLabel.text=GameLocalization.English?"Battle music · "+(BattleMusic.SavedVolume<.05f?"Off":"On"):"战斗音乐 · "+(BattleMusic.SavedVolume<.05f?"关闭":"开启");
            motionSettingLabel.text=GameLocalization.English?"Reduce motion · "+(reduceMotion?"On":"Off"):"减少动态 · "+(reduceMotion?"开启":"关闭");
            float shake=PlayerPrefs.GetFloat("hitShake",.3f);shakeSettingLabel.text=GameLocalization.English?"Impact shake · "+(shake<.1f?"Off":shake<.6f?"Low":"Strong"):"打击震动 · "+(shake<.1f?"关闭":shake<.6f?"轻微":"明显");
            chineseLanguageButton.GetComponentInChildren<Text>().text=(GameLocalization.English?"":"✓ ")+"中文";
            englishLanguageButton.GetComponentInChildren<Text>().text=(GameLocalization.English?"✓ ":"")+"English";
            chineseLanguageButton.GetComponent<StickerButton>().SetState(GameLocalization.English?UIVisualState.Normal:UIVisualState.Selected);
            englishLanguageButton.GetComponent<StickerButton>().SetState(GameLocalization.English?UIVisualState.Selected:UIVisualState.Normal);
            if(startMenuStage)startMenuStage.SetMotionEnabled(!reduceMotion);
            GameLocalization.Refresh();
        }
        void BuildLobby()
        {
            lobby=ArtPanel(artboard,"Friend room",0,0,1600,900,Color.clear,false,false);
            lobbyCard=ArtPanel(lobby,"Room ticket",44,42,676,816,UITheme.Paper,true,true);lobbyCard.gameObject.AddComponent<UIReveal>();
            Tape(lobbyCard,"MEET YOUR FRIENDS",28,24,232,38,UITheme.Pink);
            roomTitle=ArtText(lobbyCard,"房间 ------",28,76,420,47,31,UITheme.Ink,FontStyle.Bold);
            roomCount=ArtText(lobbyCard,"0 / 6 位朋友",28,119,250,32,18,UITheme.Muted,FontStyle.Bold);
            ArtButton(lobbyCard,"房间二维码",476,75,170,46,()=>StartCoroutine(ShowQr()),UITheme.Field,UITheme.Ink,"camera");
            lobbyText=roomCount;
            lobbyGrid=ArtPanel(lobbyCard,"Player grid",24,166,628,408,Color.clear,false,false);
            for(int i=0;i<6;i++)
            {
                int col=i%2,row=i/2;var player=ArtPanel(lobbyGrid,"Player card "+(i+1),col*310,row*133,298,119,UITheme.Field,true,false);playerCards[i]=player;
                playerAvatars[i]=ArtPanel(player,"Color avatar",16,19,62,62,UITheme.Mint,true,false).GetComponent<Image>();
                var leftEye=ArtPanel(player,"Left eye",33,41,7,9,UITheme.Ink,false,false);leftEye.GetComponent<Image>().sprite=UITheme.Circle;leftEye.GetComponent<Image>().type=Image.Type.Simple;
                var rightEye=ArtPanel(player,"Right eye",54,41,7,9,UITheme.Ink,false,false);rightEye.GetComponent<Image>().sprite=UITheme.Circle;rightEye.GetComponent<Image>().type=Image.Type.Simple;
                ArtPanel(player,"Smile",40,59,16,4,UITheme.Ink,false,false);
                playerNames[i]=ArtText(player,"等待朋友…",92,15,188,38,18,UITheme.Ink,FontStyle.Bold);
                playerMeta[i]=ArtText(player,"空位",92,53,180,30,15,UITheme.Muted);
                playerReadyMarks[i]=ArtIcon(player,"ready",247,79,28,UITheme.Green);
            }
            ready=ArtButton(lobbyCard,"我准备好了",24,598,306,58,()=>Send("ready"),UITheme.Mint,UITheme.Ink,"ready");readyStyle=ready.GetComponent<StickerButton>();
            start=ArtButton(lobbyCard,"开始互相介绍",346,598,306,58,()=>Send("start"),UITheme.Ink,UITheme.Paper,"attack");startStyle=start.GetComponent<StickerButton>();
            ArtButton(lobbyCard,"分享房间码",24,675,306,50,()=>platform.Share(net.State?.room??""),UITheme.Yellow,UITheme.Ink,"share");
            ArtButton(lobbyCard,"离开房间",346,675,306,50,Leave,UITheme.Field,UITheme.Ink,"exit");
            ArtText(lobbyCard,"2–6 人均可开局；达到 2 人后房主可随时开始。Ready 仅作状态提示。",28,748,610,48,15,UITheme.Muted,FontStyle.Normal,TextAnchor.MiddleLeft);
        }

        void BuildHud()
        {
            hud=ArtPanel(artboard,"Match HUD",0,0,1600,900,Color.clear,false,false);
            hudCard=ArtPanel(hud,"Player status sticker",34,28,382,154,UITheme.Paper,true,true);
            hudText=ArtText(hudCard,"玩家",20,13,260,35,22,UITheme.Ink,FontStyle.Bold);
            personaCountText=ArtText(hudCard,"PERSONA × 0",20,50,210,32,17,UITheme.Ink,FontStyle.Bold);
            stabilityText=ArtText(hudCard,"韧性 100",250,50,110,32,16,UITheme.Ink,FontStyle.Bold,TextAnchor.UpperRight);
            var stabilityTrack=ArtPanel(hudCard,"Stability track",20,94,342,25,UITheme.Field,true,false);
            stabilityFill=ArtPanel(stabilityTrack,"Stability fill",4,4,334,17,UITheme.Mint,false,false).GetComponent<Image>();stabilityFill.rectTransform.anchorMin=new Vector2(0,0);stabilityFill.rectTransform.anchorMax=new Vector2(1,1);stabilityFill.rectTransform.offsetMin=new Vector2(4,4);stabilityFill.rectTransform.offsetMax=new Vector2(-4,-4);
            phaseText=ArtText(hud,"BATTLE",650,22,300,34,18,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            var timePill=ArtPanel(hud,"Round clock",690,56,220,58,UITheme.Yellow,true,true);timerText=ArtText(timePill,"00:00",10,5,200,47,29,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            hudToast=ArtPanel(hud,"Announcement sticker",480,128,640,58,UITheme.Ink,true,true);eventText=ArtText(hudToast,"准备介绍你自己",20,7,600,44,19,UITheme.Paper,FontStyle.Bold,TextAnchor.MiddleCenter);toastPulse=hudToast.gameObject.AddComponent<UIPulse>();
            hudProgress=ArtPanel(hud,"Interaction progress",545,690,510,74,UITheme.Paper,true,true);progressText=ArtText(hudProgress,"吞噬 0%",18,8,474,25,16,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            var progressTrack=ArtPanel(hudProgress,"Progress track",18,42,474,18,UITheme.Field,true,false);progressFill=ArtPanel(progressTrack,"Progress fill",3,3,468,12,UITheme.Pink,false,false).GetComponent<Image>();progressFill.rectTransform.anchorMin=new Vector2(0,0);progressFill.rectTransform.anchorMax=new Vector2(1,1);progressFill.rectTransform.offsetMin=new Vector2(3,3);progressFill.rectTransform.offsetMax=new Vector2(-3,-3);hudProgress.gameObject.SetActive(false);
            mechanicHint=ArtText(hud,"",340,762,920,29,17,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            keyboardHint=ArtPanel(hud,"Keyboard help",34,202,382,142,new Color(1,1,1,.94f),true,true);
            ArtText(keyboardHint,"WASD 移动  ·  点击场地锁定鼠标\n移动鼠标转镜头；失败时按住右键拖动\nF 使用 · R 借用文字 · T 放下\nG/H 抓取 · E 吞噬/回收\n1–5 切换  ·  B 背包  ·  Q 挣脱",16,10,350,125,16,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);
            keyboardHint.gameObject.SetActive(false);
            keyboardHelpButton=ArtButton(hud,"操作",34,807,96,48,()=>keyboardHint.gameObject.SetActive(!keyboardHint.gameObject.activeSelf),UITheme.Paper,UITheme.Ink,"status");
            hotbar=ArtPanel(hud,"Persona hotbar",380,795,840,84,UITheme.Paper,true,true);
            for(int i=0;i<5;i++)
            {
                int slot=i;var button=ArtButton(hotbar,(i+1)+"  —",12+i*139,12,129,60,()=>{var state=net.State;if(state!=null&&slot<QuickItems(state).Length)Send("equip",QuickItems(state)[slot].id);},UITheme.Field,UITheme.Ink);
                var label=button.GetComponentInChildren<Text>();Place(label.rectTransform,50,4,74,52);label.fontSize=15;
                var icon=ArtIcon(button.transform,"status",9,11,39,UITheme.Muted);slotThumbnails.Add(icon);
                slots.Add(button);slotStyles.Add(button.GetComponent<StickerButton>());
            }
            ArtButton(hotbar,"背包",714,12,114,60,ToggleBag,UITheme.Purple,UITheme.Ink,"bag");
            ArtButton(hud,"设置",1300,28,126,46,OpenSettings,UITheme.Paper,UITheme.Ink,"settings");
            ArtButton(hud,"退出",1440,28,126,46,Leave,UITheme.Paper,UITheme.Ink,"exit");
            reconnectButton=ArtButton(hud,"重连",1440,86,126,42,()=>{if(!Mirror.NetworkClient.active)net.Connect();else net.Status=Mirror.NetworkClient.isConnected?"当前连接正常，无需重连。":"正在连接，请稍候。";},UITheme.Field,UITheme.Ink,"status");
        }

        void BuildBag()
        {
            bag=ArtPanel(artboard,"Inventory modal",0,0,1600,900,new Color(.05f,.07f,.12f,.48f),false,false);Stretch(bag);bag.GetComponent<Image>().raycastTarget=true;
            var card=ArtPanel(bag,"Persona backpack",270,108,1060,684,UITheme.Paper,true,true);card.gameObject.AddComponent<UIReveal>();
            Tape(card,"YOUR PERSONA BAG",30,25,232,38,UITheme.Purple);
            bagText=ArtText(card,"你的 Persona",30,75,770,55,26,UITheme.Ink,FontStyle.Bold);
            ArtButton(card,"返回",876,28,152,50,ToggleBag,UITheme.Mint,UITheme.Ink,"exit");
            var viewport=ArtPanel(card,"Inventory viewport",24,136,1012,516,Color.clear,false,false);viewport.gameObject.AddComponent<RectMask2D>();
            inventoryContent=ArtRect(viewport,"Inventory cards",Vector2.zero,new Vector2(1000,516));inventoryContent.anchorMin=inventoryContent.anchorMax=inventoryContent.pivot=new Vector2(0,1);
            var grid=inventoryContent.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(320,215);grid.spacing=new Vector2(12,12);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.padding=new RectOffset(6,6,6,6);
            inventoryScroll=viewport.gameObject.AddComponent<ScrollRect>();inventoryScroll.viewport=viewport;inventoryScroll.content=inventoryContent;inventoryScroll.horizontal=false;inventoryScroll.movementType=ScrollRect.MovementType.Clamped;inventoryScroll.scrollSensitivity=42;
            ArtText(card,"拖动浏览  ·  点击装备  ·  置顶加入快捷栏",30,658,810,22,14,UITheme.Muted);
        }

        void BuildResults()
        {
            results=ArtPanel(artboard,"Results",0,0,1600,900,new Color(.96f,.93f,.87f,.97f),false,false);Stretch(results);
            DecorativeDot(results,80,78,96,UITheme.Yellow,.45f);DecorativeDot(results,1405,78,112,UITheme.Pink,.45f);DecorativeDot(results,1320,730,82,UITheme.Mint,.5f);
            var card=ArtPanel(results,"Result receipt",226,64,1148,764,UITheme.Paper,true,true);card.gameObject.AddComponent<UIReveal>();
            Tape(card,"MATCH RECEIPT",35,28,202,38,UITheme.Yellow);
            ArtText(card,"WHAT DID YOU EAT?",34,77,1065,88,58,UITheme.Ink,FontStyle.Bold);
            var winner=ArtPanel(card,"Winner banner",34,170,1080,84,UITheme.Mint,true,false);ArtIcon(winner,"crown",22,17,50,UITheme.Ink);winnerText=ArtText(winner,"胜者保住了最后的自我",88,9,960,66,25,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);
            var viewport=ArtPanel(card,"Eaten records viewport",34,276,1080,355,Color.clear,false,false);viewport.gameObject.AddComponent<RectMask2D>();
            resultContent=ArtRect(viewport,"Eaten record cards",Vector2.zero,new Vector2(1060,355));resultContent.anchorMin=resultContent.anchorMax=resultContent.pivot=new Vector2(0,1);
            var layout=resultContent.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=10;layout.childControlHeight=false;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.padding=new RectOffset(4,8,4,4);
            var fitter=resultContent.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=resultContent;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            resultText=ArtText(card,"",34,640,1030,1,1,Color.clear);
            resultEventLine=ArtPanel(card,"Latest match event",34,636,1080,32,UITheme.Pink,true,false);
            resultEventText=ArtText(resultEventLine,"",12,1,1056,30,21,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            resultEventLine.gameObject.SetActive(false);
            ArtButton(card,"保存结算截图",34,672,350,58,()=>platform.Screenshot(),UITheme.Yellow,UITheme.Ink,"camera");
            ArtButton(card,"返回主菜单",776,672,338,58,Leave,UITheme.Ink,UITheme.Paper,"ready");
            replay=ArtButton(card,"再来一局",412,672,338,58,ReplaySolo,UITheme.Mint,UITheme.Ink,"ready");
        }

        void BuildStatus()
        {
            statusPill=ArtPanel(artboard,"Status toast",46,838,650,46,UITheme.Paper,true,true);statusSurface=statusPill.GetComponent<Image>();
            statusPill.gameObject.AddComponent<UIPulse>();ArtIcon(statusPill,"status",14,9,28,UITheme.Ink);status=ArtText(statusPill,"",52,4,582,38,15,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);status.horizontalOverflow=HorizontalWrapMode.Wrap;status.resizeTextForBestFit=true;status.resizeTextMinSize=10;status.resizeTextMaxSize=15;statusPill.gameObject.SetActive(false);
        }

        void BuildArtTouch()
        {
            touch=ArtPanel(hud,"Touch controls",0,0,1600,900,Color.clear,false,false);
            var pad=ArtPanel(touch,"Move joystick",54,592,224,224,new Color(1,1,1,.52f),true,true);movePad=pad.gameObject.AddComponent<TouchPad>();
            movePad.Knob=ArtPanel(pad,"Joystick center",66,66,92,92,new Color(UITheme.Mint.r,UITheme.Mint.g,UITheme.Mint.b,.78f),true,false);
            ArtText(pad,"移动",68,174,88,27,17,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            var look=ArtPanel(touch,"Camera zone",360,196,1200,330,new Color(1,1,1,.01f),false,false);look.GetComponent<Image>().raycastTarget=true;var lookPad=look.gameObject.AddComponent<TouchPad>();lookPad.CameraPad=true;lookPad.OnLook=d=>ApplyCameraLook(d,new Vector2(.12f,.1f));
            ArtButton(touch,"使用",1270,556,118,76,()=>Send("use_item"),UITheme.Yellow,UITheme.Ink,"attack");
            ArtButton(touch,"跳跃",1402,556,118,76,()=>Send("jump"),UITheme.Purple,UITheme.Ink,"jump");
            ArtButton(touch,"抓人",1138,648,118,76,()=>Send("grab"),UITheme.Mint,UITheme.Ink,"grab");
            ArtButton(touch,"抓物",1270,648,118,76,()=>Send("grab_item"),UITheme.Field,UITheme.Ink,"grab");
            ArtButton(touch,"吞噬/回收",1402,648,118,76,()=>Send("interact"),UITheme.Orange,UITheme.Ink,"status");
            ArtButton(touch,"借用文字",1268,562,118,76,()=>Send("borrow_word"),UITheme.Mint,UITheme.Ink);
            ArtButton(touch,"放下借物",1402,562,118,76,()=>Send("release_word"),UITheme.Field,UITheme.Ink);
            var escape=ArtButton(touch,"挣脱",1138,556,118,76,()=>{},UITheme.Orange,UITheme.Ink,"grab");
            var escapePress=escape.gameObject.AddComponent<PressButton>();
            escapePress.Down=()=>{touchEscapeHeld=escapeHeld=true;var me=net.State?.players.FirstOrDefault(p=>p.id==net.State.you);Send(me!=null&&(me.grabbed||me.sticky)?"escape":"cancel");};
            escapePress.Up=()=>{touchEscapeHeld=escapeHeld=false;Send("escape_end");};
            touch.gameObject.SetActive(false);
        }

        void RefreshTouchVisibility()
        {
            bool force=previewKind=="touch";bool visible=force||touchMode==1||(touchMode==0&&(Application.isMobilePlatform||platform.IsWeChat));touch.gameObject.SetActive(visible);
            if(keyboardHelpButton)keyboardHelpButton.gameObject.SetActive(!visible);
            if(visible&&keyboardHint)keyboardHint.gameObject.SetActive(false);
        }

        void ApplyAdaptiveLayout(bool force=false)
        {
            if(!force&&lastScreenWidth==Screen.width&&lastScreenHeight==Screen.height)return;
            lastScreenWidth=Screen.width;lastScreenHeight=Screen.height;
            bool next=Application.isMobilePlatform||platform.IsWeChat||Screen.height<=600||Screen.width/(float)Mathf.Max(1,Screen.height)<1.48f;
            if(!force&&next==compactLayout)return;compactLayout=next;
            if(startMenuStage)startMenuStage.SetCompact(compactLayout);
            if(draftLibrary){draftLibrary.localScale=Vector3.one;}
            if(compactLayout)
            {
                if(homeHero)Place(homeHero,22,24,800,830);Place(studioCard,672,28,900,836);Place(lobbyCard,28,28,700,834);Place(hotbar,348,788,904,92);
            }
            else
            {
                if(homeHero)Place(homeHero,42,32,825,820);Place(studioCard,672,28,900,836);Place(lobbyCard,44,42,676,816);Place(hotbar,380,795,840,84);
            }
        }

        void UpdateVisualUI(Snapshot state,PublicPlayer me)
        {
            ApplyAdaptiveLayout();UpdateStatusVisual();ApplyBusyVisuals();PositionStatus(state);
            if(state==null)return;
            UpdateLobbyCards(state);
            string phase=state.phase==MatchPhase.Reveal?"PERSONA REVEAL":state.phase==MatchPhase.FinalBurst?"FINAL BURST":state.phase==MatchPhase.Finished?"RESULT":"BATTLE";
            phaseText.text=state.phase==MatchPhase.Reveal&&state.roastPreparing?"准备开场…":state.practiceOpening>0?"练习准备 "+Mathf.CeilToInt(state.practiceOpening)+"s":phase;float clock=state.phase==MatchPhase.Reveal?Mathf.Max(0,5-state.time):Mathf.Max(0,state.finalAt-state.time);timerText.text=Mathf.FloorToInt(clock/60).ToString("00")+":"+Mathf.FloorToInt(clock%60).ToString("00");
            if(me!=null)
            {
                var mechanic=PartyMechanicRegistry.ForPlayer(me);mechanicHint.text=mechanic==null?"":(!string.IsNullOrEmpty(me.movementStatus)?me.movementStatus:mechanic.label)+(me.mechanicCooldown>0?"（冷却 "+me.mechanicCooldown.ToString("0.0")+"s）":"（F 使用）")+" · "+mechanic.hint;
                if(!string.IsNullOrEmpty(me.fieldStatus))mechanicHint.text=me.fieldStatus+" · "+mechanicHint.text;
                hudText.text=me.eliminated?"观战中 · Tab 切换玩家":GameLocalization.Protect(me.nickname);
                personaCountText.text="Persona × "+me.remaining+" · 吞噬 "+state.eaten.Count(e=>e.eaterId==me.id);stabilityText.text="韧性 "+Mathf.CeilToInt(me.stability);
                float stability=Mathf.Clamp01(me.stability/100f);stabilityFill.rectTransform.anchorMax=new Vector2(stability,1);stabilityFill.color=stability<.35f?UITheme.Red:stability<.7f?UITheme.Yellow:UITheme.Mint;
                bool interacting=!string.IsNullOrEmpty(me.interaction),dragging=!string.IsNullOrEmpty(me.grabbing);hudProgress.gameObject.SetActive(interacting||me.grabbed||me.sticky||dragging||me.portalProgress>0);
                if(me.portalProgress>0){progressText.text="传送准备  "+Mathf.RoundToInt(me.portalProgress*100)+"% · Q 取消";progressFill.rectTransform.anchorMax=new Vector2(me.portalProgress,1);}
                else if(me.grabbed||me.sticky){progressText.text=(me.sticky?"按住 Q 撕掉黏附爆风  ":"按住 Q / 挣脱按钮  ")+Mathf.RoundToInt(me.escapeProgress*100)+"%";progressFill.rectTransform.anchorMax=new Vector2(me.escapeProgress,1);}
                else if(interacting){progressText.text=(me.interaction=="eat"?"正在吞噬":"正在回收")+"  "+Mathf.RoundToInt(me.progress*100)+"%";progressFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(me.progress),1);}
                else if(dragging){progressText.text="拖拽中 · G / 抓取按钮松手";progressFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(me.grabTension),1);}
            }
            UpdateResultCards(state);
        }

        void UpdateLobbyCards(Snapshot state)
        {
            string signature=state.room+"|"+state.host+"|"+string.Join("|",state.players.Select(p=>p.id+":"+p.ready+":"+p.remaining+":"+p.connected));
            if(signature==lastLobbyVisual)return;lastLobbyVisual=signature;
            roomTitle.text="房间  "+state.room;roomCount.text=state.players.Length+" / 6 位朋友";
            var mine=state.players.FirstOrDefault(p=>p.id==state.you);if(readyStyle)readyStyle.SetState(mine!=null&&mine.ready?UIVisualState.Selected:UIVisualState.Normal);
            for(int i=0;i<6;i++)
            {
                bool occupied=i<state.players.Length;var card=playerCards[i];card.gameObject.SetActive(true);
                if(!occupied){playerAvatars[i].color=UITheme.Field;playerNames[i].text="等待朋友…";playerNames[i].color=UITheme.Muted;playerMeta[i].text="空位";playerReadyMarks[i].gameObject.SetActive(false);continue;}
                var p=state.players[i];playerAvatars[i].color=ToyFactory.ColorOf(p.color);playerNames[i].color=UITheme.Ink;playerNames[i].text=(p.id==state.host?"♛  ":"")+GameLocalization.Protect(p.nickname);
                playerMeta[i].text="Persona × "+p.remaining+(p.connected?"":"  ·  离线");playerMeta[i].color=p.connected?UITheme.Muted:UITheme.Red;
                playerReadyMarks[i].gameObject.SetActive(p.ready);playerReadyMarks[i].color=p.ready?UITheme.Green:UITheme.Muted;
                card.GetComponent<Image>().color=p.ready?Color.Lerp(UITheme.Mint,UITheme.Paper,.72f):UITheme.Field;
            }
        }

        void UpdateResultCards(Snapshot state)
        {
            if(state.phase!=MatchPhase.Finished)return;
            string signature=state.matchId+"|"+state.winner+"|"+string.Join("|",state.eaten.Select(e=>e.instanceId));if(signature==lastResultVisual)return;lastResultVisual=signature;
            var ids=state.winners!=null&&state.winners.Length>0?state.winners:new[]{state.winner};
            var names=state.players.Where(p=>ids.Contains(p.id)).Select(p=>GameLocalization.Protect(p.nickname));
            winnerText.text=string.Join("、",names)+(ids.Length>1?" · 并列获胜":" · 获胜")+"\n"+(state.endReason=="timeout"?"时间到 · 吞噬数优先，同分比较剩余":"最后一名存活者");winnerText.fontSize=22;
            foreach(Transform child in resultContent)Destroy(child.gameObject);
            if(state.rankings!=null)foreach(var rank in state.rankings)
            {
                var row=ArtPanel(resultContent,"Rank "+rank.rank,0,0,1040,60,rank.eliminated?UITheme.Field:UITheme.Mint,true,false);
                ArtText(row,rank.rank+"  "+GameLocalization.Protect(rank.nickname)+"   吞噬 "+rank.eaten+"   剩余 "+rank.remaining+(rank.eliminated?" · 已淘汰":""),18,6,1000,48,20,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);AddLayoutHeight(row,60);
            }
            if(state.eaten.Length==0)
            {
                var empty=ArtPanel(resultContent,"Empty receipt",0,0,1040,68,UITheme.Field,true,false);ArtText(empty,"这局没有吞噬记录。",20,8,1000,50,18,UITheme.Muted,FontStyle.Bold,TextAnchor.MiddleCenter);AddLayoutHeight(empty,68);return;
            }
            for(int i=0;i<state.eaten.Length;i++)
            {
                var record=state.eaten[i];var row=ArtPanel(resultContent,"Eaten record "+(i+1),0,0,1040,68,i%2==0?UITheme.Field:Color.Lerp(UITheme.Pink,UITheme.Paper,.76f),true,false);
                ArtText(row,(i+1).ToString("00"),16,12,52,43,18,UITheme.Muted,FontStyle.Bold,TextAnchor.MiddleCenter);
                ArtIcon(row,"attack",75,16,35,UITheme.Ink);ArtText(row,GameLocalization.Protect(record.eater)+"  吃掉了  "+GameLocalization.Protect(record.owner)+" 的「"+GameLocalization.PersonaName(record.persona,record.visual)+"」",126,8,885,52,18,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);AddLayoutHeight(row,68);
            }
        }

        void BuildInventoryCards(Snapshot state)
        {
            float scrollPosition=inventoryHasBeenBuilt&&inventoryScroll?inventoryScroll.verticalNormalizedPosition:1;
            foreach(var card in inventoryCards){card.gameObject.SetActive(false);Destroy(card.gameObject);}inventoryCards.Clear();
            foreach(var item in state.inventory)
            {
                string id=item.id,assetId=item.asset.assetId;
                bool equipped=item.state==ItemState.Equipped,available=item.state==ItemState.Inventory||equipped;
                var card=ArtPanel(inventoryContent,"Persona "+item.asset.name,0,0,320,215,equipped?Color.Lerp(UITheme.Yellow,UITheme.Paper,.52f):UITheme.Field,true,false);inventoryCards.Add(card);
                var thumbnail=PersonaThumbnail(item.asset.visualId);
                var well=ArtPanel(card,"Persona thumbnail",10,12,92,92,Color.Lerp(ToyFactory.ColorOf(item.asset.color),UITheme.Paper,.7f),true,false);
                var itemImage=ArtIcon(well,thumbnail?"":IconForTemplate(item.asset.templateId),6,6,80,thumbnail?Color.white:ToyFactory.ColorOf(item.asset.color));
                if(thumbnail)itemImage.sprite=thumbnail;
                ArtText(card,GameLocalization.PersonaName(item.asset),109,10,199,40,18,UITheme.Ink,FontStyle.Bold);
                ArtText(card,(PartyMechanicRegistry.ForAsset(item.asset)?.label??TemplateLabel(item.asset.templateId)),109,51,195,22,13,UITheme.Muted,FontStyle.Bold);
                var mechanic=PartyMechanicRegistry.ForAsset(item.asset);
                if(mechanic!=null)ArtText(card,mechanic.hint,10,99,298,46,14,UITheme.Muted);
                if(equipped)ArtText(card,item.ownerId!=state.you?"借用 · 原主生命":"● 装备中",109,75,90,22,13,UITheme.Orange,FontStyle.Bold);
                var equip=ArtButton(card,equipped?"已装备":"装备",10,152,141,55,()=>Send("equip",id),equipped?UITheme.Yellow:UITheme.Mint,UITheme.Ink);
                equip.interactable=available&&!equipped;
                var quick=ArtButton(card,"置顶",167,152,141,55,()=>Favorite(assetId),UITheme.Purple,UITheme.Ink);
                quick.interactable=available;
            }
            int rows=Mathf.CeilToInt(state.inventory.Length/3f);inventoryContent.sizeDelta=new Vector2(1000,Mathf.Max(516,12+rows*215+Mathf.Max(0,rows-1)*12));
            Canvas.ForceUpdateCanvases();inventoryScroll.verticalNormalizedPosition=Mathf.Clamp01(scrollPosition);inventoryHasBeenBuilt=true;
        }

        Sprite PersonaThumbnail(string visualId)
        {
            if(string.IsNullOrWhiteSpace(visualId))return null;
            if(personaThumbnails.TryGetValue(visualId,out var sprite))return sprite;
            sprite=Resources.Load<Sprite>("PersonaThumbs/"+visualId);personaThumbnails[visualId]=sprite;return sprite;
        }

        void ApplyBusyVisuals()
        {
            if(generateStyle)generateStyle.SetState(busy?UIVisualState.Loading:UIVisualState.Normal);
            if(recordStyle)recordStyle.SetState(recording?UIVisualState.Recording:UIVisualState.Normal);
            if(recordLabel)recordLabel.text=recording?"停止录音":asrCapabilityKnown&&!asrConfigured?"语音未配置":"录音介绍";
        }

        void UpdateStatusVisual()
        {
            string value=net?.Status??"";if(net?.State!=null&&value.StartsWith("能力已选择")){net.Status="";value="";}if(value==lastStatus)return;lastStatus=value;status.text=value;statusPill.gameObject.SetActive(!string.IsNullOrEmpty(value));if(string.IsNullOrEmpty(value))return;
            Color color=UITheme.Paper;
            if(value.Contains("失败")||value.Contains("错误")||value.Contains("无效")||value.Contains("没有")||value.Contains("尚未"))color=UITheme.Red;
            else if(value.Contains("正在")||value.Contains("连接"))color=UITheme.Purple;
            else if(value.Contains("完成")||value.Contains("保存")||value.Contains("已")||value.Contains("成功"))color=UITheme.Green;
            else if(value.Contains("样例"))color=UITheme.Yellow;
            statusSurface.color=Color.Lerp(color,UITheme.Paper,.2f);statusPill.GetComponent<UIPulse>()?.Play();
        }

        void PositionStatus(Snapshot state)
        {
            statusPill.SetAsLastSibling();
            if(inventoryOpen){Place(statusPill,475,40,650,46);return;}
            if(state==null){Place(statusPill,46,838,650,46);return;}
            if(state.phase==MatchPhase.Lobby){Place(statusPill,780,818,650,46);return;}
            if(state.phase==MatchPhase.Finished){Place(statusPill,46,838,650,46);return;}
            if(touch&&touch.gameObject.activeSelf)Place(statusPill,475,205,650,46);else Place(statusPill,34,638,382,46);
        }

        void RefreshColorSelection(){for(int i=0;i<colorStyles.Count;i++)colorStyles[i].SetState(i==ColorIndex(selectedColor)?UIVisualState.Selected:UIVisualState.Normal);}
        int ColorIndex(string color){string[] colors={"#79CFC5","#F3AFC2","#FFD773","#9FAFF2"};for(int i=0;i<colors.Length;i++)if(string.Equals(color,colors[i],StringComparison.OrdinalIgnoreCase))return i;return 0;}
        static string TemplateLabel(string id)
        {switch(id){case "Projectile":return "投掷型";case "Disc":return "飞盘型";case "Melee":return "近战型";case "HeavyWeapon":return "重型武器";case "Utility":return "功能型";case "Deployable":return "放置型";case "Summon":return "召唤型";default:return "特殊能力";}}
        static string IconForTemplate(string id){if(id=="Projectile"||id=="Disc")return "attack";if(id=="Utility")return "camera";if(id=="Summon")return "grab";return "status";}

        void ConfigureUIPreview()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--ui-preview");if(index<0||index+1>=args.Length)return;previewKind=args[index+1].ToLowerInvariant();
            int capture=Array.IndexOf(args,"--ui-screenshot");if(capture>=0&&capture+1<args.Length)StartCoroutine(CaptureUIPreview(args[capture+1]));
            if(previewKind=="trait-lab"){OpenTraitLab();return;}
            if(previewKind=="persona-progress")
            {
                ShowPage(MenuPage.Presets);DisplayGenerationTerms(new[]{
                    new GenerationTerm{term="湖北文理学院",kind="university",status="pending",progress=.45f,reason="正在下载校徽候选图"},
                    new GenerationTerm{term="University of Otago",kind="university",status="pending",progress=0,reason="等待国内大学优先生成"},
                    new GenerationTerm{term="北京大学",kind="university",status="ready",progress=1,reason="校徽飞盘已生成"},
                    new GenerationTerm{term="示例学校",kind="university",status="failed",progress=.65f,reason="校徽未能确认，可重试"}},"searching");
                net.Status="阶段进度示例 · 真实生成进度来自服务端";return;
            }
            if(previewKind=="wardrobe"||previewKind=="wardrobe-ds"||previewKind=="wardrobe-claude")
            {profile=new Profile{playerId="preview",nickname="预览玩家",appearance=AppearanceData.Demo(0),dsGirlUnlocked=true,claudeGirlUnlocked=true,characterSkin=previewKind=="wardrobe-ds"?"ds-girl":previewKind=="wardrobe-claude"?"claude-girl":"normal"};appearanceDirty=false;LoadAppearance();OpenWardrobe();net.Status="衣橱预览 · 不连接服务器";return;}
            if(previewKind=="home"||previewKind=="home-expanded"){ShowPage(MenuPage.Main);startMenuStage.SetExpanded(previewKind=="home-expanded",true);RefreshTouchVisibility();return;}
            if(previewKind=="persona-results")
            {
                int saved=Array.IndexOf(args,"--persona-result-profile");
                if(saved>=0&&saved+1<args.Length)
                {
                    var verified=JsonUtility.FromJson<Profile>(File.ReadAllText(args[saved+1]));
                    ShowPage(MenuPage.Presets);DisplayGenerationTerms(verified.generationTerms);
                    introduction.text=string.Join("；",verified.generationTerms.Select(t=>t.source).Where(s=>!string.IsNullOrEmpty(s)).Distinct());
                    net.Status="已保存的真实生成结果";return;
                }
                ShowPage(MenuPage.Presets);DisplayGenerationTerms(new[]{
                    new GenerationTerm{term="湖北文理学院",status="ready",reason="校徽飞盘已生成"},
                    new GenerationTerm{term="篮球",status="ready",reason="篮球"},
                    new GenerationTerm{term="星云泡泡不存在大学",status="failed",reason="未找到可确认的校徽"},
                    new GenerationTerm{term="小提琴",status="failed",reason="暂无对应的已上线道具"}});
                net.Status="生成结果界面预览";return;
            }
            if(previewKind=="presets"||previewKind=="solo"||previewKind=="multiplayer"){ShowPage(previewKind=="presets"?MenuPage.Presets:previewKind=="solo"?MenuPage.Solo:MenuPage.Multiplayer);return;}
            if(previewKind=="settings"){OpenSettings();net.Status="";RefreshTouchVisibility();return;}
            bool lobbyPreview=previewKind=="lobby"||previewKind=="lobby-ds"||previewKind=="qr";
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);var players=Enumerable.Range(0,lobbyPreview?6:3).Select(i=>new PublicPlayer{id="preview-"+i,nickname=new[]{"小宇宙","海盐汽水","凌晨三点","社团王牌","猫派代表","最后一页"}[i],color=catalog.items[i].color,remaining=Mathf.Max(1,8-i),position=Match.Spawn(i),yaw=i*55,stability=100-i*17,ready=i!=4,connected=i!=5,equippedName=catalog.items[i].label,equippedVisual=catalog.items[i].visualId,equippedColor=catalog.items[i].color,characterSkin=i==0&&previewKind.EndsWith("-ds")?"ds-girl":"normal",interaction=i==0&&(previewKind=="hud"||previewKind=="hud-ds")?"eat":"",progress=.62f}).ToArray();
            var inventory=catalog.items.Select((x,i)=>new PublicPersonaInstance{id="preview-item-"+i,ownerId="preview-0",asset=new PublicPersonaAsset{assetId="asset-"+i,name=x.label,templateId=x.templateId,visualId=x.visualId,color=x.color},state=i==0?ItemState.Equipped:ItemState.Inventory}).ToArray();
            var phase=lobbyPreview?MatchPhase.Lobby:previewKind=="results"||previewKind=="results-late-event"?MatchPhase.Finished:MatchPhase.Battle;
            net.State=new Snapshot{room="PARTY",matchId="preview",you="preview-0",host="preview-0",winner="preview-1",phase=phase,time=196,finalAt=450,players=players,inventory=inventory,ground=Array.Empty<PublicPersonaInstance>(),revealed=Array.Empty<PublicPersonaInstance>(),effects=Array.Empty<EffectState>(),props=Array.Empty<PublicProp>(),events=new[]{new MatchEvent{id=1,text="海盐汽水吃掉了小宇宙的「篮球」！"}},eaten=new[]{new EatRecord{instanceId="eat-1",eater="海盐汽水",owner="小宇宙",persona="篮球"},new EatRecord{instanceId="eat-2",eater="凌晨三点",owner="猫派代表",persona="相机"}}};
            net.Status="UI 预览 · 不连接服务器";if(previewKind=="bag"){inventoryOpen=true;bag.gameObject.SetActive(true);}if(previewKind=="qr")OpenQrOverlay(CreatePreviewQr(),"PARTY",true);if(previewKind=="results-late-event")StartCoroutine(PreviewLateResultEvent());RefreshTouchVisibility();
        }

        IEnumerator CaptureUIPreview(string path)
        {
            yield return new WaitForSecondsRealtime(.8f);
            while(settingsAuditRunning)yield return null;
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.7f);
            if(Environment.GetCommandLineArgs().Contains("--ui-preview-exit"))Application.Quit(Screen.fullScreen?1:0);
        }

        IEnumerator PreviewLateResultEvent()
        {
            yield return new WaitForSecondsRealtime(1);
            if(net.State?.matchId!="preview"||net.State.phase!=MatchPhase.Finished)yield break;
            net.State.events=new[]{new MatchEvent{id=1,text="海盐汽水吃掉了小宇宙的「篮球」！"},new MatchEvent{id=2,text="模拟晚到调侃：篮球刚上场，就进了别人的收藏夹。"}};
        }

        void OpenQrOverlay(Texture texture,string roomCode,bool destroyTexture)
        {
            var overlay=ArtPanel(artboard,"Room QR modal",0,0,1600,900,new Color(.05f,.07f,.12f,.52f),false,false);Stretch(overlay);overlay.GetComponent<Image>().raycastTarget=true;
            var card=ArtPanel(overlay,"Room QR sticker",500,96,600,708,UITheme.Paper,true,true);card.gameObject.AddComponent<UIReveal>();
            Tape(card,"SCAN TO JOIN",30,24,176,38,UITheme.Pink);ArtText(card,"扫码加入 · "+roomCode,30,76,540,49,29,UITheme.Ink,FontStyle.Bold);
            var qrPaper=ArtPanel(card,"QR paper",54,145,492,492,Color.white,true,false);var qrRect=ArtRect(qrPaper,"QR image",new Vector2(14,-14),new Vector2(464,464));var image=qrRect.gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            ArtButton(card,"关闭二维码",54,650,188,46,()=>{if(destroyTexture)Destroy(texture);Destroy(overlay.gameObject);},UITheme.Ink,UITheme.Paper,"exit");
        }

        Texture2D CreatePreviewQr()
        {
            const int size=29;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true){name="UI preview QR",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                bool finder=(x<7&&y<7)||(x>21&&y<7)||(x<7&&y>21);bool finderInk=finder&&(x==0||y==0||x==6||y==6||(x>=2&&x<=4&&y>=2&&y<=4));
                bool pattern=((x*7+y*11+x*y)%5)<2;texture.SetPixel(x,y,finderInk||(!finder&&pattern)?UITheme.Ink:Color.white);
            }
            texture.Apply(false,true);return texture;
        }

        RectTransform ArtRect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        RectTransform ArtPanel(Transform parent,string name,float x,float y,float width,float height,Color color,bool outline=true,bool shadow=false)
        {
            var rect=ArtRect(parent,name,new Vector2(x,-y),new Vector2(width,height));var image=rect.gameObject.AddComponent<Image>();image.raycastTarget=color.a>.05f;
            if(color.a<=.001f){image.color=Color.clear;image.raycastTarget=false;}else UITheme.Surface(image,color,outline,shadow);return rect;
        }
        Text ArtText(Transform parent,string value,float x,float y,float width,float height,int size,Color color,FontStyle style=FontStyle.Normal,TextAnchor alignment=TextAnchor.UpperLeft)
        {
            var rect=ArtRect(parent,"Text · "+(value.Length>16?value.Substring(0,16):value),new Vector2(x,-y),new Vector2(width,height));var label=rect.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.color=color;label.text=value;label.fontStyle=style;label.alignment=alignment;label.supportRichText=false;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.resizeTextForBestFit=true;label.resizeTextMinSize=Mathf.Max(11,size-7);label.resizeTextMaxSize=size;rect.gameObject.AddComponent<LocalizedLabel>().Configure(label);return label;
        }
        Button ArtButton(Transform parent,string label,float x,float y,float width,float height,Action action,Color background,Color foreground,string icon="")
        {
            var rect=ArtPanel(parent,"Button · "+label,x,y,width,height,background,true,false);var button=rect.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>{if(UIButtonAudit.Enabled)Debug.Log("BUTTON_AUDIT_CLICK "+label);action();});
            float textX=icon==""?9:43;Image iconImage=null;if(icon!="")iconImage=ArtIcon(rect,icon,13,(height-26)/2,26,foreground);
            var text=ArtText(rect,label,textX,3,width-textX-8,height-6,Mathf.RoundToInt(Mathf.Clamp(height*.34f,15,21)),foreground,FontStyle.Bold,TextAnchor.MiddleCenter);
            var style=rect.gameObject.AddComponent<StickerButton>();style.Configure(button,rect.GetComponent<Image>(),text,background);style.Icon=iconImage;return button;
        }
        InputField ArtField(Transform parent,float x,float y,float width,float height,string placeholder,bool multiline)
        {
            var rect=ArtPanel(parent,"Input · "+placeholder,x,y,width,height,UITheme.Field,true,false);var input=rect.gameObject.AddComponent<InputField>();input.targetGraphic=rect.GetComponent<Image>();
            var text=ArtText(rect,"",14,5,width-28,height-10,18,UITheme.Ink,FontStyle.Normal,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);var hint=ArtText(rect,placeholder,14,5,width-28,height-10,18,UITheme.Muted,FontStyle.Normal,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);
            text.GetComponent<LocalizedLabel>().enabled=false;input.textComponent=text;input.placeholder=hint;input.characterLimit=multiline?8000:256;input.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;
            input.contentType=InputField.ContentType.Standard;input.inputType=InputField.InputType.Standard;input.characterValidation=InputField.CharacterValidation.None;input.keyboardType=TouchScreenKeyboardType.Default;
            rect.gameObject.AddComponent<IMEInputBridge>().Configure(input);return input;
        }
        StickerButton ColorChip(Transform parent,string color,float x,float y,float size,Action action)
        {
            var rect=ArtRect(parent,"Color chip "+color,new Vector2(x,-y),new Vector2(size,size));var image=rect.gameObject.AddComponent<Image>();image.sprite=UITheme.Circle;image.color=ToyFactory.ColorOf(color);var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=UITheme.Ink;outline.effectDistance=new Vector2(3,-3);outline.useGraphicAlpha=true;
            var button=rect.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>action());var style=rect.gameObject.AddComponent<StickerButton>();style.Configure(button,image,null,ToyFactory.ColorOf(color));return style;
        }
        Image ArtIcon(Transform parent,string icon,float x,float y,float size,Color color)
        {var rect=ArtRect(parent,"Icon · "+icon,new Vector2(x,-y),new Vector2(size,size));var image=rect.gameObject.AddComponent<Image>();image.sprite=UITheme.Icon(icon);image.color=color;image.raycastTarget=false;image.preserveAspect=true;return image;}
        RectTransform Tape(Transform parent,string value,float x,float y,float width,float height,Color color)
        {var tape=ArtPanel(parent,"Tape · "+value,x,y,width,height,color,false,false);ArtText(tape,value,8,2,width-16,height-4,14,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);return tape;}
        RectTransform StepBadge(Transform parent,string value,float x,float y,Color color)
        {var badge=ArtPanel(parent,"Step "+value,x,y,30,30,color,true,false);badge.GetComponent<Image>().sprite=UITheme.Circle;badge.GetComponent<Image>().type=Image.Type.Simple;ArtText(badge,value,0,0,30,30,15,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);return badge;}
        RectTransform ArtFrame(Transform parent,string name,float x,float y,float width,float height)
        {
            var rect=ArtRect(parent,name,new Vector2(x,-y),new Vector2(width,height));
            ArtPanel(rect,"Top border",0,0,width,6,UITheme.Ink,false,false);ArtPanel(rect,"Bottom border",0,height-6,width,6,UITheme.Ink,false,false);
            ArtPanel(rect,"Left border",0,0,6,height,UITheme.Ink,false,false);ArtPanel(rect,"Right border",width-6,0,6,height,UITheme.Ink,false,false);return rect;
        }
        void DecorativeDot(Transform parent,float x,float y,float size,Color color,float alpha)
        {var dot=ArtPanel(parent,"Confetti",x,y,size,size,new Color(color.r,color.g,color.b,alpha),false,false);dot.GetComponent<Image>().sprite=UITheme.Circle;dot.GetComponent<Image>().type=Image.Type.Simple;dot.GetComponent<Image>().raycastTarget=false;}
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static void Place(RectTransform rect,float x,float y,float width,float height){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
        static void AddLayoutHeight(RectTransform rect,float height){var layout=rect.gameObject.AddComponent<LayoutElement>();layout.minHeight=height;layout.preferredHeight=height;}
    }
}
