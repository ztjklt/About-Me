using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace AboutMe
{
    // Explicit development-only, wall-clock test of the unaccelerated standard schedule.
    public sealed class ScheduleAudit : MonoBehaviour
    {
        Match match;GameNetwork net;int lastEvent;bool finishing;float started;
        string output;
        void Start()
        {
            net=GetComponent<GameNetwork>();var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--audit-output");output=index>=0&&index+1<args.Length?args[index+1]:Application.persistentDataPath;
            Directory.CreateDirectory(output);match=new Match("AUDIT",new MatchRules());
            var human=GameNetwork.SampleProfile("clock-human","计时验收 A");var guest=GameNetwork.SampleProfile("clock-guest","计时验收 B");match.Add(human);match.Add(guest);match.Receive(human.playerId,new Command{sequence=0,action="start"});
            started=Time.realtimeSinceStartup;net.Status="开发验收 · 标准时间，不加速";
        }
        void FixedUpdate()
        {
            if(match==null||finishing)return;match.Tick(Time.fixedDeltaTime);net.State=match.View(match.Host);
            if(match.HandEventId!=lastEvent){lastEvent=match.HandEventId;Debug.Log("SCHEDULE_HAND "+lastEvent+" battle="+net.State.time+" wall="+(Time.realtimeSinceStartup-started));StartCoroutine(CaptureHand(lastEvent));}
            if(match.Phase==MatchPhase.Finished)
            {
                finishing=true;bool ok=lastEvent==2&&Mathf.Abs(net.State.time-450)<.01f&&match.EndReason=="timeout"&&match.Winners.Length==2;
                File.WriteAllText(Path.Combine(output,"standard-result.json"),JsonUtility.ToJson(net.State,true));
                Debug.Log((ok?"SCHEDULE_PASS":"SCHEDULE_FAIL")+" wall="+(Time.realtimeSinceStartup-started)+" battle="+net.State.time);StartCoroutine(Finish(ok));
            }
        }
        IEnumerator CaptureHand(int index){yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"hand-"+index+".png"));}
        IEnumerator Finish(bool ok){yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"standard-result.png"));yield return new WaitForSeconds(1);Application.Quit(ok?0:2);}
    }
}
