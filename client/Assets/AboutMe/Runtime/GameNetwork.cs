using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Mirror;
using Mirror.SimpleWeb;
using UnityEngine;
using UnityEngine.Networking;

namespace AboutMe
{
    [Serializable] class HelloVersion { public int protocol; }
    public struct HelloMessage : NetworkMessage { public string token, cosmeticToken, room, nickname, appearanceJson; public bool sample; }
    public struct CommandMessage : NetworkMessage { public string json; }
    public struct StateMessage : NetworkMessage { public int frame;public bool last;public byte[] data; }
    public struct NoticeMessage : NetworkMessage { public string text; }
    [Serializable] public class TokenRequest { public string token; }
    [Serializable] public class ResultPayload { public string matchId, winner, endReason; public string[] winners; public MatchRank[] rankings; public EatRecord[] eaten; }
    [Serializable] public class RoastRequestPayload { public string match_id, event_id, item_name, template_id, visual_id; }
    [Serializable] public class RoastResponsePayload { public string roast, event_id, aiSource; }

    public class GameNetwork : NetworkManager, IMatchSession
    {
        public static GameNetwork Game;
        public string ApiUrl = "http://127.0.0.1:8081";
        public string Endpoint = "ws://127.0.0.1:7777", RoomCode = "", Token = "", CosmeticToken = "", Nickname = "玩家";
        public bool Sample, AllowSamples;
        public AppearanceData SampleAppearance;
        public Snapshot State;
        LocalMatchSession localSession;
        bool localResultSaved;
        public bool IsSolo => localSession!=null;
        public Snapshot Current => State;
        public void StartSolo(Profile profile)
        {
            Close();localSession=new LocalMatchSession(profile);sequence=0;localResultSaved=false;State=localSession.Current;Status="离线对战 · 陪练 NPC";
        }
        public void Submit(Command command)
        {
            command.sequence=++sequence;
            if(localSession!=null){localSession.Submit(command);return;}
            if(NetworkClient.isConnected&&State!=null)NetworkClient.Send(new CommandMessage{json=JsonUtility.ToJson(command)});
        }
        public void Close()
        {
            localSession?.Close();localSession=null;
            if(NetworkClient.active)StopClient();State=null;sequence=0;
        }
        public string Status = "";
        public MatchRules Rules = new MatchRules();
        readonly Dictionary<string, Match> rooms = new Dictionary<string, Match>();
        readonly Dictionary<int, (Match match, string player)> seats = new Dictionary<int, (Match,string)>();
        readonly HashSet<int> pending = new HashSet<int>();
        readonly Dictionary<int,float> joins = new Dictionary<int,float>();
        readonly Dictionary<string,float> emptyRooms = new Dictionary<string,float>();
        readonly HashSet<string> saved = new HashSet<string>();
        readonly Dictionary<string,int> roastCursor = new Dictionary<string,int>();
        const float ReconnectGraceSeconds = 120f;
        float reconnectGrace = ReconnectGraceSeconds;
        float nextSnapshot;
        int sequence;
        int frame,receivingFrame=-1;
        readonly MemoryStream receiveBuffer=new MemoryStream();

