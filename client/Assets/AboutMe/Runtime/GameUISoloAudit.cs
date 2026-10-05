using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe
{
    public partial class GameUI
    {
        // Explicit desktop audit entry; clicks the same controls and starts the same sessions as players.
        IEnumerator AuditSoloBots(string output)
        {
            Directory.CreateDirectory(output);
            bool hadCount=PlayerPrefs.HasKey(SoloNpcCountKey),oldLanguage=GameLocalization.English;
            int savedCount=PlayerPrefs.GetInt(SoloNpcCountKey,3),oldCount=soloNpcCount;
            var oldProfile=lastSoloProfile;int oldLastCount=lastSoloNpcCount;
            var checks=new List<string>();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("SOLO_AUDIT_FAILED "+label);checks.Add(label);Debug.Log("SOLO_CHECK "+label);};
            try
            {
                yield return null;ShowPage(MenuPage.Solo);
                check(soloNpcButtons.Count==6,"six bot count controls");
                check(LocalMatchSession.RecommendedNpcCount==3,"recommended three");
                foreach(bool english in new[]{false,true})
                {
                    GameLocalization.SetEnglish(english);
                    foreach(int count in new[]{1,3,4,5,6})
                    {
                        if(soloNpcCount!=count)soloNpcButtons[count-1].onClick.Invoke();
                        yield return null;
                        check(soloNpcCount==count&&PlayerPrefs.GetInt(SoloNpcCountKey)==count,"saved selection "+count+" "+english);
                        check(!soloNpcButtons[count-1].interactable&&soloNpcButtons.Count(x=>!x.interactable)==1,"one selected button "+count+" "+english);
                    }
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,english?"solo-six-en.png":"solo-six-zh.png"));
                    yield return new WaitForSecondsRealtime(.3f);
                }
                GameLocalization.SetEnglish(false);
                SetSoloNpcCount(3);StartSolo(true);yield return null;
                check(net.IsSolo&&net.State.players.Length==4,"default practice starts with human and three bots");
                net.Close();ShowPage(MenuPage.Solo);
                soloNpcButtons[5].onClick.Invoke();StartSolo(true);yield return null;
                check(net.State.players.Length==7,"six bots create seven actual actors");
                check(net.State.players.Select(x=>x.id).Distinct().Count()==7,"unique actor identities");
                var initial=net.State.players.Where(x=>x.id!=net.State.you).ToDictionary(x=>x.id,x=>x.position);
                yield return new WaitForSecondsRealtime(12);
                check(net.State.players.Length==7,"seven actor roster remains stable");
                check(net.State.players.Where(x=>initial.ContainsKey(x.id)).All(x=>Vector3.Distance(x.position,initial[x.id])>.25f),"all six bot controllers move in actual physics match");
                check(net.State.players.All(x=>x.bodyPose!=null),"seven authoritative physics frames");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"six-bot-battle.png"));
                string match=net.State.matchId;ReplaySolo();yield return null;
                check(net.State.players.Length==7&&net.State.matchId!=match,"replay keeps six bots with fresh match");
                check(new MatchRules().maxPlayers==6,"online rooms still cap six total");
                File.WriteAllText(Path.Combine(output,"passed.txt"),string.Join("\n",checks));
                File.WriteAllText(Path.Combine(output,"result.json"),"{\"passed\":true,\"checks\":"+checks.Count+",\"bots\":6,\"players\":7,\"scope\":\"Mac runtime UI and local authoritative body physics; no human feel, phone or public network claim\"}");
                Debug.Log("ABOUTME_SOLO_AUDIT_PASSED "+checks.Count);
            }
            finally
            {
                net.Close();lastSoloProfile=oldProfile;lastSoloNpcCount=oldLastCount;soloNpcCount=oldCount;
                if(hadCount)PlayerPrefs.SetInt(SoloNpcCountKey,savedCount);else PlayerPrefs.DeleteKey(SoloNpcCountKey);
                PlayerPrefs.Save();GameLocalization.SetEnglish(oldLanguage);RefreshSoloNpcButtons();
            }
            yield return new WaitForSecondsRealtime(.5f);Application.Quit();
        }
    }
}
