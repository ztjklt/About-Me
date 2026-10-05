using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class AnimalPersonaImport
    {
        public static void Render()
        {
            var all=new System.Collections.Generic.List<GameObject>();
            var cameraObject=new GameObject("Animal model camera");all.Add(cameraObject);var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.94f,.91f,.85f);camera.orthographic=true;camera.orthographicSize=1.1f;
            camera.transform.position=new Vector3(0,1.3f,5);camera.transform.LookAt(new Vector3(0,.45f,0));
            var lightObject=new GameObject("Animal light");all.Add(lightObject);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(35,160,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.75f,.75f,.75f);
            for(int i=0;i<4;i++)
            {
                var go=PersonaVisualCatalog.Create(AnimalPersonaCatalog.Visuals[i],null);all.Add(go);go.transform.position=new Vector3((i-1.5f)*1.25f,.48f,0);go.transform.rotation=Quaternion.Euler(0,-20,0);
                foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            }
            var target=new RenderTexture(1600,700,24);camera.targetTexture=target;camera.Render();var prior=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1600,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,700),0,0);image.Apply();
            Directory.CreateDirectory("../artifacts/animal-persona");File.WriteAllBytes("../artifacts/animal-persona/animal-models.png",image.EncodeToPNG());
            RenderTexture.active=prior;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);foreach(var go in all)UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("ANIMAL_MODELS_RENDERED");
        }
        public static void Build()
        {
            foreach(var id in AnimalPersonaCatalog.Visuals)
            {
                string folder="Assets/AboutMe/Art/TripoAnimals/"+id,path=folder+"/model.fbx";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)throw new Exception("Missing animal FBX "+path);
                importer.isReadable=true;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                Directory.CreateDirectory(folder+"/Textures");importer.ExtractTextures(folder+"/Textures");importer.SaveAndReimport();AssetDatabase.Refresh();
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);var root=new GameObject(id);
                try
                {
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(root.transform,false);
                    // Preserve the imported -90 X axis correction. FBX handedness maps Tripo +X to Unity -X; +90 yaw faces +Z.
                    Debug.Log("ANIMAL_SOURCE_TRANSFORM "+id+" root="+source.transform.localEulerAngles+" child="+string.Join(";",model.GetComponentsInChildren<Transform>().Select(t=>t.name+":"+t.localEulerAngles)));
                    model.transform.localRotation=Quaternion.Euler(0,90,0)*model.transform.localRotation;
                    var renderers=model.GetComponentsInChildren<Renderer>();
                    if(renderers.Length==0)throw new Exception("Empty model "+id);
                    Bounds Bounds(){var b=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))b.Encapsulate(renderer.bounds);return b;}
                    var bounds=Bounds();float max=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);model.transform.localScale*=.78f/max;
                    bounds=Bounds();model.transform.position-=bounds.center;bounds=Bounds();
                    int triangles=model.GetComponentsInChildren<MeshFilter>().Sum(x=>x.sharedMesh.triangles.Length/3);
                    if(triangles<=0||triangles>6000)throw new Exception("Animal triangle budget "+id+" "+triangles);
                    foreach(var renderer in renderers)
                        foreach(var mat in renderer.sharedMaterials)
                            if(mat==null||mat.mainTexture==null)throw new Exception("Missing animal texture "+id);
                    foreach(var renderer in renderers)
                    {
                        var materials=renderer.sharedMaterials;
                        for(int n=0;n<materials.Length;n++)
                        {
                            var basis=materials[n];Debug.Log("ANIMAL_IMPORTED_MATERIAL "+id+" shader="+basis.shader.name+" color="+basis.color);
                            string matPath=folder+"/Animal-"+n+".mat";
                            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                            if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,matPath);}
                            mat.color=Color.white;mat.mainTexture=basis.mainTexture;mat.SetFloat("_Metallic",0);mat.SetFloat("_Glossiness",.18f);
                            mat.SetTexture("_EmissionMap",basis.mainTexture);mat.SetColor("_EmissionColor",Color.white*.18f);mat.EnableKeyword("_EMISSION");
                            materials[n]=mat;EditorUtility.SetDirty(mat);
                        }
                        renderer.sharedMaterials=materials;
                    }
                    var grip=new GameObject("Grip");grip.transform.SetParent(root.transform,false);
                    var swallow=new GameObject("SwallowCenter");swallow.transform.SetParent(root.transform,false);
                    var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;collider.enabled=false;
                    var meta=root.AddComponent<PersonaVisualMeta>();meta.visualId=id;meta.templateId="Summon";meta.source="Tripo P1-20260311 / user authorized generation";meta.license="Generated asset; Tripo account terms";meta.colliderRadius=bounds.extents.magnitude;meta.swallowScale=1;
                    var motion=root.AddComponent<AnimalPersonaMotion>();motion.model=model.transform;motion.restPosition=model.transform.localPosition;
                    string prefab="Assets/AboutMe/Resources/PersonaProps/"+id+".prefab";PrefabUtility.SaveAsPrefabAsset(root,prefab);
                    Debug.Log("ANIMAL_MODEL_IMPORTED "+id+" triangles="+triangles+" bounds="+bounds.size+" materials="+renderers.Sum(x=>x.sharedMaterials.Length));
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();Debug.Log("ANIMAL_MODELS_IMPORTED 4");Render();
        }
    }
}
