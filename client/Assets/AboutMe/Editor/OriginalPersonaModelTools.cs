using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class OriginalPersonaModelTools
    {
        const string Folder="Assets/AboutMe/Resources/PersonaProps";
        public static void BuildDeliverables(){Build();PartyMechanicTests.Run();PartyMovementTests.Run();PartyFieldTests.Run();PartyGuardTests.Run();PartyLootTests.Run();PartyMachineryTests.Run();PartyControlTests.Run();NpcPracticeTests.Run();PersonalitySkinTests.BuildDeliverables();}
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var authored=JsonUtility.FromJson<OriginalModelRecipes>(File.ReadAllText("Assets/AboutMe/Resources/persona-original-models.json")).items;
            int count=0;var fingerprints=new System.Collections.Generic.HashSet<string>();
            foreach(var recipe in authored)
            {
                var root=OriginalPersonaModels.Create(recipe);
                try
                {
                    var filter=root.GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;
                    if(mesh.triangles.Length/3>1500||mesh.vertexCount==0||root.GetComponentInChildren<Renderer>().sharedMaterial==null)throw new Exception("Original model budget/material failure: "+recipe.id);
                    string geometry=string.Join(";",mesh.vertices.Select(v=>v.ToString("F4")))+string.Join(",",mesh.triangles);
                    if(!fingerprints.Add(geometry))throw new Exception("Original silhouettes duplicate: "+recipe.id);
                    string path=Folder+"/"+recipe.id+"-mesh.asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(previous){EditorUtility.CopySerialized(mesh,previous);UnityEngine.Object.DestroyImmediate(mesh);filter.sharedMesh=previous;}else AssetDatabase.CreateAsset(mesh,path);
                    PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+recipe.id+".prefab");count++;
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Thumbnails();Review();MachineryDeliveryReview.Render();Debug.Log("ORIGINAL_PERSONA_MODELS_PASSED "+count+" unique authored geometry / single persistent palette / <=1500 triangles / grip and swallow anchors / packaged thumbnails");
        }
        static void Thumbnails()
        {
            var camera=new GameObject("Original toy thumbnail camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.62f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            var light=new GameObject("Original toy thumbnail light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(30,-20,0);
            var render=new RenderTexture(160,160,24);var old=RenderTexture.active;camera.targetTexture=render;
            try
            {
                foreach(var recipe in OriginalPersonaModels.All)
                {
                    var model=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("PersonaProps/"+recipe.id));model.transform.position=new Vector3(1000,1000,1000);
                    try
                    {
                        camera.transform.position=model.transform.position+new Vector3(.9f,.7f,3);camera.transform.LookAt(model.transform.position);camera.Render();RenderTexture.active=render;
                        var texture=new Texture2D(160,160,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,160,160),0,0);texture.Apply();
                        string path="Assets/AboutMe/Resources/PersonaThumbs/"+recipe.id+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
                        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
                    }
                    finally{UnityEngine.Object.DestroyImmediate(model);}
                }
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);}
        }
        public static void Review()
        {
            var camera=new GameObject("Original model review camera").AddComponent<Camera>();camera.orthographic=true;int rows=Mathf.CeilToInt(OriginalPersonaModels.All.Length/10f);camera.orthographicSize=rows*1.27f*.5f+.7f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.94f,.92f,.86f);
            var light=new GameObject("Original model review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(30,-20,0);
            var group=new GameObject("Original model review");group.transform.position=new Vector3(1000,1000,1000);
            int index=0;
            foreach(var recipe in OriginalPersonaModels.All)
            {
                var model=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("PersonaProps/"+recipe.id),group.transform,false);model.transform.localPosition=new Vector3((index%10-4.5f)*1.25f,((rows-1)*.5f-index/10)*1.27f,0);model.transform.localRotation=Quaternion.Euler(-10,-18,0);
                var label=new GameObject("Label").AddComponent<TextMesh>();label.transform.SetParent(group.transform,false);label.transform.localPosition=model.transform.localPosition+Vector3.down*.51f;label.transform.localRotation=Quaternion.Euler(0,180,0);label.text=recipe.label;label.font=Resources.Load<Font>("AboutMeFont");label.fontSize=45;label.characterSize=.033f;label.anchor=TextAnchor.MiddleCenter;label.color=new Color(.13f,.17f,.24f);if(label.font)label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;index++;
            }
            camera.transform.position=group.transform.position+new Vector3(0,0,10);camera.transform.LookAt(group.transform.position);
            var render=new RenderTexture(2000,rows*220,24);var old=RenderTexture.active;camera.targetTexture=render;
            try
            {
                camera.Render();RenderTexture.active=render;var texture=new Texture2D(2000,rows*220,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,2000,rows*220),0,0);texture.Apply();
                string output=Path.GetFullPath("../artifacts/persona-original-models");Directory.CreateDirectory(output);File.WriteAllBytes(output+"/priority-202-review.png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);UnityEngine.Object.DestroyImmediate(group);}
        }
    }
}