        public override void Awake()
        {
            autoCreatePlayer=false; dontDestroyOnLoad=true; runInBackground=true; maxConnections=64;
            Game=this; transport=GetComponent<SimpleWebTransport>();
            var sw=(SimpleWebTransport)transport;sw.port=ushort.TryParse(Environment.GetEnvironmentVariable("ABOUTME_GAME_PORT"),out var configuredPort)&&configuredPort>0?configuredPort:(ushort)7777;sw.maxMessageSize=256*1024;
            base.Awake();
        }
        public override void Start()
        {
            base.Start();
            string api=Environment.GetEnvironmentVariable("ABOUTME_API_URL");if(!string.IsNullOrEmpty(api))ApiUrl=api;
            var args=Environment.GetCommandLineArgs();
            AllowSamples=args.Contains("--allow-samples");
            if(args.Contains("--accelerated")){Rules=new MatchRules{revealDuration=2,handInterval=4,finalTime=8,eatDuration=.2f,recoverDuration=.15f};reconnectGrace=3f;}
            if(args.Contains("--aboutme-server")) { Application.targetFrameRate=50;StartServer();Status="Server listening :"+((SimpleWebTransport)transport).port; }
        }
        public void Connect()
        {
            if(localSession!=null){localSession.Close();localSession=null;}
            if(NetworkClient.active){Status="正在连接或已在房间，请先离开再重试";return;}
            try { State=null;sequence=0;Status="正在连接…";StartClient(new Uri(Endpoint)); }
            catch(Exception e) { Status="连接地址无效："+e.Message; }
        }
        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<HelloMessage>(Hello);
            NetworkServer.RegisterHandler<CommandMessage>((conn,msg)=>{
                if(!seats.TryGetValue(conn.connectionId,out var seat)||msg.json==null||msg.json.Length>2048)return;
                try { var command=JsonUtility.FromJson<Command>(msg.json); if(command!=null)seat.match.Receive(seat.player,command); }
                catch(Exception) { conn.Send(new NoticeMessage{text="无效操作消息"}); }
            });
            Debug.Log("ABOUTME_SERVER_READY websocket port 7777 samples="+AllowSamples);
        }
        public override void OnServerConnect(NetworkConnectionToClient conn) { joins[conn.connectionId]=UnityEngine.Time.realtimeSinceStartup; }
        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<StateMessage>(msg=>{
                if(msg.frame!=receivingFrame){receivingFrame=msg.frame;receiveBuffer.SetLength(0);}
                receiveBuffer.Write(msg.data,0,msg.data.Length);
                if(msg.last){bool first=State==null;var json=Encoding.UTF8.GetString(receiveBuffer.ToArray());var incoming=JsonUtility.FromJson<Snapshot>(json);if(incoming.protocolVersion!=3){Status="游戏版本不一致，请更新客户端和服务器";StopClient();return;}State=incoming;RoomCode=State.room;receiveBuffer.SetLength(0);if(first)Status="已连接 · 房间 "+State.room;}
            });
            NetworkClient.RegisterHandler<NoticeMessage>(msg=>{Status=msg.text;Debug.Log("ABOUTME_NOTICE "+msg.text);});
        }
        public override void OnClientConnect()
        {
            NetworkClient.Ready();
            NetworkClient.Send(new HelloMessage{token=Token,cosmeticToken=Sample?CosmeticToken:"",room=RoomCode,nickname=Nickname,sample=Sample,appearanceJson="{\"protocol\":3,"+(Sample?JsonUtility.ToJson(SampleAppearance??AppearanceData.Demo(0)).Substring(1):"\"client\":true}")});
        }
        public override void OnClientDisconnect() { if(State!=null)Status="连接已断开，可使用原凭据重连"; base.OnClientDisconnect(); }
        void Hello(NetworkConnectionToClient conn,HelloMessage hello)
        {
            if(pending.Contains(conn.connectionId)||seats.ContainsKey(conn.connectionId))return;
            if((hello.room??"").Length>12||(hello.token??"").Length>256||(hello.cosmeticToken??"").Length>256||(hello.nickname??"").Length>24||(hello.appearanceJson??"").Length>2048) { Reject(conn,"加入信息过长");return; }
            try { if(string.IsNullOrEmpty(hello.appearanceJson)||JsonUtility.FromJson<HelloVersion>(hello.appearanceJson)?.protocol!=3){Reject(conn,"游戏版本不一致，请更新客户端和服务器后重连");return;} }
            catch(Exception){Reject(conn,"加入信息格式错误");return;}
            pending.Add(conn.connectionId);
            if(hello.sample)
            {
                if(!AllowSamples) { Reject(conn,"此服务器未开启样例测试模式，请先生成真实 Persona");return; }
                if(string.IsNullOrEmpty(hello.token)) { Reject(conn,"缺少样例身份");return; }
                var sample=SampleProfile(hello.token,hello.nickname);
                try{sample.appearance=AppearanceData.Resolve(string.IsNullOrEmpty(hello.appearanceJson)?null:JsonUtility.FromJson<AppearanceData>(hello.appearanceJson));}
                catch(Exception){Reject(conn,"样例外观数据无效");return;}
                if(string.IsNullOrEmpty(hello.cosmeticToken))Accept(conn,hello,sample);
                else StartCoroutine(VerifySampleCosmetic(conn,hello,sample));
            }
            else StartCoroutine(Verify(conn,hello));
        }
        IEnumerator VerifySampleCosmetic(NetworkConnectionToClient conn,HelloMessage hello,Profile sample)
        {
            string key=Environment.GetEnvironmentVariable("GAME_SERVER_KEY");
            if(!string.IsNullOrEmpty(key))
            {
                using(var req=ApiRequest(ApiUrl+"/internal/verify",JsonUtility.ToJson(new TokenRequest{token=hello.cosmeticToken})))
                {
                    req.timeout=10;req.SetRequestHeader("X-Game-Key",key);yield return req.SendWebRequest();
                    if(!NetworkServer.connections.ContainsKey(conn.connectionId))yield break;
                    if(req.result==UnityWebRequest.Result.Success)
                    {
                        Profile verified=null;try{verified=JsonUtility.FromJson<Profile>(req.downloadHandler.text);}catch(Exception){}
                        if(verified!=null)
                        {sample.dsGirlUnlocked=verified.dsGirlUnlocked;sample.claudeGirlUnlocked=verified.claudeGirlUnlocked;sample.characterSkin=verified.characterSkin;}
                    }
                }
            }
            Accept(conn,hello,sample);
        }
        IEnumerator Verify(NetworkConnectionToClient conn,HelloMessage hello)
        {
            string key=Environment.GetEnvironmentVariable("GAME_SERVER_KEY");
            if(string.IsNullOrEmpty(key)) { Reject(conn,"游戏服务器尚未配置身份验证");yield break; }
            using(var req=ApiRequest(ApiUrl+"/internal/verify",JsonUtility.ToJson(new TokenRequest{token=hello.token})))
            {
                req.SetRequestHeader("X-Game-Key",key);yield return req.SendWebRequest();
                if(!NetworkServer.connections.ContainsKey(conn.connectionId))yield break;
                if(req.result!=UnityWebRequest.Result.Success) { Reject(conn,"身份验证失败，请检查 AI 服务及登录状态");yield break; }
                Profile profile=null;try {profile=JsonUtility.FromJson<Profile>(req.downloadHandler.text);}catch(Exception){}
                if(profile==null||profile.personas==null||profile.personas.Length==0) {Reject(conn,"请先完成 Persona 生成");yield break;}
                Accept(conn,hello,profile);
            }
        }
        void Accept(NetworkConnectionToClient conn,HelloMessage hello,Profile profile)
        {
            string code=(hello.room??"").Trim().ToUpperInvariant();
            if(code=="")
            {
                if(rooms.Count>=32){Reject(conn,"房间已满");return;}
                do {code=Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant();}while(rooms.ContainsKey(code));
                rooms[code]=new Match(code,Rules);
            }
            if(!rooms.TryGetValue(code,out var room)){Reject(conn,"房间不存在，请核对房间码");return;}
            if(seats.Values.Any(x=>x.player==profile.playerId)){Reject(conn,"此角色已在另一连接中使用");return;}
            if(!room.Add(profile)){Reject(conn,"房间已开局、人数已满或角色无 Persona");return;}
            pending.Remove(conn.connectionId);joins.Remove(conn.connectionId);seats[conn.connectionId]=(room,profile.playerId);
            emptyRooms.Remove(code);
            SendSnapshot(conn,room.View(profile.playerId));
            Debug.Log("ABOUTME_JOIN room="+code+" players="+room.Players.Count);
        }
        void Reject(NetworkConnectionToClient conn,string text)
        { pending.Remove(conn.connectionId);conn.Send(new NoticeMessage{text=text});StartCoroutine(CloseLater(conn)); }
        IEnumerator CloseLater(NetworkConnectionToClient conn) {yield return new WaitForSeconds(.3f);conn.Disconnect();}
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            pending.Remove(conn.connectionId);joins.Remove(conn.connectionId);
            if(seats.TryGetValue(conn.connectionId,out var seat))
            {
                seat.match.Disconnect(seat.player);seats.Remove(conn.connectionId);
                if(!seats.Values.Any(x=>x.match==seat.match))
                {
                    if(seat.match.Phase==MatchPhase.Lobby)
                    {seat.match.Dispose();rooms.Remove(seat.match.Room);emptyRooms.Remove(seat.match.Room);roastCursor.Remove(seat.match.MatchId);}
                    else emptyRooms[seat.match.Room]=UnityEngine.Time.realtimeSinceStartup;
                }
            }
            base.OnServerDisconnect(conn);
        }
        public override void OnStopServer(){foreach(var room in rooms.Values)room.Dispose();rooms.Clear();seats.Clear();pending.Clear();joins.Clear();emptyRooms.Clear();saved.Clear();roastCursor.Clear();}
        void FixedUpdate()
        {
            if(localSession!=null)
            {
                localSession.Tick(UnityEngine.Time.fixedDeltaTime);State=localSession.Current;
                if(State.phase==MatchPhase.Finished&&!localResultSaved)
                {localResultSaved=true;PlayerPrefs.SetString("lastSoloResult",JsonUtility.ToJson(State));PlayerPrefs.Save();}
            }
            if(!NetworkServer.active)return;
            float now=UnityEngine.Time.realtimeSinceStartup;
            foreach(var entry in emptyRooms.ToArray())
                if(now-entry.Value>=reconnectGrace)
                {
                    if(rooms.TryGetValue(entry.Key,out var abandoned))
                    {abandoned.Dispose();rooms.Remove(entry.Key);saved.Remove(abandoned.MatchId);roastCursor.Remove(abandoned.MatchId);Debug.Log("ABOUTME_ROOM_EXPIRED room="+entry.Key+" remaining="+rooms.Count);}
                    emptyRooms.Remove(entry.Key);
                }
            foreach(var room in rooms.Values){room.EnableBodyPhysics();room.Tick(UnityEngine.Time.fixedDeltaTime);}
            foreach(var entry in joins.ToArray())if(now-entry.Value>20&&NetworkServer.connections.TryGetValue(entry.Key,out var conn))conn.Disconnect();
            if(now<nextSnapshot)return;nextSnapshot=now+.05f;
            foreach(var entry in seats.ToArray())
                if(NetworkServer.connections.TryGetValue(entry.Key,out var conn))SendSnapshot(conn,entry.Value.match.View(entry.Value.player));
            foreach(var room in rooms.Values)
            {
                QueueRoasts(room);
                if(room.Phase==MatchPhase.Finished && !saved.Contains(room.MatchId)) {saved.Add(room.MatchId);StartCoroutine(SaveResult(room));}
            }
        }
        void QueueRoasts(Match room)
        {
            int seen=roastCursor.TryGetValue(room.MatchId,out var count)?count:0;
            roastCursor[room.MatchId]=room.Eaten.Count;
            if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAME_SERVER_KEY")))return;
            // A room containing any practice identity remains a free trial room.
            if(room.Players.Values.Any(p=>p.profile.playerId.StartsWith("sample-",StringComparison.Ordinal)))return;
            for(int i=seen;i<room.Eaten.Count;i++)
            {
                var eaten=room.Eaten[i];
                var item=room.Item(eaten.instanceId);
                if(item==null)continue;
                StartCoroutine(RequestRoast(room,new RoastRequestPayload{
                    match_id=room.MatchId,event_id=eaten.instanceId,item_name=eaten.persona,
                    template_id=item.asset.templateId,visual_id=item.asset.visualId}));
            }
        }
        IEnumerator RequestRoast(Match room,RoastRequestPayload payload)
        {
            string key=Environment.GetEnvironmentVariable("GAME_SERVER_KEY");
            using(var req=ApiRequest(ApiUrl+"/internal/roast",JsonUtility.ToJson(payload)))
            {
                req.timeout=15;
                req.SetRequestHeader("X-Game-Key",key);
                yield return req.SendWebRequest();
                if(req.result!=UnityWebRequest.Result.Success||req.responseCode<200||req.responseCode>=300)
                {Debug.LogWarning("ABOUTME_ROAST_UNAVAILABLE match="+payload.match_id+" event="+payload.event_id+" status="+req.responseCode);yield break;}
                RoastResponsePayload response=null;
                try{response=JsonUtility.FromJson<RoastResponsePayload>(req.downloadHandler.text);}catch(Exception){}
                if(response==null||response.event_id!=payload.event_id||response.aiSource!="provider"||string.IsNullOrWhiteSpace(response.roast))
                {Debug.LogWarning("ABOUTME_ROAST_INVALID match="+payload.match_id+" event="+payload.event_id);yield break;}
                string line=response.roast.Trim();
                if(line.Length>120||line.IndexOfAny(new[]{'\r','\n'})>=0)
                {Debug.LogWarning("ABOUTME_ROAST_INVALID match="+payload.match_id+" event="+payload.event_id);yield break;}
                if(rooms.TryGetValue(room.Room,out var current)&&ReferenceEquals(current,room)&&current.MatchId==payload.match_id)
                    current.Say(line);
            }
        }
        void SendSnapshot(NetworkConnectionToClient conn,Snapshot view)
        {
            // Byte chunks avoid Mirror's 64 KB string limit for variable-size inventories.
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(view));int id=++frame;
            for(int offset=0;offset<bytes.Length;offset+=32768)
            {int count=Math.Min(32768,bytes.Length-offset);var chunk=new byte[count];Buffer.BlockCopy(bytes,offset,chunk,0,count);conn.Send(new StateMessage{frame=id,last=offset+count==bytes.Length,data=chunk});}
        }
        IEnumerator SaveResult(Match room)
        {
            string key=Environment.GetEnvironmentVariable("GAME_SERVER_KEY");
            var payload=new ResultPayload{matchId=room.MatchId,winner=room.Winner,winners=room.Winners,rankings=room.Rankings,endReason=room.EndReason,eaten=room.Eaten.ToArray()};
            if(string.IsNullOrEmpty(key)){Debug.Log("ABOUTME_RESULT_LOCAL "+JsonUtility.ToJson(payload));yield break;}
            for(int attempt=0;attempt<3;attempt++)
            {
                using(var req=ApiRequest(ApiUrl+"/internal/results",JsonUtility.ToJson(payload)))
                {req.SetRequestHeader("X-Game-Key",key);yield return req.SendWebRequest();if(req.result==UnityWebRequest.Result.Success)yield break;}
                yield return new WaitForSeconds(2);
            }
            Debug.LogError("ABOUTME_RESULT_NOT_PERSISTED "+room.MatchId);
        }
        public void Send(string action="",string item="",Vector2 movement=default,float yaw=0,bool running=false,bool escapeHeld=false)
        {
            if(State==null)return;
            Submit(new Command{x=movement.x,z=movement.y,yaw=yaw,action=action,itemId=item,run=running,escapeHeld=escapeHeld});
        }
        public static UnityWebRequest ApiRequest(string url,string json=null,string token="")
        {
            var request=new UnityWebRequest(url,json==null?"GET":"POST");
            if(json!=null){request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.SetRequestHeader("Content-Type","application/json");}
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=50;
            if(token!="")request.SetRequestHeader("Authorization","Bearer "+token);
            return request;
        }
        static readonly (string templateId,string visualId)[] SampleCatalogPairs =
        {
            ("Projectile","basketball"),
            ("Disc","school-disc"),
            ("Melee","book"),
            ("HeavyWeapon","computer"),
            ("Utility","camera"),
            ("Deployable","plant"),
            ("Summon","cat"),
            ("SpecialAbility","robot")
        };

        public static Profile SampleProfile(string id,string nickname)
        {
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            var sampleItems=SampleCatalogPairs.Select(pair=>catalog.items.Single(x=>x.templateId==pair.templateId&&x.visualId==pair.visualId)).ToArray();
            return new Profile{playerId="sample-"+id,nickname=string.IsNullOrWhiteSpace(nickname)?"样例玩家":nickname,color=sampleItems[Math.Abs(id.GetHashCode()%sampleItems.Length)].color,
                personas=sampleItems.Select((x,i)=>new PersonaAsset{assetId="sample-"+i,name=x.label,source="明确标注的测试样例",templateId=x.templateId,visualId=x.visualId,color=x.color}).ToArray()};
        }
    }
}
