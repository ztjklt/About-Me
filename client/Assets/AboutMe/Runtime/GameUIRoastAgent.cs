using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace AboutMe
{
    [Serializable] internal class RoastRequest { public string weapon_name, picker_name, loser_name, attribute, category; }
    [Serializable] internal class RoastReply { public string roast; }

    // 「毒舌调侃官」：玩家捡起别人掉落的 Persona 时，向 /roast 要一句临场调侃。
    // 它只读快照、只改提示文字，不参与任何规则判定；服务不可用时退回固定台词。
    public partial class GameUI
    {
        public const string RoastFallback = "这把武器有点东西，但我一时没想好怎么损它。";
        string roastHolding = "";

        // 拾取是快照状态：本地玩家的 carrying 从空变为别人掉落的 Persona 时才算一次调侃。
        void UpdateRoast(Snapshot state)
        {
            var me = state.players.FirstOrDefault(p => p.id == state.you);
            string holding = me == null ? "" : me.carrying;
            if (holding == roastHolding) return;
            roastHolding = holding;
            if (string.IsNullOrEmpty(holding)) return;
            var item = state.ground.FirstOrDefault(x => x.id == holding);
            if (item == null || item.asset == null || item.ownerId == state.you) return;
            var owner = state.players.FirstOrDefault(p => p.id == item.ownerId);
            StartCoroutine(AskRoast(new RoastRequest { weapon_name = item.asset.name, picker_name = me.nickname, loser_name = owner == null ? "" : owner.nickname }));
        }

        IEnumerator AskRoast(RoastRequest body)
        {
            string line = RoastFallback;
            using (var req = GameNetwork.ApiRequest(net.ApiUrl + "/roast", JsonUtility.ToJson(body), net.Token))
            {
                req.timeout = 20;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { var reply = JsonUtility.FromJson<RoastReply>(req.downloadHandler.text); if (!string.IsNullOrWhiteSpace(reply?.roast)) line = reply.roast.Trim(); }
                    catch (Exception) { }
                }
            }
            if (!eventText) return;
            eventText.text = line;
            if (toastPulse) toastPulse.Play();
        }
    }
}
