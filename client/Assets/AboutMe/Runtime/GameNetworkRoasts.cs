using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
namespace AboutMe
{
    [Serializable] class RoastSessionRequest { public string nickname="试玩玩家"; }
    [Serializable] class RoastSessionResponse { public string token; }
    public partial class GameNetwork
    {
        string localRoastToken="",localRoastApi="";
        bool creatingRoastSession;
        float nextRoastSession;
        void QueueRoasts(Match room,bool local=false)
        {
            int seen=roastCursor.TryGetValue(room.MatchId,out var count)?count:0;
            roastCursor[room.MatchId]=room.EatingRoasts.Count;
            if(!local&&string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAME_SERVER_KEY")))return;
            for(int i=seen;i<room.EatingRoasts.Count;i++)
            {
                var attempt=room.EatingRoasts[i];if(attempt.cancelled)continue;
                var item=room.Item(attempt.itemId);if(item==null)continue;
                StartCoroutine(RequestEatingRoast(room,attempt,new RoastRequestPayload{
                    match_id=room.MatchId,event_id=attempt.id,item_name=item.asset.name,
                    template_id=item.asset.templateId,visual_id=item.asset.visualId},local));
            }
        }
        bool RoastRoomCurrent(Match room,bool local)=>local?localSession!=null&&ReferenceEquals(localSession.Match,room):rooms.TryGetValue(room.Room,out var current)&&ReferenceEquals(current,room);
        IEnumerator EnsureLocalRoastSession()
        {
            if(localRoastApi!=ApiUrl){localRoastToken="";localRoastApi=ApiUrl;}
            if(!string.IsNullOrEmpty(localRoastToken))yield break;
            var existing=Sample?CosmeticToken:Token;
            if(!string.IsNullOrEmpty(existing)){localRoastToken=existing;yield break;}
            while(creatingRoastSession)yield return null;
            if(!string.IsNullOrEmpty(localRoastToken)||UnityEngine.Time.realtimeSinceStartup<nextRoastSession)yield break;
            creatingRoastSession=true;
            using(var req=ApiRequest(ApiUrl+"/sessions",JsonUtility.ToJson(new RoastSessionRequest())))
            {
                req.timeout=10;yield return req.SendWebRequest();
                if(req.result==UnityWebRequest.Result.Success)
                {try{localRoastToken=JsonUtility.FromJson<RoastSessionResponse>(req.downloadHandler.text)?.token??"";}catch(Exception){}}
            }
            creatingRoastSession=false;nextRoastSession=UnityEngine.Time.realtimeSinceStartup+5;
        }
        IEnumerator RequestEatingRoast(Match room,EatingRoastAttempt attempt,RoastRequestPayload payload,bool local)
        {
            if(local)yield return EnsureLocalRoastSession();
            if(local&&string.IsNullOrEmpty(localRoastToken))
            {Debug.LogWarning("ABOUTME_ROAST_UNAVAILABLE reason=session event="+attempt.id);yield break;}
            for(int retry=0;retry<3;retry++)
            {
                if(attempt.cancelled||!RoastRoomCurrent(room,local))yield break;
                using(var req=ApiRequest(ApiUrl+(local?"/roast/eating":"/internal/roast"),JsonUtility.ToJson(payload),local?localRoastToken:""))
                {
                    req.timeout=15;
                    if(!local)req.SetRequestHeader("X-Game-Key",Environment.GetEnvironmentVariable("GAME_SERVER_KEY"));
                    yield return req.SendWebRequest();
                    if(attempt.cancelled||!RoastRoomCurrent(room,local))yield break;
                    if(req.result!=UnityWebRequest.Result.Success||req.responseCode<200||req.responseCode>=300)
                    {
                        if(local&&req.responseCode==401&&retry==0){localRoastToken="";yield return CreateFreshLocalRoastSession();continue;}
                        if(retry<2&&(req.responseCode==429||req.responseCode==502||req.responseCode==504||req.responseCode==0))
                        {yield return new WaitForSeconds(2*(retry+1));continue;}
                        Debug.LogWarning("ABOUTME_ROAST_UNAVAILABLE event="+attempt.id+" status="+req.responseCode);yield break;
                    }
                    RoastResponsePayload response=null;
                    try{response=JsonUtility.FromJson<RoastResponsePayload>(req.downloadHandler.text);}catch(Exception){}
                    if(response==null||response.event_id!=attempt.id||response.aiSource!="provider"||string.IsNullOrWhiteSpace(response.roast))yield break;
                    var line=response.roast.Trim();if(line.Length>120||line.IndexOfAny(new[]{'\r','\n'})>=0)yield break;
                    if(room.SayEatingRoast(attempt,line))Debug.Log("ABOUTME_EATER_ROAST speaker="+attempt.eaterId+" item="+attempt.itemId+" event="+attempt.id+" source=provider");
                    yield break;
                }
            }
        }
        IEnumerator CreateFreshLocalRoastSession()
        {
            // An expired game login must not prevent an otherwise playable local match.
            while(creatingRoastSession)yield return null;
            creatingRoastSession=true;
            using(var req=ApiRequest(ApiUrl+"/sessions",JsonUtility.ToJson(new RoastSessionRequest())))
            {
                req.timeout=10;yield return req.SendWebRequest();
                if(req.result==UnityWebRequest.Result.Success)
                {try{localRoastToken=JsonUtility.FromJson<RoastSessionResponse>(req.downloadHandler.text)?.token??"";}catch(Exception){}}
            }
            creatingRoastSession=false;
        }
    }
}
