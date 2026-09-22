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
        float nextTrace;
        void Start()
        {
            net=GetComponent<GameNetwork>();var args=Environment.GetCommandLineArgs();
            string Arg(string key,string fallback=""){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
            requiredPlayers=int.Parse(Arg("--players","2"));
            marker=Arg("--marker","/tmp/about-me-room");host=args.Contains("--create-room");
            net.Endpoint=Arg("--endpoint","ws://127.0.0.1:7777");net.Sample=true;net.Token=Arg("--identity",Guid.NewGuid().ToString("N"));net.Nickname="TEST "+net.Token.Substring(0,Math.Min(5,net.Token.Length));
            net.RoomCode=host?"":File.ReadAllText(marker).Trim();startTime=UnityEngine.Time.realtimeSinceStartup;net.Connect();
        }
        void Update()
        {
            if(net==null)return;
            float now=UnityEngine.Time.realtimeSinceStartup;
            if(now-startTime>100){Debug.LogError("PROBE_TIMEOUT seen="+seen);Application.Quit(2);return;}
            var s=net.State;if(s==null)return;
            if(host&&now>=nextTrace){nextTrace=now+5;File.WriteAllText(marker+".state.json",JsonUtility.ToJson(s));Debug.Log("PROBE_STATE "+s.phase+" ground="+s.ground.Length+" eaten="+s.eaten.Length);}
            if(host&&!File.Exists(marker)){File.WriteAllText(marker,s.room);Debug.Log("PROBE_ROOM "+s.room);}
            if(!ready){net.Send("ready");ready=true;}
            if(host&&!started&&s.players.Length>=requiredPlayers){net.Send("start");started=true;}
            if(s.phase==MatchPhase.Lobby)return;
            seen++;
            if(s.phase==MatchPhase.Finished&&seen>30){Debug.Log("PROBE_PASS players="+s.players.Length+" eaten="+s.eaten.Length+" phase="+s.phase);Application.Quit(0);return;}
            if(now<next)return;next=now+.06f;
            var me=s.players.FirstOrDefault(p=>p.id==s.you);if(me==null)return;
            if(s.phase==MatchPhase.FinalBurst)
            {
                if(!host||me.eliminated){net.Send();return;}
                if(me.interaction!=""){net.Send();return;}
                var item=s.ground.Where(x=>x.state==ItemState.Ground).OrderBy(x=>Vector3.Distance(x.position,me.position)).FirstOrDefault();
                if(item==null){net.Send();return;}
                var delta=item.position-me.position;delta.y=0;var step=delta.normalized;
                bool near=Vector3.Distance(item.position,me.position)<2.1f;
                string moveAction=near?"interact":(++sequence%12==0?"jump":"");
                net.Send(moveAction,"",near?Vector2.zero:new Vector2(step.x,step.z),Mathf.Atan2(step.x,step.z)*Mathf.Rad2Deg);return;
            }
            var target=s.players.FirstOrDefault(p=>p.id!=s.you);if(target==null)return;
            var d=target.position-me.position;d.y=0;var direction=d.normalized;
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            string action=(++sequence%15)==0?"light":"";
            net.Send(action,"",d.magnitude>1.7f?new Vector2(direction.x,direction.z):Vector2.zero,yaw);
        }
    }
}
