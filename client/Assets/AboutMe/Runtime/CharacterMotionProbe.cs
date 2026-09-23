using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Profiling;

namespace AboutMe
{
    // Explicit offline review mode, never substituted for real players or authoritative game state.
    public sealed class CharacterMotionProbe : MonoBehaviour
    {
        readonly ToyActor[] actors=new ToyActor[6];readonly PublicPlayer[] players=new PublicPlayer[6];readonly GameObject[] items=new GameObject[6];
        readonly List<float> frames=new List<float>(1200);readonly List<CharacterJoint> joints=new List<CharacterJoint>();float nextSnapshot,stamp,started,maxJointSeparation,maxRebound;long maximumGC;bool captured,captureEnabled,capturedLight,capturedGrab;int lastCycle=-1;Camera camera;ProfilerRecorder gc;
        string output;
        void Start()
        {
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"--probe-output");captureEnabled=args.Contains("--probe-capture");output=arg>=0&&arg+1<args.Length?args[arg+1]:Application.persistentDataPath;Directory.CreateDirectory(output);
            gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            var campus=Resources.Load<GameObject>("CampusV04/Campus");if(campus)Instantiate(campus);
            var light=new GameObject("Review daylight").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,155,0);light.shadows=LightShadows.Soft;
            camera=new GameObject("Six player review camera").AddComponent<Camera>();camera.backgroundColor=ToyFactory.ColorOf("#D8E6EA");camera.clearFlags=CameraClearFlags.SolidColor;camera.transform.position=new Vector3(9,6,17);camera.transform.LookAt(new Vector3(0,1,-1));camera.fieldOfView=48;
            Screen.SetResolution(1600,900,false);QualitySettings.vSyncCount=0;
            for(int i=0;i<6;i++)
            {
                var a=AppearanceData.Demo(i);if(i>=3){a.top="varsity";a.bag="backpack";a.hair="bob";a.topColor=new[]{"#FFD773","#FFF9EF","#F3AFC2"}[i-3];}
                a.expression=new[]{"friendly","joy","confident","sleepy","surprised","determined"}[i];
                actors[i]=new ToyActor("Review "+i,a.topColor,0,transform,a);players[i]=new PublicPlayer{appearance=a,stability=100,yaw=0,connected=true};
                joints.AddRange(actors[i].root.GetComponentsInChildren<CharacterJoint>());
                items[i]=ToyFactory.Item(new[]{"camera","book","basketball"}[i%3],"#FFD773",transform);
            }
            started=Time.unscaledTime;
        }
        void Update()
        {
            float elapsed=Time.unscaledTime-started,time=elapsed%8;int cycle=(int)(elapsed/8);
            if(elapsed>=nextSnapshot)
            {
                nextSnapshot=elapsed+.05f;stamp=elapsed;
                for(int i=0;i<6;i++)
                {
                    var p=players[i];p.simulationTime=elapsed;p.position=new Vector3((i%3-1)*2.2f,1.02f,(i/3)*-3+Mathf.Min(time,1)*5.5f+Mathf.Clamp01(time-1)*7.8f-8f);p.running=time>=1&&time<2;p.locomotionSpeed=time<1?5.5f:p.running?7.8f:0;p.interaction=time>=3&&time<5.5f?"eat":"";p.progress=p.interaction=="eat"?(time-3)/2.5f:0;p.grabbing=time>=2&&time<3?"review-target":"";p.grabbed=time>=2&&time<2.6f;p.stagger=time>=6&&time<7?1.5f-(time-6):0;p.stability=p.stagger>0?0:100;
                    p.velocity=new Vector3(0,0,p.locomotionSpeed);
                    p.actionKind=time<2?"":time<2.5f?"light":time<3?"grab":time<5.5f?"eat":time<6?"heavy":time<7?"hurt":"escape";
                    int section=time<2?0:time<2.5f?1:time<3?2:time<5.5f?3:time<6?4:time<7?5:6;
                    p.actionId=cycle*10+section;p.actionStartedAt=cycle*8+(section==1?2:section==2?2.5f:section==3?3:section==4?5.5f:section==5?6:section==6?7:0);
                    p.actionVariant=i%2;p.impactId=time>=6?cycle+1:cycle;p.impactImpulse=new Vector3(i%2==0?3:-3,0,-5);p.grabAnchor=p.position+new Vector3(.1f,.3f,.55f);p.grabTension=p.grabbed?.65f:0;
                    if(i>=3&&time<2){p.velocity=i==3?Vector3.left*p.locomotionSpeed:i==4?Vector3.right*p.locomotionSpeed:Vector3.back*p.locomotionSpeed;}

                }
            }
            if(time>=5.5f&&lastCycle!=cycle){lastCycle=cycle;foreach(var actor in actors)actor.ConfirmSwallow("review-"+cycle);}
            for(int i=0;i<6;i++)
            {
                actors[i].DrawAt(players[i],camera,Time.deltaTime,elapsed,stamp);maxRebound=Mathf.Max(maxRebound,actors[i].BodyRebound);bool eating=players[i].interaction=="eat";items[i].SetActive(eating);
                if(eating){items[i].transform.position=actors[i].CarriedPosition(true);items[i].transform.localScale=Vector3.one*actors[i].CarriedScale(true);}

            }
            foreach(var joint in joints)if(!joint.GetComponent<Rigidbody>().isKinematic)maxJointSeparation=Mathf.Max(maxJointSeparation,Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)));
            if(elapsed-Time.unscaledDeltaTime>=3){frames.Add(Time.unscaledDeltaTime*1000);if(gc.Valid)maximumGC=Math.Max(maximumGC,gc.LastValue);}
            if(captureEnabled&&!capturedLight&&time>2.12f&&time<2.4f){capturedLight=true;ScreenCapture.CaptureScreenshot(Path.Combine(output,"six-player-light.png"));}
            if(captureEnabled&&!capturedGrab&&time>2.75f&&time<2.95f){capturedGrab=true;ScreenCapture.CaptureScreenshot(Path.Combine(output,"six-player-grab.png"));}
            if(captureEnabled&&!captured&&time>4.3f){captured=true;ScreenCapture.CaptureScreenshot(Path.Combine(output,"six-player-runtime.png"));}
            if(elapsed>=17)
            {
                frames.Sort();float average=frames.Average(),p95=frames[(int)(frames.Count*.95f)];bool passed=maxJointSeparation<.3f&&maxRebound<=.121f;
                var report=new Report{device=SystemInfo.deviceModel,gpu=SystemInfo.graphicsDeviceName,frames=frames.Count,fps=1000/average,p95Milliseconds=p95,maxJointSeparation=maxJointSeparation,maximumGCBytes=maximumGC,maxBodyRebound=maxRebound,passed=passed};File.WriteAllText(Path.Combine(output,"runtime-motion-metrics.json"),JsonUtility.ToJson(report,true));
                Debug.Log("CHARACTER_RUNTIME_"+(passed?"PASS":"FAIL")+" fps="+report.fps+" p95="+p95+" jointGap="+maxJointSeparation);Application.Quit(passed?0:3);
            }
        }
        [Serializable] class Report {public string device,gpu;public int frames;public float fps,p95Milliseconds,maxJointSeparation,maxBodyRebound;public long maximumGCBytes;public bool passed;}
        void OnDestroy(){gc.Dispose();}
    }
}
