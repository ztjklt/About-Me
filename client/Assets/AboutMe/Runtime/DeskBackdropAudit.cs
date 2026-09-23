using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace AboutMe
{
    // Explicit offline visual/performance audit. It never joins or changes a network match.
    [DefaultExecutionOrder(10000)]
    public sealed class DeskBackdropAudit : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DeskAuditReport(string json);
        [DllImport("__Internal")] static extern void DeskAuditStartDrawCounter();
        [DllImport("__Internal")] static extern int DeskAuditDrawCount();
#endif
        public static bool Requested => Debug.isDebugBuild && (Environment.GetCommandLineArgs().Contains("--desk-audit") || Application.absoluteURL.Contains("desk-audit=1"));
        ToyWorld world;DeskBackdrop backdrop;GameNetwork network;Match match;Camera camera;
        ProfilerRecorder draws;string output;bool measure;float stamp;
        readonly List<float> frames=new List<float>();readonly List<long> calls=new List<long>();readonly List<long> memory=new List<long>();
        readonly List<Sample> samples=new List<Sample>();
        public string View="overview";
        [Serializable] public class Sample {public bool background;public int frames;public float averageMs,p95Ms,averageDrawCalls,allocatedMB;public bool drawCallsAvailable;}
        [Serializable] public class Report {public string platform,gpu,device,unity;public int width,height;public string method;public Sample[] samples;public bool fallPassed;public float lowestFeet,deskTop;}

        IEnumerator Start()
        {
            output=Application.persistentDataPath;var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--audit-output");if(index>=0&&index+1<args.Length)output=args[index+1];
            if(Application.platform!=RuntimePlatform.WebGLPlayer)Directory.CreateDirectory(output);
            world=FindObjectOfType<ToyWorld>();backdrop=FindObjectOfType<DeskBackdrop>();network=GetComponent<GameNetwork>();camera=world.ViewCamera;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
#if UNITY_WEBGL && !UNITY_EDITOR
            DeskAuditStartDrawCounter();
#endif
            match=new Match("DESK-AUDIT",new MatchRules{handInterval=10000,finalTime=20000});
            for(int i=0;i<6;i++)match.Add(GameNetwork.SampleProfile("desk-"+i,"场景验收 "+(i+1)));
            match.Phase=MatchPhase.Battle;
            network.Status="离线场景验收";
            foreach(var p in match.Players.Values)p.position=new Vector3((Array.IndexOf(match.Players.Keys.ToArray(),p.profile.playerId)%3-1)*4,1,-15+(Array.IndexOf(match.Players.Keys.ToArray(),p.profile.playerId)/3)*5);
            network.State=match.View(match.Host);
            yield return new WaitForSeconds(3);
            // ABBA at an identical static camera, 2 s settling then 8 s samples per phase.
            foreach(bool on in new[]{false,true,true,false})
            {
                backdrop.gameObject.SetActive(on);yield return new WaitForSeconds(2);frames.Clear();calls.Clear();memory.Clear();measure=true;stamp=Time.realtimeSinceStartup;
                yield return new WaitForSeconds(8);measure=false;frames.Sort();
                samples.Add(new Sample{background=on,frames=frames.Count,averageMs=frames.Average(),p95Ms=frames[Mathf.Min(frames.Count-1,(int)(frames.Count*.95f))],averageDrawCalls=calls.Count>0?(float)calls.Average():-1,drawCallsAvailable=calls.Count>0,allocatedMB=memory.Count>0?(float)memory.Average()/1048576f:0});
                Debug.Log("DESK_AUDIT_PHASE "+JsonUtility.ToJson(samples.Last()));
            }
            backdrop.gameObject.SetActive(true);
            var fall=CheckFall();
            var report=new Report{platform=Application.platform.ToString(),gpu=SystemInfo.graphicsDeviceName,device=SystemInfo.deviceModel,unity=Application.unityVersion,width=Screen.width,height=Screen.height,method="Offline six-character render fixture; identical overview; 60 fps cap; ABBA, 2s settling + 8s sampling each; Unity allocated memory, not process RSS; both assets remain loaded in A/B.",samples=samples.ToArray(),fallPassed=fall.Item1,lowestFeet=fall.Item2,deskTop=DeskBackdrop.DeskTop};
            string json=JsonUtility.ToJson(report,true);Debug.Log("DESK_AUDIT_REPORT "+json);
#if UNITY_WEBGL && !UNITY_EDITOR
            DeskAuditReport(json);
#else
            File.WriteAllText(Path.Combine(output,"runtime-metrics.json"),json);
#endif
            foreach(var view in new[]{"overview","south","east","west","ground-outward","upper-outward","hand-warning","hand-contact","spectate"})
            {
                View=view;
                if(view.StartsWith("hand"))
                {
                    match.Rules.handInterval=.1f;match.Phase=MatchPhase.Reveal;match.Rules.revealDuration=.01f;match.Tick(.02f);match.Rules.handInterval=10000;match.Tick(.12f);
                    for(int i=0;i<(view=="hand-warning"?12:35);i++)match.Tick(.02f);
                    network.State=match.View(match.Host);
                }
                yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
                if(Application.platform!=RuntimePlatform.WebGLPlayer)ScreenCapture.CaptureScreenshot(Path.Combine(output,view+".png"));
            }
            View="overview";
            if(Application.platform!=RuntimePlatform.WebGLPlayer){yield return new WaitForSeconds(1);Application.Quit(fall.Item1?0:3);}
        }
        public static Tuple<bool,float> CheckFall()
        {
            var m=new Match("FALL-DESK",new MatchRules{handInterval=10000,finalTime=20000});m.Add(GameNetwork.SampleProfile("a","a"));m.Add(GameNetwork.SampleProfile("b","b"));m.Phase=MatchPhase.Battle;
            var p=m.Players["sample-a"];p.position=new Vector3(18,1,-26);float lowest=100;bool rescued=false;
            for(int i=0;i<150;i++){m.Tick(.02f);lowest=Mathf.Min(lowest,p.position.y-1);if(p.position.z> -26&&i>2){rescued=true;break;}}
            return Tuple.Create(rescued&&lowest>DeskBackdrop.DeskTop&&m.Items.Count(x=>x.ownerId=="sample-a"&&x.state==ItemState.Ground)==1,lowest);
        }
        void LateUpdate()
        {
            if(!camera)return;
            camera.rect=new Rect(0,0,1,1);
            camera.fieldOfView=(View=="south"||View=="east"||View=="west")?58:48;
            if(View=="ground-outward"){camera.transform.position=new Vector3(2,3,-16);camera.transform.LookAt(new Vector3(0,1,-32));}
            else if(View=="south"){camera.transform.position=new Vector3(0,29,55);camera.transform.LookAt(new Vector3(0,9,-76));}
            else if(View=="east"){camera.transform.position=new Vector3(-54,26,0);camera.transform.LookAt(new Vector3(68,3,0));}
            else if(View=="west"){camera.transform.position=new Vector3(54,26,0);camera.transform.LookAt(new Vector3(-68,3,0));}
            else if(View=="upper-outward"){camera.transform.position=new Vector3(27,8,-21);camera.transform.LookAt(new Vector3(61,5,-45));}
            else if(View=="spectate"){camera.transform.position=new Vector3(-18,12,15);camera.transform.LookAt(new Vector3(0,1,-10));}
            else if(View.StartsWith("hand")){camera.transform.position=new Vector3(2,7,-26);camera.transform.LookAt(new Vector3(0,2,-12));}
            else {camera.transform.position=new Vector3(0,36,-91);camera.transform.LookAt(new Vector3(0,3,3));}
            if(measure&&Time.realtimeSinceStartup-stamp>.05f)
            {
                frames.Add(Time.unscaledDeltaTime*1000);
#if UNITY_WEBGL && !UNITY_EDITOR
                int count=DeskAuditDrawCount();if(count>=0)calls.Add(count);
#else
                if(draws.Valid&&draws.LastValue>0)calls.Add(draws.LastValue);
#endif
                if(frames.Count%30==0)memory.Add(Profiler.GetTotalAllocatedMemoryLong());
            }
        }
        void OnDestroy(){draws.Dispose();}
    }
}
