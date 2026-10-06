using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace AboutMe
{
    public partial class GameUI
    {
        IEnumerator AuditEatingRoasts(string output)
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            net.Sample=true;net.Token="";net.CosmeticToken="";
            var human=GameNetwork.SampleProfile("eater-audit","吞噬者甲");net.StartSolo(human,1);
            var session=(LocalMatchSession)typeof(GameNetwork).GetField("localSession",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(net);
            var match=session.Match;match.Rules.revealDuration=.1f;match.Rules.handInterval=999;match.Rules.finalTime=999;
            while(!match.Active)yield return null;
            var a=match.Players[session.PlayerId];var b=match.Players.Values.Single(x=>x!=a);b.profile.nickname="吞噬者乙（人机）";
            a.position=new Vector3(-1.5f,1,-22);b.position=new Vector3(1.5f,1,-22);match.BodyWorld.Reset(a);match.BodyWorld.Reset(b);
            var ball=match.Items.First(x=>x.ownerId==b.profile.playerId&&x.asset.visualId=="basketball");
            var camera=match.Items.First(x=>x.ownerId==a.profile.playerId&&x.asset.visualId=="camera");
            foreach(var pair in new[]{(item:ball,eater:a),(item:camera,eater:b)})
            {pair.item.state=ItemState.Ground;pair.item.carrierId="";pair.item.position=pair.eater.position;pair.item.velocity=Vector3.zero;match.Interact(pair.eater,pair.item.id);}
            string[] speakers={a.profile.playerId,b.profile.playerId};string[] items={ball.id,camera.id};
            float deadline=UnityEngine.Time.realtimeSinceStartup+45;
            while(speakers.Any(id=>!match.Events.Any(e=>e.kind=="eating-roast"&&e.speakerId==id)))
            {
                if(UnityEngine.Time.realtimeSinceStartup>deadline){File.WriteAllText(Path.Combine(output,"failed.txt"),"Provider reply or attribution missing");Debug.LogError("EATER_ROAST_RUNTIME_FAILED provider reply missing");Application.Quit(12);yield break;}
                yield return null;
            }
            if(match.PreparedRoasts.Count<2||match.EatingRoasts.Any(x=>!x.spoken))
            {Debug.LogError("EATER_ROAST_RUNTIME_FAILED preparation missing");Application.Quit(14);yield break;}
            var lines=match.Events.Where(e=>e.kind=="eating-roast").ToArray();
            if(lines.Length!=2||lines.Any(e=>e.itemId!=items[Array.IndexOf(speakers,e.speakerId)]||!e.line.Contains(e.speakerId==a.profile.playerId?"篮球":"相机")))
            {Debug.LogError("EATER_ROAST_RUNTIME_FAILED speaker/item/topic mismatch");Application.Quit(13);yield break;}
            yield return new WaitForFixedUpdate();yield return null;
            File.WriteAllText(Path.Combine(output,"snapshot.json"),JsonUtility.ToJson(net.State,true));
            File.WriteAllText(Path.Combine(output,"passed.txt"),string.Join("\n",lines.Select(e=>e.text)));
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"eater-bubbles.png"));
            Debug.Log("EATER_ROAST_RUNTIME_PASSED human=1 npc=1 source=provider");yield return new WaitForSecondsRealtime(2);Application.Quit();
        }
    }
}
