using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AboutMe.Editor
{
    public static class CampusImport
    {
        [Serializable] class MeshData { public string name, color; public Vector3[] vertices; public int[] triangles; }
        [Serializable] class ModelData { public MeshData[] meshes; }
        const string AssetRoot="Assets/AboutMe/Art/CampusV04";
        const string ResourceRoot="Assets/AboutMe/Resources/CampusV04";
        [MenuItem("About Me/Preview Tripo campus V0.4")]
        public static void Preview()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("CampusV04/Campus"));
            RenderSettings.ambientLight=new Color(.72f,.76f,.8f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            var sun=new GameObject("Campus daylight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);QualitySettings.shadowDistance=150;
            var camera=new GameObject("Campus preview camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#D8E6EA");camera.fieldOfView=48;camera.farClipPlane=220;
            camera.transform.position=new Vector3(0,36,-91);camera.transform.LookAt(new Vector3(0,3,3));
            DeskBackdrop.Create();DeskBackdrop.ConfigureView(camera);
            DeskBackdrop.ConfigureLight(sun);
            var actors=new GameObject("Scale reference players").transform;
            for(int i=0;i<3;i++)
            {
                var actor=new ToyActor("Scale player "+i,new[]{"#79CFC5","#F3AFC2","#FFD773"}[i],0,actors);actor.root.transform.position=new Vector3((i-1)*5,1,-16);actor.label.text="";
            }
            foreach(var prop in new Match("PREVIEW",new MatchRules()).Props){var go=ToyFactory.Item(prop.visual,"#79CFC5",actors);go.transform.position=prop.position;}
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,"Assets/AboutMe/Scenes/CampusV04Preview.unity");
            var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            File.WriteAllBytes("../artifacts/campus-v04/unity-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);Debug.Log("CAMPUS_PREVIEW_RENDERED");
        }
        [MenuItem("About Me/Import Tripo campus V0.4")]
        public static void Build()
        {
            Directory.CreateDirectory(AssetRoot);Directory.CreateDirectory(ResourceRoot);AssetDatabase.Refresh();
            var root=Path.GetFullPath("../artifacts/campus-v04/engine");
            foreach(var modelName in new[]{"Campus","ClubBay","HandOpen","HandFist"})
            {
                var source=JsonUtility.FromJson<ModelData>(File.ReadAllText(Path.Combine(root,modelName+".json")));
                var model=new GameObject(modelName);int index=0;
                foreach(var data in source.meshes)
                {
                    var mesh=new Mesh{name=data.name,indexFormat=IndexFormat.UInt32};mesh.vertices=data.vertices;mesh.triangles=data.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
                    var path=$"{AssetRoot}/{modelName}-{index}.asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
                    else AssetDatabase.CreateAsset(mesh,path);
                    string materialPath=$"{AssetRoot}/{data.color.TrimStart('#')}.mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(!material){material=new Material(Shader.Find("AboutMe/CampusFlat"));AssetDatabase.CreateAsset(material,materialPath);}
                    material.shader=Shader.Find("AboutMe/CampusFlat");material.color=ToyFactory.ColorOf(data.color);
                    var part=new GameObject(data.name);part.transform.SetParent(model.transform,false);
                    part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=material;
                    if(modelName=="Campus")part.isStatic=true;
                    index++;
                }
                if(modelName=="Campus")
                {
                    var collision=JsonUtility.FromJson<CampusCollisionData>(File.ReadAllText(Path.Combine(root,"Collision.json")));
                    foreach(var shape in collision.solids)
                    {
                        var go=new GameObject("Collision_"+shape.name);go.transform.SetParent(model.transform,false);go.isStatic=true;
                        var collider=go.AddComponent<BoxCollider>();collider.center=shape.center;collider.size=shape.size;
                    }
                    File.Copy(Path.Combine(root,"Collision.json"),ResourceRoot+"/Collision.json",true);
                }
                PrefabUtility.SaveAsPrefabAsset(model,ResourceRoot+"/"+modelName+".prefab");UnityEngine.Object.DestroyImmediate(model);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("CAMPUS_V04_IMPORTED");
            ProjectTools.TestRules();
        }
    }
}
