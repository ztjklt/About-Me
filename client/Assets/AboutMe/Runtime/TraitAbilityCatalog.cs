using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    [Serializable] public class WordAbilityChoice {public string text,category,abilityId;}
    [Serializable] public class TraitAbilityDefinition {public string id,label,hint;}
    [Serializable] public class TraitWordDefinition {public string category,text,abilityId;public string[] aliases;}
    [Serializable] public class TraitAbilityData {public int version;public TraitAbilityDefinition[] abilities;public TraitWordDefinition[] words,mbti;}
    public static class TraitAbilityCatalog
    {
        public const int ProtocolVersion=5;
        public const string VisualId="trait-word";
        static TraitAbilityData data;
        public static TraitAbilityData Data=>data??(data=JsonUtility.FromJson<TraitAbilityData>(Resources.Load<TextAsset>("trait-abilities").text));
        public static TraitWordDefinition[] Words=>Data.words.Concat(Data.mbti).ToArray();
        public static bool IsAbility(string id)=>Data.abilities.Any(a=>a.id==id);
        public static bool IsCategory(string id)=>id=="personality"||id=="mood"||id=="mbti"||id=="custom";
        public static TraitWordDefinition FindWord(string text)=>Words.FirstOrDefault(w=>string.Equals(w.text,text,StringComparison.OrdinalIgnoreCase)||(w.aliases??Array.Empty<string>()).Any(a=>string.Equals(a,text,StringComparison.OrdinalIgnoreCase)));
        public static PartyMechanic Resolve(string category,string text,string ability)
        {
            if(!IsCategory(category)||string.IsNullOrWhiteSpace(text)||!IsAbility(ability))return null;
            var known=FindWord(text);if(category!="custom"&&(known==null||known.category!=category))return null;
            return PartyMechanicRegistry.Find(ability);
        }
        public static string AbilityLabel(string id)=>Data.abilities.FirstOrDefault(a=>a.id==id)?.label??"请选择能力";
    }
}
