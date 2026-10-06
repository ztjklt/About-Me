using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace AboutMe
{
    [Serializable] class RoastSessionRequest { public string nickname="试玩玩家"; }
    [Serializable] class RoastSessionResponse { public string token; }
    [Serializable] class PublicRoastItem { public string item_name,template_id,visual_id; }
    [Serializable] class PreparedRoastLine { public string item_name,template_id,visual_id,roast; }
    [Serializable] class RoastPreparationReply { public string aiSource; public PreparedRoastLine[] lines; }
    public partial class GameNetwork
    {
        string localRoastToken="",localRoastApi="";
        bool creatingRoastSession;
        float nextRoastSession;
        void QueueRoasts(Match room,bool local=false)
        {
            if(room.Phase==MatchPhase.Finished)return;
            if(!local&&string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAME_SERVER_KEY")))return;
            var pending=room.Items.Select(x=>x.asset).GroupBy(Match.RoastItemKey).Select(g=>g.First())
                .Where(x=>!room.RoastPreparationRequested.Contains(Match.RoastItemKey(x))).ToArray();
            if(pending.Length>0&&!room.RoastPreparationPending)
            {
                foreach(var asset in pending)room.RoastPreparationRequested.Add(Match.RoastItemKey(asset));
                room.RoastPreparationPending=true;
                StartCoroutine(PrepareEatingRoasts(room,pending,local));
            }
            int seen=roastCursor.TryGetValue(room.MatchId,out var count)?count:0;
            roastCursor[room.MatchId]=room.EatingRoasts.Count;
            for(int i=seen;i<room.EatingRoasts.Count;i++)
            {
                var attempt=room.EatingRoasts[i];var item=room.Item(attempt.itemId);
                if(item!=null&&room.PreparedRoasts.TryGetValue(Match.RoastItemKey(item.asset),out var line))
                    room.SayEatingRoast(attempt,line);
            }
        }
        IEnumerator PrepareEatingRoasts(Match room,PersonaAsset[] assets,bool local)
        {
            try
            {
                if(local)yield return EnsureLocalRoastSession();
                if(!RoastRoomCurrent(room,local)||(local&&string.IsNullOrEmpty(localRoastToken)))yield break;
                // The preparation API accepts public item descriptors, never player introductions.
                string json="{\"items\":["+string.Join(",",assets.Select(x=>JsonUtility.ToJson(new PublicRoastItem{
                    item_name=x.name,template_id=x.templateId,visual_id=x.visualId})))+"]}";
                using(var req=ApiRequest(ApiUrl+(local?"/roast/prepare":"/internal/roast/prepare"),json,local?localRoastToken:""))
                {
                    req.timeout=45;if(!local)req.SetRequestHeader("X-Game-Key",Environment.GetEnvironmentVariable("GAME_SERVER_KEY"));
                    yield return req.SendWebRequest();
                    if(!RoastRoomCurrent(room,local)||req.result!=UnityWebRequest.Result.Success)yield break;
                    RoastPreparationReply reply=null;try{reply=JsonUtility.FromJson<RoastPreparationReply>(req.downloadHandler.text);}catch(Exception){}
                    if(reply?.aiSource!="provider"||reply.lines==null)yield break;
                    foreach(var row in reply.lines)
                    {
                        var asset=assets.FirstOrDefault(x=>x.name==row.item_name&&x.templateId==row.template_id&&x.visualId==row.visual_id);
                        if(asset!=null&&!string.IsNullOrWhiteSpace(row.roast)&&row.roast.Length<=120&&row.roast.IndexOfAny(new[]{'\r','\n'})<0)
                            room.PreparedRoasts[Match.RoastItemKey(asset)]=row.roast.Trim();
                    }
                    Debug.Log("ABOUTME_ROAST_PREPARED count="+room.PreparedRoasts.Count+" source=provider");
                }
            }
            finally{room.RoastPreparationPending=false;}
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
    }
}
