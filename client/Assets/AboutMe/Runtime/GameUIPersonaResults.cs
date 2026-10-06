using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AboutMe
{
    [Serializable] public class GenerationTerm
    { public string term,source,status,reason,assetId,kind,stage,wordCategory,wordText,abilityId,skinId; public float progress; }
    [Serializable] class GenerationProgress
    { public string generationId,phase; public GenerationTerm[] generationTerms; }

    public partial class GameUI
    {
        RectTransform generationResultContent,generationTermGrid;
        ScrollRect generationResultScroll;
        Text generationScrollHint;
        Coroutine generationPoll;
        string activeGenerationId;
        GenerationTerm[] displayedGenerationTerms=Array.Empty<GenerationTerm>();
        public static Color GenerationTermColor(string state) => state=="ready"?new Color(.12f,.56f,.34f):
            state=="failed"?new Color(.78f,.21f,.25f):state=="needs-confirmation"?new Color(.65f,.44f,.10f):new Color(.30f,.37f,.43f);

        void BuildPersonaResults(RectTransform card)
        {
            profileText=ArtText(card,"生成后，关键词会在这里显示结果。",12,6,510,22,15,UITheme.Muted,FontStyle.Bold);
            generationScrollHint=ArtText(card,"左右拖动 / 滚轮查看更多",602,8,216,20,12,UITheme.Muted,FontStyle.Normal,TextAnchor.UpperRight);
            var viewport=ArtRect(card,"Generation results viewport",new Vector2(12,-32),new Vector2(806,132));
            var surface=viewport.gameObject.AddComponent<Image>();surface.color=Color.clear;surface.raycastTarget=true;
            viewport.gameObject.AddComponent<RectMask2D>();
            generationResultContent=ArtRect(viewport,"Generation results content",Vector2.zero,new Vector2(806,132));
            generationTermGrid=generationResultContent;
            generationResultScroll=viewport.gameObject.AddComponent<PersonaResultScrollRect>();
            generationResultScroll.viewport=viewport;generationResultScroll.content=generationResultContent;
            generationResultScroll.horizontal=true;generationResultScroll.vertical=false;
            generationResultScroll.movementType=ScrollRect.MovementType.Clamped;generationResultScroll.scrollSensitivity=35;
            var track=ArtPanel(card,"Generation results scrollbar",12,164,806,6,new Color(0,0,0,.12f),false,false);
            var handle=ArtPanel(track,"Scrollbar handle",0,0,806,6,UITheme.Muted,false,false);
            handle.sizeDelta=Vector2.zero;handle.anchoredPosition=Vector2.zero;
            var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=handle;
            scrollbar.targetGraphic=handle.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.LeftToRight;
            generationResultScroll.horizontalScrollbar=scrollbar;
            generationResultScroll.horizontalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }

        void DisplayGenerationTerms(GenerationTerm[] terms,string phase="complete")
        {
            if(!generationTermGrid)return;
            foreach(Transform child in generationTermGrid)Destroy(child.gameObject);
            terms=terms??Array.Empty<GenerationTerm>();
            bool changed=!displayedGenerationTerms.Select(t=>t.term).SequenceEqual(terms.Select(t=>t.term));
            displayedGenerationTerms=terms;
            int ready=terms.Count(x=>x.status=="ready"),failed=terms.Count(x=>x.status=="failed");
            profileText.text=phase=="extracting"?"正在提取学校、兴趣和经历…":
                phase=="failed"?"生成暂未完成，请稍后重试。":
                terms.Length==0?"Persona 尚未生成。":$"生成结果 · {ready} 项正常 / {failed} 项未生成"+(phase=="searching"?" · 国内大学优先":"");
            int i=0;
            foreach(var term in terms)
            {
                var card=ArtPanel(generationTermGrid,"Keyword · "+term.term,(i/2)*144,(i%2)*67,134,61,GenerationTermColor(term.status),false,false);
                var click=card.gameObject.AddComponent<Button>();click.onClick.AddListener(()=>ShowGenerationTermDetail(term));
                var title=ArtText(card,term.term,8,3,118,32,15,Color.white,FontStyle.Bold);title.horizontalOverflow=HorizontalWrapMode.Wrap;title.resizeTextMinSize=12;
                string state=term.kind=="ai-skin"&&term.status=="ready"?"皮肤已解锁":term.status=="ready"?"正常":term.status=="failed"?"未生成":term.status=="needs-confirmation"?"请选择能力":"处理中";
                float progress=term.status=="ready"?1:Mathf.Clamp(term.progress,0,.95f);
                ArtText(card,state+(term.kind=="university"?" · "+Mathf.RoundToInt(progress*100)+"%":" · 查看"),8,36,118,20,12,Color.white);
                if(term.kind=="university")ArtPanel(card,"University progress",8,58,118*progress,3,UITheme.Mint,false,false).GetComponent<Image>().raycastTarget=false;
                i++;
            }
            generationResultContent.sizeDelta=new Vector2(Mathf.Max(806,((terms.Length+1)/2)*144-10),132);
            generationScrollHint.gameObject.SetActive(terms.Length>10);
            Canvas.ForceUpdateCanvases();
            if(changed){generationResultScroll.StopMovement();generationResultScroll.horizontalNormalizedPosition=0;}
        }

        RectTransform generationTermDetail;
        void ShowGenerationTermDetail(GenerationTerm term)
        {
            if(generationTermDetail)Destroy(generationTermDetail.gameObject);
            generationTermDetail=ArtPanel(studioCard,"Result detail",450,352,408,194,UITheme.Paper,true,true);
            var fullDetail=ArtScrollableField(generationTermDetail,14,12,378,122,"");fullDetail.readOnly=true;fullDetail.characterLimit=0;fullDetail.text=term.term+"\n"+GameLocalization.Text(term.reason??"")+"\n"+GameLocalization.Text(term.stage??"")+(term.kind=="trait-word"&&!string.IsNullOrEmpty(term.abilityId)?"\n"+GameLocalization.Text(TraitAbilityCatalog.AbilityLabel(term.abilityId)):"");
            ArtButton(generationTermDetail,"关闭",290,145,102,34,()=>Destroy(generationTermDetail.gameObject),UITheme.Field,UITheme.Ink);
            if(term.kind=="ai-skin"&&term.status=="ready")ArtButton(generationTermDetail,"切换皮肤",14,145,160,34,()=>{Destroy(generationTermDetail.gameObject);OpenWardrobe();},UITheme.Mint,UITheme.Ink);
            else if(term.kind=="trait-word")ArtButton(generationTermDetail,string.IsNullOrEmpty(term.abilityId)?"请选择能力":"改选能力",14,145,160,34,()=>OpenWordAbilityPicker(term),UITheme.Mint,UITheme.Ink);
            else if(term.status=="failed")ArtButton(generationTermDetail,"重试生成",14,145,160,34,()=>{Destroy(generationTermDetail.gameObject);if(!busy)StartCoroutine(Generate());},UITheme.Mint,UITheme.Ink);
        }

        GenerationTerm[] ProfileGenerationTerms()
        {
            if(profile?.generationTerms!=null&&profile.generationTerms.Length>0)
            {
                foreach(var term in profile.generationTerms)
                {
                    if(string.IsNullOrEmpty(term.kind))
                    {
                        bool school=(profile.personas??Array.Empty<PersonaAsset>()).Any(p=>p.assetId==term.assetId&&p.visualId=="school-disc")
                            ||(term.reason??"").Contains("校徽");
                        term.kind=school?"university":"prop";
                    }
                    if(term.status=="ready")term.progress=1;
                }
                return profile.generationTerms;
            }
            return (profile?.personas??Array.Empty<PersonaAsset>()).Select(p=>new GenerationTerm{
                term=p.name.Replace("校徽圆盘",""),assetId=p.assetId,status=p.visualId=="school-disc"&&string.IsNullOrEmpty(p.emblemPath)?"failed":"ready",
                wordCategory=p.wordCategory,wordText=p.wordText,abilityId=p.abilityId,kind=p.visualId==TraitAbilityCatalog.VisualId?"trait-word":p.visualId=="school-disc"?"university":"prop",progress=string.IsNullOrEmpty(p.emblemPath)&&p.visualId=="school-disc"?0:1,
                reason=p.visualId=="school-disc"?(string.IsNullOrEmpty(p.emblemPath)?"校徽尚未找到":"校徽飞盘已生成"):p.name}).ToArray();
        }

        void BeginGenerationResults(string token,string id)
        {
            EndGenerationPoll();activeGenerationId=id;DisplayGenerationTerms(null,"extracting");
            generationPoll=StartCoroutine(PollGenerationResults(token,id));
        }
        void EndGenerationPoll()
        {if(generationPoll!=null)StopCoroutine(generationPoll);generationPoll=null;activeGenerationId=null;}
        void DisplayGenerationFailure()
        {
            EndGenerationPoll();
            DisplayGenerationTerms(displayedGenerationTerms.Select(t=>new GenerationTerm{
                term=t.term,source=t.source,assetId=t.assetId,kind=t.kind,wordCategory=t.wordCategory,wordText=t.wordText,abilityId=t.abilityId,stage="结果未保存",progress=Mathf.Min(t.progress,.95f),status="failed",
                reason=t.status=="failed"?t.reason:"结果未保存，请重试"}).ToArray(),"failed");
        }
        IEnumerator PollGenerationResults(string token,string id)
        {
            while(busy&&activeGenerationId==id)
            {
                using(var req=UnityWebRequest.Get(net.ApiUrl+"/personas/generation-status?generationId="+id))
                {
                    req.SetRequestHeader("Authorization","Bearer "+token);req.timeout=5;
                    yield return req.SendWebRequest();
                    if(req.result==UnityWebRequest.Result.Success&&activeGenerationId==id&&busy)
                    {
                        var progress=JsonUtility.FromJson<GenerationProgress>(req.downloadHandler.text);
                        if(progress!=null&&progress.generationId==id)DisplayGenerationTerms(progress.generationTerms,progress.phase);
                    }
                }
                yield return new WaitForSecondsRealtime(.7f);
            }
        }
    }
}
