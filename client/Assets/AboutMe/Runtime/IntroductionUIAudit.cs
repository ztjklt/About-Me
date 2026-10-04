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
            int originalReduceMotion=PlayerPrefs.GetInt("reduceMotion",0);bool originalEnglish=GameLocalization.English;GameLocalization.SetEnglish(false);
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
            Check(scroll.content.anchoredPosition.x< -100,"pointer drag moves clickable results without activation");
            scroll.horizontalNormalizedPosition=1;yield return null;Canvas.ForceUpdateCanvases();
            var last=(RectTransform)scroll.content.GetChild(20);var corners=new Vector3[4];last.GetWorldCorners(corners);var viewportCorners=new Vector3[4];scroll.viewport.GetWorldCorners(viewportCorners);
            Check(corners[0].x>=viewportCorners[0].x-.1f&&corners[2].x<=viewportCorners[2].x+.1f,"last result fully visible at horizontal end");
            var old=scroll.content.anchoredPosition.x;Call("DisplayGenerationTerms",terms,"searching");yield return null;
            Check(Mathf.Abs(scroll.content.anchoredPosition.x-old)<.1f,"generation polling preserves scroll position");
            var handle=scroll.horizontalScrollbar.handleRect;handle.GetWorldCorners(corners);scroll.horizontalScrollbar.GetComponent<RectTransform>().GetWorldCorners(viewportCorners);
            Check(corners[0].x>=viewportCorners[0].x-.1f&&corners[2].x<=viewportCorners[2].x+.1f,"scrollbar handle contained within result card");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"results-last.png"));yield return new WaitForSecondsRealtime(.2f);

            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            Check(GameUI.IntroductionQuestions.Length==15&&GameUI.IntroductionQuestions.SelectMany(q=>q.visuals).Count()==40,"fifteen questions; original forty catalog choices retained");
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
            GameLocalization.SetEnglish(true);yield return null;GameLocalization.Refresh();
            Check(intro.text==original&&draft.text=="我喜欢摄影。\n我也喜欢猫。","language redraw preserves original and editable draft byte for byte");
            Check(!Field<RectTransform>("introductionQuestionnaire").GetComponentsInChildren<Text>().Where(t=>t!=draft.textComponent).Any(t=>t.text.Any(c=>c>='\u4e00'&&c<='\u9fff')),"questionnaire controls fully translated to English");
            Call("CreateQuestionnaireDraft");Check(draft.text.Contains("gaming")||draft.text.Contains("Playing games")||draft.text.Contains("play"),"English choices create editable English interest draft");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"questionnaire-english.png"));yield return new WaitForSecondsRealtime(.2f);
            GameLocalization.SetEnglish(false);draft.text="我喜欢摄影。\n我也喜欢猫。";
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
            Check(catalog.items.Length==274,"latest 274 prop catalog preserved");
            Call("OpenSettings");GameLocalization.SetEnglish(true);GameLocalization.Refresh();
            Check(Field<RectTransform>("settings").gameObject.activeInHierarchy&&Field<Text>("sensitivityLabel").text.Contains("Camera"),"latest English player settings retained");
            Field<RectTransform>("settings").gameObject.SetActive(false);
            typeof(GameUI).GetField("optionsOpen",Flags).SetValue(ui,false);GameLocalization.SetEnglish(false);
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(output,"original-input-preserved.png"));yield return new WaitForSecondsRealtime(.3f);
            Call("ShowGenerationTermDetail",new GenerationTerm{term="一个很长很长的完整大学名称与完整Persona关键词",reason=new string('因',350),status="failed"});
            var fullDetail=Field<RectTransform>("generationTermDetail").GetComponentInChildren<InputField>();Check(fullDetail.text.Contains("完整Persona关键词")&&fullDetail.text.Length>350,"clicked result detail preserves complete long name and reason");
            UnityEngine.Object.Destroy(Field<RectTransform>("generationTermDetail").gameObject);
            Call("ShowPage",MenuPage.Presets);
            intro.text=original;Call("OpenIntroductionQuestionnaire");
            var others=Field<List<InputField>>("questionnaireOther");Check(others.Count==15,"every question including mood/personality/AI/MBTI/ability has free text");
            others[0].text="我喜欢中文输入与长篇故事";others[12].text="不存在模型的自定义AI";
            Call("CreateQuestionnaireDraft");Check(draft.text.Contains(others[0].text)&&draft.text.Contains(others[12].text),"self-written answers appear unchanged in editable draft");
            Check(intro.text==original&&ReferenceEquals(profile,Field<Profile>("profile")),"custom answers do not mutate introduction or Persona");
            var saved=JsonUtility.ToJson(profile);Call("ToggleQuestionnaireAnswer",46);Call("ToggleQuestionnaireAnswer",48);Call("ToggleQuestionnaireAnswer",40);Call("ApplyQuestionnaireAppearance");
            Check(Field<AppearanceData>("appearanceDraft").expression=="confident","questionnaire mood selects existing real expression ID");
            Check(Field<InputField>("personalityInput").text.Contains("热情"),"questionnaire traits enter existing AI wardrobe input");
            Check(JsonUtility.ToJson(profile)==saved,"wardrobe pending preview does not save or replace actual appearance");
            Call("CloseIntroductionQuestionnaire");Call("OpenWardrobe");yield return null;
            Check(intro.text==original,"opening wardrobe preserves original introduction");
            Call("CycleAppearance","top",1);Check(JsonUtility.ToJson(profile)==saved,"manual wardrobe cycle is preview only");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"wardrobe-r3.png"));yield return new WaitForSecondsRealtime(.3f);
            Call("CloseWardrobe");Check(intro.text==original,"closing wardrobe preserves original introduction");
            Call("RefreshSavedCharacterCard",true);Check(Field<ToyActor>("savedCharacterActor")!=null&&Field<ToyActor>("savedCharacterActor").held!=null,"current profile preview renders actual actor and actual Persona asset");
            Check(Field<ToyActor>("savedCharacterActor").Appearance.expression!=Field<AppearanceData>("appearanceDraft").expression,"saved avatar card does not silently show unsaved mood");
            var detail=Field<RectTransform>("savedCharacterDetails");detail.gameObject.SetActive(true);yield return null;detail.gameObject.SetActive(false);Check(intro.text==original,"expanded identity card leaves introduction unchanged");
            intro.text=new string('长',1200);intro.ActivateInputField();yield return null;yield return null;
            var textScroll=intro.transform.parent.GetComponent<ScrollRect>();Check(textScroll.content.rect.height>textScroll.viewport.rect.height&&intro.text.Length==1200,"half-width long input retains complete 1200 characters with scroll overflow");
            textScroll.verticalNormalizedPosition=0;yield return null;Check(textScroll.content.anchoredPosition.y>0,"long introduction scroll reaches final lines");
            intro.DeactivateInputField();intro.text=original;
            PlayerPrefs.SetInt("reduceMotion",1);var style=Field<Button>("generate").GetComponent<StickerButton>();style.OnPointerClick(new PointerEventData(EventSystem.current));yield return null;Check(style.transform.localScale==Vector3.one,"reduce-motion keeps buttons static");PlayerPrefs.SetInt("reduceMotion",originalReduceMotion);
            scroll.horizontalNormalizedPosition=1;yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(output,"studio-r3.png"));yield return new WaitForSecondsRealtime(.3f);
            Call("OpenIntroductionQuestionnaire");GameLocalization.SetEnglish(true);Call("RefreshQuestionnaireOptions");yield return null;
            var aiAnswers=new bool[54];aiAnswers[52]=aiAnswers[53]=true;var englishAI=GameUI.BuildQuestionnaireIntroduction(aiAnswers);Check(englishAI.Contains("DeepSeek")&&englishAI.Contains("Claude")&&!englishAI.Any(c=>c>='\u4e00'&&c<='\u9fff'),"English AI questionnaire draft stays pure English without recognition codewords");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"questionnaire-r3-english.png"));yield return new WaitForSecondsRealtime(.3f);
            Call("CloseIntroductionQuestionnaire");GameLocalization.SetEnglish(false);
            GameLocalization.SetEnglish(originalEnglish);
        }
    }
}
