using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AboutMe
{
    public partial class GameUI
    {
        IEnumerator AuditPlayerSettings(string output)
        {
            yield return null;
            if(File.Exists(output))File.Delete(output);
            bool english=GameLocalization.English,oldMotion=reduceMotion;int oldTouch=touchMode;
            float oldSensitivity=cameraSensitivity,oldShake=PlayerPrefs.GetFloat("hitShake",.3f),oldMusic=BattleMusic.SavedVolume;
            string savedIntro=introduction.text,savedEndpoint=net.Endpoint,savedApi=net.ApiUrl;
            int passed=0;
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("Settings audit failed: "+name);passed++;Debug.Log("SETTINGS_CHECK "+name);};
            try
            {
                OpenSettings();
                check(settings.GetComponentsInChildren<InputField>(true).Length==0,"no service address fields");
                check(!settings.GetComponentsInChildren<Text>(true).Any(t=>t.text.Contains("WebSocket")||t.text.Contains("密钥")||t.text.Contains("HTTPS")),"no service or key instructions");
                sensitivityLabel.GetComponentInParent<Button>().onClick.Invoke();check(cameraSensitivity!=oldSensitivity&&PlayerPrefs.GetFloat("cameraSensitivity")==cameraSensitivity,"camera sensitivity saved and applied");
                touchSettingLabel.GetComponentInParent<Button>().onClick.Invoke();check(touchMode!=(oldTouch)&&PlayerPrefs.GetInt("touchMode")==touchMode,"touch controls changed");
                motionSettingLabel.GetComponentInParent<Button>().onClick.Invoke();check(startMenuStage.MotionEnabled==!reduceMotion,"reduce motion affects menu");check(reduceMotion!=oldMotion&&ImpactFeedback.ShakeStrength==(reduceMotion?0:PlayerPrefs.GetFloat("hitShake",.3f)),"reduce motion affects camera feedback");
                musicSettingLabel.GetComponentInParent<Button>().onClick.Invoke();check(BattleMusic.SavedVolume!=oldMusic,"battle music volume changed");
                PlayerPrefs.SetFloat("battleMusicVolume",.3f);battleMusic.Sync(new Snapshot{matchId="settings-audit",phase=MatchPhase.Battle},true);
                check(battleMusic.GetComponent<AudioSource>().isPlaying,"battle music plays in battle");
                PlayerPrefs.SetFloat("battleMusicVolume",0);battleMusic.Sync(null,false);
                shakeSettingLabel.GetComponentInParent<Button>().onClick.Invoke();check(PlayerPrefs.GetFloat("hitShake")!=oldShake,"impact shake strength changed");
                englishLanguageButton.onClick.Invoke();GameLocalization.Refresh();check(GameLocalization.English&&sensitivityLabel.text.Contains("Camera"),"English settings applied immediately");
                check(introduction.text==savedIntro&&net.ApiUrl==savedApi&&net.Endpoint==savedEndpoint,"authored data and services preserved");
                var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
                foreach(var item in catalog.items)check(!GameLocalization.Text(item.label).Any(c=>c>='\u4e00'&&c<='\u9fff'),"English model: "+item.visualId);
                check(GameLocalization.Text("生成结果 · 13 项正常 / 2 项未生成").Contains("13 OK / 2 not generated"),"formatted generation counts");
                var mesh=GameLocalization.AddMesh(new GameObject("Localization audit world label"));mesh.text="按住 Q 撕掉 · 1.2s";mesh.GetComponent<LocalizedLabel>().Refresh();check(!mesh.text.Any(c=>c>='\u4e00'&&c<='\u9fff'),"world interaction hint English");
                chineseLanguageButton.onClick.Invoke();GameLocalization.Refresh();check(!GameLocalization.English&&sensitivityLabel.text.Contains("镜头灵敏度"),"Chinese settings restored immediately");
                mesh.GetComponent<LocalizedLabel>().Refresh();check(mesh.text=="按住 Q 撕掉 · 1.2s","world hint Chinese restored");Destroy(mesh.gameObject);
                File.WriteAllText(output,"{\"passed\":"+passed+",\"models\":"+catalog.items.Length+",\"result\":\"passed\"}");Debug.Log("ABOUTME_SETTINGS_AUDIT_OK "+passed);
            }
            finally
            {
                cameraSensitivity=oldSensitivity;touchMode=oldTouch;reduceMotion=oldMotion;
                PlayerPrefs.SetFloat("cameraSensitivity",oldSensitivity);PlayerPrefs.SetInt("touchMode",oldTouch);PlayerPrefs.SetInt("reduceMotion",oldMotion?1:0);PlayerPrefs.SetFloat("hitShake",oldShake);PlayerPrefs.SetFloat("battleMusicVolume",oldMusic);
                SetGameLanguage(english);SavePlayerSettings();RefreshTouchVisibility();
            }
        }
    }
}
