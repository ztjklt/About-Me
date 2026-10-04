using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe
{
    // Opt-in audit: generated API fixture profiles are never written to a player's account.
    public sealed class TraitPersonaRuntimeAudit:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        GameUI ui;GameNetwork net;ToyWorld world;string output;readonly List<string> checks=new List<string>();
        T Get<T>(string n)=>(T)typeof(GameUI).GetField(n,F).GetValue(ui);
        void Set(string n,object v)=>typeof(GameUI).GetField(n,F).SetValue(ui,v);
        void Call(string n,params object[] values)=>typeof(GameUI).GetMethod(n,F).Invoke(ui,values);
        void Check(bool b,string label){if(!b)throw new Exception(label);checks.Add(label);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Begin(){if(Environment.GetCommandLineArgs().Contains("--trait-persona-audit"))new GameObject("Trait Persona audit").AddComponent<TraitPersonaRuntimeAudit>();}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--trait-persona-audit-output");output=args[i+1];Directory.CreateDirectory(output);
            var run=Run();while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failed.txt"),e.ToString());Debug.LogException(e);Application.Quit(1);yield break;}yield return next;}
            File.WriteAllText(Path.Combine(output,"passed.txt"),string.Join("\n",checks));Debug.Log("TRAIT_PERSONA_AUDIT_PASSED "+checks.Count);Application.Quit();
        }
        IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return new WaitForSecondsRealtime(.25f);}
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);ui=GameUI.UI;net=GameNetwork.Game;world=FindObjectOfType<ToyWorld>();
            GameLocalization.SetEnglish(false);var original="我的性格是热情；我喜欢的人物表情是友好微笑；我的 MBTI 是 ENFP；我喜欢篮球。";Get<InputField>("introduction").text=original;
            var fixture=JsonUtility.FromJson<Profile>(File.ReadAllText(Path.Combine(output,"profile-fixture.json")));Set("profile",fixture);Get<InputField>("nickname").text=fixture.nickname;net.Sample=false;
            Call("DisplayGenerationTerms",fixture.generationTerms,"complete");Call("RefreshSavedCharacterCard",true);yield return null;
            Check(fixture.personas.Any(a=>a.wordText=="热情")&&fixture.personas.Any(a=>a.wordText=="友好微笑"),"API validated heat and friendly-smile fixture assets available");
            var model=Get<ToyActor>("savedCharacterActor").held;var mesh=model.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh.bounds.size.z>=.159f&&model.GetComponentsInChildren<TextMesh>().Length==0,"saved profile preview uses actual caps and sidewall mesh with thickness");
            Check(model.GetComponent<BoxCollider>()!=null,"word preview carries bounded collision geometry");yield return Shot("introduction-words-zh");
            Call("OpenIntroductionQuestionnaire");Call("ToggleQuestionnaireAnswer",GameUI.IntroductionQuestions.Take(13).Sum(q=>q.labels.Length));Call("ToggleQuestionnaireAnswer",GameUI.IntroductionQuestions.Take(14).Sum(q=>q.labels.Length));Call("CreateQuestionnaireDraft");
            var draft=Get<InputField>("questionnaireDraft");Check(draft.text.Contains("ENFP"),"optional MBTI selection included in editable draft");
            Check(Get<InputField>("introduction").text==original&&ReferenceEquals(Get<Profile>("profile"),fixture),"MBTI ability draft leaves original and saved Persona unchanged");
            var panel=Get<RectTransform>("introductionQuestionnaire");var form=panel.GetComponentsInChildren<ScrollRect>().First(s=>s.horizontal==false&&s.content.GetComponentsInChildren<Button>().Length>20);form.verticalNormalizedPosition=0;
            yield return null;yield return Shot("mbti-ability-zh");
            Call("CloseIntroductionQuestionnaire");var term=fixture.generationTerms.First(t=>t.wordText=="热情");Call("OpenWordAbilityPicker",term);yield return null;yield return Shot("word-ability-picker-zh");
            var picker=Get<RectTransform>("wordAbilityPicker");picker.GetComponentsInChildren<Button>()[2].onClick.Invoke();yield return null;
            Check(fixture.personas.First(a=>a.wordText=="热情").abilityId=="flame-fan","ability picker requires Generate; saved asset not silently changed");
            Get<InputField>("introduction").text="我的性格是热情。";var selections=(WordAbilityChoice[])typeof(GameUI).GetMethod("SelectedWordAbilities",F).Invoke(ui,null);
            Check(selections.Any(c=>c.text=="热情"&&c.abilityId!="flame-fan"),"per-word override reaches authenticated generation request contract");Get<InputField>("introduction").text=original;
            Call("OpenIntroductionQuestionnaire");GameLocalization.SetEnglish(true);GameLocalization.Refresh();yield return null;
            Call("CreateQuestionnaireDraft");Check(draft.text.Contains("ENFP")&&!draft.text.Any(c=>c>=0x4e00&&c<=0x9fff),"English MBTI draft stays English");
            Check(Get<InputField>("introduction").text==original,"language redraw protects original introduction byte for byte");
            var remark=panel.GetComponentsInChildren<Text>().First(t=>t.text.Contains("optional")&&t.text.Contains("AI"));Check(remark.text.Contains("may"),"AI remark states optional model without automatic skin promise");
            var rt=remark.rectTransform;Canvas.ForceUpdateCanvases();form.content.anchoredPosition=new Vector2(0,Mathf.Clamp(-rt.anchoredPosition.y-80,0,form.content.sizeDelta.y-form.viewport.rect.height));
            yield return null;yield return Shot("ai-note-english");form.verticalNormalizedPosition=0;yield return null;yield return Shot("mbti-ability-english");Call("CloseIntroductionQuestionnaire");
            GameLocalization.SetEnglish(false);Get<InputField>("introduction").text=original;
            using(var match=new Match("WORDQA",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999}))
            {
                for(int n=0;n<3;n++)match.Add(new Profile{playerId="w"+n,nickname="构造测试 "+n,appearance=AppearanceData.Demo(n),personas=fixture.personas.Select(a=>a.Copy()).ToArray()});
                match.Phase=MatchPhase.Reveal;match.Tick(.02f);match.Time=10;match.Props.Clear();
                var a=match.Players["w0"];var b=match.Players["w1"];var c=match.Players["w2"];
                a.position=new Vector3(-6,1,-22);b.position=a.position+Vector3.forward*2;c.position=a.position+Vector3.right*5;a.yaw=0;b.yaw=180;c.yaw=270;foreach(var p in match.Players.Values){p.motorHeight=1;p.lastInput=match.Time;}
                world.CameraYaw=135;world.CameraPitch=20;
                void Input(string id,string action="",string item="",float x=0){var p=match.Players[id];match.Receive(id,new Command{sequence=p.sequence+1,action=action,itemId=item,yaw=p.yaw,x=x});}
                void Tick(float sec){for(int n=0;n<Mathf.CeilToInt(sec/.02f);n++){foreach(var p in match.Players.Values)Input(p.profile.playerId);match.Tick(.02f);}net.State=match.View("w0");}
                Input("w0","equip",match.Items.First(i=>i.ownerId=="w0"&&i.asset.wordText=="热情").id);Tick(.4f);net.State=match.View("w0");yield return new WaitForSecondsRealtime(.5f);yield return Shot("battle-held-word");
                Input("w0","use_item");Tick(.36f);Check(match.Effects.Any(e=>e.mechanicId=="flame-fan"&&e.visual=="trait-word"),"real held word dispatches formal flame-fan");Tick(.22f);
                Check(b.stability<100,"formal flame word damages target within cone");yield return new WaitForSecondsRealtime(.1f);yield return Shot("battle-flame-word");
                match.Drop(a,1);var flame=match.Items.First(i=>i.ownerId=="w0"&&i.asset.wordText=="热情");flame.position=a.position+Vector3.right*.6f;flame.velocity=Vector3.zero;Tick(.02f);
                Check(!match.Effects.Any(e=>e.mechanicId=="flame-fan"&&e.owner=="w0"),"drop immediately ends holder-dependent flame");yield return null;yield return Shot("battle-dropped-word");
                var bubble=match.Items.First(i=>i.ownerId=="w0"&&i.asset.wordText=="友好微笑");a.switchUntil=0;Input("w0","equip",bubble.id);b.position=a.position+Vector3.forward*2.4f;b.staggerUntil=0;b.bodyMode=BodyMode.Standing;b.bodyProtectedUntil=0;Tick(.4f);Input("w0","use_item");Tick(.7f);
                Check(match.Effects.Any(e=>e.mechanicId=="bubble-lift"&&e.stage=="attached"),"formal bubble word attaches and lifts opponent");Check(b.motorHeight>1.15f,"formal bubble lifts actual authoritative character");yield return null;
                var actors=(Dictionary<string,ToyActor>)typeof(ToyWorld).GetField("actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world);Check(actors["w0"].held.GetComponent<ThickWordMetadata>().Text=="友好微笑","held model refreshes distinct text within same trait-word visual");
                Check(!Get<RectTransform>("statusPill").gameObject.activeSelf,"editor pending-confirmation hint cleared on battle entry");yield return Shot("battle-bubble-word");
                net.State=null;
            }
            using(var perf=new Match("WORD6QA",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999}))
            {
                var abilityIds=TraitAbilityCatalog.Data.abilities.Select(d=>d.id).ToArray();
                for(int n=0;n<6;n++)
                {
                    var first=TraitAbilityCatalog.Words.First(w=>w.abilityId==abilityIds[n]);
                    var words=new[]{first}.Concat(TraitAbilityCatalog.Words.Where(w=>w!=first).Take(11)).ToArray();
                    perf.Add(new Profile{playerId="six"+n,nickname="构造六人 "+n,appearance=AppearanceData.Demo(n),personas=words.Select(w=>new PersonaAsset{assetId=Guid.NewGuid().ToString("N"),name=w.text,visualId=TraitAbilityCatalog.VisualId,templateId="SpecialAbility",wordCategory=w.category,wordText=w.text,abilityId=w.abilityId,color="#FFB347"}).ToArray()});
                }
                perf.Phase=MatchPhase.Reveal;perf.Tick(.02f);perf.Time=10;perf.Props.Clear();
                int placed=0;foreach(var player in perf.Players.Values){player.position=new Vector3(-6+(placed%3)*3,1,-22+(placed/3)*3);player.motorHeight=1;player.yaw=0;player.lastInput=perf.Time;placed++;}
                int ground=0;foreach(var item in perf.Items.Where(i=>i.state==ItemState.Inventory)){item.state=ItemState.Ground;item.position=new Vector3(-10+(ground%11)*1.25f,.35f,-25+(ground/11)*1.15f);ground++;}
                var activeAbilities=new HashSet<string>();int peakEffects=0,effectFrames=0;
                for(int frame=0;frame<35;frame++){foreach(var player in perf.Players.Values)perf.Receive(player.profile.playerId,new Command{sequence=player.sequence+1,yaw=player.yaw,action=frame==0?"use_item":""});perf.Tick(.02f);foreach(var fx in perf.Effects)activeAbilities.Add(fx.mechanicId);peakEffects=Mathf.Max(peakEffects,perf.Effects.Count);if(perf.Effects.Count>0)effectFrames++;net.State=perf.View("six0");yield return null;}
                Check(perf.Items.Count==72&&perf.Players.Count==6&&perf.Items.All(i=>i.state!=ItemState.Consumed),"six actual characters and 72 word instances retain twelve-life budgets");
                Check(perf.Effects.Count>0,"six-player presentation contains formal effects");yield return Shot("six-player-word-effects");
                var deltas=new List<float>();for(int frame=0;frame<180;frame++){foreach(var player in perf.Players.Values)perf.Receive(player.profile.playerId,new Command{sequence=player.sequence+1,yaw=player.yaw});perf.Tick(.02f);foreach(var fx in perf.Effects)activeAbilities.Add(fx.mechanicId);peakEffects=Mathf.Max(peakEffects,perf.Effects.Count);if(perf.Effects.Count>0)effectFrames++;net.State=perf.View("six0");yield return null;deltas.Add(UnityEngine.Time.unscaledDeltaTime*1000);}
                deltas.Sort();File.WriteAllText(Path.Combine(output,"six-player-performance.json"),"{\"evidence\":\"Mac Release actual ToyWorld render, constructed six-player Match and 72 normal word assets, 180 frames; no mobile claim\",\"meanFrameMs\":"+deltas.Average().ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+",\"p95FrameMs\":"+deltas[(int)(deltas.Count*.95)].ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+",\"wordInstances\":72,\"players\":6,\"warmupFrames\":35,\"sampleFrames\":180,\"effectFramesAcross215\":"+effectFrames+",\"peakConcurrentEffects\":"+peakEffects+",\"observedAbilityIds\":[\""+string.Join("\",\"",activeAbilities.OrderBy(x=>x))+"\"]}");net.State=null;
            }
            Check(Get<InputField>("introduction").text==original,"battle audit retains original introduction");
        }
    }
}
