using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AboutMe
{
    [Serializable] class SessionRequest {public string nickname;}
    [Serializable] class SessionResponse {public string token;public Profile profile;}
    [Serializable] class GenerationRequest {public string nickname,introduction,color;public int outfit;}
    [Serializable] class Transcription {public string text;}
    [Serializable] class WechatLoginRequest {public string code,nickname;}
    public class TouchPad : MonoBehaviour, IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public Vector2 Value; public bool CameraPad; public Action<Vector2> OnLook;
        Vector2 origin;
        public void OnPointerDown(PointerEventData e){origin=e.position;Value=Vector2.zero;}
        public void OnDrag(PointerEventData e){if(CameraPad)OnLook?.Invoke(e.delta);else Value=Vector2.ClampMagnitude((e.position-origin)/70,1);}
        public void OnPointerUp(PointerEventData e){Value=Vector2.zero;}
    }
    public class PressButton : MonoBehaviour,IPointerDownHandler,IPointerUpHandler
    {
        public Action Down,Up;
        public void OnPointerDown(PointerEventData e)=>Down?.Invoke();
        public void OnPointerUp(PointerEventData e)=>Up?.Invoke();
    }
    public class GameUI : MonoBehaviour
    {
        public static GameUI UI;
        GameNetwork net;ToyWorld world;PlatformBridge platform;
        Font font;RectTransform canvas,home,lobby,hud,results,settings,bag,touch;
        InputField nickname,introduction,room,endpoint,api;
        Text status,profileText,lobbyText,hudText,eventText,resultText,bagText,recordLabel;
        Button generate,ready,start,join;
        readonly List<Button> slots=new List<Button>();
        readonly List<Button> bagButtons=new List<Button>();
        readonly List<string> favorites=new List<string>();
        Profile profile;string lastInventory="";int lastEvent;
        bool busy,inventoryOpen,recording,optionsOpen,pendingRoom;float nextSend,nextUI;
        string selectedColor="#77C8BD";int outfit;
        Vector2 movement;float yaw;TouchPad movePad;
        readonly Color ink=new Color(.13f,.17f,.23f),cream=new Color(.98f,.96f,.92f);
        public void Build(GameNetwork network,ToyWorld toyWorld)
        {
            UI=this;net=network;world=toyWorld;platform=gameObject.AddComponent<PlatformBridge>();
            favorites.AddRange(PlayerPrefs.GetString("favorites","").Split(',').Where(x=>x!=""));
            font=Resources.Load<Font>("AboutMeFont");if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"PingFang SC","Arial","Microsoft YaHei"},20);
            var c=new GameObject("About Me UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            c.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas=(RectTransform)c.transform;
            var scaler=c.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            if(!FindObjectOfType<EventSystem>())new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            home=Panel(canvas,"Introduction",0,0,1600,900,new Color(0,0,0,0));
            Text(home,"ABOUT",74,48,720,210,112,ink,FontStyle.Bold);
            Text(home,"ME.",74,195,720,210,112,ink,FontStyle.Bold);
            Text(home,"你的「自我」，既是武器，也是生命。",82,376,790,54,27,ink);
            Text(home,"介绍自己。拿自己打架。小心朋友吃掉你的篮球。",82,430,790,48,21,ink);
            Text(home,"01  认识自己     /     02  介绍朋友     /     03  吃掉对手",82,805,950,38,18,ink);
            var card=Panel(home,"Persona studio",940,46,590,770,cream);
            Text(card,"LET'S MEET YOU",32,25,510,32,18,ink,FontStyle.Bold);
            Text(card,"先介绍一下，你是谁？",32,65,530,48,31,ink,FontStyle.Bold);
            nickname=Field(card,32,128,248,45,"昵称",false);nickname.text=PlayerPrefs.GetString("nickname","玩家");
            room=Field(card,300,128,250,45,"房间码 · 留空创建",false);
            introduction=Field(card,32,195,518,145,"我来自哪里，喜欢什么，有什么特别的经历……",true);
            introduction.text=PlayerPrefs.GetString("introduction","");
            var rec=Button(card,"录音介绍",32,352,152,42,()=>ToggleRecording());recordLabel=rec.GetComponentInChildren<Text>();
            for(int i=0;i<4;i++){string color=new[]{"#77C8BD","#F5C0CF","#FFE08D","#A2B7FF"}[i];Button(card,"",205+i*48,352,38,42,()=>selectedColor=color,ToyFactory.ColorOf(color));}
            Button(card,"换装",410,352,140,42,()=>{outfit=(outfit+1)%3;net.Status="外观方案 "+(outfit+1);});
            generate=Button(card,"生成我的 Persona  →",32,414,518,52,()=>StartCoroutine(Generate()),ink,cream);
            profileText=Text(card,"AI 会把你的介绍变成可战斗的道具。\n结果由真实服务生成，不固定数量。",32,480,518,122,19,ink);
            join=Button(card,"带上自己，进入房间",32,624,518,50,Enter,ink,cream);
            Button(card,"明确标注的样例测试",32,689,260,36,()=>{net.Sample=true;net.Token=PlayerPrefs.GetString("sampleToken",Guid.NewGuid().ToString("N"));PlayerPrefs.SetString("sampleToken",net.Token);profile=GameNetwork.SampleProfile(net.Token,nickname.text);DisplayProfile();net.Status="样例测试：不是 AI 生成。服务器也须开启样例模式。";});
            Button(home,"连接设置",1370,839,160,40,()=>{optionsOpen=!optionsOpen;settings.gameObject.SetActive(optionsOpen);});
            settings=Panel(canvas,"Connection settings",840,425,690,395,cream);
            Text(settings,"连接与服务",28,22,500,40,28,ink,FontStyle.Bold);
            Text(settings,"游戏服务器 WebSocket 地址",28,77,620,30,18,ink);
            endpoint=Field(settings,28,112,630,44,"wss://game.example.com",false);endpoint.text=PlayerPrefs.GetString("endpoint",net.Endpoint);
            Text(settings,"AI / 角色服务 HTTPS 地址",28,170,620,30,18,ink);
            api=Field(settings,28,205,630,44,"https://api.example.com",false);api.text=PlayerPrefs.GetString("api",net.ApiUrl);
            Button(settings,"保存连接设置",28,279,300,48,()=>{net.Endpoint=endpoint.text.Trim();net.ApiUrl=api.text.Trim().TrimEnd('/');PlayerPrefs.SetString("endpoint",net.Endpoint);PlayerPrefs.SetString("api",net.ApiUrl);settings.gameObject.SetActive(false);optionsOpen=false;});
            var localServer=Button(settings,"本机测试服务器",352,279,305,48,()=>{net.AllowSamples=true;if(!Mirror.NetworkServer.active)net.StartServer();net.Status="本机样例服务器已启动，端口 7777";});
            localServer.gameObject.SetActive(Application.platform==RuntimePlatform.OSXPlayer||Application.platform==RuntimePlatform.WindowsPlayer||Application.isEditor);
            settings.gameObject.SetActive(false);
            lobby=Panel(canvas,"Friend room",55,50,515,715,cream);
            Text(lobby,"MEET YOUR FRIENDS",28,22,465,38,19,ink,FontStyle.Bold);
            Button(lobby,"房间二维码",332,24,155,39,()=>StartCoroutine(ShowQr()));
            lobbyText=Text(lobby,"",28,82,460,380,27,ink);
            ready=Button(lobby,"准备好了",28,490,460,52,()=>Send("ready"),ink,cream);
            start=Button(lobby,"开始互相介绍",28,556,460,52,()=>Send("start"),ink,cream);
            Button(lobby,"分享 / 复制房间码",28,621,286,42,()=>platform.Share(net.State?.room??""));
            Button(lobby,"离开",330,621,158,42,Leave);
            lobby.gameObject.SetActive(false);
            hud=Panel(canvas,"Match HUD",0,0,1600,900,new Color(0,0,0,0));
            var top=Panel(hud,"Scoreboard",40,30,500,125,cream);hudText=Text(top,"",20,16,462,100,22,ink);
            var toast=Panel(hud,"Announcement",495,28,640,65,ink);eventText=Text(toast,"",18,10,603,45,20,cream);
            Text(hud,"WASD 移动 · 鼠标右键视角 · 空格跳跃\nJ 轻击 · 按住 K 蓄力 / 松开重击 · G 抓人 · H 抓道具\nE 拿起并吞噬 / 回收 · Q 放下 / 取消 · B 背包",40,695,650,100,18,ink);
            for(int i=0;i<5;i++){int slot=i;slots.Add(Button(hud,(i+1).ToString(),480+i*135,810,125,62,()=>{if(net.State!=null&&slot<net.State.inventory.Length)Send("equip",QuickItems(net.State)[slot].id);}));}
            Button(hud,"完整背包",1190,810,165,62,()=>ToggleBag());
            Button(hud,"退出",1420,30,135,45,Leave);
            Button(hud,"重连",1420,89,135,40,()=>{if(!Mirror.NetworkClient.active)net.Connect();});
            hud.gameObject.SetActive(false);
            bag=Panel(canvas,"Full inventory",380,150,850,600,cream);bagText=Text(bag,"",25,18,800,45,25,ink,FontStyle.Bold);
            var viewport=Panel(bag,"Viewport",20,82,810,420,new Color(0,0,0,0));viewport.gameObject.AddComponent<RectMask2D>();
            var content=Panel(viewport,"Content",0,0,790,420,new Color(0,0,0,0));content.gameObject.AddComponent<VerticalLayoutGroup>().spacing=7;
            var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            inventoryContent=content;
            Button(bag,"回到战斗",25,530,800,46,()=>ToggleBag());bag.gameObject.SetActive(false);
            results=Panel(canvas,"What did you eat",320,75,960,740,cream);
            Text(results,"WHAT DID YOU EAT?",38,35,890,125,62,ink,FontStyle.Bold);
            var resultViewport=Panel(results,"Eaten list",42,225,865,385,new Color(0,0,0,0));resultViewport.gameObject.AddComponent<RectMask2D>();
            resultText=Text(resultViewport,"",0,0,865,385,27,ink);
            var resultScroll=resultViewport.gameObject.AddComponent<ScrollRect>();resultScroll.viewport=resultViewport;resultScroll.content=resultText.rectTransform;resultScroll.horizontal=false;resultScroll.movementType=ScrollRect.MovementType.Clamped;
            Button(results,"保存结算截图",40,653,420,48,()=>platform.Screenshot(),ink,cream);
            Button(results,"再认识一次",490,653,420,48,Leave,ink,cream);results.gameObject.SetActive(false);
            status=Text(canvas,"",50,865,1250,32,17,ink);
            BuildTouch();
            net.Endpoint=endpoint.text;net.ApiUrl=api.text;
            net.Token=PlayerPrefs.GetString("token","");if(net.Token!="")StartCoroutine(LoadProfile());
            platform.Initialize();
        }
        RectTransform inventoryContent;
        PersonaInstance[] QuickItems(Snapshot state)=>state.inventory.OrderBy(x=>{int i=favorites.IndexOf(x.asset.assetId);return i<0?10000:i;}).ToArray();
        void Favorite(string assetId){favorites.Remove(assetId);favorites.Insert(0,assetId);if(favorites.Count>5)favorites.RemoveAt(5);PlayerPrefs.SetString("favorites",string.Join(",",favorites));lastInventory="";}
        public void WechatLogin(string code){StartCoroutine(LoginWechat(code));}
        IEnumerator LoginWechat(string code)
        {
            busy=true;
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/sessions/wechat",JsonUtility.ToJson(new WechatLoginRequest{code=code,nickname=nickname.text})))
            {
                yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                var response=JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text);net.Token=response.token;profile=response.profile;net.Sample=false;PlayerPrefs.SetString("token",net.Token);DisplayProfile();TryAutoJoin();
            }
            busy=false;
        }
        IEnumerator ShowQr()
        {
            if(net.State==null)yield break;
            if(net.Sample){net.Status="微信房间二维码需要真实账号及 AppID，样例模式请复制房间码";yield break;}
            using(var req=UnityWebRequestTexture.GetTexture(net.ApiUrl+"/rooms/"+net.State.room+"/qrcode"))
            {
                req.SetRequestHeader("Authorization","Bearer "+net.Token);req.timeout=25;yield return req.SendWebRequest();
                if(req.result!=UnityWebRequest.Result.Success){net.Status="二维码生成失败，请检查微信体验版和服务配置";yield break;}
                var overlay=Panel(canvas,"Room QR",535,130,530,610,cream);var texture=DownloadHandlerTexture.GetContent(req);
                Text(overlay,"扫码加入 · "+net.State.room,35,20,460,45,28,ink,FontStyle.Bold);
                var image=Panel(overlay,"QR",50,83,430,430,Color.white).gameObject.AddComponent<RawImage>();image.texture=texture;
                Button(overlay,"关闭",50,545,430,42,()=>{Destroy(texture);Destroy(overlay.gameObject);});
            }
        }
        void BuildTouch()
        {
            touch=Panel(hud,"Touch controls",0,0,1600,900,new Color(0,0,0,0));
            var pad=Panel(touch,"Move",65,485,230,195,new Color(.98f,.96f,.92f,.55f));movePad=pad.gameObject.AddComponent<TouchPad>();Text(pad,"移动",70,75,100,45,24,ink);
            var look=Panel(touch,"Look",900,180,640,275,new Color(1,1,1,.01f));var lookPad=look.gameObject.AddComponent<TouchPad>();lookPad.CameraPad=true;lookPad.OnLook=d=>{world.CameraYaw+=d.x*.12f;world.CameraPitch=Mathf.Clamp(world.CameraPitch-d.y*.1f,10,65);};
            Button(touch,"轻击",1200,575,120,68,()=>Send("light"),ink,cream);
            var heavy=Button(touch,"蓄力重击",1340,550,155,95,()=>{},ink,cream).gameObject.AddComponent<PressButton>();heavy.Down=()=>Send("charge");heavy.Up=()=>Send("heavy");
            Button(touch,"跳跃",1360,670,125,60,()=>Send("jump"));Button(touch,"抓人",1060,575,120,60,()=>Send("grab"));
            Button(touch,"抓道具",1060,650,120,60,()=>Send("grab_item"));Button(touch,"吃 / 回收",1210,665,135,66,()=>Send("interact"));
            touch.gameObject.SetActive(Application.isMobilePlatform||platform.IsWeChat);
        }
        void DisplayProfile()
        {profileText.text=(net.Sample?"[样例测试 · 非 AI 生成]\n":"")+"PERSONA × "+profile.personas.Length+"\n"+string.Join("  /  ",profile.personas.Select(p=>p.name));}
        IEnumerator LoadProfile()
        {
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/profiles/me",null,net.Token))
            {yield return req.SendWebRequest();if(req.result==UnityWebRequest.Result.Success){profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);DisplayProfile();TryAutoJoin();}else net.Status="已保存的角色暂时无法读取，请检查服务地址";}
        }
        IEnumerator Generate()
        {
            if(busy)yield break;
            busy=true;net.Sample=false;net.Status="正在把你变成游戏内容…";
            net.ApiUrl=api.text.Trim().TrimEnd('/');
            if(string.IsNullOrEmpty(net.Token)||net.Token==PlayerPrefs.GetString("sampleToken","!"))
            {
                using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/sessions",JsonUtility.ToJson(new SessionRequest{nickname=nickname.text})))
                {
                    yield return req.SendWebRequest();
                    if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                    net.Token=JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text).token;PlayerPrefs.SetString("token",net.Token);
                }
            }
            var body=new GenerationRequest{nickname=nickname.text,introduction=introduction.text,color=selectedColor,outfit=outfit};
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/personas/generate",JsonUtility.ToJson(body),net.Token))
            {
                yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);DisplayProfile();
                PlayerPrefs.SetString("introduction",introduction.text);PlayerPrefs.SetString("nickname",nickname.text);PlayerPrefs.Save();net.Status="你的 Persona 已保存，下次仍可继续使用。";
            }
            busy=false;
            TryAutoJoin();
        }
        void Fail(UnityWebRequest request){busy=false;net.Status="服务请求失败："+(string.IsNullOrEmpty(request.downloadHandler.text)?request.error:request.downloadHandler.text);}
        void Enter()
        {
            if(profile==null||profile.personas.Length==0){net.Status="请先生成 Persona，或明确选择样例测试";return;}
            net.Nickname=nickname.text;net.RoomCode=room.text.Trim();net.Endpoint=endpoint.text.Trim();net.Connect();
        }
        void Leave(){if(Mirror.NetworkClient.active)net.StopClient();net.State=null;lastInventory="";inventoryOpen=false;bag.gameObject.SetActive(false);}
        void ToggleBag(){inventoryOpen=!inventoryOpen;bag.gameObject.SetActive(inventoryOpen);if(inventoryOpen)Send("cancel");}
        public void SetRoom(string value){room.text=value;pendingRoom=true;TryAutoJoin();}
        void TryAutoJoin(){if(pendingRoom&&profile!=null&&profile.personas.Length>0){pendingRoom=false;Enter();}}
        void ToggleRecording()
        {
            if(recording){platform.StopRecording();recording=false;recordLabel.text="录音介绍";return;}
            StartCoroutine(PrepareRecording());
        }
        IEnumerator PrepareRecording()
        {
            if(busy)yield break;
            if(net.Token==""||net.Sample)
            {
                busy=true;net.ApiUrl=api.text.Trim().TrimEnd('/');
                using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/sessions",JsonUtility.ToJson(new SessionRequest{nickname=nickname.text})))
                {
                    yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                    net.Token=JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text).token;net.Sample=false;profile=null;PlayerPrefs.SetString("token",net.Token);
                }
                busy=false;
            }
            recording=true;recordLabel.text="停止录音";platform.StartRecording();
        }
        public void Speech(byte[] data,string extension)
        {recording=false;recordLabel.text="录音介绍";StartCoroutine(Transcribe(data,extension));}
        IEnumerator Transcribe(byte[] data,string extension)
        {
            net.Status="正在识别语音…";var form=new WWWForm();form.AddBinaryData("file",data,"recording."+extension,"application/octet-stream");
            using(var req=UnityWebRequest.Post(net.ApiUrl+"/speech/transcribe",form))
            {req.timeout=50;req.SetRequestHeader("Authorization","Bearer "+net.Token);yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}introduction.text=JsonUtility.FromJson<Transcription>(req.downloadHandler.text).text;net.Status="转写完成，请检查介绍后生成 Persona";}
        }
        void Send(string action,string item="")=>net.Send(action,item,movement,yaw);
        void Update()
        {
            if(net==null)return;
            status.text=net.Status;generate.interactable=!busy;join.interactable=!busy;
            var state=net.State;
            home.gameObject.SetActive(state==null);
            lobby.gameObject.SetActive(state!=null&&state.phase==MatchPhase.Lobby);
            hud.gameObject.SetActive(state!=null&&state.phase!=MatchPhase.Lobby&&state.phase!=MatchPhase.Finished);
            results.gameObject.SetActive(state!=null&&state.phase==MatchPhase.Finished);
            if(state==null)return;
            if(UnityEngine.Time.unscaledTime>=nextUI)
            {
                nextUI=UnityEngine.Time.unscaledTime+.1f;
                lobbyText.text="房间  "+state.room+"\n"+state.players.Length+" / 6 位朋友\n\n"+string.Join("\n",state.players.Select(p=>(p.ready?"✓  ":"○  ")+p.nickname+"  ·  Persona ×"+p.remaining));
                start.gameObject.SetActive(state.you==state.host);start.interactable=state.players.Length>=2&&state.players.All(p=>p.ready);
                var me=state.players.FirstOrDefault(p=>p.id==state.you);
                if(me!=null)hudText.text=(me.eliminated?"观战中 · Tab 切换玩家":me.nickname+"  ·  Persona ×"+me.remaining)+"\n"+Mathf.FloorToInt(state.time/60).ToString("00")+":"+Mathf.FloorToInt(state.time%60).ToString("00")+"  /  "+(state.phase==MatchPhase.FinalBurst?"终局抢夺":state.phase==MatchPhase.Reveal?"开局介绍":"韧性 "+Mathf.CeilToInt(me.stability))+(me.interaction!=""?"\n"+(me.interaction=="eat"?"吞噬":"回收")+" "+Mathf.RoundToInt(me.progress*100)+"%":"");
                if(state.events.Length>0&&state.events.Last().id!=lastEvent){lastEvent=state.events.Last().id;eventText.text=state.events.Last().text;}
                string inventory=string.Join("|",state.inventory.Select(x=>x.id+":"+x.state));
                if(inventory!=lastInventory)
                {
                    lastInventory=inventory;
                    var quick=QuickItems(state);
                    for(int i=0;i<slots.Count;i++){slots[i].interactable=i<quick.Length;slots[i].GetComponentInChildren<Text>().text=i<quick.Length?(i+1)+"  "+quick[i].asset.name+(quick[i].state==ItemState.Equipped?" ●":""):(i+1)+"  —";}
                    foreach(var b in bagButtons)Destroy(b.gameObject);bagButtons.Clear();
                    foreach(var item in state.inventory){string id=item.id;string assetId=item.asset.assetId;var button=Button(inventoryContent,item.asset.name+"  /  "+item.asset.templateId+(item.state==ItemState.Equipped?"  · 当前装备":""),0,0,760,55,()=>{Send("equip",id);});button.GetComponentInChildren<Text>().rectTransform.sizeDelta=new Vector2(570,49);Button(button.transform,"放入快捷栏",602,5,150,45,()=>Favorite(assetId));var layout=button.gameObject.AddComponent<LayoutElement>();layout.minHeight=55;layout.preferredHeight=55;bagButtons.Add(button);}
                }
                bagText.text="你的 Persona  ·  随身 "+state.inventory.Length+" / 剩余 "+(me?.remaining??0);
                if(state.phase==MatchPhase.Finished)
                {
                    var winner=state.players.FirstOrDefault(p=>p.id==state.winner);
                    resultText.text=(winner?.nickname??"玩家")+" 保住了最后的自我。\n\n"+string.Join("\n",state.eaten.Select(e=>e.eater+"  吃掉了  "+e.owner+" 的「"+e.persona+"」"));
                    resultText.rectTransform.sizeDelta=new Vector2(865,Mathf.Max(385,resultText.preferredHeight+12));
                    inventoryOpen=false;bag.gameObject.SetActive(false);
                }
            }
            if(state.phase==MatchPhase.Lobby||state.phase==MatchPhase.Finished)return;
            if(Input.GetKeyDown(KeyCode.B))ToggleBag();
            movement=inventoryOpen?Vector2.zero:new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            if(movePad!=null&&movePad.Value.sqrMagnitude>0)movement=movePad.Value;
            var worldMove=Quaternion.Euler(0,world.CameraYaw,0)*new Vector3(movement.x,0,movement.y);movement=new Vector2(worldMove.x,worldMove.z);
            if(worldMove.sqrMagnitude>.01f)yaw=Mathf.Atan2(worldMove.x,worldMove.z)*Mathf.Rad2Deg;
            if(Input.GetMouseButton(1)){world.CameraYaw+=Input.GetAxis("Mouse X")*3;world.CameraPitch=Mathf.Clamp(world.CameraPitch-Input.GetAxis("Mouse Y")*2,10,65);}
            if(!inventoryOpen)
            {
                if(Input.GetKeyDown(KeyCode.Space))Send("jump");if(Input.GetKeyDown(KeyCode.J))Send("light");
                if(Input.GetKeyDown(KeyCode.K))Send("charge");if(Input.GetKeyUp(KeyCode.K))Send("heavy");
                if(Input.GetKeyDown(KeyCode.G))Send("grab");if(Input.GetKeyDown(KeyCode.H))Send("grab_item");
                if(Input.GetKeyDown(KeyCode.E))Send("interact");if(Input.GetKeyDown(KeyCode.Q))Send("cancel");
                for(int i=0;i<5;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))&&i<state.inventory.Length)Send("equip",QuickItems(state)[i].id);
            }
            if(UnityEngine.Time.unscaledTime>=nextSend){nextSend=UnityEngine.Time.unscaledTime+.05f;Send("");}
        }
        RectTransform Panel(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=color.a>0;return r;
        }
        Text Text(Transform parent,string value,float x,float y,float w,float h,int size,Color color,FontStyle style=FontStyle.Normal)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.color=color;text.text=value;text.fontStyle=style;text.supportRichText=false;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;return text;
        }
        Button Button(Transform parent,string label,float x,float y,float w,float h,Action action,Color? background=null,Color? foreground=null)
        {
            var p=Panel(parent,label,x,y,w,h,background??new Color(.88f,.9f,.87f));var b=p.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>action());
            var t=Text(p,label,8,3,w-16,h-6,20,foreground??ink);t.alignment=TextAnchor.MiddleCenter;return b;
        }
        InputField Field(Transform parent,float x,float y,float w,float h,string placeholder,bool multi)
        {
            var panel=Panel(parent,"Input",x,y,w,h,new Color(.92f,.92f,.89f));var input=panel.gameObject.AddComponent<InputField>();
            var text=Text(panel,"",12,4,w-24,h-8,20,ink);var hint=Text(panel,placeholder,12,4,w-24,h-8,20,new Color(.45f,.49f,.49f));
            input.textComponent=text;input.placeholder=hint;input.characterLimit=multi?8000:256;input.lineType=multi?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;return input;
        }
    }
}
