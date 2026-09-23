using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    // Explicit test client, never substituted for a real person in normal play.
    public class NetworkProbe : MonoBehaviour
    {
        GameNetwork net;float next,startTime;bool ready,started;int seen,sequence,requiredPlayers=2;
        string marker;bool host;
        float nextTrace,reconnectAt;bool reconnectRequested,reconnecting,reconnected;string originalId;int physicalFrames;
        void Start()
        {
            net=GetComponent<GameNetwork>();var args=Environment.GetCommandLineArgs();
            string Arg(string key,string fallback=""){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
            requiredPlayers=int.Parse(Arg("--players","2"));reconnectRequested=args.Contains("--reconnect");
            marker=Arg("--marker","/tmp/about-me-room");host=args.Contains("--create-room");
            net.Endpoint=Arg("--endpoint","ws://127.0.0.1:7777");net.Sample=true;net.Token=Arg("--identity",Guid.NewGuid().ToString("N"));net.Nickname="TEST "+net.Token.Substring(0,Math.Min(5,net.Token.Length));
            int appearanceIndex=host?0:int.TryParse(net.Token.Replace("client",""),out int parsedIndex)?parsedIndex:1;
            net.SampleAppearance=AppearanceData.Demo(appearanceIndex);net.SampleAppearance.expression=new[]{"friendly","joy","confident","sleepy","surprised","determined"}[appearanceIndex%6];net.SampleAppearance.topColor=new[]{"#79CFC5","#F3AFC2","#FFD773","#9FAFF2","#FFF9EF","#202A3D"}[appearanceIndex%6];
            net.RoomCode=host?"":File.ReadAllText(marker).Trim();startTime=UnityEngine.Time.realtimeSinceStartup;net.Connect();
        }
        void Update()
        {
            if(net==null)return;
            float now=UnityEngine.Time.realtimeSinceStartup;
            if(now-startTime>100){Debug.LogError("PROBE_TIMEOUT seen="+seen);Application.Quit(2);return;}
            if(reconnecting){if(now<reconnectAt)return;reconnecting=false;net.Connect();return;}
            var s=net.State;if(s==null)return;
            if(JsonUtility.ToJson(s).Contains("\"source\""))
            {Debug.LogError("PROBE_PRIVATE_SOURCE_LEAK");Application.Quit(8);return;}
            if(originalId==null)originalId=s.you;
            if(s.you!=originalId){Debug.LogError("PROBE_RECONNECT_ID_CHANGED");Application.Quit(5);return;}
            if(reconnectRequested&&!reconnected&&s.phase==MatchPhase.Battle&&s.time>4){reconnected=true;reconnecting=true;reconnectAt=now+1;net.StopClient();net.State=null;Debug.Log("PROBE_RECONNECT_STARTED");return;}
            if(s.phase==MatchPhase.Battle||s.phase==MatchPhase.FinalBurst){
                foreach(var person in s.players.Where(p=>!p.eliminated)){
                    var pose=person.bodyPose;
                    if(pose==null||pose.positions==null||pose.rotations==null||pose.positions.Length!=15||pose.rotations.Length!=15||pose.positions.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.y)||v.magnitude>8)){
                        Debug.LogError("PROBE_PHYSICS_POSE_INVALID player="+person.id+" time="+s.time+" phase="+s.phase+" bones="+(pose?.positions?.Length??-1));Application.Quit(6);return;
                    }
                }
                physicalFrames++;
            }
            if(host&&now>=nextTrace){nextTrace=now+5;File.WriteAllText(marker+".state.json",JsonUtility.ToJson(s));Debug.Log("PROBE_STATE "+s.phase+" ground="+s.ground.Length+" eaten="+s.eaten.Length);}
            if(host&&!File.Exists(marker)){File.WriteAllText(marker,s.room);Debug.Log("PROBE_ROOM "+s.room);}
            if(!ready){net.Send("ready");ready=true;}
            if(host&&!started&&s.players.Length>=requiredPlayers){net.Send("start");started=true;}
            if(s.phase==MatchPhase.Lobby)return;
            seen++;
            if(s.phase==MatchPhase.Finished&&seen>30){if(s.winners==null||s.winners.Length==0||s.rankings.Length!=s.players.Length||s.time>s.finalAt+.05f){Debug.LogError("PROBE_RESULT_INVALID");Application.Quit(4);return;}if(s.players.Any(p=>p.appearance==null)||s.players.Select(p=>p.appearance.topColor).Distinct().Count()!=s.players.Length||s.players.Select(p=>p.appearance.expression).Distinct().Count()!=s.players.Length){Debug.LogError("PROBE_APPEARANCE_FAILED");Application.Quit(3);return;}if(physicalFrames<10||reconnectRequested&&!reconnected){Debug.LogError("PROBE_PHYSICS_OR_RECONNECT_MISSING");Application.Quit(7);return;}Debug.Log("PROBE_PASS physicsFrames="+physicalFrames+" reconnect="+reconnected+" appearances="+s.players.Length+" players="+s.players.Length+" eaten="+s.eaten.Length+" phase="+s.phase);Application.Quit(0);return;}
            if(now<next)return;next=now+.06f;
            var me=s.players.FirstOrDefault(p=>p.id==s.you);if(me==null)return;if(!net.SampleAppearance.SameAs(me.appearance)){Debug.LogError("PROBE_OWN_APPEARANCE_CHANGED");Application.Quit(3);return;}
            if(s.ground.Any(x=>x.state==ItemState.Ground))
            {
                if(!host||me.eliminated){net.Send();return;}
                if(me.interaction!=""){net.Send();return;}
                var item=s.ground.Where(x=>x.state==ItemState.Ground).OrderBy(x=>Vector3.Distance(x.position,me.position)).FirstOrDefault();
                if(item==null){net.Send();return;}
                var delta=item.position-me.position;delta.y=0;var step=delta.normalized;
                bool near=Vector3.Distance(item.position,me.position)<2.1f;
                string moveAction=near?"interact":(++sequence%12==0?"jump":"");
                net.Send(moveAction,"",near?Vector2.zero:new Vector2(step.x,step.z),Mathf.Atan2(step.x,step.z)*Mathf.Rad2Deg,!near);return;
            }
            var target=s.players.FirstOrDefault(p=>p.id!=s.you);if(target==null)return;
            var d=target.position-me.position;d.y=0;var direction=d.normalized;
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            string action=(++sequence%15)==0?"light":"";
            net.Send(action,"",d.magnitude>1.7f?new Vector2(direction.x,direction.z):Vector2.zero,yaw,d.magnitude>3);
        }
    }
}
