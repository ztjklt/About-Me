#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AboutMe
{
    // Opt-in local UI regression. Synthetic results never enter a saved profile or game room.
    public sealed class IntroductionUIAudit : MonoBehaviour
    {
        readonly List<string> passed=new List<string>();
        GameUI ui;string output;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(GameUI).GetField(name,Flags).GetValue(ui);
        void Call(string name,params object[] args)=>typeof(GameUI).GetMethod(name,Flags).Invoke(ui,args);
        void Check(bool valid,string label){if(!valid)throw new Exception(label);passed.Add(label);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {if(Environment.GetCommandLineArgs().Contains("--introduction-ui-audit"))new GameObject("Introduction UI regression").AddComponent<IntroductionUIAudit>();}

        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--introduction-ui-audit-output");
            output=index>=0?args[index+1]:Path.Combine(Application.persistentDataPath,"introduction-ui-audit");Directory.CreateDirectory(output);
            var run=Run();
            while(true)
            {
                object next;
                try {if(!run.MoveNext())break;next=run.Current;}
                catch(Exception error){File.WriteAllText(Path.Combine(output,"failed.txt"),error.ToString());Debug.LogException(error);Application.Quit(1);yield break;}
                yield return next;
            }
            File.WriteAllText(Path.Combine(output,"passed.txt"),string.Join("\n",passed));
            Debug.Log("INTRODUCTION_UI_AUDIT_PASSED "+passed.Count);Application.Quit(0);
        }

        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            ui=GameUI.UI;Check(ui!=null,"running client UI available");
            var terms=Enumerable.Range(0,21).Select(i=>new GenerationTerm{term="结果 "+(i+1).ToString("00"),status=i%3==0?"failed":"ready",reason=i%3==0?"未找到匹配道具":"已保存道具",kind="prop"}).ToArray();
            Call("DisplayGenerationTerms",terms,"complete");yield return null;
            var scroll=Field<ScrollRect>("generationResultScroll");
            Check(scroll.content.childCount==21,"all 21 mixed red/green terms retained");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"results-first.png"));yield return new WaitForSecondsRealtime(.2f);
            var wheel=new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-100)};
            scroll.OnScroll(wheel);Check(scroll.content.anchoredPosition.x< -100,"downward mouse wheel reveals later results");
            scroll.horizontalNormalizedPosition=0;
            wheel.scrollDelta=new Vector2(-100,0);scroll.OnScroll(wheel);Check(scroll.content.anchoredPosition.x< -100,"horizontal trackpad delta reveals later results");
            scroll.horizontalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
            var point=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(new Vector3(150,-30,0)));
            var pointer=new PointerEventData(EventSystem.current){pointerId=0,position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject)==scroll.gameObject,"card raycast routes finger drag to result scroll");
            scroll.OnInitializePotentialDrag(pointer);scroll.OnBeginDrag(pointer);pointer.position=point+Vector2.left*300;scroll.OnDrag(pointer);scroll.OnEndDrag(pointer);scroll.StopMovement();
            Check(scroll.content.anchoredPosition.x< -100,"touch pointer drag moves results without buttons");
            scroll.horizontalNormalizedPosition=1;yield return null;Canvas.ForceUpdateCanvases();
            var last=(RectTransform)scroll.content.GetChild(20);var corners=new Vector3[4];last.GetWorldCorners(corners);var viewportCorners=new Vector3[4];scroll.viewport.GetWorldCorners(viewportCorners);
            Check(corners[0].x>=viewportCorners[0].x-.1f&&corners[2].x<=viewportCorners[2].x+.1f,"last result fully visible at horizontal end");
            var old=scroll.content.anchoredPosition.x;Call("DisplayGenerationTerms",terms,"searching");yield return null;
            Check(Mathf.Abs(scroll.content.anchoredPosition.x-old)<.1f,"generation polling preserves scroll position");
            var handle=scroll.horizontalScrollbar.handleRect;handle.GetWorldCorners(corners);scroll.horizontalScrollbar.GetComponent<RectTransform>().GetWorldCorners(viewportCorners);
            Check(corners[0].x>=viewportCorners[0].x-.1f&&corners[2].x<=viewportCorners[2].x+.1f,"scrollbar handle contained within result card");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"results-last.png"));yield return new WaitForSecondsRealtime(.2f);

            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            Check(GameUI.IntroductionQuestions.Length==10&&GameUI.IntroductionQuestions.SelectMany(q=>q.visuals).Count()==40,"ten questions and forty choices");
            Check(GameUI.IntroductionQuestions.SelectMany(q=>q.visuals).All(v=>catalog.items.Any(i=>i.visualId==v)),"all choices map to current catalog");
            Check(GameUI.BuildQuestionnaireIntroduction(new bool[40])=="","all questions may be skipped");
            var intro=Field<InputField>("introduction");string original="  我来自这里，喜欢篮球。\n原稿保留！  ";intro.text=original;
            typeof(GameUI).GetField("profile",Flags).SetValue(ui,new Profile{nickname="UI regression",personas=new[]{new PersonaAsset{assetId="existing-persona",name="已有篮球",visualId="basketball",templateId="Projectile"}}});
            var profile=Field<Profile>("profile");var net=Field<GameNetwork>("net");string token=net.Token;bool busy=Field<bool>("busy");
            Call("OpenIntroductionQuestionnaire");Call("ToggleQuestionnaireAnswer",0);Call("ToggleQuestionnaireAnswer",1);Call("CreateQuestionnaireDraft");
            Check(Field<InputField>("questionnaireDraft").text.Contains("玩游戏、摄影"),"multiple answers produce interest wording");
            Check(intro.text==original,"draft generation preserves original text byte for byte");
            Call("CloseIntroductionQuestionnaire");Call("OpenIntroductionQuestionnaire");
            Check(intro.text==original,"close and reopen preserve original text byte for byte");
            Check(ReferenceEquals(profile,Field<Profile>("profile"))&&net.Token==token&&Field<bool>("busy")==busy,"choices and draft do not replace Persona or initiate generation");
            var draft=Field<InputField>("questionnaireDraft");draft.text="我喜欢摄影。\n我也喜欢猫。";
            Call("AppendQuestionnaireDraft");string merged=intro.text;
            Check(merged.StartsWith(original)&&merged.Contains("我也喜欢猫"),"edited draft appends while preserving original prefix");
            Call("OpenIntroductionQuestionnaire");Call("AppendQuestionnaireDraft");Check(intro.text==merged,"repeat append does not duplicate sentences");
            intro.text=new string('甲',1198);draft.text="我喜欢摄影。";Call("AppendQuestionnaireDraft");
            Check(intro.text.Length==1198&&intro.text.All(c=>c=='甲'),"oversized append rejected without partial text overwrite");
            Check(draft.text=="我喜欢摄影。","oversized append preserves complete editable draft");
            Check(ReferenceEquals(profile,Field<Profile>("profile"))&&profile.personas[0].assetId=="existing-persona","append leaves existing Persona intact");
            intro.text=original;draft.text="空闲时，我喜欢玩游戏、摄影。\n我对天文望远镜感兴趣。";
            Field<Text>("questionnaireFeedback").text="草稿可继续编辑。原自我介绍未改变。";
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(output,"questionnaire.png"));yield return new WaitForSecondsRealtime(.3f);
            Call("CloseIntroductionQuestionnaire");
            Check(Field<Button>("generate").gameObject.activeInHierarchy&&intro.gameObject.activeInHierarchy,"original free input and generate button remain available");
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(output,"original-input-preserved.png"));yield return new WaitForSecondsRealtime(.3f);
        }
    }
}
#endif
