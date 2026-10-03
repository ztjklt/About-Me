using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AboutMe
{
    [Serializable] public class GenerationTerm
    { public string term,source,status,reason,assetId,kind,stage; public float progress; }
    [Serializable] class GenerationProgress
    { public string generationId,phase; public GenerationTerm[] generationTerms; }

    public partial class GameUI
    {
        RectTransform generationResultContent,generationTermGrid;
        Coroutine generationPoll;
        string activeGenerationId;
        GenerationTerm[] displayedGenerationTerms=Array.Empty<GenerationTerm>();
        public static Color GenerationTermColor(string state) => state=="ready"?new Color(.12f,.56f,.34f):
            state=="failed"?new Color(.78f,.21f,.25f):new Color(.30f,.37f,.43f);

        void BuildPersonaResults(RectTransform card)
        {
            card.gameObject.AddComponent<RectMask2D>();
            generationResultContent=ArtRect(card,"Generation results content",new Vector2(12,-6),new Vector2(566,100));
            profileText=ArtText(generationResultContent,"生成后，关键词会在这里显示结果。",0,0,554,22,15,UITheme.Muted,FontStyle.Bold);
            generationTermGrid=ArtRect(generationResultContent,"Extracted keyword cards",new Vector2(0,-29),new Vector2(554,0));
            var scroll=card.gameObject.AddComponent<ScrollRect>();scroll.viewport=card;scroll.content=generationResultContent;
            scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
        }

        void DisplayGenerationTerms(GenerationTerm[] terms,string phase="complete")
        {
            if(!generationTermGrid)return;
            foreach(Transform child in generationTermGrid)Destroy(child.gameObject);
            terms=terms??Array.Empty<GenerationTerm>();
            displayedGenerationTerms=terms;
            int ready=terms.Count(x=>x.status=="ready"),failed=terms.Count(x=>x.status=="failed");
            profileText.text=phase=="extracting"?"正在提取学校、兴趣和经历…":
                phase=="failed"?"生成暂未完成，请稍后重试。":
                terms.Length==0?"Persona 尚未生成。":$"生成结果 · {ready} 项正常 / {failed} 项未生成"+(phase=="searching"?" · 国内大学优先":"");
            int i=0;
            foreach(var term in terms)
            {
                var card=ArtPanel(generationTermGrid,"Keyword · "+term.term,(i%2)*282,(i/2)*57,272,50,GenerationTermColor(term.status),true,false);
                bool university=term.kind=="university";
                var title=ArtText(card,term.term,10,2,university?207:252,22,17,Color.white,FontStyle.Bold);
                title.horizontalOverflow=HorizontalWrapMode.Wrap;title.resizeTextForBestFit=true;title.resizeTextMinSize=13;title.resizeTextMaxSize=17;
                string state=term.status=="ready"?"正常":term.status=="failed"?"未生成":"处理中";
                var detail=ArtText(card,state+" · "+(term.reason??""),10,23,252,20,12,Color.white);
                detail.resizeTextForBestFit=true;detail.resizeTextMinSize=9;detail.resizeTextMaxSize=12;
                if(university)
                {
                    float progress=term.status=="ready"?1:Mathf.Clamp(term.progress,0,.95f);
                    ArtText(card,Mathf.RoundToInt(progress*100)+"%",219,5,43,18,12,Color.white,FontStyle.Bold);
                    var track=ArtPanel(card,"University progress track",10,44,252,4,new Color(0,0,0,.25f),false,false);
                    var fill=ArtPanel(track,"University progress fill",0,0,252*progress,4,
                        term.status=="failed"?new Color(1,.70f,.70f):new Color(.66f,1,.81f),false,false);
                    fill.GetComponent<Image>().raycastTarget=false;
                }
                i++;
            }
            int height=((terms.Length+1)/2)*57;
            generationTermGrid.sizeDelta=new Vector2(554,height);
            generationResultContent.sizeDelta=new Vector2(566,Mathf.Max(100,35+height));
            Canvas.ForceUpdateCanvases();
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
                kind=p.visualId=="school-disc"?"university":"prop",progress=string.IsNullOrEmpty(p.emblemPath)&&p.visualId=="school-disc"?0:1,
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
                term=t.term,source=t.source,assetId=t.assetId,kind=t.kind,stage="结果未保存",progress=Mathf.Min(t.progress,.95f),status="failed",
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
