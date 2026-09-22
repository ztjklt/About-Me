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
    [Serializable] class ServiceHealth {public bool ok,aiConfigured,asrConfigured;}
    [Serializable] class ErrorDetail {public string detail;}
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
    public partial class GameUI : MonoBehaviour
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
        bool busy,inventoryOpen,recording,optionsOpen,pendingRoom,aiCapabilityKnown,aiConfigured;float nextSend,nextUI;
        string selectedColor="#79CFC5";int outfit;
        Vector2 movement;float yaw;TouchPad movePad;
        public void Build(GameNetwork network,ToyWorld toyWorld)
        {
            UI=this;net=network;world=toyWorld;platform=gameObject.AddComponent<PlatformBridge>();
            favorites.AddRange(PlayerPrefs.GetString("favorites","").Split(',').Where(x=>x!=""));
            font=Resources.Load<Font>("AboutMeFont");if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"PingFang SC","Arial","Microsoft YaHei"},20);
            BuildArtUI();
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
        {profileText.text=(net.Sample?"[样例测试 · 非 AI 生成]\n":"")+"PERSONA × "+profile.personas.Length+"\n"+string.Join("  /  ",profile.personas.Select(p=>p.name));}
        IEnumerator LoadProfile()
        {
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/profiles/me",null,net.Token))
            {yield return req.SendWebRequest();if(req.result==UnityWebRequest.Result.Success){profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);DisplayProfile();TryAutoJoin();}else net.Status="已保存的角色暂时无法读取，请检查服务地址";}
        }
        IEnumerator Generate()
        {
            if(busy)yield break;
            if(string.IsNullOrWhiteSpace(introduction.text)||introduction.text.Trim().Length<3){net.Status="请先写至少 3 个字的自我介绍。";yield break;}
            if(introduction.text.Length>1200){net.Status="自我介绍最多 1200 字，请精简后再生成。";yield break;}
            if(aiCapabilityKnown&&!aiConfigured){net.Status="真实 AI 尚未配置；请配置 server/.env，或点击“本地样例（无需 AI）”继续测试。";yield break;}
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
            generate.interactable=!busy;join.interactable=!busy;ApplyBusyVisuals();UpdateStatusVisual();ApplyAdaptiveLayout();
            var state=net.State;
            home.gameObject.SetActive(state==null);
            lobby.gameObject.SetActive(state!=null&&state.phase==MatchPhase.Lobby);
            hud.gameObject.SetActive(state!=null&&state.phase!=MatchPhase.Lobby&&state.phase!=MatchPhase.Finished);
            results.gameObject.SetActive(state!=null&&state.phase==MatchPhase.Finished);
            if(state==null){PositionStatus(null);return;}
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
                        slots[i].interactable=i<quick.Length;slots[i].GetComponentInChildren<Text>().text=i<quick.Length?(i+1)+"  "+quick[i].asset.name:(i+1)+"  —";
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
    }
}
