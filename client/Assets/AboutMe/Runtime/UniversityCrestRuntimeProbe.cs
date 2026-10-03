using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    // Opt-in native verification of HTTP texture loading and switching between university discs.
    public sealed class UniversityCrestRuntimeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartProbe()
        {
            if(!Environment.GetCommandLineArgs().Contains("--university-crest-probe"))return;
            new GameObject("University crest runtime verification").AddComponent<UniversityCrestRuntimeProbe>();
        }
        IEnumerator Start()
        {
            yield return null;
            string profilePath=Environment.GetEnvironmentVariable("ABOUTME_CREST_PROBE_PROFILE"),output=Environment.GetEnvironmentVariable("ABOUTME_CREST_PROBE_OUTPUT");
            if(string.IsNullOrEmpty(profilePath)||string.IsNullOrEmpty(output)||!GameNetwork.Game){Application.Quit(1);yield break;}
            var profile=JsonUtility.FromJson<Profile>(File.ReadAllText(profilePath));var assets=profile.personas.Where(p=>p.visualId=="school-disc"&&UniversityCrestTexture.ValidPath(p.emblemPath)).ToArray();
            if(assets.Length<2){Application.Quit(1);yield break;}
            Directory.CreateDirectory(output);
            if(Screen.fullScreen){Debug.LogError("FULLSCREEN_TEST_FORBIDDEN");Application.Quit(1);yield break;}
            if(FindObjectsOfType<StartMenuStage2D>().Length==0){Debug.LogError("LATEST_UI_MISSING");Application.Quit(1);yield break;}
            yield return new WaitForSeconds(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"latest-ui-window.png"));
            yield return new WaitForSeconds(.5f);
            foreach(var ui in FindObjectsOfType<GameUI>())ui.StopAllCoroutines();
            GameNetwork.Game.ApiUrl=Environment.GetEnvironmentVariable("ABOUTME_API_URL")??GameNetwork.Game.ApiUrl;
            var root=new GameObject("Two official university emblems");
            var actor=new ToyActor("crest-switch-test","#77C8BD",0,root.transform);
            var p=new PublicPlayer{id="crest-switch-test",nickname="",remaining=2,connected=true,color="#77C8BD",stability=100,position=new Vector3(0,1,0),appearance=AppearanceData.Demo(0),interaction="",publicProp=""};
            GameObject previous=null;var displays=new GameObject[2];
            for(int i=0;i<2;i++)
            {
                p.equippedVisual=assets[i].visualId;p.equippedName=assets[i].name;p.equippedColor=assets[i].color;p.equippedEmblemPath=assets[i].emblemPath;
                p.interaction="eat";actor.DrawAt(p,null,.02f,0);
                yield return null;p.interaction="";actor.DrawAt(p,null,.02f,0);
                if(!actor.held||(i>0&&actor.held==previous)){Debug.LogError("CREST_SWITCH_FAILED");Application.Quit(1);yield break;}
                previous=actor.held;
                float end=Time.realtimeSinceStartup+25;
                var top=actor.held.transform.Find("Official university crest · top");
                while(top&&!top.gameObject.activeSelf&&Time.realtimeSinceStartup<end)yield return null;
                if(!top||!top.gameObject.activeSelf){Debug.LogError("CREST_HTTP_LOAD_FAILED "+assets[i].name);Application.Quit(1);yield break;}
                var actual=top.GetComponent<Renderer>().sharedMaterial.mainTexture as Texture2D;
                var expected=new Texture2D(2,2);expected.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(profilePath),"crests",Path.GetFileName(assets[i].emblemPath))));
                if(!actual||!actual.GetPixels32().SequenceEqual(expected.GetPixels32())){Debug.LogError("CREST_TEXTURE_MISMATCH");Application.Quit(1);yield break;}
                var ink=UniversityCrestTexture.MainColor(expected);
                var body=actor.held.transform.Find("disc").GetComponent<Renderer>().sharedMaterial.color;
                if(Vector4.Distance(body,ink)>.02f||UniversityCrestTexture.FaceMesh.vertices[1].magnitude<.399f){Debug.LogError("CREST_FULL_DISC_APPEARANCE_FAILED");Application.Quit(1);yield break;}
                Destroy(expected);
                displays[i]=ToyFactory.Item(assets[i].visualId,assets[i].color,root.transform,assets[i].name,assets[i].emblemPath);
                displays[i].transform.localPosition=new Vector3((i==0?-.55f:.55f),0,0);
                float wait=Time.realtimeSinceStartup+5;
                while(!displays[i].transform.Find("Official university crest · top").gameObject.activeSelf&&Time.realtimeSinceStartup<wait)yield return null;
            }
            actor.root.SetActive(false);
            // Isolate the actual game-rendered props from the surrounding scene for the report.
            int layer=29;foreach(var d in displays)foreach(var t in d.GetComponentsInChildren<Transform>())t.gameObject.layer=layer;
            var cameraGo=new GameObject("University emblem verification camera");var camera=cameraGo.AddComponent<Camera>();camera.cullingMask=1<<layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#EEE8DC");camera.orthographic=true;camera.orthographicSize=.9f;camera.transform.position=new Vector3(0,2.5f,-1.5f);camera.transform.LookAt(Vector3.zero);
            var rt=new RenderTexture(1200,800,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
            var screenshot=new Texture2D(1200,800,TextureFormat.RGB24,false);screenshot.ReadPixels(new Rect(0,0,1200,800),0,0);screenshot.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(output,"university-disc-game.png"),screenshot.EncodeToPNG());
            File.WriteAllText(Path.Combine(output,"runtime-report.json"),"{\"latestFourBubbleUI\":true,\"windowed\":true,\"fullDiscCoverage\":true,\"bodyUsesEmblemColor\":true,\"httpTexturesLoaded\":true,\"exactOfficialPixels\":true,\"sameVisualSchoolSwitch\":true,\"hiddenDiscRecovered\":true,\"schools\":[\""+assets[0].name+"\",\""+assets[1].name+"\"]}");
            Debug.Log("ABOUTME_UNIVERSITY_CREST_RUNTIME_PASSED real HTTP / exact pixels / same-visual school switch");Application.Quit(0);
        }
    }
}
