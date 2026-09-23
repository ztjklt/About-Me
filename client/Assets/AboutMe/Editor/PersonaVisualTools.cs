using System;
using System.IO;
using System.Linq;
using AboutMe;
using UnityEditor;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class PersonaVisualTools
    {
        const string PrefabFolder="Assets/AboutMe/Resources/PersonaProps";
        [MenuItem("About Me/Persona visuals/Build CC0 prefabs")]
        public static void BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            int built=0;
            foreach(var definition in PersonaVisualCatalog.All.Where(x=>!string.IsNullOrWhiteSpace(x.sourceAssetPath)))
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(definition.sourceAssetPath);
                if(!source) throw new Exception("PERSONA_SOURCE_MISSING " + definition.sourceAssetPath);
                var root=new GameObject(definition.visualId);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform,false);
                model.transform.localScale=Vector3.one*Mathf.Max(.001f,definition.scale);
                model.transform.localPosition=Vector3.up*definition.groundOffset;
                var grip=new GameObject("Grip");grip.transform.SetParent(root.transform,false);grip.transform.localPosition=definition.grip;
                var collider=root.AddComponent<BoxCollider>();collider.enabled=false;collider.size=Vector3.one*Mathf.Max(.05f,definition.colliderRadius*2);
                var meta=root.AddComponent<PersonaVisualMeta>();meta.visualId=definition.visualId;meta.templateId=definition.templateId;
                meta.source=definition.source;meta.license=definition.license;meta.colliderRadius=definition.colliderRadius;meta.swallowScale=definition.swallowScale;
                PrefabUtility.SaveAsPrefabAsset(root,PrefabFolder+"/"+definition.visualId+".prefab");
                UnityEngine.Object.DestroyImmediate(root);built++;
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("PERSONA_PREFABS_BUILT "+built);
        }
        [MenuItem("About Me/Persona visuals/Validate CC0 prefabs")]
        public static void Validate()
        {
            var definitions=PersonaVisualCatalog.All.Where(x=>!string.IsNullOrWhiteSpace(x.sourceAssetPath)).ToArray();
            if(definitions.Length!=48)throw new Exception("PERSONA_VISUAL_COUNT expected 48, got "+definitions.Length);
            foreach(var definition in definitions)
            {
                if(!File.Exists(definition.sourceAssetPath))throw new Exception("PERSONA_FILE_MISSING "+definition.sourceAssetPath);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/"+definition.visualId+".prefab");
                if(!prefab)throw new Exception("PERSONA_PREFAB_MISSING "+definition.visualId);
                var meta=prefab.GetComponent<PersonaVisualMeta>();
                if(!meta||meta.visualId!=definition.visualId||!meta.Grip)throw new Exception("PERSONA_META_INVALID "+definition.visualId);
                if(!prefab.GetComponent<BoxCollider>())throw new Exception("PERSONA_COLLIDER_MISSING "+definition.visualId);
                var renderers=prefab.GetComponentsInChildren<Renderer>(true);
                if(renderers.Length==0||renderers.Any(x=>x.sharedMaterials.Any(m=>m==null)))throw new Exception("PERSONA_MATERIAL_INVALID "+definition.visualId);
            }
            Debug.Log("PERSONA_VISUALS_VALIDATED "+definitions.Length);
        }
    }
}
