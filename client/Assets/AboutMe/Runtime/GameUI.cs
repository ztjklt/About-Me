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
        GameNetwork net;ToyWorld world;PlatformBridge platform;BattleMusic battleMusic;
        Font font;RectTransform canvas,home,lobby,hud,results,settings,bag,touch;
        InputField nickname,introduction,room,endpoint,api;
        Text status,profileText,lobbyText,hudText,eventText,resultText,resultEventText,bagText,recordLabel;
        Button generate,ready,start,join,imageUpload;
        readonly List<Button> slots=new List<Button>();
        readonly List<string> favorites=new List<string>();
        Profile profile;string lastInventory="",lastEventMatch="";int lastEvent;
        string webPracticeToken;
        bool busy,inventoryOpen,recording,optionsOpen,pendingRoom,aiCapabilityKnown,aiConfigured,asrCapabilityKnown,asrConfigured;float nextSend,nextUI;
        string selectedColor="#79CFC5";int outfit;
        Vector2 movement;float yaw;TouchPad movePad;
        readonly LocomotionInput locomotionInput=new LocomotionInput(); bool running,hasFocus=true;
        bool escapeHeld,touchEscapeHeld,cursorReleased,skipMouseFrame,webLockAttempted,webDragCamera;
        Vector3 webDragPosition;
        float webLockAttemptAt;
        void ResetMovement(){escapeHeld=touchEscapeHeld=false;movement=Vector2.zero;running=false;locomotionInput.Reset();if(movePad)movePad.Reset();}
        void OnApplicationFocus(bool focus){hasFocus=focus;if(!focus){cursorReleased=true;ReleaseCursor();ResetMovement();if(net)Send("cancel");}}
        public void Build(GameNetwork network,ToyWorld toyWorld)
        {
            UI=this;net=network;world=toyWorld;platform=gameObject.AddComponent<PlatformBridge>();
            favorites.AddRange(PlayerPrefs.GetString("favorites","").Split(',').Where(x=>x!=""));
            font=Resources.Load<Font>("AboutMeFont");if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"PingFang SC","Arial","Microsoft YaHei"},20);
            BuildArtUI();BuildMenus();battleMusic=gameObject.AddComponent<BattleMusic>();
            var configuredApi=Environment.GetEnvironmentVariable("ABOUTME_API_URL");
            if(!string.IsNullOrWhiteSpace(configuredApi))api.text=configuredApi.Trim().TrimEnd('/');
            var sessionEndpoint=Environment.GetEnvironmentVariable("ABOUTME_GAME_ENDPOINT");
            if(Application.platform==RuntimePlatform.WebGLPlayer)
            {
                var webEndpoint=WebGameEndpoint(Application.absoluteURL);
                if(webEndpoint!=null)sessionEndpoint=webEndpoint;
                var webApi=WebApiEndpoint(Application.absoluteURL);
                if(webApi!=null)api.text=webApi;
                else if(Uri.TryCreate(Application.absoluteURL,UriKind.Absolute,out var page)
                    &&page.Scheme==Uri.UriSchemeHttps
                    &&Uri.TryCreate(api.text,UriKind.Absolute,out var service)
                    &&service.IsLoopback)api.text="";
            }
            if(sessionEndpoint!=null)endpoint.text=sessionEndpoint;
            net.Endpoint=endpoint.text;net.ApiUrl=api.text;
            net.Token=SavedTokenForApi();if(net.Token!=""&&net.ApiUrl!=""&&!Environment.GetCommandLineArgs().Contains("--ui-preview"))StartCoroutine(LoadProfile());
            platform.Initialize();
            RefreshTouchVisibility();ConfigureUIPreview();StartCoroutine(CheckService());
        }
        RectTransform inventoryContent;
        static string WebGameEndpoint(string absoluteUrl)
        {
            if(!Uri.TryCreate(absoluteUrl,UriKind.Absolute,out var page))return null;
            var value=WebQueryValue(page,"game");
            if(value==null)return null;
            if(Uri.TryCreate(value,UriKind.Absolute,out var game)
                &&(game.Scheme=="wss"||(page.Scheme==Uri.UriSchemeHttp&&game.Scheme=="ws"))
                &&game.UserInfo==""&&game.Fragment=="")return game.AbsoluteUri;
            return "";
        }
        static string WebApiEndpoint(string absoluteUrl)
        {
            if(!Uri.TryCreate(absoluteUrl,UriKind.Absolute,out var page))return null;
            var value=WebQueryValue(page,"api");
            if(value==null)return null;
            if(string.IsNullOrWhiteSpace(value)||!Uri.TryCreate(page,value,out var service)
                ||(service.Scheme!=Uri.UriSchemeHttps&&!(page.Scheme==Uri.UriSchemeHttp&&service.Scheme==Uri.UriSchemeHttp))
                ||!string.Equals(service.Host,page.Host,StringComparison.OrdinalIgnoreCase)
                ||service.UserInfo!=""||service.Fragment!="")return "";
            return service.AbsoluteUri.TrimEnd('/');
        }
        string SavedTokenForApi()
        {
            var savedApi=PlayerPrefs.GetString("tokenApi",PlayerPrefs.GetString("api","")).TrimEnd('/');
            return savedApi==net.ApiUrl?PlayerPrefs.GetString("token",""):"";
        }
        void SaveTokenForApi(string value)
        {
            PlayerPrefs.SetString("token",value);
            PlayerPrefs.SetString("tokenApi",net.ApiUrl);
            PlayerPrefs.Save();
        }
        static string WebQueryValue(Uri page,string key)
        {
            foreach(var part in page.Query.TrimStart('?').Split('&'))
            {
                var pair=part.Split(new[]{'='},2);
                if(pair[0]==key)return pair.Length==2?Uri.UnescapeDataString(pair[1]):"";
            }
            return null;
        }
        PublicPersonaInstance[] QuickItems(Snapshot state)=>state.inventory.OrderBy(x=>{int i=favorites.IndexOf(x.asset.assetId);return i<0?10000:i;}).ToArray();
        void Favorite(string assetId){favorites.Remove(assetId);favorites.Insert(0,assetId);if(favorites.Count>5)favorites.RemoveAt(5);PlayerPrefs.SetString("favorites",string.Join(",",favorites));lastInventory="";}
        public void WechatLogin(string code){StartCoroutine(LoginWechat(code));}
        IEnumerator LoginWechat(string code)
        {
            busy=true;
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/sessions/wechat",JsonUtility.ToJson(new WechatLoginRequest{code=code,nickname=nickname.text})))
            {
                yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                var response=JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text);net.Token=response.token;profile=response.profile;net.Sample=false;SaveTokenForApi(net.Token);DisplayProfile();TryAutoJoin();
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
        {LoadAppearance();profileText.text=(net.Sample?"[样例测试 · 非 AI 生成]\n":"")+"PERSONA × "+profile.personas.Length+"\n"+string.Join("  /  ",profile.personas.Select(p=>p.name+(string.IsNullOrEmpty(p.generatedBy)?"":" [DeepSeek 生成]")));profileText.rectTransform.sizeDelta=new Vector2(554,Mathf.Max(72,profileText.preferredHeight));CacheProfile();}
        void LoadPracticePersona()
        {
            net.Sample=true;
            if(Application.platform==RuntimePlatform.WebGLPlayer)
            {
                if(webPracticeToken==null)
                {
                    string provided=null;
                    if(Uri.TryCreate(Application.absoluteURL,UriKind.Absolute,out var page))provided=WebQueryValue(page,"practiceSession");
                    webPracticeToken=Guid.TryParseExact(provided,"N",out var parsed)?parsed.ToString("N"):Guid.NewGuid().ToString("N");
                }
                net.Token=webPracticeToken;
            }
            else
            {
                net.Token=PlayerPrefs.GetString("sampleToken",Guid.NewGuid().ToString("N"));
                PlayerPrefs.SetString("sampleToken",net.Token);
            }
            profile=GameNetwork.SampleProfile(net.Token,nickname.text);
            DisplayProfile();
            net.Status="已载入练习道具（非 AI）";
            TryAutoJoin();
        }
        IEnumerator LoadProfile()
        {
            var requestedToken=net.Token;
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/profiles/me",null,requestedToken))
            {
                yield return req.SendWebRequest();
                if(req.result==UnityWebRequest.Result.Success&&!net.Sample&&!busy&&net.State==null)
                {profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);DisplayProfile();TryAutoJoin();}
                else if(req.responseCode==401&&net.Token==requestedToken)
                {net.Token="";SaveTokenForApi("");if(profile==null)net.Status="登录已过期，请重新生成 Persona。";}
                else if(profile==null)net.Status="在线角色暂时无法读取；仍可使用练习道具进行单人游戏。";
            }
        }
        IEnumerator Generate()
        {
            if(busy)yield break;
            if(string.IsNullOrWhiteSpace(introduction.text)||introduction.text.Trim().Length<3){net.Status="请先写至少 3 个字的自我介绍。";yield break;}
            if(introduction.text.Length>1200){net.Status="自我介绍最多 1200 字，请精简后再生成。";yield break;}
            net.ApiUrl=api.text.Trim().TrimEnd('/');
            if(net.ApiUrl==""){net.Status="AI 服务尚未开放，请先在连接设置中填写服务地址。";yield break;}
            if(aiCapabilityKnown&&!aiConfigured){net.Status="真实 AI 暂不可用，请稍后重试，或选择练习道具。";yield break;}
            busy=true;SaveDraft(false);net.Status="正在把你变成游戏内容…";
            string generationToken=SavedTokenForApi();
            var body=new GenerationRequest{nickname=nickname.text,introduction=introduction.text,color=selectedColor,outfit=outfit};
            for(int attempt=0;attempt<2;attempt++)
            {
                if(string.IsNullOrEmpty(generationToken))
                {
                    using(var session=GameNetwork.ApiRequest(net.ApiUrl+"/sessions",JsonUtility.ToJson(new SessionRequest{nickname=nickname.text})))
                    {
                        yield return session.SendWebRequest();
                        if(session.result!=UnityWebRequest.Result.Success){Fail(session,"建立角色会话");yield break;}
                        generationToken=JsonUtility.FromJson<SessionResponse>(session.downloadHandler.text)?.token;
                        if(string.IsNullOrEmpty(generationToken)){busy=false;net.Status="无法建立角色会话，请稍后重试。";yield break;}
                        SaveTokenForApi(generationToken);
                    }
                }
                using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/personas/generate",JsonUtility.ToJson(body),generationToken))
                {
                    req.timeout=100;yield return req.SendWebRequest();
                    if(req.responseCode==401&&attempt==0)
                    {generationToken="";SaveTokenForApi("");continue;}
                    if(req.result!=UnityWebRequest.Result.Success){Fail(req,"Persona 生成");yield break;}
                    var generated=JsonUtility.FromJson<Profile>(req.downloadHandler.text);
                    if(generated?.personas==null||generated.personas.Length==0)
                    {busy=false;net.Status="AI 未返回可用 Persona，请修改介绍后重试。";yield break;}
                    net.Token=generationToken;net.Sample=false;profile=generated;DisplayProfile();
                    PlayerPrefs.SetString("introduction",introduction.text);PlayerPrefs.SetString("nickname",nickname.text);PlayerPrefs.Save();
                    busy=false;net.Status=profile.personas.Any(p=>!string.IsNullOrEmpty(p.generatedBy))?"DeepSeek 已完成校徽选择与完整裁切，Persona 已保存。":"你的 Persona 已保存，下次仍可继续使用。";TryAutoJoin();yield break;
                }
            }
            busy=false;net.Status="角色会话已失效，请重新尝试生成。";
        }
        IEnumerator CheckService()
        {
            net.ApiUrl=api.text.Trim().TrimEnd('/');
            if(net.ApiUrl=="")
            {
                aiCapabilityKnown=true;aiConfigured=false;asrCapabilityKnown=true;asrConfigured=false;
                SetAIServiceStatus("AI / 角色服务本次试玩未开放",UITheme.Yellow);
                yield break;
            }
            using(var req=UnityWebRequest.Get(net.ApiUrl+"/health"))
            {
                req.timeout=4;yield return req.SendWebRequest();
                if(req.result==UnityWebRequest.Result.Success)
                {
                    var health=JsonUtility.FromJson<ServiceHealth>(req.downloadHandler.text);aiCapabilityKnown=true;aiConfigured=health!=null&&health.aiConfigured;asrCapabilityKnown=true;asrConfigured=health!=null&&health.asrConfigured;
                    SetAIServiceStatus(aiConfigured?(health.visionConfigured?"DeepSeek 文本 + Vision 已连接":"真实 AI 已连接，Vision 未配置"):"真实 AI 未配置",aiConfigured&&health.visionConfigured?UITheme.Mint:UITheme.Yellow);
                }
                else{aiCapabilityKnown=false;aiConfigured=false;asrCapabilityKnown=false;asrConfigured=false;SetAIServiceStatus("AI 服务不可达",UITheme.Red);}
            }
        }
        void SetAIServiceStatus(string value,Color color){if(aiServiceText)aiServiceText.text=value;if(aiServiceSurface)aiServiceSurface.color=color;}
        void SaveConnectionSettings()
        {
            var nextApi=api.text.Trim().TrimEnd('/');
            var apiChanged=net.ApiUrl!=nextApi;
            net.Endpoint=endpoint.text.Trim();net.ApiUrl=nextApi;
            if(apiChanged)
            {
                aiCapabilityKnown=false;asrCapabilityKnown=false;asrConfigured=false;
                if(!net.Sample){net.Token=SavedTokenForApi();profile=null;RefreshMenuSummary();}
            }
            PlayerPrefs.SetString("endpoint",net.Endpoint);PlayerPrefs.SetString("api",net.ApiUrl);PlayerPrefs.Save();
            settings.gameObject.SetActive(false);optionsOpen=false;net.Status="连接设置已保存";
            if(apiChanged&&!net.Sample&&net.Token!="")StartCoroutine(LoadProfile());
            StartCoroutine(CheckService());
        }
        void Fail(UnityWebRequest request,string operation="请求")
        {
            busy=false;
            string detail="";var body=request.downloadHandler?.text;
            if(!string.IsNullOrWhiteSpace(body))
            {try{detail=JsonUtility.FromJson<ErrorDetail>(body)?.detail??"";}catch(Exception){}}
            if(string.IsNullOrWhiteSpace(detail))detail=request.error??"";
            detail=detail.Replace('\n',' ').Replace('\r',' ').Trim();if(detail.Length>160)detail=detail.Substring(0,160)+"…";
            string path="",address="API";
            if(Uri.TryCreate(request.url,UriKind.Absolute,out var uri))
            {path=uri.AbsolutePath;address=uri.Host+(uri.IsDefaultPort?"":":"+uri.Port);}
            if(request.result==UnityWebRequest.Result.ConnectionError)
                net.Status=operation+"失败：无法连接 "+address+"。请启动本机 API，或在设置中核对服务地址。";
            else net.Status=operation+"失败（HTTP "+request.responseCode+"）："+(detail==""?"请稍后重试。":detail);
            Debug.LogWarning("ABOUTME_API_FAILURE op="+operation+" path="+path+" status="+request.responseCode+" result="+request.result+" detail="+detail);
        }
        string CurrentRoomCode()
        {
            var bridge=room.GetComponent<IMEInputBridge>();
            return bridge?bridge.CurrentText():room.text;
        }
        void SetRoomCode(string value)
        {
            var bridge=room.GetComponent<IMEInputBridge>();
            if(bridge)bridge.SetText(value);else room.text=value;
        }
        void CreateRoom(){Enter("");}
        void JoinRoom()
        {
            var code=(CurrentRoomCode()??"").Trim().ToUpperInvariant();
            if(code.Length!=6){net.Status="请输入 6 位房间码（当前 "+code.Length+" 位）";return;}
            Enter(code);
        }
        void Enter(string requestedRoom=null)
        {
            if(busy)return;
            if(profile==null||profile.personas.Length==0){ShowPage(MenuPage.Presets);net.Status="请先生成 Persona，或明确选择样例测试";return;}
            net.Nickname=nickname.text;net.RoomCode=(requestedRoom??CurrentRoomCode()).Trim().ToUpperInvariant();SetRoomCode(net.RoomCode);net.Endpoint=endpoint.text.Trim();net.Connect();
        }
        void Leave(){net.Close();lastInventory="";lastEvent=0;lastEventMatch="";lastResultVisual="";inventoryOpen=false;inventoryWasOpened=false;bag.gameObject.SetActive(false);ShowPage(MenuPage.Main);}
        void ToggleBag()
        {
            inventoryOpen=!inventoryOpen;bag.gameObject.SetActive(inventoryOpen);
            if(!inventoryOpen)return;
            if(!inventoryWasOpened&&inventoryScroll){Canvas.ForceUpdateCanvases();inventoryScroll.verticalNormalizedPosition=1;inventoryWasOpened=true;}
            Send("cancel");
        }
        public void SetRoom(string value){ShowPage(MenuPage.Multiplayer);SetRoomCode(value);pendingRoom=true;TryAutoJoin();}
        void TryAutoJoin(){if(pendingRoom&&profile!=null&&profile.personas.Length>0){pendingRoom=false;Enter();}}
        void ToggleRecording()
        {
            if(recording){platform.StopRecording();recording=false;recordLabel.text="录音介绍";return;}
            if(!asrCapabilityKnown||!asrConfigured){net.Status="语音识别尚未配置，请使用文字或图片介绍。";return;}
            StartCoroutine(PrepareRecording());
        }
        IEnumerator PrepareRecording()
        {
            if(busy)yield break;
            if(!asrCapabilityKnown||!asrConfigured){net.Status="语音识别尚未配置，请使用文字或图片介绍。";yield break;}
            if(net.Token==""||net.Sample)
            {
                busy=true;net.ApiUrl=api.text.Trim().TrimEnd('/');
                using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/sessions",JsonUtility.ToJson(new SessionRequest{nickname=nickname.text})))
                {
                    yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Fail(req);yield break;}
                    net.Token=JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text).token;net.Sample=false;profile=null;SaveTokenForApi(net.Token);
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
                    net.Token=JsonUtility.FromJson<SessionResponse>(session.downloadHandler.text).token;net.Sample=false;profile=null;SaveTokenForApi(net.Token);
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
        void ReleaseCursor(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;skipMouseFrame=true;webDragCamera=false;}
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
            if(Cursor.lockState!=CursorLockMode.Locked)
            {
                if(Application.platform!=RuntimePlatform.WebGLPlayer||!webLockAttempted)
                {
                    Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;skipMouseFrame=true;
                    if(Application.platform==RuntimePlatform.WebGLPlayer){webLockAttempted=true;webLockAttemptAt=Time.unscaledTime;}
                }
                if(Cursor.lockState!=CursorLockMode.Locked&&Application.platform==RuntimePlatform.WebGLPlayer)
                {
                    // Embedded browsers may deny pointer lock. Right drag remains usable
                    // without repeatedly requesting a permission the browser rejected.
                    if(Time.unscaledTime-webLockAttemptAt<.4f)return;
                    Cursor.visible=true;
                    if(Input.GetMouseButtonDown(1)){webDragCamera=true;webDragPosition=Input.mousePosition;}
                    if(Input.GetMouseButtonUp(1))webDragCamera=false;
                    if(webDragCamera&&Input.GetMouseButton(1))
                    {
                        var delta=Input.mousePosition-webDragPosition;webDragPosition=Input.mousePosition;
                        world.CameraYaw+=delta.x*.22f*cameraSensitivity;
                        world.CameraPitch=Mathf.Clamp(world.CameraPitch-delta.y*.18f*cameraSensitivity,10,65);
                    }
                    return;
                }
            }
            if(skipMouseFrame){skipMouseFrame=false;return;}
            world.CameraYaw+=Input.GetAxis("Mouse X")*3*cameraSensitivity;
            world.CameraPitch=Mathf.Clamp(world.CameraPitch-Input.GetAxis("Mouse Y")*2*cameraSensitivity,10,65);
        }
        void Update()
        {
            if(net==null)return;
            generate.interactable=!busy;join.interactable=!busy;if(imageUpload)imageUpload.interactable=!busy;foreach(var button in wardrobeButtons)button.interactable=!busy;ApplyBusyVisuals();UpdateStatusVisual();ApplyAdaptiveLayout();
            var state=net.State;
            if(battleMusic)battleMusic.Sync(state,net.IsSolo||Mirror.NetworkClient.isConnected);
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
                if(state.matchId!=lastEventMatch)
                {lastEventMatch=state.matchId;lastEvent=0;if(resultEventLine)resultEventLine.gameObject.SetActive(false);}
                if(state.events.Length>0&&state.events.Last().id!=lastEvent)
                {
                    lastEvent=state.events.Last().id;
                    var line=state.events.Last().text;
                    eventText.text=line;if(resultEventText)resultEventText.text=line;
                    if(resultEventLine)resultEventLine.gameObject.SetActive(true);
                    toastPulse.Play();
                }
                string inventory=string.Join("|",state.inventory.Select(x=>x.id+":"+x.state));
                if(inventory!=lastInventory)
                {
                    lastInventory=inventory;
                    var quick=QuickItems(state);
                    for(int i=0;i<slots.Count;i++)
                    {
                        bool filled=i<quick.Length;slots[i].interactable=filled;
                        slots[i].GetComponentInChildren<Text>().text=filled?(i+1)+"\n"+ShortPersonaName(quick[i].asset.name):(i+1)+"\n—";
                        if(i<slotThumbnails.Count)
                        {
                            var image=slotThumbnails[i];var item=filled?quick[i]:null;
                            var thumbnail=item!=null?PersonaThumbnail(item.asset.visualId):null;
                            image.sprite=thumbnail?thumbnail:UITheme.Icon(item!=null?IconForTemplate(item.asset.templateId):"status");
                            image.color=thumbnail?Color.white:item!=null?ToyFactory.ColorOf(item.asset.color):UITheme.Muted;
                        }
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
            running=locomotionInput.Step(Time.unscaledTime,Time.unscaledDeltaTime,DirectionMask(true),DirectionMask(false),movement,usingTouch,canMove&&!controlled.grabbed&&string.IsNullOrEmpty(controlled.interaction)&&string.IsNullOrEmpty(controlled.grabbing)&&controlled.charge<=0);
            if(movePad){movePad.ShowRun(running);if(!canMove)movePad.Reset();}
            yaw=locomotionInput.FacingYaw(world.CameraYaw,movement);
            var worldMove=Quaternion.Euler(0,world.CameraYaw,0)*new Vector3(movement.x,0,movement.y);movement=new Vector2(worldMove.x,worldMove.z);
            if(controls&&Input.GetKeyDown(KeyCode.Q))Send(controlled.grabbed?"escape":"cancel");
            if(Input.GetKeyUp(KeyCode.Q))Send("escape_end");
            if(canAct)
            {
                if(Input.GetKeyDown(KeyCode.Space))Send("jump");if(Input.GetKeyDown(KeyCode.F))Send("use_item");
                if(Input.GetKeyDown(KeyCode.G))Send("grab");if(Input.GetKeyDown(KeyCode.H))Send("grab_item");
                if(Input.GetKeyDown(KeyCode.E))Send("interact");
                for(int i=0;i<5;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))){var quick=QuickItems(state);if(i<quick.Length)Send("equip",quick[i].id);}
            }
            if(UnityEngine.Time.unscaledTime>=nextSend){nextSend=UnityEngine.Time.unscaledTime+.05f;Send("");}
        }
    }
}
