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
        RectTransform[] playerCards=new RectTransform[6];
        Text[] playerNames=new Text[6],playerMeta=new Text[6];
        Image[] playerAvatars=new Image[6],playerReadyMarks=new Image[6];
        Text roomTitle,roomCount,phaseText,timerText,stabilityText,personaCountText,progressText,winnerText,outfitText;
        Image stabilityFill,progressFill,statusSurface;
        RectTransform statusPill;
        StickerButton generateStyle,recordStyle,readyStyle,startStyle;
        readonly List<StickerButton> slotStyles=new List<StickerButton>(),colorStyles=new List<StickerButton>();
        UIPulse toastPulse;
        string lastStatus="",lastLobbyVisual="",lastResultVisual="",previewKind="";
        int lastScreenWidth,lastScreenHeight;
        bool compactLayout;
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
            DecorativeDot(home,36,82,112,UITheme.Pink,.12f);DecorativeDot(home,785,52,56,UITheme.Yellow,.14f);DecorativeDot(home,860,426,38,UITheme.Mint,.17f);
            homeHero=ArtPanel(home,"Brand and toy window",42,32,825,820,Color.clear,false,false);
            var beta=Tape(homeHero,"LOCAL PARTY BUILD",32,6,205,38,UITheme.Yellow);beta.localRotation=Quaternion.Euler(0,0,-3);
            var logoShadow=ArtText(homeHero,"ABOUT\nME.",49,64,650,270,112,UITheme.Pink,FontStyle.Bold,TextAnchor.UpperLeft);logoShadow.rectTransform.anchoredPosition+=new Vector2(7,-8);
            ArtText(homeHero,"ABOUT\nME.",42,56,650,270,112,UITheme.Ink,FontStyle.Bold,TextAnchor.UpperLeft);
            ArtText(homeHero,"你的「自我」，既是武器，也是生命。",48,323,720,48,28,UITheme.Ink,FontStyle.Bold);
            ArtText(homeHero,"介绍自己。拿自己打架。小心朋友吃掉你的篮球。",48,373,735,40,21,UITheme.Ink);
            var line=ArtPanel(homeHero,"Accent line",48,426,236,9,UITheme.Mint,false,false);line.localRotation=Quaternion.Euler(0,0,-1.5f);
            var frame=ArtFrame(homeHero,"Toy stage sticker frame",12,446,812,344);
            Tape(frame,"你的派对舞台",26,268,180,36,UITheme.Pink).localRotation=Quaternion.Euler(0,0,2);
            ArtText(frame,"01  认识自己     02  介绍朋友     03  吃掉对手",235,293,540,31,17,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleRight);

            studioCard=ArtPanel(home,"Persona studio",900,34,650,820,UITheme.Paper,true,true);studioCard.gameObject.AddComponent<UIReveal>();
            Tape(studioCard,"PERSONA WORKSHOP",30,24,216,38,UITheme.Mint);
            ArtText(studioCard,"先介绍一下，你是谁？",30,72,575,45,31,UITheme.Ink,FontStyle.Bold);
            StepBadge(studioCard,"1",30,126,UITheme.Yellow);ArtText(studioCard,"写下关于你的线索",70,127,260,34,18,UITheme.Ink,FontStyle.Bold);
            ArtText(studioCard,"昵称",30,168,120,25,15,UITheme.Muted,FontStyle.Bold);
            ArtText(studioCard,"房间",314,168,120,25,15,UITheme.Muted,FontStyle.Bold);
            nickname=ArtField(studioCard,30,195,268,50,"你的名字",false);nickname.text=PlayerPrefs.GetString("nickname","玩家");
            room=ArtField(studioCard,314,195,306,50,"房间码 · 留空创建",false);
            ArtText(studioCard,"自我介绍",30,257,200,25,15,UITheme.Muted,FontStyle.Bold);
            introduction=ArtField(studioCard,30,284,590,128,"我来自哪里、喜欢什么、有什么特别经历……",true);introduction.text=PlayerPrefs.GetString("introduction","");
            StepBadge(studioCard,"2",30,428,UITheme.Pink);ArtText(studioCard,"选择你的玩具外观",70,429,250,34,18,UITheme.Ink,FontStyle.Bold);
            var record=ArtButton(studioCard,"录音介绍",30,470,174,48,ToggleRecording,UITheme.Field,UITheme.Ink,"mic");recordLabel=record.GetComponentInChildren<Text>();recordStyle=record.GetComponent<StickerButton>();
            string[] colors={"#79CFC5","#F3AFC2","#FFD773","#9FAFF2"};
            for(int i=0;i<colors.Length;i++)
            {
                string choice=colors[i];var chip=ColorChip(studioCard,choice,222+i*53,473,42,()=>{selectedColor=choice;RefreshColorSelection();});colorStyles.Add(chip);
            }
            var outfitButton=ArtButton(studioCard,"换装 · 1",451,470,169,48,()=>{outfit=(outfit+1)%3;outfitText.text="换装 · "+(outfit+1);net.Status="外观方案 "+(outfit+1);},UITheme.Field,UITheme.Ink,"status");outfitText=outfitButton.GetComponentInChildren<Text>();
            generate=ArtButton(studioCard,"生成我的 Persona",30,536,590,56,()=>StartCoroutine(Generate()),UITheme.Ink,UITheme.Paper,"status");generateStyle=generate.GetComponent<StickerButton>();
            var profileCard=ArtPanel(studioCard,"Persona result",30,610,590,91,UITheme.Field,true,false);
            profileText=ArtText(profileCard,"AI 会把你的介绍变成可战斗的道具。\n结果由真实服务生成，不固定数量。",18,9,554,72,17,UITheme.Ink,FontStyle.Normal,TextAnchor.MiddleLeft);
            join=ArtButton(studioCard,"带上自己，进入房间",30,718,590,56,Enter,UITheme.Mint,UITheme.Ink,"ready");
            ArtButton(studioCard,"使用样例测试",30,785,208,37,()=>{net.Sample=true;net.Token=PlayerPrefs.GetString("sampleToken",Guid.NewGuid().ToString("N"));PlayerPrefs.SetString("sampleToken",net.Token);profile=GameNetwork.SampleProfile(net.Token,nickname.text);DisplayProfile();net.Status="样例测试：不是 AI 生成。服务器也须开启样例模式。";},UITheme.Field,UITheme.Ink,"status");
            ArtButton(home,"连接设置",1370,848,174,40,()=>{optionsOpen=!optionsOpen;settings.gameObject.SetActive(optionsOpen);},UITheme.Paper,UITheme.Ink,"settings");
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
            ArtText(card,"AI / 角色服务 HTTPS 地址",32,250,630,26,16,UITheme.Muted,FontStyle.Bold);
            api=ArtField(card,32,282,660,54,"https://api.example.com",false);api.text=PlayerPrefs.GetString("api",net.ApiUrl);
            var note=ArtPanel(card,"Secret note",32,360,660,76,UITheme.Yellow,true,false);
            ArtIcon(note,"status",18,18,38,UITheme.Ink);ArtText(note,"密钥只放在服务端 .env；客户端仅保存公开地址。",68,10,570,56,17,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleLeft);
            ArtButton(card,"保存连接设置",32,468,316,58,()=>{net.Endpoint=endpoint.text.Trim();net.ApiUrl=api.text.Trim().TrimEnd('/');PlayerPrefs.SetString("endpoint",net.Endpoint);PlayerPrefs.SetString("api",net.ApiUrl);settings.gameObject.SetActive(false);optionsOpen=false;net.Status="连接设置已保存";},UITheme.Mint,UITheme.Ink,"ready");
            var local=ArtButton(card,"本机测试服务器",376,468,316,58,()=>{net.AllowSamples=true;if(!Mirror.NetworkServer.active)net.StartServer();net.Status="本机样例服务器已启动，端口 7777";},UITheme.Field,UITheme.Ink,"settings");
            local.gameObject.SetActive(Application.platform==RuntimePlatform.OSXPlayer||Application.platform==RuntimePlatform.WindowsPlayer||Application.isEditor);
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
            ArtText(lobbyCard,"所有人 Ready 后，由房主开始。掉线角色在开局后会留在场内。",28,748,610,48,15,UITheme.Muted,FontStyle.Normal,TextAnchor.MiddleLeft);
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
            keyboardHint=ArtPanel(hud,"Keyboard help",34,700,382,157,new Color(1,1,1,.84f),true,false);
            ArtText(keyboardHint,"KEYBOARD KIT",16,9,330,25,14,UITheme.Muted,FontStyle.Bold);
            ArtText(keyboardHint,"WASD 移动  ·  右键视角  ·  SPACE 跳跃\nJ 轻击  ·  K 蓄力重击  ·  G 抓人\nH 抓道具  ·  E 吞噬/回收  ·  B 背包",16,37,348,107,15,UITheme.Ink,FontStyle.Normal,TextAnchor.MiddleLeft);
            hotbar=ArtPanel(hud,"Persona hotbar",380,795,840,84,UITheme.Paper,true,true);
            for(int i=0;i<5;i++)
            {
                int slot=i;var button=ArtButton(hotbar,(i+1)+"  —",12+i*139,12,129,60,()=>{var state=net.State;if(state!=null&&slot<QuickItems(state).Length)Send("equip",QuickItems(state)[slot].id);},UITheme.Field,UITheme.Ink,"status");slots.Add(button);slotStyles.Add(button.GetComponent<StickerButton>());
            }
            ArtButton(hotbar,"背包",714,12,114,60,ToggleBag,UITheme.Purple,UITheme.Ink,"bag");
            ArtButton(hud,"退出",1440,28,126,46,Leave,UITheme.Paper,UITheme.Ink,"exit");
            ArtButton(hud,"重连",1440,86,126,42,()=>{if(!Mirror.NetworkClient.active)net.Connect();},UITheme.Field,UITheme.Ink,"status");
        }

        void BuildBag()
        {
            bag=ArtPanel(artboard,"Inventory modal",0,0,1600,900,new Color(.05f,.07f,.12f,.48f),false,false);Stretch(bag);bag.GetComponent<Image>().raycastTarget=true;
            var card=ArtPanel(bag,"Persona backpack",270,108,1060,684,UITheme.Paper,true,true);card.gameObject.AddComponent<UIReveal>();
            Tape(card,"YOUR PERSONA BAG",30,25,232,38,UITheme.Purple);
            bagText=ArtText(card,"你的 Persona",30,75,770,42,29,UITheme.Ink,FontStyle.Bold);
            ArtButton(card,"回到战斗",830,28,198,50,ToggleBag,UITheme.Mint,UITheme.Ink,"exit");
            var viewport=ArtPanel(card,"Inventory viewport",24,136,1012,468,Color.clear,false,false);viewport.gameObject.AddComponent<RectMask2D>();
            inventoryContent=ArtRect(viewport,"Inventory cards",Vector2.zero,new Vector2(990,468));inventoryContent.anchorMin=inventoryContent.anchorMax=inventoryContent.pivot=new Vector2(0,1);
            var grid=inventoryContent.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(482,94);grid.spacing=new Vector2(16,14);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.padding=new RectOffset(4,4,4,4);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=inventoryContent;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            ArtText(card,"点击卡片装备；“放入快捷栏”会将它排到数字键前列。",30,625,810,32,16,UITheme.Muted);
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
            ArtButton(card,"保存结算截图",34,672,520,58,()=>platform.Screenshot(),UITheme.Yellow,UITheme.Ink,"camera");
            ArtButton(card,"再认识一次",594,672,520,58,Leave,UITheme.Ink,UITheme.Paper,"ready");
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
            ArtPanel(pad,"Joystick center",66,66,92,92,new Color(UITheme.Mint.r,UITheme.Mint.g,UITheme.Mint.b,.78f),true,false);ArtText(pad,"移动",62,154,100,34,17,UITheme.Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            var look=ArtPanel(touch,"Camera zone",850,180,690,300,new Color(1,1,1,.01f),false,false);var lookPad=look.gameObject.AddComponent<TouchPad>();lookPad.CameraPad=true;lookPad.OnLook=d=>{world.CameraYaw+=d.x*.12f;world.CameraPitch=Mathf.Clamp(world.CameraPitch-d.y*.1f,10,65);};
            var light=ArtButton(touch,"轻击",1188,578,132,70,()=>Send("light"),UITheme.Yellow,UITheme.Ink,"attack");
            var heavyButton=ArtButton(touch,"按住重击",1334,542,178,106,()=>{},UITheme.Pink,UITheme.Ink,"attack");var heavy=heavyButton.gameObject.AddComponent<PressButton>();heavy.Down=()=>{heavyButton.GetComponent<StickerButton>().SetState(UIVisualState.Recording);Send("charge");};heavy.Up=()=>{heavyButton.GetComponent<StickerButton>().SetState(UIVisualState.Normal);Send("heavy");};
            ArtButton(touch,"跳跃",1362,670,150,66,()=>Send("jump"),UITheme.Purple,UITheme.Ink,"jump");
            ArtButton(touch,"抓人",1035,578,132,70,()=>Send("grab"),UITheme.Mint,UITheme.Ink,"grab");
            ArtButton(touch,"抓道具",1035,665,132,66,()=>Send("grab_item"),UITheme.Field,UITheme.Ink,"grab");
            ArtButton(touch,"吃 / 回收",1188,667,153,70,()=>Send("interact"),UITheme.Orange,UITheme.Ink,"status");
            touch.gameObject.SetActive(false);
        }

        void RefreshTouchVisibility()
        {
            bool force=previewKind=="touch";bool visible=force||Application.isMobilePlatform||platform.IsWeChat;touch.gameObject.SetActive(visible);if(keyboardHint)keyboardHint.gameObject.SetActive(!visible);
        }

        void ApplyAdaptiveLayout(bool force=false)
        {
            if(!force&&lastScreenWidth==Screen.width&&lastScreenHeight==Screen.height)return;
            lastScreenWidth=Screen.width;lastScreenHeight=Screen.height;
            bool next=Application.isMobilePlatform||platform.IsWeChat||Screen.height<=600||Screen.width/(float)Mathf.Max(1,Screen.height)<1.48f;
            if(!force&&next==compactLayout)return;compactLayout=next;
            if(compactLayout)
            {
                Place(homeHero,22,24,800,830);Place(studioCard,830,24,738,832);Place(lobbyCard,28,28,700,834);Place(hotbar,348,788,904,92);
            }
            else
            {
                Place(homeHero,42,32,825,820);Place(studioCard,900,34,650,820);Place(lobbyCard,44,42,676,816);Place(hotbar,380,795,840,84);
            }
        }

        void UpdateVisualUI(Snapshot state,PublicPlayer me)
        {
            ApplyAdaptiveLayout();UpdateStatusVisual();ApplyBusyVisuals();PositionStatus(state);
            if(state==null)return;
            UpdateLobbyCards(state);
            string phase=state.phase==MatchPhase.Reveal?"PERSONA REVEAL":state.phase==MatchPhase.FinalBurst?"FINAL BURST":state.phase==MatchPhase.Finished?"RESULT":"BATTLE";
            phaseText.text=phase;timerText.text=Mathf.FloorToInt(state.time/60).ToString("00")+":"+Mathf.FloorToInt(state.time%60).ToString("00");
            if(me!=null)
            {
                hudText.text=me.eliminated?"观战中 · Tab 切换玩家":me.nickname;
                personaCountText.text="PERSONA × "+me.remaining;stabilityText.text="韧性 "+Mathf.CeilToInt(me.stability);
                float stability=Mathf.Clamp01(me.stability/100f);stabilityFill.rectTransform.anchorMax=new Vector2(stability,1);stabilityFill.color=stability<.35f?UITheme.Red:stability<.7f?UITheme.Yellow:UITheme.Mint;
                bool interacting=!string.IsNullOrEmpty(me.interaction);hudProgress.gameObject.SetActive(interacting);
                if(interacting){progressText.text=(me.interaction=="eat"?"正在吞噬":"正在回收")+"  "+Mathf.RoundToInt(me.progress*100)+"%";progressFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(me.progress),1);}
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
            string signature=state.winner+"|"+string.Join("|",state.eaten.Select(e=>e.instanceId));if(signature==lastResultVisual)return;lastResultVisual=signature;
            var winner=state.players.FirstOrDefault(p=>p.id==state.winner);winnerText.text=(winner?.nickname??"玩家")+" 保住了最后的自我";
            foreach(Transform child in resultContent)Destroy(child.gameObject);
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
            foreach(var b in bagButtons)Destroy(b.gameObject);bagButtons.Clear();
            foreach(var item in state.inventory)
            {
                string id=item.id,assetId=item.asset.assetId;var card=ArtPanel(inventoryContent,"Persona "+item.asset.name,0,0,482,94,item.state==ItemState.Equipped?Color.Lerp(UITheme.Yellow,UITheme.Paper,.5f):UITheme.Field,true,false);
                ArtIcon(card,IconForTemplate(item.asset.templateId),14,18,48,ToyFactory.ColorOf(item.asset.color));
                ArtText(card,item.asset.name,76,10,225,32,18,UITheme.Ink,FontStyle.Bold);ArtText(card,TemplateLabel(item.asset.templateId)+(item.state==ItemState.Equipped?" · 当前装备":""),76,44,235,27,14,item.state==ItemState.Equipped?UITheme.Orange:UITheme.Muted,FontStyle.Bold);
                var equip=card.gameObject.AddComponent<Button>();equip.onClick.AddListener(()=>Send("equip",id));equip.transition=Selectable.Transition.None;
                var quick=ArtButton(card,"快捷栏",332,22,132,50,()=>Favorite(assetId),UITheme.Purple,UITheme.Ink,"bag");quick.transform.SetAsLastSibling();bagButtons.Add(equip);
            }
            int rows=Mathf.CeilToInt(state.inventory.Length/2f);inventoryContent.sizeDelta=new Vector2(990,Mathf.Max(468,8+rows*108));
        }

        void ApplyBusyVisuals()
        {
            if(generateStyle)generateStyle.SetState(busy?UIVisualState.Loading:UIVisualState.Normal);
            if(recordStyle)recordStyle.SetState(recording?UIVisualState.Recording:UIVisualState.Normal);
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
            if(previewKind=="home"){RefreshTouchVisibility();return;}
            if(previewKind=="settings"){optionsOpen=true;settings.gameObject.SetActive(true);net.Status="连接设置预览";RefreshTouchVisibility();return;}
            bool lobbyPreview=previewKind=="lobby"||previewKind=="qr";
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);var players=Enumerable.Range(0,lobbyPreview?6:3).Select(i=>new PublicPlayer{id="preview-"+i,nickname=new[]{"小宇宙","海盐汽水","凌晨三点","社团王牌","猫派代表","最后一页"}[i],color=catalog.items[i].color,remaining=Mathf.Max(1,8-i),position=Match.Spawn(i),yaw=i*55,stability=100-i*17,ready=i!=4,connected=i!=5,equippedName=catalog.items[i].label,equippedVisual=catalog.items[i].visualId,equippedColor=catalog.items[i].color,interaction=i==0&&previewKind=="hud"?"eat":"",progress=.62f}).ToArray();
            var inventory=catalog.items.Select((x,i)=>new PersonaInstance{id="preview-item-"+i,ownerId="preview-0",asset=new PersonaAsset{assetId="asset-"+i,name=x.label,templateId=x.templateId,visualId=x.visualId,color=x.color},state=i==0?ItemState.Equipped:ItemState.Inventory}).ToArray();
            var phase=lobbyPreview?MatchPhase.Lobby:previewKind=="results"?MatchPhase.Finished:MatchPhase.Battle;
            net.State=new Snapshot{room="PARTY",matchId="preview",you="preview-0",host="preview-0",winner="preview-1",phase=phase,time=196,players=players,inventory=inventory,ground=Array.Empty<PersonaInstance>(),revealed=Array.Empty<PersonaInstance>(),effects=Array.Empty<EffectState>(),props=Array.Empty<PublicProp>(),events=new[]{new MatchEvent{id=1,text="海盐汽水吃掉了小宇宙的「篮球」！"}},eaten=new[]{new EatRecord{instanceId="eat-1",eater="海盐汽水",owner="小宇宙",persona="篮球"},new EatRecord{instanceId="eat-2",eater="凌晨三点",owner="猫派代表",persona="相机"}}};
            net.Status="UI 预览 · 不连接服务器";if(previewKind=="bag"){inventoryOpen=true;bag.gameObject.SetActive(true);}if(previewKind=="qr")OpenQrOverlay(CreatePreviewQr(),"PARTY",true);RefreshTouchVisibility();
        }

        void OpenQrOverlay(Texture texture,string roomCode,bool destroyTexture)
        {
            var overlay=ArtPanel(artboard,"Room QR modal",0,0,1600,900,new Color(.05f,.07f,.12f,.52f),false,false);Stretch(overlay);overlay.GetComponent<Image>().raycastTarget=true;
            var card=ArtPanel(overlay,"Room QR sticker",500,96,600,708,UITheme.Paper,true,true);card.gameObject.AddComponent<UIReveal>();
            Tape(card,"SCAN TO JOIN",30,24,176,38,UITheme.Pink);ArtText(card,"扫码加入 · "+roomCode,30,76,540,49,29,UITheme.Ink,FontStyle.Bold);
            var qrPaper=ArtPanel(card,"QR paper",54,145,492,492,Color.white,true,false);var qrRect=ArtRect(qrPaper,"QR image",new Vector2(14,-14),new Vector2(464,464));var image=qrRect.gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            ArtButton(card,"关闭二维码",54,650,492,46,()=>{if(destroyTexture)Destroy(texture);Destroy(overlay.gameObject);},UITheme.Ink,UITheme.Paper,"exit");
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
            input.textComponent=text;input.placeholder=hint;input.characterLimit=multiline?8000:256;input.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;return input;
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
