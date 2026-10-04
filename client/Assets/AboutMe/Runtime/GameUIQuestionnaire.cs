using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AboutMe
{
    public partial class GameUI
    {
        public sealed class IntroductionQuestion
        {
            public readonly string title,prefix;
            public readonly string[] labels,visuals;
            public IntroductionQuestion(string title,string prefix,string[] labels,string[] visuals)
            {this.title=title;this.prefix=prefix;this.labels=labels;this.visuals=visuals;}
        }
        public static readonly IntroductionQuestion[] IntroductionQuestions={
            new IntroductionQuestion("空闲时喜欢做什么？","空闲时，我喜欢",new[]{"玩游戏","摄影","看电影","阅读"},new[]{"gamepad-cc0","camera","film-clapper","book-single"}),
            new IntroductionQuestion("喜欢哪些运动？","我感兴趣的运动有",new[]{"篮球","足球","游泳","轮滑"},new[]{"basketball","football-cc0","swimming-ring","roller-skates"}),
            new IntroductionQuestion("喜欢哪些乐器？","我喜欢的乐器有",new[]{"尤克里里","电子琴","古筝","萨克斯"},new[]{"ukulele-cc0","piano-keyboard","guzheng","saxophone"}),
            new IntroductionQuestion("喜欢哪种创作？","我对这些创作活动感兴趣：",new[]{"水彩","书法","编织","折纸"},new[]{"watercolor-box","calligraphy-brush","yarn-ball","origami-crane"}),
            new IntroductionQuestion("厨房里喜欢做什么？","在厨房里，我喜欢",new[]{"烹饪","烘焙","做寿司","做果汁"},new[]{"cooking-pot","baker-rollingpin","sushi-roll","blender"}),
            new IntroductionQuestion("喜欢哪种户外活动？","我喜欢的户外活动有",new[]{"露营","徒步","攀岩","皮划艇"},new[]{"camping-lantern","hiking-pole","climbing-carabiner","mini-kayak"}),
            new IntroductionQuestion("对哪些知识感兴趣？","我对这些知识领域感兴趣：",new[]{"计算机","化学","建筑","语言"},new[]{"computer","beaker","architecture-maquette","translation-cardwheel"}),
            new IntroductionQuestion("日常喜欢什么？","日常生活中，我喜欢",new[]{"写手账","养植物","咖啡","茶"},new[]{"notebook-cc0","plant","moka-pot","tea-caddy"}),
            new IntroductionQuestion("喜欢研究或动手做什么？","我喜欢研究或摆弄",new[]{"无人机","电路","机械","模型"},new[]{"mini-drone","circuit-breadboard","gearbox-model","mini-lighthouse"}),
            new IntroductionQuestion("哪些代表物适合你？","我觉得这些事物很适合代表自己：",new[]{"猫","拼图","天文望远镜","地球仪"},new[]{"cat","puzzle-piece","telescope","globe"})
        };

        RectTransform introductionQuestionnaire;
        InputField questionnaireDraft;
        Text questionnaireFeedback;
        readonly bool[] introductionAnswers=new bool[40];
        readonly List<Button> questionnaireOptions=new List<Button>();

        public static string BuildQuestionnaireIntroduction(bool[] answers)
        {
            var sentences=new List<string>();int index=0;
            foreach(var question in IntroductionQuestions)
            {
                var selected=new List<string>();
                foreach(var label in question.labels){if(answers!=null&&index<answers.Length&&answers[index])selected.Add(label);index++;}
                if(selected.Count>0)sentences.Add(GameLocalization.Text(question.prefix)+
                    string.Join(GameLocalization.English?", ":"、",selected.Select(GameLocalization.Text))+
                    (GameLocalization.English?".":"。"));
            }
            return string.Join("\n",sentences);
        }

        public static string MergeQuestionnaireIntroduction(string original,string draft)
        {
            original=original??"";
            var added=(draft??"").Split(new[]{'。','；','\n','\r'},StringSplitOptions.RemoveEmptyEntries)
                .Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct().Where(s=>!original.Contains(s)).ToArray();
            if(added.Length==0)return original;
            return original+(original.Length==0||original.EndsWith("\n")?"":"\n")+string.Join("\n",added.Select(s=>
                s.EndsWith(".")||s.EndsWith("!")||s.EndsWith("?")||s.EndsWith("！")||s.EndsWith("？")?s:s+"。"));
        }

        void OpenIntroductionQuestionnaire()
        {
            if(!introductionQuestionnaire)BuildIntroductionQuestionnaire();
            introductionQuestionnaire.gameObject.SetActive(true);introductionQuestionnaire.SetAsLastSibling();
            ReleaseCursor();
        }

        void CloseIntroductionQuestionnaire(){if(introductionQuestionnaire)introductionQuestionnaire.gameObject.SetActive(false);}

        void BuildIntroductionQuestionnaire()
        {
            introductionQuestionnaire=ArtPanel(artboard,"Introduction questionnaire",0,0,1600,900,new Color(.05f,.07f,.12f,.65f),false,false);
            Stretch(introductionQuestionnaire);
            var card=ArtPanel(introductionQuestionnaire,"Questionnaire card",280,34,1040,828,UITheme.Paper,true,true);
            ArtText(card,"选择题模式",30,23,550,44,29,UITheme.Ink,FontStyle.Bold);
            ArtText(card,"可多选，可跳过。先整理草稿，再追加到自我介绍。",30,70,850,30,17,UITheme.Muted);
            ArtButton(card,"关闭",908,24,102,42,CloseIntroductionQuestionnaire,UITheme.Field,UITheme.Ink);
            var viewport=ArtPanel(card,"Questionnaire questions viewport",30,112,560,650,UITheme.Field,false,false);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=ArtRect(viewport,"Questionnaire questions",new Vector2(10,-8),new Vector2(540,1510));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            int index=0;
            for(int q=0;q<IntroductionQuestions.Length;q++)
            {
                var question=IntroductionQuestions[q];
                ArtText(content,(q+1)+". "+question.title,0,q*150,540,30,19,UITheme.Ink,FontStyle.Bold);
                for(int a=0;a<question.labels.Length;a++)
                {
                    int choice=index++;
                    var button=ArtButton(content,"未选 · "+question.labels[a],(a%2)*270,q*150+36+(a/2)*52,258,44,()=>ToggleQuestionnaireAnswer(choice),UITheme.Paper,UITheme.Ink);
                    questionnaireOptions.Add(button);
                }
            }
            ArtText(card,"上下滑动查看更多问题 · 不要求全部作答",30,775,560,28,14,UITheme.Muted);
            ArtText(card,"可编辑的介绍草稿",620,112,390,30,20,UITheme.Ink,FontStyle.Bold);
            ArtButton(card,"根据已选答案生成草稿",620,152,390,46,CreateQuestionnaireDraft,UITheme.Yellow,UITheme.Ink);
            questionnaireDraft=ArtField(card,620,210,390,374,"选好后生成草稿，也可以直接在这里写。",true);
            questionnaireDraft.characterLimit=1200;
            ArtText(card,"草稿尚未加入原介绍。\n生成道具仍需点击原来的「生成 Persona」。",620,601,390,62,16,UITheme.Muted);
            ArtButton(card,"追加到自我介绍",620,678,390,48,AppendQuestionnaireDraft,UITheme.Mint,UITheme.Ink);
            questionnaireFeedback=ArtText(card,"关闭时保留原自我介绍和当前 Persona。",620,739,390,69,15,UITheme.Muted);
        }

        void ToggleQuestionnaireAnswer(int choice)
        {
            introductionAnswers[choice]=!introductionAnswers[choice];
            var button=questionnaireOptions[choice];
            button.GetComponentInChildren<Text>().text=(introductionAnswers[choice]?"已选 · ":"未选 · ")+IntroductionQuestions[choice/4].labels[choice%4];
            button.GetComponent<StickerButton>().SetState(introductionAnswers[choice]?UIVisualState.Selected:UIVisualState.Normal);
        }

        void CreateQuestionnaireDraft()
        {
            var draft=BuildQuestionnaireIntroduction(introductionAnswers);
            if(draft.Length==0){questionnaireFeedback.text="还没选择答案。可以跳过问题或直接编辑草稿。";return;}
            questionnaireDraft.text=draft;
            questionnaireFeedback.text="草稿已整理，可继续编辑；追加前不会修改原介绍。";
        }

        void AppendQuestionnaireDraft()
        {
            var merged=MergeQuestionnaireIntroduction(introduction.text,questionnaireDraft.text);
            if(merged==introduction.text){questionnaireFeedback.text="没有新内容可追加，原介绍保持不变。";return;}
            if(merged.Length>introduction.characterLimit){questionnaireFeedback.text="追加后超过 1200 字，请缩短草稿后再追加；原介绍保持不变。";return;}
            introduction.text=merged;
            net.Status="介绍草稿已追加。检查后点击「生成 Persona」。";
            CloseIntroductionQuestionnaire();
        }
    }
}
