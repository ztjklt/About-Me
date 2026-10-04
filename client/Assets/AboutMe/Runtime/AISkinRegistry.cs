using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    [Serializable] public class AISkinDefinition {public string brand,character,skin,label,english;}
    [Serializable] public class AISkinCatalog {public int version;public AISkinDefinition[] skins;}
    public static class AISkinRegistry
    {
        static AISkinCatalog data;
        public static AISkinDefinition[] All=>(data??(data=JsonUtility.FromJson<AISkinCatalog>(Resources.Load<TextAsset>("ai-skins").text))).skins;
        public static string CharacterForSkin(string skin)=>All.FirstOrDefault(x=>x.skin==skin)?.character??"normal";
        public static string SkinForCharacter(string character)=>All.FirstOrDefault(x=>x.character==character)?.skin??"normal";
        public static bool KnownCharacter(string character)=>character=="normal"||All.Any(x=>x.character==character);
        public static bool CanUse(Profile profile,string skin)=>skin=="normal"||profile!=null&&All.Any(x=>x.skin==skin)&&
            ((profile.unlockedCharacterSkins??Array.Empty<string>()).Contains(skin)||skin=="ds-girl"&&profile.dsGirlUnlocked||skin=="claude-girl"&&profile.claudeGirlUnlocked);
        public static string Verified(Profile profile)=>profile!=null&&CanUse(profile,profile.characterSkin)?profile.characterSkin:"normal";
        public static string Label(string skin)=>skin=="normal"?(GameLocalization.English?"Normal":"普通"):All.FirstOrDefault(x=>x.skin==skin) is AISkinDefinition d?(GameLocalization.English?d.english:d.label):skin;
    }
}
