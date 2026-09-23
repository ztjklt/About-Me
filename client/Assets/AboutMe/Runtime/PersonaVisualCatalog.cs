using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    [Serializable] public class PersonaVisualDefinition
    {
        public string visualId, label, templateId, resourcePath, sourceAssetPath, source, license;
        public string color = "#FFFFFF";
        public float scale = 1, groundOffset, colliderRadius = .5f, swallowScale = 1;
        public Vector3 grip;
    }
    [Serializable] public class PersonaVisualDefinitions { public int version; public PersonaVisualDefinition[] items; }

    // A visual registry deliberately contains no gameplay code. The server accepts only the
    // matching (templateId, visualId) pairs from shared/catalog.json.
    public static class PersonaVisualCatalog
    {
        static Dictionary<string, PersonaVisualDefinition> definitions;
        static readonly HashSet<string> missing = new HashSet<string>();
        static void Load()
        {
            if(definitions != null) return;
            var source = Resources.Load<TextAsset>("persona-visuals");
            var data = source ? JsonUtility.FromJson<PersonaVisualDefinitions>(source.text) : null;
            definitions = (data?.items ?? Array.Empty<PersonaVisualDefinition>())
                .Where(x => !string.IsNullOrWhiteSpace(x.visualId))
                .GroupBy(x => x.visualId).ToDictionary(x => x.Key, x => x.First());
        }
        public static PersonaVisualDefinition Find(string visualId)
        {
            Load(); definitions.TryGetValue(visualId ?? "", out var definition); return definition;
        }
        public static PersonaVisualDefinition[] All { get { Load(); return definitions.Values.OrderBy(x=>x.visualId).ToArray(); } }
        public static GameObject Create(string visualId, Transform parent)
        {
            var definition=Find(visualId);
            if(definition==null || string.IsNullOrWhiteSpace(definition.resourcePath)) return null;
            var source=Resources.Load<GameObject>(definition.resourcePath);
            if(!source)
            {
                if(missing.Add(visualId)) Debug.LogWarning("PERSONA_VISUAL_FALLBACK " + visualId + " missing " + definition.resourcePath);
                return null;
            }
            var result=UnityEngine.Object.Instantiate(source,parent,false);
            result.name=visualId;
            return result;
        }
    }

}
