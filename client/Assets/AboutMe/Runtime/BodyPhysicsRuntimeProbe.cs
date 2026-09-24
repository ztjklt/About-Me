using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace AboutMe
{
    // Opt-in diagnostic build mode: actual Match physics and ordinary ToyWorld rendering.
    public sealed class BodyPhysicsRuntimeProbe : MonoBehaviour
    {
        Match match;GameNetwork network;string output;float started,nextCapture;int capture,stage=-1;bool record,dsProbe,dsAll,claudeProbe,claudeAll;
        readonly List<float> frames=new List<float>();readonly List<double> physics=new List<double>();
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--probe-output");output=at>=0&&at+1<args.Length?args[at+1]:Application.persistentDataPath;Directory.CreateDirectory(output);record=args.Contains("--probe-capture");dsProbe=args.Contains("--probe-ds");dsAll=args.Contains("--probe-ds-all");claudeProbe=args.Contains("--probe-claude");claudeAll=args.Contains("--probe-claude-all");
            Screen.SetResolution(1280,720,false);QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            network=GetComponent<GameNetwork>();match=new Match("V4-RUNTIME",new MatchRules{revealDuration=.01f,handInterval=999,finalTime=999});
            for(int i=0;i<6;i++){var p=GameNetwork.SampleProfile("v4-"+i,"V4 "+(i+1));p.appearance=AppearanceData.Demo(i);if(dsAll||dsProbe&&i==0){p.appearance.character="ds";p.dsGirlUnlocked=true;p.characterSkin="ds-girl";}if(claudeAll||claudeProbe&&i==0){p.appearance.character="claude";p.claudeGirlUnlocked=true;p.characterSkin="claude-girl";}match.Add(p);match.Players[p.playerId].position=new Vector3((i%3-1)*2.8f,1,-4-(i/3)*3.2f);}
            match.EnableBodyPhysics();match.Phase=MatchPhase.Reveal;started=Time.unscaledTime;
        }
        void FixedUpdate()
        {
            if(match==null)return;float t=Time.unscaledTime-started;int section=(int)(t/4);
            int i=0;foreach(var p in match.Players.Values){float sign=i++%2==0?1:-1;match.Receive(p.profile.playerId,new Command{sequence=p.sequence+1,x=section==0?sign*.45f:0,z=section==0?.5f:0,run=t>2&&t<4,yaw=section==0?sign*40:0});}
            if(section!=stage){stage=section;if(section==1||section==2)foreach(var p in match.Players.Values)match.Hit(p,match.Players.Values.First(x=>x!=p),1,8,true,true);}
            match.Tick(Time.fixedDeltaTime);network.State=match.View(match.Players.Keys.First());if(t>3)physics.Add(match.BodyWorld.LastStepMs);
        }
        void Update()
        {
            if(match==null)return;float t=Time.unscaledTime-started;
            if((dsProbe||claudeProbe)&&Camera.main)Camera.main.fieldOfView=22;
            if(t>3)frames.Add(Time.unscaledDeltaTime*1000);
            if(record&&t>=nextCapture){nextCapture=t+.1f;ScreenCapture.CaptureScreenshot(Path.Combine(output,(capture++).ToString("D4")+".png"));}
            if(t<14)return;
            frames.Sort();physics.Sort();var report=new Report{device=SystemInfo.deviceModel,gpu=SystemInfo.graphicsDeviceName,frames=frames.Count,fps=1000/frames.Average(),p95FrameMs=frames[(int)(frames.Count*.95f)],meanPhysicsMs=physics.Average(),p95PhysicsMs=physics[(int)(physics.Count*.95f)],captureEnabled=record};
            File.WriteAllText(Path.Combine(output,"metrics.json"),JsonUtility.ToJson(report,true));Debug.Log("V4_RUNTIME_COMPLETE "+JsonUtility.ToJson(report));Application.Quit();
        }
        [Serializable] class Report{public string device,gpu;public int frames;public float fps,p95FrameMs;public double meanPhysicsMs,p95PhysicsMs;public bool captureEnabled;}
        void OnDestroy(){match?.Dispose();}
    }
}
