using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace AboutMe
{
    [Serializable] internal class RoastRequest
    {
        public string item_name, visual_id, template_id;
    }

    [Serializable] internal class RoastReply { public string roast; }

    // Fires when an enemy Persona enters the eating interaction. Only public item
    // fields are sent to the API; the original introduction is never in a match snapshot.
    public partial class GameUI
    {
        const string RoastFallback = "这名字还没来得及证明自己，就先证明了挺好吃。";
        string roastKey = "";

        void UpdateRoast(Snapshot state)
        {
            var me = state.players.FirstOrDefault(p => p.id == state.you);
            string targetId = me != null && me.interaction == "eat" ? me.carrying : "";
            string key = string.IsNullOrEmpty(targetId) ? "" : state.matchId + ":" + targetId;
            if (key == roastKey) return;
            roastKey = key;
            if (string.IsNullOrEmpty(targetId)) return;

            var item = state.ground.FirstOrDefault(x => x.id == targetId);
            if (item == null || item.asset == null || item.ownerId == state.you) return;

            // Show an item-specific line immediately, then replace it with the generated
            // version only if this same eating interaction is still active.
            eventText.text = RoastFallback;
            if (toastPulse) toastPulse.Play();
            StartCoroutine(AskRoast(new RoastRequest
            {
                item_name = item.asset.name,
                visual_id = item.asset.visualId,
                template_id = item.asset.templateId
            }, state.matchId, targetId));
        }

        IEnumerator AskRoast(RoastRequest body, string matchId, string itemId)
        {
            using (var req = GameNetwork.ApiRequest(net.ApiUrl + "/roast", JsonUtility.ToJson(body), net.Token))
            {
                req.timeout = 20;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;

                string line = "";
                try
                {
                    var reply = JsonUtility.FromJson<RoastReply>(req.downloadHandler.text);
                    if (!string.IsNullOrWhiteSpace(reply?.roast)) line = reply.roast.Trim();
                }
                catch (Exception) { }
                if (string.IsNullOrEmpty(line) || !StillEating(matchId, itemId) || !eventText) yield break;
                eventText.text = line;
                if (toastPulse) toastPulse.Play();
            }
        }

        bool StillEating(string matchId, string itemId)
        {
            var state = net == null ? null : net.State;
            if (state == null || state.matchId != matchId) return false;
            var me = state.players.FirstOrDefault(p => p.id == state.you);
            return me != null && me.interaction == "eat" && me.carrying == itemId;
        }
    }
}
