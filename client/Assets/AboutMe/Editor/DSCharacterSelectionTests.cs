using System;
using System.Linq;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class DSCharacterSelectionTests
    {
        static void Check(bool passed,string message)
        {if(!passed)throw new Exception(message);}

        static Profile Player(string id,bool unlocked,string skin)
        {
            return new Profile{playerId=id,nickname=id,dsGirlUnlocked=unlocked,characterSkin=skin,
                appearance=AppearanceData.Demo(0),personas=new[]{new PersonaAsset{assetId=id+"-ball",name="篮球",templateId="Projectile",visualId="basketball",color="#FFD773"}}};
        }

        public static void Run()
        {
            var ds=Player("ds",true,"ds-girl");
            var normal=Player("normal",false,"normal");
            using(var match=new Match("DS",new MatchRules()))
            {
                Check(match.Add(ds)&&match.Add(normal),"Two profiles could not join");
                var wire=JsonUtility.ToJson(match.View("ds"));
                var received=JsonUtility.FromJson<Snapshot>(wire);
                Check(received.players.Single(p=>p.id=="ds").characterSkin=="ds-girl","DS choice was lost in the room snapshot");
                Check(received.players.Single(p=>p.id=="normal").characterSkin=="normal","Normal player changed in the room snapshot");

                var forged=Player("normal",false,"ds-girl");
                Check(match.Add(forged),"Lobby reconnect failed");
                Check(match.View("ds").players.Single(p=>p.id=="normal").characterSkin=="normal","Locked choice reached the room snapshot");

                Check(match.Add(Player("ds",true,"normal")),"DS lobby reconnect failed");
                Check(match.View("normal").players.Single(p=>p.id=="ds").characterSkin=="normal","Lobby reconnect did not refresh the choice");
            }

            var appearance=AppearanceData.Demo(0);appearance.character="ds";
            Check(!JsonUtility.ToJson(appearance).Contains("character"),"Character choice leaked into catalog appearance JSON");
            var actor=new ToyActor("DS costume",appearance.topColor,0,null,appearance);
            try
            {
                Check(actor.Skin&&actor.Skin.enabled&&actor.root.GetComponentInChildren<DSGirlOutfit>()!=null,"DS outfit did not attach to the original body");
                appearance.character="normal";actor.ApplyAppearance(appearance);
                Check(actor.Skin&&actor.Skin.enabled&&actor.root.GetComponentInChildren<DSGirlOutfit>()==null,"Normal outfit did not return after switching");
            }
            finally{UnityEngine.Object.DestroyImmediate(actor.root);}
            Debug.Log("DS_CHARACTER_CONTRACT_OK");
        }
    }
}
