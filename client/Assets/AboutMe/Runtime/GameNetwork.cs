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
    public struct HelloMessage : NetworkMessage { public string token, room, nickname; public bool sample; }
    public struct CommandMessage : NetworkMessage { public string json; }
    public struct StateMessage : NetworkMessage { public int frame;public bool last;public byte[] data; }
    public struct NoticeMessage : NetworkMessage { public string text; }
    [Serializable] public class TokenRequest { public string token; }
    [Serializable] public class ResultPayload { public string matchId, winner; public EatRecord[] eaten; }

    public class GameNetwork : NetworkManager
    {
        public static GameNetwork Game;
        public string ApiUrl = "http://127.0.0.1:8081";
        public string Endpoint = "ws://127.0.0.1:7777", RoomCode = "", Token = "", Nickname = "玩家";
        public bool Sample, AllowSamples;
        public Snapshot State;
        public string Status = "";
        public MatchRules Rules = new MatchRules();
        readonly Dictionary<string, Match> rooms = new Dictionary<string, Match>();
        readonly Dictionary<int, (Match match, string player)> seats = new Dictionary<int, (Match,string)>();
        readonly HashSet<int> pending = new HashSet<int>();
        readonly Dictionary<int,float> joins = new Dictionary<int,float>();
        readonly HashSet<string> saved = new HashSet<string>();
        float nextSnapshot;
        int sequence;
        int frame,receivingFrame=-1;
        readonly MemoryStream receiveBuffer=new MemoryStream();

        public override void Awake()
        {
            autoCreatePlayer=false; dontDestroyOnLoad=true; runInBackground=true; maxConnections=64;
            Game=this; transport=GetComponent<SimpleWebTransport>();
            var sw=(SimpleWebTransport)transport;sw.port=7777;sw.maxMessageSize=256*1024;
            base.Awake();
        }
        public override void Start()
        {
            base.Start();
            string api=Environment.GetEnvironmentVariable("ABOUTME_API_URL");if(!string.IsNullOrEmpty(api))ApiUrl=api;
            var args=Environment.GetCommandLineArgs();
            AllowSamples=args.Contains("--allow-samples");
            if(args.Contains("--accelerated"))Rules=new MatchRules{revealDuration=2,handInterval=4,finalTime=8,eatDuration=.2f,recoverDuration=.15f};
            if(args.Contains("--aboutme-server")) { Application.targetFrameRate=50;StartServer();Status="Server listening :7777"; }
        }
        public void Connect()
        {
            if(NetworkClient.active)return;
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
                if(msg.last){bool first=State==null;State=JsonUtility.FromJson<Snapshot>(Encoding.UTF8.GetString(receiveBuffer.ToArray()));RoomCode=State.room;receiveBuffer.SetLength(0);if(first)Status="已连接 · 房间 "+State.room;}
            });
            NetworkClient.RegisterHandler<NoticeMessage>(msg=>{Status=msg.text;Debug.Log("ABOUTME_NOTICE "+msg.text);});
        }
        public override void OnClientConnect()
        {
            NetworkClient.Ready();
            NetworkClient.Send(new HelloMessage{token=Token,room=RoomCode,nickname=Nickname,sample=Sample});
        }
        public override void OnClientDisconnect() { if(State!=null)Status="连接已断开，可使用原凭据重连"; base.OnClientDisconnect(); }
        void Hello(NetworkConnectionToClient conn,HelloMessage hello)
        {
            if(pending.Contains(conn.connectionId)||seats.ContainsKey(conn.connectionId))return;
            if((hello.room??"").Length>12||(hello.token??"").Length>256||(hello.nickname??"").Length>24) { Reject(conn,"加入信息过长");return; }
            pending.Add(conn.connectionId);
            if(hello.sample)
            {
                if(!AllowSamples) { Reject(conn,"此服务器未开启样例测试模式，请先生成真实 Persona");return; }
                if(string.IsNullOrEmpty(hello.token)) { Reject(conn,"缺少样例身份");return; }
                Accept(conn,hello,SampleProfile(hello.token,hello.nickname));
            }
            else StartCoroutine(Verify(conn,hello));
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
                if(seat.match.Players.Count==0||(seat.match.Phase==MatchPhase.Finished&&!seats.Values.Any(x=>x.match==seat.match)))rooms.Remove(seat.match.Room);
            }
            base.OnServerDisconnect(conn);
        }
        public override void OnStopServer(){rooms.Clear();seats.Clear();pending.Clear();joins.Clear();saved.Clear();}
        void FixedUpdate()
        {
            if(!NetworkServer.active)return;
            foreach(var room in rooms.Values)room.Tick(UnityEngine.Time.fixedDeltaTime);
            float now=UnityEngine.Time.realtimeSinceStartup;
            foreach(var entry in joins.ToArray())if(now-entry.Value>20&&NetworkServer.connections.TryGetValue(entry.Key,out var conn))conn.Disconnect();
            if(now<nextSnapshot)return;nextSnapshot=now+.05f;
            foreach(var entry in seats.ToArray())
                if(NetworkServer.connections.TryGetValue(entry.Key,out var conn))SendSnapshot(conn,entry.Value.match.View(entry.Value.player));
            foreach(var room in rooms.Values)
                if(room.Phase==MatchPhase.Finished && !saved.Contains(room.MatchId)) {saved.Add(room.MatchId);StartCoroutine(SaveResult(room));}
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
            var payload=new ResultPayload{matchId=room.MatchId,winner=room.Winner,eaten=room.Eaten.ToArray()};
            if(string.IsNullOrEmpty(key)){Debug.Log("ABOUTME_RESULT_LOCAL "+JsonUtility.ToJson(payload));yield break;}
            for(int attempt=0;attempt<3;attempt++)
            {
                using(var req=ApiRequest(ApiUrl+"/internal/results",JsonUtility.ToJson(payload)))
                {req.SetRequestHeader("X-Game-Key",key);yield return req.SendWebRequest();if(req.result==UnityWebRequest.Result.Success)yield break;}
                yield return new WaitForSeconds(2);
            }
            Debug.LogError("ABOUTME_RESULT_NOT_PERSISTED "+room.MatchId);
        }
        public void Send(string action="",string item="",Vector2 movement=default,float yaw=0)
        {
            if(!NetworkClient.isConnected||State==null)return;
            NetworkClient.Send(new CommandMessage{json=JsonUtility.ToJson(new Command{sequence=++sequence,x=movement.x,z=movement.y,yaw=yaw,action=action,itemId=item})});
        }
        public static UnityWebRequest ApiRequest(string url,string json=null,string token="")
        {
            var request=new UnityWebRequest(url,json==null?"GET":"POST");
            if(json!=null){request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.SetRequestHeader("Content-Type","application/json");}
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=50;
            if(token!="")request.SetRequestHeader("Authorization","Bearer "+token);
            return request;
        }
        public static Profile SampleProfile(string id,string nickname)
        {
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            return new Profile{playerId="sample-"+id,nickname=string.IsNullOrWhiteSpace(nickname)?"样例玩家":nickname,color=catalog.items[Math.Abs(id.GetHashCode()%8)].color,
                personas=catalog.items.Select((x,i)=>new PersonaAsset{assetId="sample-"+i,name=x.label,source="明确标注的测试样例",templateId=x.templateId,visualId=x.visualId,color=x.color}).ToArray()};
        }
    }
}
