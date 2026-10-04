using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class GameUI
    {
        static readonly string[] QuestionnaireAbilityIds={"flame-fan","bubble-lift","front-shield","calm-dome","tractor-loose","spring-pad"};
        readonly Dictionary<string,WordAbilityChoice> wordAbilityOverrides=new Dictionary<string,WordAbilityChoice>();
        RectTransform wordAbilityPicker;
        WordAbilityChoice[] SelectedWordAbilities()
        {
            var choices=new Dictionary<string,WordAbilityChoice>(wordAbilityOverrides);
            var selected=Enumerable.Range(0,6).FirstOrDefault(i=>introductionAnswers[70+i]);
            if(Enumerable.Range(0,6).Any(i=>introductionAnswers[70+i]))
            {
                foreach(var word in TraitAbilityCatalog.Words)
                    if((word.aliases??Array.Empty<string>()).Append(word.text).Any(t=>introduction.text.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0))
                        if(!choices.ContainsKey(word.text))choices[word.text]=new WordAbilityChoice{text=word.text,category=word.category,abilityId=QuestionnaireAbilityIds[selected]};
                foreach(int q in new[]{10,11,13})if(questionnaireOther.Count>q&&!string.IsNullOrWhiteSpace(questionnaireOther[q].text))
                {var text=questionnaireOther[q].text.Trim();if(introduction.text.Contains(text)&&!choices.ContainsKey(text))choices[text]=new WordAbilityChoice{text=text,category="custom",abilityId=QuestionnaireAbilityIds[selected]};}
            }
            return choices.Count==0?null:choices.Values.ToArray();
        }
        void OpenWordAbilityPicker(GenerationTerm term)
        {
            if(wordAbilityPicker)Destroy(wordAbilityPicker.gameObject);
            wordAbilityPicker=ArtPanel(artboard,"Choose word ability",390,184,820,526,UITheme.Paper,true,true);
            ArtText(wordAbilityPicker,GameLocalization.Text("文字道具能力 · ")+GameLocalization.Protect(term.wordText??term.term),28,24,610,48,27,UITheme.Ink);
            ArtText(wordAbilityPicker,"只更改这一件道具。选择后点击 AI生成，成功保存后生效；原资产保留。",28,78,764,52,16,UITheme.Muted);
            ArtButton(wordAbilityPicker,"关闭",672,24,120,42,()=>Destroy(wordAbilityPicker.gameObject),UITheme.Field,UITheme.Ink);
            for(int i=0;i<QuestionnaireAbilityIds.Length;i++)
            {
                string id=QuestionnaireAbilityIds[i];var d=PartyMechanicRegistry.Find(id);
                ArtRaisedButton(wordAbilityPicker,GameLocalization.Text(d.label)+"\n"+GameLocalization.Text(d.hint),28+(i%3)*256,151+(i/3)*158,240,140,()=>
                {
                    var text=string.IsNullOrEmpty(term.wordText)?term.term:term.wordText;
                    wordAbilityOverrides[text]=new WordAbilityChoice{text=text,category=string.IsNullOrEmpty(term.wordCategory)?"custom":term.wordCategory,abilityId=id};
                    Destroy(wordAbilityPicker.gameObject);if(generationTermDetail)Destroy(generationTermDetail.gameObject);
                    net.Status="能力已选择；点击 AI生成确认并保存。当前 Persona 尚未改变。";
                },term.abilityId==id?UITheme.Mint:UITheme.Field,UITheme.Ink);
            }
        }
    }
}
