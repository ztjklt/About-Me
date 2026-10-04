using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AboutMe
{
    public partial class GameUI
    {
        bool settingsAuditRunning;
        [Serializable] sealed class SettingsAuditReport {public int passed,models;public string result;public string[] checks;}
        // Match GameUI.Update: synchronize the real consumer every rendered frame.
        // The music is streamed in the background; readiness is a condition, not a fixed sleep.
        IEnumerator WaitForAuditedMusic(BattleMusic music,Snapshot state,AudioSource source,bool playing,float minimum)
        {
            float deadline=Time.realtimeSinceStartup+10;
            do
            {
                music.Sync(state,true);
                yield return null;
                yield return new WaitForEndOfFrame();
                bool ready=playing?source.isPlaying&&source.volume>minimum:!source.isPlaying&&source.volume<=.001f;
                if(ready)break;
            }while(Time.realtimeSinceStartup<deadline);
            Debug.Log("SETTINGS_MUSIC_STATE playing="+source.isPlaying+" volume="+source.volume+" clip="+source.clip.loadState);
        }
        IEnumerator AuditPlayerSettings(string output)
        {
            settingsAuditRunning=true;
            yield return null;
            if(File.Exists(output))File.Delete(output);
            bool english=GameLocalization.English,oldMotion=reduceMotion,oldOptions=optionsOpen;
            int oldTouch=touchMode;var oldState=net.State;var oldPage=page;
            float oldSensitivity=cameraSensitivity,oldShake=PlayerPrefs.GetFloat("hitShake",.3f),oldMusic=BattleMusic.SavedVolume;
            float oldYaw=world.CameraYaw,oldPitch=world.CameraPitch;
            string savedIntro=introduction.text,savedEndpoint=net.Endpoint,savedApi=net.ApiUrl;
            var checks=new List<string>();GameObject musicProbe=null,impactProbe=null,cameraProbe=null,pulseProbe=null;
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("Settings audit failed: "+name);checks.Add(name);Debug.Log("SETTINGS_CHECK "+name);};
            Action<Text> click=label=>label.GetComponentInParent<Button>(true).onClick.Invoke();
            Action<float> sensitivity=value=>{for(int i=0;i<4&&cameraSensitivity!=value;i++)click(sensitivityLabel);};
            Action<bool> motion=value=>{if(reduceMotion!=value)click(motionSettingLabel);};
            try
            {
                OpenSettings();
                check(settings.GetComponentsInChildren<InputField>(true).Length==0,"no service address fields");
                check(!settings.GetComponentsInChildren<Text>(true).Any(t=>t.text.Contains("WebSocket")||t.text.Contains("密钥")||t.text.Contains("HTTPS")),"no service or key instructions");
                click(sensitivityLabel);check(cameraSensitivity!=oldSensitivity&&PlayerPrefs.GetFloat("cameraSensitivity")==cameraSensitivity,"camera setting saves immediately");
                sensitivity(.5f);world.CameraYaw=0;world.CameraPitch=32;
                ApplyCameraLook(new Vector2(1,.5f),new Vector2(3,2));float slowYaw=world.CameraYaw,slowPitch=32-world.CameraPitch;
                sensitivity(2);world.CameraYaw=0;world.CameraPitch=32;
                ApplyCameraLook(new Vector2(1,.5f),new Vector2(3,2));
                check(Mathf.Approximately(world.CameraYaw,slowYaw*4)&&Mathf.Approximately(32-world.CameraPitch,slowPitch*4),"mouse camera rotation changes fourfold between 0.5x and 2x");
                world.CameraPitch=32;world.CameraYaw=0;ApplyCameraLook(new Vector2(20,10),new Vector2(.22f,.18f));
                check(Mathf.Approximately(world.CameraYaw,8.8f)&&Mathf.Approximately(world.CameraPitch,28.4f),"browser fallback drag uses sensitivity");
                ApplyCameraLook(new Vector2(0,10000),Vector2.one);check(world.CameraPitch==10,"camera pitch stays within limits");
                world.CameraPitch=32;
                if(net.State!=null)
                {
                    yield return new WaitForEndOfFrame();var before=world.ViewCamera.transform.rotation;
                    world.CameraYaw+=60;yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
                    check(Quaternion.Angle(before,world.ViewCamera.transform.rotation)>1,"world camera follows changed look angle");
                }
                for(int i=0;i<3;i++)
                {
                    click(touchSettingLabel);bool expected=touchMode==1||touchMode==0&&(Application.isMobilePlatform||platform.IsWeChat);
                    check(touch.gameObject.activeSelf==expected&&PlayerPrefs.GetInt("touchMode")==touchMode,"touch visibility mode "+touchMode);
                }
                while(touchMode!=1)click(touchSettingLabel);hud.gameObject.SetActive(true);
                var pointer=new PointerEventData(EventSystem.current){pointerId=71,position=new Vector2(100,100)};
                movePad.OnPointerDown(pointer);pointer.position=new Vector2(100,190);movePad.OnDrag(pointer);
                check(movePad.Pressed&&movePad.Value.y>.9f,"touch joystick produces movement input");
                var lookPad=touch.GetComponentsInChildren<TouchPad>().First(p=>p.CameraPad);
                pointer.position=Vector2.zero;pointer.delta=new Vector2(20,10);lookPad.OnPointerDown(pointer);
                sensitivity(.5f);world.CameraYaw=0;world.CameraPitch=32;lookPad.OnDrag(pointer);slowYaw=world.CameraYaw;slowPitch=32-world.CameraPitch;
                sensitivity(2);world.CameraYaw=0;world.CameraPitch=32;lookPad.OnDrag(pointer);
                check(Mathf.Approximately(world.CameraYaw,slowYaw*4)&&Mathf.Approximately(32-world.CameraPitch,slowPitch*4),"touch drag uses selected camera sensitivity");
                var escape=touch.GetComponentInChildren<PressButton>();escape.OnPointerDown(pointer);
                check(touchEscapeHeld,"touch hold starts escape input");click(touchSettingLabel);
                check(touchMode==2&&!touch.gameObject.activeSelf&&!movePad.Pressed&&movePad.Value==Vector2.zero&&!touchEscapeHeld,"hiding touch releases joystick and held action");

                // Exercise the real start-menu animation over multiple rendered frames.
                net.State=null;optionsOpen=false;ShowPage(MenuPage.Main);motion(false);
                yield return null;var title=(RectTransform)startMenuStage.transform.Find("Menu title · SINGLE PLAYER");var rest=title.anchoredPosition;
                yield return new WaitForSecondsRealtime(.12f);
                check(Vector2.Distance(rest,title.anchoredPosition)>.01f,"menu animates when reduce motion is off");
                motion(true);rest=title.anchoredPosition;yield return new WaitForSecondsRealtime(.12f);
                check(title.anchoredPosition==rest&&!startMenuStage.MotionEnabled,"reduce motion holds menu at rest");
                pulseProbe=new GameObject("Settings pulse audit");var pulse=pulseProbe.AddComponent<UIPulse>();
                motion(false);pulse.Play();check(pulseProbe.transform.localScale!=Vector3.one,"feedback pulse starts normally");
                motion(true);yield return null;check(pulseProbe.transform.localScale==Vector3.one,"reduce motion stops an already running pulse");
                net.State=oldState;OpenSettings();

                // Use actual runtime feedback with a controlled authoritative combat event.
                impactProbe=new GameObject("Settings impact audit");var impact=impactProbe.AddComponent<ImpactFeedback>();
                cameraProbe=new GameObject("Settings impact camera");var camera=cameraProbe.AddComponent<Camera>();camera.enabled=false;
                var combat=new Snapshot{matchId="settings-audit",you="audit-player",simulationTime=1,players=Array.Empty<PublicPlayer>()};
                var actors=new Dictionary<string,ToyActor>();int eventId=0;
                Func<float> hit=()=>{camera.transform.position=Vector3.zero;combat.combatEvents=new[]{new CombatEvent{id=++eventId,kind="hit",sourceId=combat.you,targetId="other",time=1,severity=1,point=Vector3.forward*5}};impact.Draw(combat,actors,camera,.01f);return camera.transform.position.magnitude;};
                motion(false);PlayerPrefs.SetFloat("hitShake",0);click(shakeSettingLabel);float low=hit();click(shakeSettingLabel);float strong=hit();
                check(low>0&&Mathf.Abs(strong/low-1/.3f)<.001f,"strong impact causes larger actual camera displacement than low");
                click(shakeSettingLabel);check(hit()==0,"impact off produces zero camera displacement");
                click(shakeSettingLabel);hit();click(shakeSettingLabel);click(shakeSettingLabel);camera.transform.position=Vector3.zero;impact.Draw(combat,actors,camera,.02f);
                check(camera.transform.position==Vector3.zero,"impact off stops an in-progress shake");
                click(shakeSettingLabel);motion(true);check(hit()==0,"reduce motion suppresses actual combat camera shake");

                // A separate instance avoids the menu's disconnected state overriding this battle fixture.
                musicProbe=new GameObject("Settings music audit");var music=musicProbe.AddComponent<BattleMusic>();var audio=musicProbe.GetComponent<AudioSource>();
                var battle=new Snapshot{matchId="settings-audit",phase=MatchPhase.Battle};
                PlayerPrefs.SetFloat("battleMusicVolume",0);click(musicSettingLabel);yield return WaitForAuditedMusic(music,battle,audio,true,.29f);
                check(audio.isPlaying&&audio.volume>.29f,"battle music on reaches audible AudioSource volume");
                click(musicSettingLabel);yield return WaitForAuditedMusic(music,battle,audio,false,0);
                check(BattleMusic.SavedVolume==0&&!audio.isPlaying&&audio.volume<=.001f,"battle music off reaches silence and stops during battle");
                click(musicSettingLabel);yield return WaitForAuditedMusic(music,battle,audio,true,0);
                check(audio.isPlaying&&audio.volume>0,"battle music can resume in the same match");
                battle.phase=MatchPhase.Lobby;yield return WaitForAuditedMusic(music,battle,audio,false,0);
                check(!audio.isPlaying,"battle music stops outside battle");

                englishLanguageButton.onClick.Invoke();GameLocalization.Refresh();check(GameLocalization.English&&sensitivityLabel.text.Contains("Camera"),"English settings applied immediately");
                check(introduction.text==savedIntro&&net.ApiUrl==savedApi&&net.Endpoint==savedEndpoint,"authored data and services preserved");
                var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
                foreach(var item in catalog.items)check(!GameLocalization.Text(item.label).Any(c=>c>='\u4e00'&&c<='\u9fff'),"English model: "+item.visualId);
                check(GameLocalization.Text("生成结果 · 13 项正常 / 2 项未生成").Contains("13 OK / 2 not generated"),"formatted generation counts");
                var mesh=GameLocalization.AddMesh(new GameObject("Localization audit world label"));mesh.text="按住 Q 撕掉 · 1.2s";mesh.GetComponent<LocalizedLabel>().Refresh();check(!mesh.text.Any(c=>c>='\u4e00'&&c<='\u9fff'),"world interaction hint English");
                var entries=startMenuStage.GetComponentsInChildren<StickerButton>(true).Where(s=>s.name.StartsWith("Menu title · ")).ToArray();
                check(entries.Length==4&&entries.All(s=>s.Surface.sprite&&s.Surface.color.a>.99f&&!s.Label.gameObject.activeSelf),"all four English home artworks visible in English mode");
                chineseLanguageButton.onClick.Invoke();GameLocalization.Refresh();check(!GameLocalization.English&&sensitivityLabel.text.Contains("镜头灵敏度"),"Chinese settings restored immediately");
                check(entries.All(s=>s.Surface.sprite&&s.Surface.color.a>.99f&&!s.Label.gameObject.activeSelf),"all four English home artworks remain in Chinese mode");
                mesh.GetComponent<LocalizedLabel>().Refresh();check(mesh.text=="按住 Q 撕掉 · 1.2s","world hint Chinese restored");Destroy(mesh.gameObject);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllText(output,JsonUtility.ToJson(new SettingsAuditReport{passed=checks.Count,models=catalog.items.Length,result="passed",checks=checks.ToArray()},true));Debug.Log("ABOUTME_SETTINGS_AUDIT_OK "+checks.Count);
            }
            finally
            {
                foreach(var probe in new[]{musicProbe,impactProbe,cameraProbe,pulseProbe})if(probe)Destroy(probe);
                net.State=oldState;page=oldPage;optionsOpen=oldOptions;settings.gameObject.SetActive(oldOptions);
                world.CameraYaw=oldYaw;world.CameraPitch=oldPitch;cameraSensitivity=oldSensitivity;touchMode=oldTouch;reduceMotion=oldMotion;
                PlayerPrefs.SetFloat("cameraSensitivity",oldSensitivity);PlayerPrefs.SetInt("touchMode",oldTouch);PlayerPrefs.SetInt("reduceMotion",oldMotion?1:0);PlayerPrefs.SetFloat("hitShake",oldShake);PlayerPrefs.SetFloat("battleMusicVolume",oldMusic);
                SetGameLanguage(english);SavePlayerSettings();RefreshTouchVisibility();settingsAuditRunning=false;
            }
            if(Environment.GetCommandLineArgs().Contains("--settings-audit-exit"))Application.Quit(0);
        }
    }
}
