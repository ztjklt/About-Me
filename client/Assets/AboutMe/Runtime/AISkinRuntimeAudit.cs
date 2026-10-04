using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe
{
    // Explicit authenticated QA account; never auto-runs or grants a player skin.
    public sealed class AISkinRuntimeAudit:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
        GameUI ui;GameNetwork net;string output;readonly List<string> passed=new List<string>();
        T Get<T>(string n)=>(T)typeof(GameUI).GetField(n,F).GetValue(ui);
        void Set(string n,object v)=>typeof(GameUI).GetField(n,F).SetValue(ui,v);
        object Call(string n,params object[] p)=>typeof(GameUI).GetMethod(n,F).Invoke(ui,p);
        void Check(bool yes,string n){if(!yes)throw new Exception(n);passed.Add(n);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Begin(){if(Environment.GetCommandLineArgs().Contains("--ai-skin-ui-audit"))new GameObject("AI skin real UI audit").AddComponent<AISkinRuntimeAudit>();}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"--ai-skin-ui-audit-output")+1];Directory.CreateDirectory(output);var run=Run();
            while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failed.txt"),e.ToString());Debug.LogException(e);Application.Quit(1);yield break;}yield return next;}
            File.WriteAllText(Path.Combine(output,"passed.txt"),string.Join("\n",passed));Application.Quit();
        }
        IEnumerator Shot(string n){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,n+".png"));yield return new WaitForSecondsRealtime(.25f);}
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);ui=GameUI.UI;net=GameNetwork.Game;GameLocalization.SetEnglish(false);
            var profile=JsonUtility.FromJson<Profile>(File.ReadAllText(Path.Combine(output,"profile.json")));string intro=File.ReadAllText(Path.Combine(output,"intro.txt"));
            Set("profile",profile);net.Sample=false;net.Token=Environment.GetEnvironmentVariable("ABOUTME_SKIN_AUDIT_TOKEN");Check(!string.IsNullOrEmpty(net.Token),"independent authenticated QA token supplied privately");
            Get<InputField>("introduction").text=intro;Get<InputField>("nickname").text=profile.nickname;Set("appearanceDirty",false);Call("LoadAppearance");Call("OpenWardrobe");yield return null;
            var buttons=Get<Dictionary<string,Button>>("aiSkinButtons");Check(buttons.Count==11,"normal wardrobe exposes all eleven real character choices");Check(buttons.Values.All(b=>b.interactable),"ten skin qualifications derive from authenticated API profile");
            foreach(var entry in AISkinRegistry.All)
            {
                buttons[entry.skin].onClick.Invoke();yield return null;var actor=Get<ToyActor>("wardrobeActor");
                Check(actor.Appearance.character==entry.character&&actor.Skin!=null&&actor.Skin.enabled,"normal wardrobe actually renders "+entry.skin);
                Check(Get<InputField>("introduction").text==intro,"skin selection protects original introduction "+entry.skin);
                if(entry.skin=="gpt-girl")yield return Shot("wardrobe-gpt-zh");
                if(entry.skin=="glm-girl")yield return Shot("wardrobe-glm-zh");
            }
            buttons["normal"].onClick.Invoke();yield return null;Check(Get<ToyActor>("wardrobeActor").Appearance.character=="normal","normal restores ordinary outfit after ten real model switches");
            buttons["gpt-girl"].onClick.Invoke();yield return (IEnumerator)Call("SaveAppearance",false);
            Check(!Get<bool>("busy")&&!Get<bool>("appearanceDirty")&&Get<Profile>("profile").characterSkin=="gpt-girl","actual wardrobe Save persisted GPT through real API");
            Call("CloseWardrobe");Call("LoadAppearance");Call("OpenWardrobe");yield return null;
            Check(Get<string>("skinDraft")=="gpt-girl"&&Get<ToyActor>("wardrobeActor").Appearance.character=="gpt","closing and reopening restores API-saved GPT without changing intro");
            GameLocalization.SetEnglish(true);GameLocalization.Refresh();Call("RefreshAppearancePreview");yield return null;
            Check(buttons.All(p=>p.Value.GetComponentInChildren<Text>().text==AISkinRegistry.Label(p.Key)),"all eleven wardrobe names render in English");yield return Shot("wardrobe-gpt-english");
            var scroll=Get<RectTransform>("wardrobe").GetComponentsInChildren<ScrollRect>().First(s=>s.horizontal&&s.content.childCount>=11);scroll.horizontalNormalizedPosition=1;buttons["glm-girl"].onClick.Invoke();yield return null;yield return Shot("wardrobe-last-options-english");
            Check(scroll.content.GetChild(10).gameObject.activeInHierarchy,"last AI wardrobe choice remains reachable by horizontal scroll");
            Call("CloseWardrobe");Check(Get<InputField>("introduction").text==intro,"close after unsaved switch preserves original introduction");
            File.WriteAllText(Path.Combine(output,"saved-profile.json"),JsonUtility.ToJson(Get<Profile>("profile"),true));
        }
    }
}
