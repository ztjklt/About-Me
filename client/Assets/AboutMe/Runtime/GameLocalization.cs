using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AboutMe
{
    [Serializable] public class GameLanguageEntry { public string zh,en; }
    [Serializable] public class GameLanguageCatalog { public GameLanguageEntry[] entries; }
    public static class GameLocalization
    {
        const string LanguageKey="gameLanguage";
        static Dictionary<string,string> exact;
        static Dictionary<char,GameLanguageEntry[]> fragments;
        static Catalog propCatalog;
        static readonly List<KeyValuePair<Regex,string>> templates=new List<KeyValuePair<Regex,string>>();
        static readonly Dictionary<string,string> cache=new Dictionary<string,string>();
        static readonly Dictionary<string,int> protectedIds=new Dictionary<string,int>();
        static readonly List<string> protectedValues=new List<string>();
        static readonly HashSet<LocalizedLabel> labels=new HashSet<LocalizedLabel>();
        public static bool English=>PlayerPrefs.GetString(LanguageKey,"zh")=="en";
        public static void SetEnglish(bool value){PlayerPrefs.SetString(LanguageKey,value?"en":"zh");PlayerPrefs.Save();cache.Clear();Refresh();}
        static void Load()
        {
            if(exact!=null)return;
            var asset=Resources.Load<TextAsset>("game-language");
            var entries=JsonUtility.FromJson<GameLanguageCatalog>(asset.text).entries;
            exact=entries.ToDictionary(x=>x.zh,x=>x.en);
            foreach(var entry in entries.Where(x=>x.zh.Contains("{")))
            {
                var slots=Regex.Matches(entry.zh,@"\{[^}]+\}").Cast<System.Text.RegularExpressions.Match>().Select(x=>x.Value).ToArray();
                string pattern=Regex.Escape(entry.zh),replacement=entry.en;
                for(int i=0;i<slots.Length;i++){pattern=pattern.Replace(Regex.Escape(slots[i]),"(.+?)");replacement=replacement.Replace(slots[i],"$"+(i+1));}
                templates.Add(new KeyValuePair<Regex,string>(new Regex(pattern),replacement));
            }
            fragments=entries.Where(x=>x.zh.Length>0).GroupBy(x=>x.zh[0]).ToDictionary(x=>x.Key,x=>x.OrderByDescending(v=>v.zh.Length).ToArray());
        }
        // Keep player-authored names and introductions as data; only presentation is translated.
        public static string Protect(string value)
        {
            if(string.IsNullOrEmpty(value))return value;
            if(!protectedIds.TryGetValue(value,out int id)){id=protectedValues.Count;protectedValues.Add(value);protectedIds[value]=id;}
            return "\uE000"+id+"\uE001";
        }
        static string Unprotect(string value)
        {
            if(value.IndexOf('\uE000')<0)return value;
            var text=new StringBuilder();
            for(int i=0;i<value.Length;i++)
            {
                if(value[i]=='\uE000')
                {
                    int end=value.IndexOf('\uE001',i+1);
                    if(end>i&&int.TryParse(value.Substring(i+1,end-i-1),out int id)&&id>=0&&id<protectedValues.Count){text.Append(protectedValues[id]);i=end;continue;}
                }
                text.Append(value[i]);
            }
            return text.ToString();
        }
        public static void AddTranslation(string source,string english){Load();exact[source]=english;cache.Clear();Refresh();}
        public static string Text(string value)=>Text(value,English);
        public static string Text(string value,bool english)
        {
            if(string.IsNullOrEmpty(value))return value??"";
            if(!english)return Unprotect(value);
            Load();
            if(cache.TryGetValue(value,out var result))return result;
            if(exact.TryGetValue(value,out result))return Unprotect(result);
            foreach(var template in templates)if(template.Key.IsMatch(value))value=template.Key.Replace(value,template.Value);
            var translated=new StringBuilder();
            for(int i=0;i<value.Length;)
            {
                if(value[i]=='\uE000')
                {
                    int end=value.IndexOf('\uE001',i+1);
                    if(end>i){translated.Append(value,i,end-i+1);i=end+1;continue;}
                }
                GameLanguageEntry found=null;
                if(fragments.TryGetValue(value[i],out var choices))
                    foreach(var choice in choices)if(i+choice.zh.Length<=value.Length&&string.CompareOrdinal(value,i,choice.zh,0,choice.zh.Length)==0){found=choice;break;}
                if(found!=null){translated.Append(found.en);i+=found.zh.Length;}else translated.Append(value[i++]);
            }
            result=Unprotect(translated.ToString());if(cache.Count>8192)cache.Clear();cache[value]=result;return result;
        }
        public static string PersonaName(PersonaAsset asset)=>PersonaName(asset?.name,asset?.visualId);
        public static string PersonaName(PublicPersonaAsset asset)=>PersonaName(asset?.name,asset?.visualId);
        public static string PersonaName(string name,string visualId)
        {
            if(!English)return name??"";
            string value=Text(name);
            if(!value.Any(c=>c>='\u4e00'&&c<='\u9fff'))return value;
            if(propCatalog==null)propCatalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("catalog").text);
            var entry=propCatalog.items.FirstOrDefault(x=>x.visualId==visualId);
            return entry==null?value:Text(entry.label);
        }
        public static TextMesh AddMesh(GameObject target)
        {var text=target.AddComponent<TextMesh>();target.AddComponent<LocalizedLabel>().Configure(text);return text;}
        internal static void Register(LocalizedLabel label){labels.Add(label);}
        internal static void Unregister(LocalizedLabel label){labels.Remove(label);}
        public static void Refresh(){foreach(var label in labels.ToArray())if(label)label.Refresh();}
    }
}
