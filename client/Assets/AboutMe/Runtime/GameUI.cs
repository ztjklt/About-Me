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
    [Serializable] class IntroductionRecognition {public string introduction;}
    [Serializable] class ServiceHealth {public bool ok,aiConfigured,asrConfigured,visionConfigured;}
    [Serializable] class ErrorDetail {public string detail;}
    [Serializable] class WechatLoginRequest {public string code,nickname;}
    public class TouchPad : MonoBehaviour, IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public Vector2 Value; public bool CameraPad; public Action<Vector2> OnLook;
        Vector2 origin,knobRest;int pointerId=int.MinValue;
        RectTransform knob;public RectTransform Knob {get=>knob;set{knob=value;if(knob)knobRest=knob.anchoredPosition;}} public Text Hint; public bool Pressed {get;private set;}
        public void Reset(){Value=Vector2.zero;Pressed=false;pointerId=int.MinValue;if(Knob)Knob.anchoredPosition=knobRest;}
        void OnDisable(){Reset();}
        public void ShowRun(bool run){if(Hint)Hint.text=run?"奔跑中":"向前推满奔跑";}
        public void OnPointerDown(PointerEventData e){if(Pressed)return;pointerId=e.pointerId;origin=e.position;Value=Vector2.zero;Pressed=true;}
        public void OnDrag(PointerEventData e)
        {
            if(!Pressed||e.pointerId!=pointerId)return;
            if(CameraPad)OnLook?.Invoke(e.delta);
            else{float scale=transform.lossyScale.x;Value=Vector2.ClampMagnitude((e.position-origin)/(70*Mathf.Max(.1f,scale)),1);if(Knob)Knob.anchoredPosition=knobRest+Value*48;}
        }
        public void OnPointerUp(PointerEventData e){if(e.pointerId==pointerId)Reset();}

    }
    public class PressButton : MonoBehaviour,IPointerDownHandler,IPointerUpHandler
    {
        public Action Down,Up;
        bool pressed;
        public void OnPointerDown(PointerEventData e){if(pressed)return;pressed=true;Down?.Invoke();}
        public void OnPointerUp(PointerEventData e){if(!pressed)return;pressed=false;Up?.Invoke();}
        void OnDisable(){if(pressed){pressed=false;Up?.Invoke();}}
    }
    public partial class GameUI : MonoBehaviour
    {
        public static GameUI UI;
        GameNetwork net;ToyWorld world;PlatformBridge platform;
        Font font;RectTransform canvas,home,lobby,hud,results,settings,bag,touch;
        InputField nickname,introduction,room,endpoint,api;
        Text status,profileText,lobbyText,hudText,eventText,resultText,bagText,recordLabel;
        Button generate,ready,start,join,imageUpload;
        readonly List<Button> slots=new List<Button>();
        readonly List<Button> bagButtons=new List<Button>();
        readonly List<string> favorites=new List<string>();
        Profile profile;string lastInventory="";int lastEvent;
        bool busy,inventoryOpen,recording,optionsOpen,pendingRoom,aiCapabilityKnown,aiConfigured;float nextSend,nextUI;
        string selectedColor="#79CFC5";int outfit;
        Vector2 movement;float yaw;TouchPad movePad;
        readonly LocomotionInput locomotionInput=new LocomotionInput(); bool running,hasFocus=true;
        bool escapeHeld,touchEscapeHeld,cursorReleased,skipMouseFrame;
        void ResetMovement(){escapeHeld=touchEscapeHeld=false;movement=Vector2.zero;running=false;locomotionInput.Reset();if(movePad)movePad.Reset();}
        void OnApplicationFocus(bool focus){hasFocus=focus;if(!focus){cursorReleased=true;ReleaseCursor();ResetMovement();if(net)Send("cancel");}}
        public void Build(GameNetwork network,ToyWorld toyWorld)
        {
            UI=this;net=network;world=toyWorld;platform=gameObject.AddComponent<PlatformBridge>();
            favorites.AddRange(PlayerPrefs.GetString("favorites","").Split(',').Where(x=>x!=""));
            font=Resources.Load<Font>("AboutMeFont");if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"PingFang SC","Arial","Microsoft YaHei"},20);
            BuildArtUI();BuildMenus();
            var sessionEndpoint=Environment.GetEnvironmentVariable("ABOUTME_GAME_ENDPOINT");if(!string.IsNullOrEmpty(sessionEndpoint))endpoint.text=sessionEndpoint;
            net.Endpoint=endpoint.text;net.ApiUrl=api.text;
            net.Token=PlayerPrefs.GetString("token","");if(net.Token!=""&&!Environment.GetCommandLineArgs().Contains("--ui-preview"))StartCoroutine(LoadProfile());
            platform.Initialize();
            RefreshTouchVisibility();ConfigureUIPreview();StartCoroutine(CheckService());
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
                OpenQrOverlay(DownloadHandlerTexture.GetContent(req),net.State.room,true);
            }
        }
        void DisplayProfile()
        {LoadAppearance();profileText.text=(net.Sample?"[样例测试 · 非 AI 生成]\n":"")+"PERSONA × "+profile.personas.Length+"\n"+string.Join("  /  ",profile.personas.Select(p=>p.name));profileText.rectTransform.sizeDelta=new Vector2(554,Mathf.Max(72,profileText.preferredHeight));CacheProfile();}
        IEnumerator LoadProfile()
        {
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/profiles/me",null,net.Token))
            {yield return req.SendWebRequest();if(req.result==UnityWebRequest.Result.Success&&!net.Sample&&!busy&&net.State==null){profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);DisplayProfile();TryAutoJoin();}else if(profile==null)net.Status="在线角色暂时无法读取；仍可使用练习道具进行单人游戏。";}
        }
        IEnumerator Generate()
        {
            if(busy)yield break;
            if(string.IsNullOrWhiteSpace(introduction.text)||introduction.text.Trim().Length<3){net.Status="请先写至少 3 个字的自我介绍。";yield break;}
            if(introduction.text.Length>1200){net.Status="自我介绍最多 1200 字，请精简后再生成。";yield break;}
            if(aiCapabilityKnown&&!aiConfigured){net.Status="真实 AI 尚未配置；请配置 server/.env，或点击“本地样例（无需 AI）”继续测试。";yield break;}
            busy=true;SaveDraft(false);net.Status="正在把你变成游戏内容…";
            string generationToken=PlayerPrefs.GetString("token","");
            net.ApiUrl=api.text.Trim().TrimEnd('/');
            if(string.IsNullOrEmpty(generationToken))
            {
                using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/sessions",JsonUtility.ToJson(new SessionRequest{nickname=nickname.text})))
                {
                    yield return req.SendWebRequest();
                    if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                    generationToken=JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text).token;PlayerPrefs.SetString("token",generationToken);
                }
            }
            var body=new GenerationRequest{nickname=nickname.text,introduction=introduction.text,color=selectedColor,outfit=outfit};
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/personas/generate",JsonUtility.ToJson(body),generationToken))
            {
                req.timeout=100;yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                net.Token=generationToken;net.Sample=false;profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);DisplayProfile();
                PlayerPrefs.SetString("introduction",introduction.text);PlayerPrefs.SetString("nickname",nickname.text);PlayerPrefs.Save();net.Status="你的 Persona 已保存，下次仍可继续使用。";
            }
            busy=false;
            TryAutoJoin();
        }
        IEnumerator CheckService()
        {
            net.ApiUrl=api.text.Trim().TrimEnd('/');
            using(var req=UnityWebRequest.Get(net.ApiUrl+"/health"))
            {
                req.timeout=4;yield return req.SendWebRequest();
                if(req.result==UnityWebRequest.Result.Success)
                {
                    var health=JsonUtility.FromJson<ServiceHealth>(req.downloadHandler.text);aiCapabilityKnown=true;aiConfigured=health!=null&&health.aiConfigured;
                    SetAIServiceStatus(aiConfigured?"真实 AI 已连接":"真实 AI 未配置",aiConfigured?UITheme.Mint:UITheme.Yellow);
                }
                else{aiCapabilityKnown=false;aiConfigured=false;SetAIServiceStatus("AI 服务不可达",UITheme.Red);}
            }
        }
        void SetAIServiceStatus(string value,Color color){if(aiServiceText)aiServiceText.text=value;if(aiServiceSurface)aiServiceSurface.color=color;}
        void Fail(UnityWebRequest request)
        {
            busy=false;string message=request.error;
            if(request.downloadHandler!=null&&!string.IsNullOrEmpty(request.downloadHandler.text))
            {try{var parsed=JsonUtility.FromJson<ErrorDetail>(request.downloadHandler.text);message=string.IsNullOrWhiteSpace(parsed?.detail)?request.downloadHandler.text:parsed.detail;}catch(Exception){message=request.downloadHandler.text;}}
            net.Status="服务请求失败："+message;
        }
        void Enter()
        {
            if(busy)return;
            if(profile==null||profile.personas.Length==0){ShowPage(MenuPage.Presets);net.Status="请先生成 Persona，或明确选择样例测试";return;}
            net.Nickname=nickname.text;net.RoomCode=room.text.Trim();net.Endpoint=endpoint.text.Trim();net.Connect();
        }
        void Leave(){net.Close();lastInventory="";lastEvent=0;lastResultVisual="";inventoryOpen=false;bag.gameObject.SetActive(false);ShowPage(MenuPage.Main);}
        void ToggleBag(){inventoryOpen=!inventoryOpen;bag.gameObject.SetActive(inventoryOpen);if(inventoryOpen)Send("cancel");}
        public void SetRoom(string value){ShowPage(MenuPage.Multiplayer);room.text=value;pendingRoom=true;TryAutoJoin();}
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
        void PickIntroductionImage()
        {
            if(busy)return;
            net.Status="请选择包含自我介绍的图片…";
            platform.PickIntroductionImage();
        }
        public void IntroductionImage(byte[] data,string mime)
        {StartCoroutine(RecognizeIntroductionImage(data,mime));}
        IEnumerator RecognizeIntroductionImage(byte[] data,string mime)
        {
            if(busy)yield break;
            if(data==null||data.Length==0){net.Status="图片数据不可用，请重新选择。";yield break;}
            if(data.Length>10*1024*1024){net.Status="图片不能超过 10 MB，请重新选择。";yield break;}
            busy=true;net.ApiUrl=api.text.Trim().TrimEnd('/');net.Status="正在用 DeepSeek V4 Flash Vision 识别自我介绍…";
            if(string.IsNullOrEmpty(net.Token)||net.Sample)
            {
                using(var session=GameNetwork.ApiRequest(net.ApiUrl+"/sessions",JsonUtility.ToJson(new SessionRequest{nickname=nickname.text})))
                {
                    yield return session.SendWebRequest();if(session.result!=UnityWebRequest.Result.Success){Fail(session);yield break;}
                    net.Token=JsonUtility.FromJson<SessionResponse>(session.downloadHandler.text).token;net.Sample=false;profile=null;PlayerPrefs.SetString("token",net.Token);
                }
            }
            var form=new WWWForm();form.AddBinaryData("file",data,"introduction-image"+ImageExtension(mime),mime);
            using(var req=UnityWebRequest.Post(net.ApiUrl+"/introduction/recognize-image",form))
            {
                req.timeout=70;req.SetRequestHeader("Authorization","Bearer "+net.Token);yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                var result=JsonUtility.FromJson<IntroductionRecognition>(req.downloadHandler.text);
                if(result==null||string.IsNullOrWhiteSpace(result.introduction)){net.Status="没有识别到自我介绍，请换一张更清晰的图片。";busy=false;yield break;}
                introduction.text=result.introduction;SaveDraft(false);net.Status="图片识别完成，已回填当前自我介绍草稿。";
            }
            busy=false;
        }
        static string ImageExtension(string mime)
        {
            switch(mime){case "image/jpeg":return ".jpg";case "image/png":return ".png";case "image/gif":return ".gif";case "image/webp":return ".webp";default:return ".img";}
        }
        void Send(string action,string item=""){if(CombatInputAudit.Enabled&&action!="")Debug.Log("COMBAT_INPUT_ACTION "+action);net.Send(action,item,movement,yaw,running,escapeHeld);}
        void ReleaseCursor(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;skipMouseFrame=true;}
        static readonly KeyCode[] directionKeys={KeyCode.W,KeyCode.A,KeyCode.S,KeyCode.D};
        static int DirectionMask(bool down)
        {
            int mask=0;
            for(int i=0;i<4;i++)if(down?Input.GetKeyDown(directionKeys[i]):Input.GetKey(directionKeys[i]))mask|=1<<i;
            return mask;
        }
        void UpdateBattleCamera(bool allowed)
        {
            bool desktop=!(touch&&touch.gameObject.activeSelf);
            if(!allowed||!desktop){ReleaseCursor();return;}
            if(cursorReleased&&Input.GetMouseButtonDown(0)&&!(EventSystem.current&&EventSystem.current.IsPointerOverGameObject()))cursorReleased=false;
            if(cursorReleased){ReleaseCursor();return;}
            if(Cursor.lockState!=CursorLockMode.Locked){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;skipMouseFrame=true;}
            if(skipMouseFrame){skipMouseFrame=false;return;}
            world.CameraYaw+=Input.GetAxis("Mouse X")*3*cameraSensitivity;
            world.CameraPitch=Mathf.Clamp(world.CameraPitch-Input.GetAxis("Mouse Y")*2*cameraSensitivity,10,65);
        }
        void Update()
        {
            if(net==null)return;
            generate.interactable=!busy;join.interactable=!busy;if(imageUpload)imageUpload.interactable=!busy;foreach(var button in wardrobeButtons)button.interactable=!busy;ApplyBusyVisuals();UpdateStatusVisual();ApplyAdaptiveLayout();
            var state=net.State;
            UpdateMenuVisibility();
            if(Input.GetKeyDown(KeyCode.Escape)){if(inventoryOpen)ToggleBag();else if(optionsOpen){optionsOpen=false;settings.gameObject.SetActive(false);}else if(wardrobe&&wardrobe.gameObject.activeSelf)CloseWardrobe();else if(state==null)ShowPage(MenuPage.Main);else {cursorReleased=!cursorReleased;if(cursorReleased){ResetMovement();Send("cancel");}}}
            lobby.gameObject.SetActive(state!=null&&state.phase==MatchPhase.Lobby);
            hud.gameObject.SetActive(state!=null&&state.phase!=MatchPhase.Lobby&&state.phase!=MatchPhase.Finished);
            results.gameObject.SetActive(state!=null&&state.phase==MatchPhase.Finished);
            if(state==null){ResetMovement();ReleaseCursor();cursorReleased=false;PositionStatus(null);return;}
            if(replay)replay.gameObject.SetActive(net.IsSolo);
            if(reconnectButton)reconnectButton.gameObject.SetActive(!net.IsSolo);
            if(wardrobe&&wardrobe.gameObject.activeSelf)CloseWardrobe();
            if(UnityEngine.Time.unscaledTime>=nextUI)
            {
                nextUI=UnityEngine.Time.unscaledTime+.1f;
                start.gameObject.SetActive(state.you==state.host);start.interactable=state.players.Length>=2;
                if(startStyle)startStyle.SetState(start.interactable?UIVisualState.Selected:UIVisualState.Normal);
                var me=state.players.FirstOrDefault(p=>p.id==state.you);
                if(state.events.Length>0&&state.events.Last().id!=lastEvent){lastEvent=state.events.Last().id;eventText.text=state.events.Last().text;toastPulse.Play();}
                string inventory=string.Join("|",state.inventory.Select(x=>x.id+":"+x.state));
                if(inventory!=lastInventory)
                {
                    lastInventory=inventory;
                    var quick=QuickItems(state);
                    for(int i=0;i<slots.Count;i++)
                    {
                        slots[i].interactable=i<quick.Length;slots[i].GetComponentInChildren<Text>().text=i<quick.Length?(i+1)+"  "+ShortPersonaName(quick[i].asset.name):(i+1)+"  —";
                        if(i<slotStyles.Count)slotStyles[i].SetState(i<quick.Length&&quick[i].state==ItemState.Equipped?UIVisualState.Selected:UIVisualState.Normal);
                    }
                    BuildInventoryCards(state);
                }
                bagText.text="你的 Persona  ·  随身 "+state.inventory.Length+" / 剩余 "+(me?.remaining??0);
                if(state.phase==MatchPhase.Finished)
                {
                    inventoryOpen=false;bag.gameObject.SetActive(false);
                }
                UpdateVisualUI(state,me);
            }
            if(state.phase==MatchPhase.Lobby||state.phase==MatchPhase.Finished){ResetMovement();ReleaseCursor();cursorReleased=false;return;}
            if(Input.GetKeyDown(KeyCode.B))ToggleBag();
            bool typing=EventSystem.current&&EventSystem.current.currentSelectedGameObject&&EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
            var controlled=state.players.FirstOrDefault(p=>p.id==state.you);
            bool controls=hasFocus&&!inventoryOpen&&!optionsOpen&&!typing&&controlled!=null&&!controlled.eliminated&&!controlled.reveal;
            UpdateBattleCamera(hasFocus&&!inventoryOpen&&!optionsOpen&&!typing&&controlled!=null&&!controlled.reveal);
            bool canMove=controls&&!cursorReleased&&controlled.stagger<=0;
            bool canAct=controls&&!cursorReleased&&controlled.stagger<=0;
            bool usingTouch=movePad&&movePad.Pressed;
            escapeHeld=controls&&(touchEscapeHeld||Input.GetKey(KeyCode.Q));
            movement=canMove?(usingTouch?movePad.Value:new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"))):Vector2.zero;
            movement=Vector2.ClampMagnitude(movement,1);
            // Backward keyboard travel uses the forward locomotion clip: keep the
            // world-space route, but turn the character toward that route first.
            bool walkingBackward=!usingTouch&&Input.GetKey(KeyCode.S)&&!Input.GetKey(KeyCode.W)&&movement.y<-.01f;
            running=locomotionInput.Step(Time.unscaledTime,Time.unscaledDeltaTime,DirectionMask(true),DirectionMask(false),movement,usingTouch,canMove&&!controlled.grabbed&&string.IsNullOrEmpty(controlled.interaction)&&string.IsNullOrEmpty(controlled.grabbing)&&controlled.charge<=0);
            if(movePad){movePad.ShowRun(running);if(!canMove)movePad.Reset();}
            var worldMove=Quaternion.Euler(0,world.CameraYaw,0)*new Vector3(movement.x,0,movement.y);movement=new Vector2(worldMove.x,worldMove.z);
            yaw=world.CameraYaw+(walkingBackward?180:0);
            if(controls&&Input.GetKeyDown(KeyCode.Q))Send(controlled.grabbed?"escape":"cancel");
            if(Input.GetKeyUp(KeyCode.Q))Send("escape_end");
            if(canAct)
            {
                if(Input.GetKeyDown(KeyCode.Space))Send("jump");if(Input.GetKeyDown(KeyCode.J))Send("light");
                if(Input.GetKeyDown(KeyCode.K))Send("charge");if(Input.GetKeyUp(KeyCode.K))Send("heavy");
                if(Input.GetKeyDown(KeyCode.G))Send("grab");if(Input.GetKeyDown(KeyCode.H))Send("grab_item");
                if(Input.GetKeyDown(KeyCode.E))Send("interact");
                for(int i=0;i<5;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))){var quick=QuickItems(state);if(i<quick.Length)Send("equip",quick[i].id);}
            }
            if(UnityEngine.Time.unscaledTime>=nextSend){nextSend=UnityEngine.Time.unscaledTime+.05f;Send("");}
        }
    }
}
