using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AboutMe
{
    public partial class GameUI
    {
        RectTransform safeRoot,artboard,studioCard,homeHero,lobbyCard,lobbyGrid,hudCard,hudToast,hudProgress,hotbar,resultContent,keyboardHint;
        ScrollRect inventoryScroll;
        Button keyboardHelpButton;
        readonly List<RectTransform> inventoryCards=new List<RectTransform>();
        readonly List<Image> slotThumbnails=new List<Image>();
        readonly Dictionary<string,Sprite> personaThumbnails=new Dictionary<string,Sprite>();
        RectTransform[] playerCards=new RectTransform[6];
        Text[] playerNames=new Text[6],playerMeta=new Text[6];
        Image[] playerAvatars=new Image[6],playerReadyMarks=new Image[6];
        Text roomTitle,roomCount,phaseText,timerText,stabilityText,personaCountText,progressText,winnerText,outfitText,aiServiceText,introCounter;
        Image stabilityFill,progressFill,statusSurface,aiServiceSurface;
        RectTransform statusPill;
        StickerButton generateStyle,recordStyle,imageUploadStyle,readyStyle,startStyle;
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
            safeRoot=ArtRect(canvas,"Safe area",Vector2.zero,Vector2.zero);Stretch(safeRoot);
            artboard=ArtRect(safeRoot,"1600 × 900 artboard",Vector2.zero,new Vector2(1600,900));artboard.anchorMin=artboard.anchorMax=artboard.pivot=new Vector2(.5f,.5f);
            var safe=canvasObject.AddComponent<UISafeArtboard>();safe.SafeRoot=safeRoot;safe.Artboard=artboard;

            BuildHome();BuildSettings();BuildLobby();BuildHud();BuildBag();BuildResults();BuildArtTouch();BuildStatus();
            home.gameObject.SetActive(true);lobby.gameObject.SetActive(false);hud.gameObject.SetActive(false);results.gameObject.SetActive(false);settings.gameObject.SetActive(false);bag.gameObject.SetActive(false);
            ApplyAdaptiveLayout(true);
        }

        void BuildHome()
        {
            home=ArtPanel(artboard,"Introduction",0,0,1600,900,Color.clear,false,false);
            // The old split title/window page is retired. This canvas is now the
            // introduction editor only; the layered four-bubble stage owns home.

            studioCard=ArtPanel(home,"Persona studio",900,34,650,820,UITheme.Paper,true,true);studioCard.gameObject.AddComponent<UIReveal>();
            Tape(studioCard,"PERSONA WORKSHOP",30,24,216,38,UITheme.Mint);
            ArtText(studioCard,"先介绍一下，你是谁？",30,72,575,45,31,UITheme.Ink,FontStyle.Bold);
            StepBadge(studioCard,"1",30,126,UITheme.Yellow);ArtText(studioCard,"写下关于你的线索",70,127,260,34,18,UITheme.Ink,FontStyle.Bold);
            ArtText(studioCard,"昵称",30,168,120,25,15,UITheme.Muted,FontStyle.Bold);
            ArtText(studioCard,"房间",314,168,120,25,15,UITheme.Muted,FontStyle.Bold);
            nickname=ArtField(studioCard,30,195,268,50,"你的名字",false);nickname.text=PlayerPrefs.GetString("nickname","玩家");
            room=ArtField(studioCard,314,195,306,50,"房间码 · 留空创建",false);
            ArtText(studioCard,"自我介绍",30,257,200,25,15,UITheme.Muted,FontStyle.Bold);introCounter=ArtText(studioCard,"0 / 1200",450,257,170,25,14,UITheme.Muted,FontStyle.Bold,TextAnchor.UpperRight);
            introduction=ArtField(studioCard,30,284,590,128,"我来自哪里、喜欢什么、有什么特别经历……",true);introduction.text=PlayerPrefs.GetString("introduction","");
            introduction.characterLimit=1200;introduction.onValueChanged.AddListener(value=>introCounter.text=value.Length+" / 1200");introCounter.text=introduction.text.Length+" / 1200";
            StepBadge(studioCard,"2",30,428,UITheme.Pink);ArtText(studioCard,"设计人物外观（不影响 Persona）",70,429,350,34,18,UITheme.Ink,FontStyle.Bold);
            var record=ArtButton(studioCard,"录音介绍",30,470,174,48,ToggleRecording,UITheme.Field,UITheme.Ink,"mic");recordLabel=record.GetComponentInChildren<Text>();recordStyle=record.GetComponent<StickerButton>();
            string[] colors={"#79CFC5","#F3AFC2","#FFD773","#9FAFF2"};
            for(int i=0;i<colors.Length;i++)
            {
                string choice=colors[i];var chip=ColorChip(studioCard,choice,222+i*53,473,42,()=>{SetDraftColor(choice);});colorStyles.Add(chip);
            }
            var outfitButton=ArtButton(studioCard,"我的衣橱",451,470,169,48,OpenWardrobe,UITheme.Field,UITheme.Ink,"status");outfitText=outfitButton.GetComponentInChildren<Text>();
            BuildExpressionChoices(studioCard);
            imageUpload=ArtButton(studioCard,"图片识别",30,526,230,42,PickIntroductionImage,UITheme.Purple,UITheme.Ink,"status");imageUploadStyle=imageUpload.GetComponent<StickerButton>();
            generate=ArtButton(studioCard,"AI 生成 Persona",30,578,230,48,()=>StartCoroutine(Generate()),UITheme.Ink,UITheme.Paper,"status");generateStyle=generate.GetComponent<StickerButton>();
            ArtButton(studioCard,"免费解锁 DS 娘",278,578,342,48,()=>StartCoroutine(UnlockDSGirl()),UITheme.Mint,UITheme.Ink,"status");
            var profileCard=ArtPanel(studioCard,"Persona result",30,638,590,66,UITheme.Field,true,false);
            profileText=ArtText(profileCard,"AI 会把你的介绍变成可战斗的道具。\n结果由真实服务生成，不固定数量。",18,5,554,56,16,UITheme.Ink,FontStyle.Normal,TextAnchor.MiddleLeft);
            profileCard.gameObject.AddComponent<RectMask2D>();var profileScroll=profileCard.gameObject.AddComponent<ScrollRect>();profileScroll.viewport=profileCard;profileScroll.content=profileText.rectTransform;profileScroll.horizontal=false;profileScroll.movementType=ScrollRect.MovementType.Clamped;profileScroll.scrollSensitivity=24;
            join=ArtButton(studioCard,"进入房间",30,718,230,56,()=>StartCoroutine(EnterWithAppearance()),UITheme.Mint,UITheme.Ink,"ready");
            ArtButton(studioCard,"本地样例（无需 AI）",30,785,260,37,LoadPracticePersona,UITheme.Yellow,UITheme.Ink,"status");
            var service=ArtPanel(studioCard,"AI service status",306,785,314,37,UITheme.Field,true,false);aiServiceSurface=service.GetComponent<Image>();ArtIcon(service,"status",10,7,23,UITheme.Ink);aiServiceText=ArtText(service,"正在检查 AI 服务…",39,2,265,32,14,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);
            RefreshColorSelection();
        }

        void BuildSettings()
        {
            settings=ArtPanel(artboard,"Settings modal",0,0,1600,900,new Color(.05f,.07f,.12f,.52f),false,false);Stretch(settings);settings.GetComponent<Image>().raycastTarget=true;
            var card=ArtPanel(settings,"Connection settings card",438,154,724,592,UITheme.Paper,true,true);card.gameObject.AddComponent<UIReveal>();
            Tape(card,"CONNECTION KIT",32,26,204,38,UITheme.Purple);
            ArtText(card,"连接与服务",32,74,560,49,33,UITheme.Ink,FontStyle.Bold);
            ArtButton(card,"关闭",630,28,60,42,()=>{optionsOpen=false;settings.gameObject.SetActive(false);},UITheme.Field,UITheme.Ink,"exit");
            ArtText(card,"游戏服务器 WebSocket 地址",32,142,630,26,16,UITheme.Muted,FontStyle.Bold);
            endpoint=ArtField(card,32,174,660,54,"wss://game.example.com",false);endpoint.text=PlayerPrefs.GetString("endpoint",net.Endpoint);
            ArtText(card,"AI / 角色服务地址",32,250,630,26,16,UITheme.Muted,FontStyle.Bold);
            api=ArtField(card,32,282,660,54,"https://api.example.com",false);api.text=PlayerPrefs.GetString("api",net.ApiUrl);
            var note=ArtPanel(card,"Secret note",32,360,660,58,UITheme.Yellow,true,false);
            ArtIcon(note,"status",18,10,38,UITheme.Ink);ArtText(note,"密钥仅保存在服务端",68,3,570,48,17,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);
            ArtButton(card,"保存连接设置",32,468,316,58,SaveConnectionSettings,UITheme.Mint,UITheme.Ink,"ready");
            Place(card,438,54,724,790);
            foreach(var button in card.GetComponentsInChildren<Button>())if(button.name=="Button · 保存连接设置")Place((RectTransform)button.transform,32,698,224,58);
            var sensitivity=ArtButton(card,"",32,460,256,54,()=>{cameraSensitivity=cameraSensitivity>=2?.5f:cameraSensitivity+.5f;PlayerPrefs.SetFloat("cameraSensitivity",cameraSensitivity);RefreshSettingLabels();},UITheme.Field,UITheme.Ink);sensitivityLabel=sensitivity.GetComponentInChildren<Text>();
            var touchButton=ArtButton(card,"",32,534,256,54,()=>{touchMode=(touchMode+1)%3;PlayerPrefs.SetInt("touchMode",touchMode);RefreshTouchVisibility();RefreshSettingLabels();},UITheme.Field,UITheme.Ink);touchSettingLabel=touchButton.GetComponentInChildren<Text>();
            var music=ArtButton(card,"",370,534,322,54,()=>{float volume=BattleMusic.SavedVolume;PlayerPrefs.SetFloat("battleMusicVolume",volume<.05f?.16f:volume<.22f?.30f:volume<.4f?.50f:0);RefreshSettingLabels();},UITheme.Field,UITheme.Ink);musicSettingLabel=music.GetComponentInChildren<Text>();
            var motion=ArtButton(card,"",32,608,322,54,()=>{reduceMotion=!reduceMotion;PlayerPrefs.SetInt("reduceMotion",reduceMotion?1:0);RefreshSettingLabels();},UITheme.Field,UITheme.Ink);motionSettingLabel=motion.GetComponentInChildren<Text>();
            var shake=ArtButton(card,"",370,608,322,54,()=>{float v=PlayerPrefs.GetFloat("hitShake",.3f);PlayerPrefs.SetFloat("hitShake",v<.1f?.3f:v<.6f?1:0);RefreshSettingLabels();},UITheme.Field,UITheme.Ink);shakeSettingLabel=shake.GetComponentInChildren<Text>();

        }

        Text sensitivityLabel,touchSettingLabel,musicSettingLabel,motionSettingLabel,shakeSettingLabel;
        void RefreshSettingLabels()
        {sensitivityLabel.text="镜头灵敏度 · "+cameraSensitivity.ToString("0.0")+" 倍";touchSettingLabel.text="触控按钮 · "+new[]{"自动","显示","隐藏"}[touchMode];if(musicSettingLabel){float volume=BattleMusic.SavedVolume;musicSettingLabel.text="战斗音乐 · "+(volume<.05f?"关闭":volume<.22f?"轻柔":volume<.4f?"标准":"响亮");}motionSettingLabel.text="减少动态 · "+(reduceMotion?"开启":"关闭");if(shakeSettingLabel)shakeSettingLabel.text="打击震动 · "+(PlayerPrefs.GetFloat("hitShake",.3f)<.1f?"关闭":PlayerPrefs.GetFloat("hitShake",.3f)<.6f?"轻微":"明显");if(startMenuStage)startMenuStage.SetMotionEnabled(!reduceMotion);}
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
            keyboardHint=ArtPanel(hud,"Keyboard help",34,202,382,142,new Color(1,1,1,.94f),true,true);
            ArtText(keyboardHint,"WASD 移动  ·  鼠标镜头  ·  空格跳跃\n锁鼠失败时按住右键拖镜头\nF 使用  ·  G/H 抓取  ·  E 吃/收\n1–5 切换  ·  B 背包  ·  Q 挣脱",16,10,350,125,16,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);
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
            ArtButton(hud,"退出",1440,28,126,46,Leave,UITheme.Paper,UITheme.Ink,"exit");
            reconnectButton=ArtButton(hud,"重连",1440,86,126,42,()=>{if(!Mirror.NetworkClient.active)net.Connect();},UITheme.Field,UITheme.Ink,"status");
        }

        void BuildBag()
        {
            bag=ArtPanel(artboard,"Inventory modal",0,0,1600,900,new Color(.05f,.07f,.12f,.48f),false,false);Stretch(bag);bag.GetComponent<Image>().raycastTarget=true;
            var card=ArtPanel(bag,"Persona backpack",270,108,1060,684,UITheme.Paper,true,true);card.gameObject.AddComponent<UIReveal>();
            Tape(card,"YOUR PERSONA BAG",30,25,232,38,UITheme.Purple);
            bagText=ArtText(card,"你的 Persona",30,75,770,42,29,UITheme.Ink,FontStyle.Bold);
            ArtButton(card,"返回",876,28,152,50,ToggleBag,UITheme.Mint,UITheme.Ink,"exit");
            var viewport=ArtPanel(card,"Inventory viewport",24,136,1012,516,Color.clear,false,false);viewport.gameObject.AddComponent<RectMask2D>();
            inventoryContent=ArtRect(viewport,"Inventory cards",Vector2.zero,new Vector2(1000,516));inventoryContent.anchorMin=inventoryContent.anchorMax=inventoryContent.pivot=new Vector2(0,1);
            var grid=inventoryContent.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(320,168);grid.spacing=new Vector2(12,12);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.padding=new RectOffset(6,6,6,6);
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
            ArtButton(card,"保存结算截图",34,672,350,58,()=>platform.Screenshot(),UITheme.Yellow,UITheme.Ink,"camera");
            ArtButton(card,"返回主菜单",776,672,338,58,Leave,UITheme.Ink,UITheme.Paper,"ready");
            replay=ArtButton(card,"再来一局",412,672,338,58,ReplaySolo,UITheme.Mint,UITheme.Ink,"ready");
        }

        void BuildStatus()
        {
            statusPill=ArtPanel(artboard,"Status toast",46,838,650,46,UITheme.Paper,true,true);statusSurface=statusPill.GetComponent<Image>();
            statusPill.gameObject.AddComponent<UIPulse>();ArtIcon(statusPill,"status",14,9,28,UITheme.Ink);status=ArtText(statusPill,"",52,4,582,38,15,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);statusPill.gameObject.SetActive(false);
        }

        void BuildArtTouch()
        {
            touch=ArtPanel(hud,"Touch controls",0,0,1600,900,Color.clear,false,false);
            var pad=ArtPanel(touch,"Move joystick",54,592,224,224,new Color(1,1,1,.52f),true,true);movePad=pad.gameObject.AddComponent<TouchPad>();
            movePad.Knob=ArtPanel(pad,"Joystick center",66,66,92,92,new Color(UITheme.Mint.r,UITheme.Mint.g,UITheme.Mint.b,.78f),true,false);
            ArtText(pad,"移动",68,174,88,27,17,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            var look=ArtPanel(touch,"Camera zone",360,196,1200,330,new Color(1,1,1,.01f),false,false);look.GetComponent<Image>().raycastTarget=true;var lookPad=look.gameObject.AddComponent<TouchPad>();lookPad.CameraPad=true;lookPad.OnLook=d=>{world.CameraYaw+=d.x*.12f*cameraSensitivity;world.CameraPitch=Mathf.Clamp(world.CameraPitch-d.y*.1f*cameraSensitivity,10,65);};
            ArtButton(touch,"使用",1270,556,118,76,()=>Send("use_item"),UITheme.Yellow,UITheme.Ink,"attack");
            ArtButton(touch,"跳跃",1402,556,118,76,()=>Send("jump"),UITheme.Purple,UITheme.Ink,"jump");
            ArtButton(touch,"抓人",1138,648,118,76,()=>Send("grab"),UITheme.Mint,UITheme.Ink,"grab");
            ArtButton(touch,"抓物",1270,648,118,76,()=>Send("grab_item"),UITheme.Field,UITheme.Ink,"grab");
            ArtButton(touch,"吃/收",1402,648,118,76,()=>Send("interact"),UITheme.Orange,UITheme.Ink,"status");
            var escape=ArtButton(touch,"挣脱",1138,556,118,76,()=>{},UITheme.Orange,UITheme.Ink,"grab");
            var escapePress=escape.gameObject.AddComponent<PressButton>();
            escapePress.Down=()=>{touchEscapeHeld=escapeHeld=true;var me=net.State?.players.FirstOrDefault(p=>p.id==net.State.you);Send(me!=null&&me.grabbed?"escape":"cancel");};
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
            if(draftLibrary){draftLibrary.localScale=Vector3.one*(compactLayout?.94f:1);}
            if(compactLayout)
            {
                if(homeHero)Place(homeHero,22,24,800,830);Place(studioCard,830,24,738,832);Place(lobbyCard,28,28,700,834);Place(hotbar,348,788,904,92);
            }
            else
            {
                if(homeHero)Place(homeHero,42,32,825,820);Place(studioCard,900,34,650,820);Place(lobbyCard,44,42,676,816);Place(hotbar,380,795,840,84);
            }
        }

        void UpdateVisualUI(Snapshot state,PublicPlayer me)
        {
            ApplyAdaptiveLayout();UpdateStatusVisual();ApplyBusyVisuals();PositionStatus(state);
            if(state==null)return;
            UpdateLobbyCards(state);
            string phase=state.phase==MatchPhase.Reveal?"PERSONA REVEAL":state.phase==MatchPhase.FinalBurst?"FINAL BURST":state.phase==MatchPhase.Finished?"RESULT":"BATTLE";
            phaseText.text=phase;float clock=state.phase==MatchPhase.Reveal?Mathf.Max(0,5-state.time):Mathf.Max(0,state.finalAt-state.time);timerText.text=Mathf.FloorToInt(clock/60).ToString("00")+":"+Mathf.FloorToInt(clock%60).ToString("00");
            if(me!=null)
            {
                hudText.text=me.eliminated?"观战中 · Tab 切换玩家":me.nickname;
                personaCountText.text="Persona × "+me.remaining+" · 吞噬 "+state.eaten.Count(e=>e.eaterId==me.id);stabilityText.text="韧性 "+Mathf.CeilToInt(me.stability);
                float stability=Mathf.Clamp01(me.stability/100f);stabilityFill.rectTransform.anchorMax=new Vector2(stability,1);stabilityFill.color=stability<.35f?UITheme.Red:stability<.7f?UITheme.Yellow:UITheme.Mint;
                bool interacting=!string.IsNullOrEmpty(me.interaction);hudProgress.gameObject.SetActive(interacting||me.grabbed);
                if(me.grabbed){progressText.text="按住 Q / 挣脱按钮  "+Mathf.RoundToInt(me.escapeProgress*100)+"%";progressFill.rectTransform.anchorMax=new Vector2(me.escapeProgress,1);}
                else if(interacting){progressText.text=(me.interaction=="eat"?"正在吞噬":"正在回收")+"  "+Mathf.RoundToInt(me.progress*100)+"%";progressFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(me.progress),1);}
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
                var p=state.players[i];playerAvatars[i].color=ToyFactory.ColorOf(p.color);playerNames[i].color=UITheme.Ink;playerNames[i].text=(p.id==state.host?"♛  ":"")+p.nickname;
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
            var names=state.players.Where(p=>ids.Contains(p.id)).Select(p=>p.nickname);
            winnerText.text=string.Join("、",names)+(ids.Length>1?" · 并列获胜":" · 获胜")+"\n"+(state.endReason=="timeout"?"时间到 · 吞噬数优先，同分比较剩余":"最后一名存活者");winnerText.fontSize=22;
            foreach(Transform child in resultContent)Destroy(child.gameObject);
            if(state.rankings!=null)foreach(var rank in state.rankings)
            {
                var row=ArtPanel(resultContent,"Rank "+rank.rank,0,0,1040,60,rank.eliminated?UITheme.Field:UITheme.Mint,true,false);
                ArtText(row,rank.rank+"  "+rank.nickname+"   吞噬 "+rank.eaten+"   剩余 "+rank.remaining+(rank.eliminated?" · 已淘汰":""),18,6,1000,48,20,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);AddLayoutHeight(row,60);
            }
            if(state.eaten.Length==0)
            {
                var empty=ArtPanel(resultContent,"Empty receipt",0,0,1040,68,UITheme.Field,true,false);ArtText(empty,"这局没有吞噬记录。",20,8,1000,50,18,UITheme.Muted,FontStyle.Bold,TextAnchor.MiddleCenter);AddLayoutHeight(empty,68);return;
            }
            for(int i=0;i<state.eaten.Length;i++)
            {
                var record=state.eaten[i];var row=ArtPanel(resultContent,"Eaten record "+(i+1),0,0,1040,68,i%2==0?UITheme.Field:Color.Lerp(UITheme.Pink,UITheme.Paper,.76f),true,false);
                ArtText(row,(i+1).ToString("00"),16,12,52,43,18,UITheme.Muted,FontStyle.Bold,TextAnchor.MiddleCenter);
                ArtIcon(row,"attack",75,16,35,UITheme.Ink);ArtText(row,record.eater+"  吃掉了  "+record.owner+" 的「"+record.persona+"」",126,8,885,52,18,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);AddLayoutHeight(row,68);
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
                var card=ArtPanel(inventoryContent,"Persona "+item.asset.name,0,0,320,168,equipped?Color.Lerp(UITheme.Yellow,UITheme.Paper,.52f):UITheme.Field,true,false);inventoryCards.Add(card);
                var thumbnail=PersonaThumbnail(item.asset.visualId);
                var well=ArtPanel(card,"Persona thumbnail",10,12,92,92,Color.Lerp(ToyFactory.ColorOf(item.asset.color),UITheme.Paper,.7f),true,false);
                var itemImage=ArtIcon(well,thumbnail?"":IconForTemplate(item.asset.templateId),6,6,80,thumbnail?Color.white:ToyFactory.ColorOf(item.asset.color));
                if(thumbnail)itemImage.sprite=thumbnail;
                ArtText(card,item.asset.name,109,10,199,40,18,UITheme.Ink,FontStyle.Bold);
                ArtText(card,TemplateLabel(item.asset.templateId),109,51,195,22,13,UITheme.Muted,FontStyle.Bold);
                if(equipped)ArtText(card,"● 装备中",109,75,90,22,13,UITheme.Orange,FontStyle.Bold);
                var equip=ArtButton(card,equipped?"已装备":"装备",10,105,141,55,()=>Send("equip",id),equipped?UITheme.Yellow:UITheme.Mint,UITheme.Ink);
                equip.interactable=available&&!equipped;
                var quick=ArtButton(card,"置顶",167,105,141,55,()=>Favorite(assetId),UITheme.Purple,UITheme.Ink);
                quick.interactable=available;
            }
            int rows=Mathf.CeilToInt(state.inventory.Length/3f);inventoryContent.sizeDelta=new Vector2(1000,Mathf.Max(516,12+rows*168+Mathf.Max(0,rows-1)*12));
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
            if(imageUploadStyle)imageUploadStyle.SetState(busy?UIVisualState.Loading:UIVisualState.Normal);
            if(recordLabel)recordLabel.text=recording?"停止录音":"录音介绍";
        }

        void UpdateStatusVisual()
        {
            string value=net?.Status??"";if(value==lastStatus)return;lastStatus=value;status.text=value;statusPill.gameObject.SetActive(!string.IsNullOrEmpty(value));if(string.IsNullOrEmpty(value))return;
            Color color=UITheme.Paper;
            if(value.Contains("失败")||value.Contains("错误")||value.Contains("无效")||value.Contains("没有")||value.Contains("尚未"))color=UITheme.Red;
            else if(value.Contains("正在")||value.Contains("连接"))color=UITheme.Purple;
            else if(value.Contains("完成")||value.Contains("保存")||value.Contains("已")||value.Contains("成功"))color=UITheme.Green;
            else if(value.Contains("样例"))color=UITheme.Yellow;
            statusSurface.color=Color.Lerp(color,UITheme.Paper,.2f);statusPill.GetComponent<UIPulse>()?.Play();
        }

        void PositionStatus(Snapshot state)
        {
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
            if(previewKind=="wardrobe"||previewKind=="wardrobe-ds")
            {profile=new Profile{playerId="preview",nickname="预览玩家",appearance=AppearanceData.Demo(0),dsGirlUnlocked=previewKind=="wardrobe-ds",characterSkin=previewKind=="wardrobe-ds"?"ds-girl":"normal"};appearanceDirty=false;LoadAppearance();OpenWardrobe();net.Status="衣橱预览 · 不连接服务器";return;}
            if(previewKind=="home"||previewKind=="home-expanded"){ShowPage(MenuPage.Main);startMenuStage.SetExpanded(previewKind=="home-expanded",true);RefreshTouchVisibility();return;}
            if(previewKind=="presets"||previewKind=="solo"||previewKind=="multiplayer"){ShowPage(previewKind=="presets"?MenuPage.Presets:previewKind=="solo"?MenuPage.Solo:MenuPage.Multiplayer);return;}
            if(previewKind=="settings"){OpenSettings();net.Status="连接设置预览";RefreshTouchVisibility();return;}
            bool lobbyPreview=previewKind=="lobby"||previewKind=="lobby-ds"||previewKind=="qr";
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);var players=Enumerable.Range(0,lobbyPreview?6:3).Select(i=>new PublicPlayer{id="preview-"+i,nickname=new[]{"小宇宙","海盐汽水","凌晨三点","社团王牌","猫派代表","最后一页"}[i],color=catalog.items[i].color,remaining=Mathf.Max(1,8-i),position=Match.Spawn(i),yaw=i*55,stability=100-i*17,ready=i!=4,connected=i!=5,equippedName=catalog.items[i].label,equippedVisual=catalog.items[i].visualId,equippedColor=catalog.items[i].color,characterSkin=i==0&&previewKind.EndsWith("-ds")?"ds-girl":"normal",interaction=i==0&&(previewKind=="hud"||previewKind=="hud-ds")?"eat":"",progress=.62f}).ToArray();
            var inventory=catalog.items.Select((x,i)=>new PublicPersonaInstance{id="preview-item-"+i,ownerId="preview-0",asset=new PublicPersonaAsset{assetId="asset-"+i,name=x.label,templateId=x.templateId,visualId=x.visualId,color=x.color},state=i==0?ItemState.Equipped:ItemState.Inventory}).ToArray();
            var phase=lobbyPreview?MatchPhase.Lobby:previewKind=="results"?MatchPhase.Finished:MatchPhase.Battle;
            net.State=new Snapshot{room="PARTY",matchId="preview",you="preview-0",host="preview-0",winner="preview-1",phase=phase,time=196,finalAt=450,players=players,inventory=inventory,ground=Array.Empty<PublicPersonaInstance>(),revealed=Array.Empty<PublicPersonaInstance>(),effects=Array.Empty<EffectState>(),props=Array.Empty<PublicProp>(),events=new[]{new MatchEvent{id=1,text="海盐汽水吃掉了小宇宙的「篮球」！"}},eaten=new[]{new EatRecord{instanceId="eat-1",eater="海盐汽水",owner="小宇宙",persona="篮球"},new EatRecord{instanceId="eat-2",eater="凌晨三点",owner="猫派代表",persona="相机"}}};
            net.Status="UI 预览 · 不连接服务器";if(previewKind=="bag"){inventoryOpen=true;bag.gameObject.SetActive(true);}if(previewKind=="qr")OpenQrOverlay(CreatePreviewQr(),"PARTY",true);RefreshTouchVisibility();
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
            var rect=ArtRect(parent,"Text · "+(value.Length>16?value.Substring(0,16):value),new Vector2(x,-y),new Vector2(width,height));var label=rect.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.color=color;label.text=value;label.fontStyle=style;label.alignment=alignment;label.supportRichText=false;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }
        Button ArtButton(Transform parent,string label,float x,float y,float width,float height,Action action,Color background,Color foreground,string icon="")
        {
            var rect=ArtPanel(parent,"Button · "+label,x,y,width,height,background,true,false);var button=rect.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>action());
            float textX=icon==""?9:43;Image iconImage=null;if(icon!="")iconImage=ArtIcon(rect,icon,13,(height-26)/2,26,foreground);
            var text=ArtText(rect,label,textX,3,width-textX-8,height-6,Mathf.RoundToInt(Mathf.Clamp(height*.34f,15,21)),foreground,FontStyle.Bold,TextAnchor.MiddleCenter);
            var style=rect.gameObject.AddComponent<StickerButton>();style.Configure(button,rect.GetComponent<Image>(),text,background);style.Icon=iconImage;return button;
        }
        InputField ArtField(Transform parent,float x,float y,float width,float height,string placeholder,bool multiline)
        {
            var rect=ArtPanel(parent,"Input · "+placeholder,x,y,width,height,UITheme.Field,true,false);var input=rect.gameObject.AddComponent<InputField>();input.targetGraphic=rect.GetComponent<Image>();
            var text=ArtText(rect,"",14,5,width-28,height-10,18,UITheme.Ink,FontStyle.Normal,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);var hint=ArtText(rect,placeholder,14,5,width-28,height-10,18,UITheme.Muted,FontStyle.Normal,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);
            input.textComponent=text;input.placeholder=hint;input.characterLimit=multiline?8000:256;input.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;
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
