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
    // Opt-in regression for the reordered questionnaire; no generation or profile save.
    public sealed class AIPreferenceUIAudit : MonoBehaviour
    {
        GameUI ui; string output; readonly List<string> passed=new List<string>();
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(GameUI).GetField(name,Flags).GetValue(ui);
        object Call(string name,params object[] args)=>typeof(GameUI).GetMethod(name,Flags).Invoke(ui,args);
        void Check(bool valid,string label){if(!valid)throw new Exception(label);passed.Add(label);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin(){if(Environment.GetCommandLineArgs().Contains("--ai-question-audit"))new GameObject("AI preference regression").AddComponent<AIPreferenceUIAudit>();}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();var i=Array.IndexOf(args,"--ai-question-audit-output");
            output=i>=0?args[i+1]:Path.Combine(Application.persistentDataPath,"ai-question-audit");Directory.CreateDirectory(output);
            var run=Run();while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failed.txt"),e.ToString());Application.Quit(1);yield break;}yield return next;}
            File.WriteAllText(Path.Combine(output,"passed.txt"),string.Join("\n",passed));Application.Quit(0);
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);ui=GameUI.UI;Check(ui!=null,"real client UI loaded");
            bool english=GameLocalization.English;GameLocalization.SetEnglish(false);Call("OpenIntroductionQuestionnaire");
            var answers=Field<bool[]>("introductionAnswers");var intro=Field<InputField>("introduction");string original=intro.text;
            var profile=Field<Profile>("profile");string saved=JsonUtility.ToJson(profile);var net=Field<GameNetwork>("net");string token=net.Token;
            var question=GameUI.IntroductionQuestions[GameUI.AIQuestionIndex];int ai=GameUI.IntroductionQuestionOffset(GameUI.AIQuestionIndex);
            Check(question.title=="你喜欢什么 AI？"&&question.labels.Length==10&&question.labels.Distinct().Count()==10,"one AI question with all ten distinct brands");
            Check(GameUI.IntroductionQuestions.Count(q=>q.labels.Contains("DeepSeek")||q.labels.Contains("Claude"))==1,"DeepSeek and Claude share the complete AI question");
            Check(question.hint.Contains("彩蛋模型")&&question.hint.Contains("衣橱"),"question explains optional easter egg model and wardrobe switching");
            foreach(var brand in question.labels){Array.Clear(answers,0,answers.Length);int choice=ai+Array.IndexOf(question.labels,brand);Field<List<Button>>("questionnaireOptions")[choice].onClick.Invoke();Call("CreateQuestionnaireDraft");Check(Field<InputField>("questionnaireDraft").text=="我喜欢"+brand+"。",brand+" click produces correct AI preference");}
            Array.Clear(answers,0,answers.Length);for(int a=0;a<10;a++)answers[ai+a]=true;Call("CreateQuestionnaireDraft");
            Check(Field<InputField>("questionnaireDraft").text=="我喜欢"+string.Join("、",question.labels)+"。","all brands combine in one sentence");
            Array.Clear(answers,0,answers.Length);var mbti=GameUI.IntroductionQuestions[GameUI.MBTIQuestionIndex];
            answers[GameUI.IntroductionQuestionOffset(GameUI.MBTIQuestionIndex)+Array.IndexOf(mbti.labels,"INTP")]=true;Call("CreateQuestionnaireDraft");
            Check(Field<InputField>("questionnaireDraft").text=="我的 MBTI 是INTP。"&&Call("SelectedWordAbilities")==null,"INTP produces MBTI sentence without accidental ability override");
            Check(TraitAbilityCatalog.Data.mbti.Select(w=>w.abilityId).Distinct().Count()==16,"all sixteen MBTI defaults use different mechanics");
            foreach(var type in mbti.labels){Array.Clear(answers,0,answers.Length);int index=GameUI.IntroductionQuestionOffset(GameUI.MBTIQuestionIndex)+Array.IndexOf(mbti.labels,type);answers[index]=true;Call("CreateQuestionnaireDraft");Check(Field<InputField>("questionnaireDraft").text=="我的 MBTI 是"+type+"。"&&Call("SelectedWordAbilities")==null,type+" selection preserves its default ability");Check(Field<List<Button>>("questionnaireOptions")[index].GetComponentInChildren<Text>().text.Contains(TraitAbilityCatalog.AbilityLabel(TraitAbilityCatalog.FindWord(type).abilityId)),type+" displays its default ability beside the option");}
            Array.Clear(answers,0,answers.Length);answers[GameUI.IntroductionQuestionOffset(GameUI.MBTIQuestionIndex)+Array.IndexOf(mbti.labels,"INTP")]=true;
            answers[GameUI.IntroductionQuestionOffset(GameUI.PersonalityQuestionIndex)]=true;intro.text="我的性格是热情；我的 MBTI 是 INTP";
            answers[GameUI.IntroductionQuestionOffset(GameUI.AbilityQuestionIndex)+4]=true;
            var choices=(WordAbilityChoice[])Call("SelectedWordAbilities");Check(choices.Any(c=>c.text=="INTP")&&choices.Any(c=>c.text=="热情")&&choices.All(c=>c.abilityId=="tractor-loose"),"manual tractor choice applies to the intended words after reordering");
            Call("ApplyQuestionnaireAppearance");Check(Field<InputField>("personalityInput").text=="热情、INTP","wardrobe preview receives personality and MBTI without AI brands");
            intro.text=original;Check(JsonUtility.ToJson(profile)==saved&&net.Token==token,"draft and wardrobe preview preserve profile and session");
            var panel=Field<RectTransform>("introductionQuestionnaire");var scroll=panel.GetComponentsInChildren<ScrollRect>().First(s=>s.content&&s.content.GetComponentsInChildren<Button>().Length>20);
            var title=scroll.content.GetComponentsInChildren<Text>().First(t=>t.text.Contains(question.title));
            scroll.content.anchoredPosition=new Vector2(10,-title.rectTransform.anchoredPosition.y);Call("RefreshQuestionnaireOptions");
            yield return null;Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(output,"ai-question-zh.png"));yield return new WaitForSecondsRealtime(.3f);
            var mbtiTitle=scroll.content.GetComponentsInChildren<Text>().First(t=>t.text.Contains(mbti.title));scroll.content.anchoredPosition=new Vector2(10,-mbtiTitle.rectTransform.anchoredPosition.y);yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(output,"mbti-abilities-zh.png"));yield return new WaitForSecondsRealtime(.3f);
            GameLocalization.SetEnglish(true);GameLocalization.Refresh();Call("RefreshQuestionnaireOptions");yield return null;
            Check(!panel.GetComponentsInChildren<Text>().Where(t=>t!=Field<InputField>("questionnaireDraft").textComponent).Any(t=>t.text.Any(c=>c>='\u4e00'&&c<='\u9fff')),"AI controls and hint translate completely to English");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"mbti-abilities-en.png"));yield return new WaitForSecondsRealtime(.3f);scroll.content.anchoredPosition=new Vector2(10,-title.rectTransform.anchoredPosition.y);yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"ai-question-en.png"));yield return new WaitForSecondsRealtime(.3f);GameLocalization.SetEnglish(false);Call("CloseIntroductionQuestionnaire");Call("OpenWordAbilityPicker",new GenerationTerm{term="INTP",wordText="INTP",wordCategory="mbti",abilityId="tractor-loose"});yield return null;
            var picker=Field<RectTransform>("wordAbilityPicker");var buttons=picker.GetComponentsInChildren<Button>();Check(buttons.Length==17,"picker exposes sixteen distinct abilities and close button");var abilityScroll=picker.GetComponentInChildren<ScrollRect>();abilityScroll.verticalNormalizedPosition=0;yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(output,"ability-picker-last-zh.png"));yield return new WaitForSecondsRealtime(.3f);
            buttons.Last().onClick.Invoke();Check(((WordAbilityChoice[])Call("SelectedWordAbilities")).Any(c=>c.text=="INTP"&&c.abilityId=="short-dash"),"last new picker option selects short dash for INTP only");Check(JsonUtility.ToJson(profile)==saved,"expanded picker retains saved Persona until regeneration");
            GameLocalization.SetEnglish(english);
        }
    }
}
