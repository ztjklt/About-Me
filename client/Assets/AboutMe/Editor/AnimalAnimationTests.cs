using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace AboutMe.Editor
{
    public static class AnimalAnimationTests
    {
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        static Vector3[] Vertices(GameObject go)
        {
            var all=new System.Collections.Generic.List<Vector3>();
            foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {var mesh=new Mesh();skin.BakeMesh(mesh,true);all.AddRange(mesh.vertices.Select(v=>skin.transform.TransformPoint(v)));UnityEngine.Object.DestroyImmediate(mesh);}
            return all.ToArray();
        }
        static float Delta(Vector3[] a,Vector3[] b)=>a.Zip(b,(x,y)=>Vector3.Distance(x,y)).Max();
        public static void Run()
        {
            int passed=0;var report=new System.Collections.Generic.List<string>();
            foreach(var id in AnimalPersonaCatalog.Visuals)
            {
                var go=PersonaVisualCatalog.Create(id,null);
                try
                {
                    var motion=go.GetComponent<AnimalPersonaMotion>();var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();
                    Check(motion&&motion.walk&&motion.walk.length>.1f&&motion.walk.legacy&&skins.Length>0&&skins.All(s=>s.bones.Length>=7),id+" missing actual rig or walk");passed++;
                    Check(skins.All(s=>s.sharedMesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.01f)),id+" unweighted vertices");passed++;
                    var rest=Vertices(go);var position=motion.model.localPosition;var rotation=motion.model.localRotation;var scale=motion.model.localScale;
                    float cycle=motion.walk.length/motion.walkSpeed;
                    motion.Present(true,0);var a=Vertices(go);motion.Present(true,cycle*.25f);var b=Vertices(go);
                    var bounds=new Bounds(rest[0],Vector3.zero);foreach(var v in rest)bounds.Encapsulate(v);
                    Check(Mathf.Abs(Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)-.78f)<.005f&&Mathf.Abs(bounds.min.y+.35f)<.005f,id+" visual size or foot alignment");passed++;
                    float deformation=Delta(a,b);Check(deformation>.005f&&deformation<.25f,id+" only moved wrapper; no limb deformation");passed++;
                    Check(motion.model.localPosition==position&&Quaternion.Angle(motion.model.localRotation,rotation)<.001f&&motion.model.localScale==scale,id+" clip displaced import wrapper");passed++;
                    motion.Present(true,cycle*1.25f);Check(Delta(b,Vertices(go))<.002f,id+" walk loop mismatch");passed++;
                    motion.Present(false,cycle*1.25f);Check(Delta(rest,Vertices(go))<.0002f,id+" landed/held skin retained walk pose");passed++;
                    report.Add(id+" bones="+skins.Sum(s=>s.bones.Length)+" triangles="+skins.Sum(s=>s.sharedMesh.triangles.Length/3)+" clip="+motion.walk.name+" seconds="+motion.walk.length+" maxMeshDeformation="+deformation);
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            Directory.CreateDirectory("../artifacts/animal-persona-animation");File.WriteAllLines("../artifacts/animal-persona-animation/unity-animation-checks.txt",report);
            Debug.Log("ANIMAL_SKIN_ANIMATION_TESTS_PASSED "+passed);
        }
        public static void RenderWalk()
        {
            Run();
            var cameraObject=new GameObject("Walk review camera");var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.94f,.91f,.85f);camera.orthographic=true;camera.orthographicSize=.94f;
            camera.transform.position=new Vector3(0,1.25f,5);camera.transform.LookAt(new Vector3(0,.4f,0));
            var lightObject=new GameObject("Walk review light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(35,160,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.75f,.75f,.75f);
            var all=new GameObject[4];for(int i=0;i<4;i++){var go=PersonaVisualCatalog.Create(AnimalPersonaCatalog.Visuals[i],null);all[i]=go;go.transform.position=new Vector3((i-1.5f)*1.1f,.35f,0);go.transform.rotation=Quaternion.Euler(0,-25,0);foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;}
            // Bake each sampled pose into a temporary render mesh. Camera.Render in one editor
            // frame otherwise reuses the GPU skin cache from the first pose.
            var skins=all.SelectMany(go=>go.GetComponentsInChildren<SkinnedMeshRenderer>()).ToArray();
            var baked=skins.Select(s=>new Mesh()).ToArray();
            var proxies=new GameObject[skins.Length];
            for(int i=0;i<skins.Length;i++)
            {
                var proxy=new GameObject("Baked walk review");proxies[i]=proxy;proxy.layer=30;
                proxy.transform.SetParent(skins[i].transform,false);proxy.AddComponent<MeshFilter>().sharedMesh=baked[i];
                proxy.AddComponent<MeshRenderer>().sharedMaterials=skins[i].sharedMaterials;skins[i].enabled=false;
            }
            var target=new RenderTexture(1600,650,24);camera.targetTexture=target;
            string output="../artifacts/animal-persona-animation/unity-frames";Directory.CreateDirectory(output);
            try
            {
                for(int frame=0;frame<120;frame++)
                {
                    foreach(var go in all)go.GetComponent<AnimalPersonaMotion>().Present(true,frame/30f);
                    for(int i=0;i<skins.Length;i++){skins[i].BakeMesh(baked[i],true);baked[i].RecalculateBounds();}
                    camera.Render();var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1600,650,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,650),0,0);image.Apply();File.WriteAllBytes(output+"/frame-"+frame.ToString("000")+".png",image.EncodeToPNG());RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(image);
                }
            }
            finally{foreach(var mesh in baked)UnityEngine.Object.DestroyImmediate(mesh);foreach(var go in all)UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lightObject);UnityEngine.Object.DestroyImmediate(target);}
            Debug.Log("ANIMAL_WALK_FRAMES_RENDERED 120");
        }
    }
}
