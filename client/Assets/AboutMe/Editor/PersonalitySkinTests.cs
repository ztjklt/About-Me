using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class PersonalitySkinTests
    {
        const string Folder="Assets/AboutMe/Resources/PersonaProps";
        public static void BuildSwimmingAsset()
        {
            var ring=SwimmingRingVisual.Create(null,"#86ADCB");
            try
            {
                string meshPath=Folder+"/swimming-ring-mesh.asset";
                var filter=ring.GetComponentInChildren<MeshFilter>();var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(existing){UnityEngine.Object.DestroyImmediate(filter.sharedMesh);filter.sharedMesh=existing;}
                else AssetDatabase.CreateAsset(filter.sharedMesh,meshPath);
                var renderer=ring.GetComponentInChildren<MeshRenderer>();
                var persisted=renderer.sharedMaterials;
                for(int i=0;i<persisted.Length;i++)
                {
                    string materialPath=Folder+"/swimming-ring-"+i+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(!material){material=new Material(persisted[i]);AssetDatabase.CreateAsset(material,materialPath);}
                    persisted[i]=material;
                }
                renderer.sharedMaterials=persisted;
                PrefabUtility.SaveAsPrefabAsset(ring,Folder+"/swimming-ring.prefab");
                AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                var light=new GameObject("Swimming thumbnail light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(25,-30,0);
                var camera=new GameObject("Swimming thumbnail camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.66f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                ring.transform.position=new Vector3(1000,1000,1000);camera.transform.position=ring.transform.position+new Vector3(.6f,.3f,3);camera.transform.LookAt(ring.transform.position);
                var rt=new RenderTexture(160,160,24);var previous=RenderTexture.active;camera.targetTexture=rt;
                try
                {
                    camera.Render();RenderTexture.active=rt;var image=new Texture2D(160,160,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,160,160),0,0);image.Apply();
                    string path="Assets/AboutMe/Resources/PersonaThumbs/swimming-ring.png";File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
                }
                finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);}
            }
            finally{UnityEngine.Object.DestroyImmediate(ring);}
        }
        public static void Run()
        {
            var ids=AppearanceCatalog.Data.slots.First(s=>s.id=="skinPattern").options.Select(o=>o.id).ToArray();
            var fingerprints=new System.Collections.Generic.HashSet<string>();
            foreach(var id in ids.Where(x=>x!="plain"))
            {
                var tex=PersonalitySkinSurface.CreateTexture(id,42);var pixels=tex.GetPixels32();
                if(!pixels.Any(p=>p.a>0)||!pixels.Any(p=>p.a==0))throw new Exception("Pattern lacks a visible design: "+id);
                fingerprints.Add(string.Join("",pixels.Select(p=>p.a.ToString("X2"))));UnityEngine.Object.DestroyImmediate(tex);
            }
            if(fingerprints.Count!=8)throw new Exception("Patterns are duplicates");
            var actor=new ToyActor("Personality test","#79CFC5",0,null,new AppearanceData{skinPattern="flame",skinSeed=42});
            try
            {
                var skin=actor.Skin;var uv=new System.Collections.Generic.List<Vector3>();skin.sharedMesh.GetUVs(0,uv);
                if(!uv.Any(v=>v.z==1)||!uv.Any(v=>v.z==0)||skin.sharedMaterial.GetTexture("_PatternTex")==null)throw new Exception("Pattern is not attached to actual skinned clothing");
                int bones=actor.DeformationBoneCount;actor.ApplyAppearance(new AppearanceData{skinPattern="wave",skinSeed=123});
                if(actor.DeformationBoneCount!=bones||actor.Skin.sharedMesh.blendShapeCount!=4)throw new Exception("Personality skin changed the rig or swallow animation");
                actor.ApplyAppearance(new AppearanceData());if(actor.root.GetComponent<PersonalitySkinSurface>().Active)throw new Exception("Plain reset leaked a pattern");
            }
            finally{UnityEngine.Object.DestroyImmediate(actor.root);}
            var prefab=Resources.Load<GameObject>("PersonaProps/swimming-ring");
            if(!prefab||prefab.GetComponent<PersonaVisualMeta>()?.templateId!="Cushion"||!prefab.GetComponent<PersonaVisualMeta>().Grip)throw new Exception("Swimming prefab is not usable");
            var mesh=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            if(prefab.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>!m||!AssetDatabase.Contains(m))))throw new Exception("Swimming materials are not packaged assets");
            if(mesh.vertexCount>600||mesh.vertices.Min(v=>Mathf.Sqrt(v.x*v.x+v.y*v.y))<.29f)throw new Exception("Swimming ring lost its hole or mesh budget");
            Debug.Log("ABOUTME_PERSONALITY_SKIN_TESTS_PASSED eight distinct textures / real skinned mesh / unchanged rig and swallow / swimming ring asset");
        }
        public static void BuildDeliverables(){BuildSwimmingAsset();Run();SoftCombatTests.Run();ButtonFlowTests.BuildDeliverables();}
    }
}
