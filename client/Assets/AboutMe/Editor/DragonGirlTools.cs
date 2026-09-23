using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class DragonGirlTools
    {
        const string ModelPath="Assets/AboutMe/Art/DragonGirl/DragonGirl_Clean.fbx";
        const string MaterialPath="Assets/AboutMe/Art/DragonGirl/DragonGirl_DoubleSided.mat";
        const string BaseColorPath="Assets/AboutMe/Art/DragonGirl/Textures/tripo_node_2bf669be-c1fa-4e7e-84cb-a8306a8bd575_BaseColor.jpg";
        const string PrefabPath="Assets/AboutMe/Resources/CampusDoll/DragonGirl.prefab";
        const string Output="../artifacts/dragon-girl";

        [Serializable] sealed class WeightAudit
        {
            public int schemaVersion,vertices,polygons,lockedVertices;
            public float lockedMaxDisplacementUnderLimbStress,lockedMeanDisplacementUnderLimbStress;
            public float movingMaxDisplacementUnderLimbStress,movingMeanDisplacementUnderLimbStress;
        }

        struct ReviewPose
        {
            public string name,action;
            public float time,runBlend;
            public Vector3 velocity;
            public int variant;
            public ReviewPose(string n,float t,Vector3 v,string a="",float run=0,int side=0)
            {name=n;time=t;velocity=v;action=a;runBlend=run;variant=side;}
        }

        static readonly ReviewPose[] reviewPoses=
        {
            new ReviewPose("idle",.55f,Vector3.zero),
            new ReviewPose("walk",.55f,new Vector3(0,0,5.5f)),
            new ReviewPose("run",.45f,new Vector3(0,0,7.8f),"",1),
            new ReviewPose("light-hit",.10f,Vector3.zero,"light",0,0),
            new ReviewPose("heavy-hit",.16f,Vector3.zero,"heavy",0,1),
            new ReviewPose("grab",.30f,Vector3.zero,"grab"),
            new ReviewPose("hurt",.10f,Vector3.zero,"hurt"),
            new ReviewPose("fall",.50f,Vector3.zero,"fall"),
        };

        [MenuItem("About Me/Dragon Girl/Import, validate and review")]
        public static void ImportValidateReview()
        {
            Import();Validate();Review();
            Debug.Log("DRAGON_GIRL_PIPELINE_PASSED");
        }

        [MenuItem("About Me/Dragon Girl/Import reviewed asset")]
        public static void Import()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            foreach(string guid in AssetDatabase.FindAssets("t:Texture",new[]{"Assets/AboutMe/Art/DragonGirl/Textures"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(!importer)continue;
                importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                bool normal=path.IndexOf("normal",StringComparison.OrdinalIgnoreCase)>=0;
                bool data=normal||path.IndexOf("metallic",StringComparison.OrdinalIgnoreCase)>=0||path.IndexOf("roughness",StringComparison.OrdinalIgnoreCase)>=0;
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=!data;importer.SaveAndReimport();
            }
            var modelImporter=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if(!modelImporter)throw new FileNotFoundException("Dragon girl FBX is missing",ModelPath);
            modelImporter.animationType=ModelImporterAnimationType.Human;
            modelImporter.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            modelImporter.importAnimation=false;modelImporter.optimizeGameObjects=false;
            modelImporter.isReadable=true;modelImporter.meshCompression=ModelImporterMeshCompression.Medium;
            modelImporter.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            modelImporter.SaveAndReimport();

            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if(!source)throw new InvalidOperationException("Dragon girl model did not import");
            var shader=Shader.Find("AboutMe/DragonGirlDoubleSided");
            var baseColor=AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            if(!shader||!baseColor)throw new InvalidOperationException("Dragon girl review material inputs are missing");
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(!material){material=new Material(shader){name="DragonGirl_DoubleSided"};AssetDatabase.CreateAsset(material,MaterialPath);}
            material.shader=shader;material.SetColor("_Color",Color.white);material.SetTexture("_MainTex",baseColor);EditorUtility.SetDirty(material);
            var wrapper=new GameObject("DragonGirl");var pivot=new GameObject("MotionPivot").transform;pivot.SetParent(wrapper.transform,false);
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,pivot);
            visual.name="Visual";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
            var renderers=visual.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)throw new InvalidOperationException("Dragon girl has no renderer");
            foreach(var renderer in renderers)renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
            Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            float scale=1.62f/Mathf.Max(bounds.size.y,.001f);visual.transform.localScale=Vector3.one*scale;
            bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            visual.transform.localPosition+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
            var adapter=wrapper.AddComponent<DragonGirlMotionAdapter>();adapter.Initialize();
            PrefabUtility.SaveAsPrefabAsset(wrapper,PrefabPath);
            UnityEngine.Object.DestroyImmediate(wrapper);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("DRAGON_GIRL_IMPORTED prefab="+PrefabPath);
        }

        [MenuItem("About Me/Dragon Girl/Validate garment isolation")]
        public static void Validate()
        {
            string auditPath=Path.GetFullPath(Output+"/weight-audit.json");
            if(!File.Exists(auditPath))throw new FileNotFoundException("Run scripts/dragon-girl-assets.py first",auditPath);
            var audit=JsonUtility.FromJson<WeightAudit>(File.ReadAllText(auditPath));
            if(audit==null||audit.schemaVersion!=1||audit.lockedVertices<8000)throw new Exception("Dragon girl weight audit is missing or incomplete");
            if(audit.lockedMaxDisplacementUnderLimbStress>1e-6f)throw new Exception("Garment follows limb bones: "+audit.lockedMaxDisplacementUnderLimbStress);
            if(audit.movingMaxDisplacementUnderLimbStress<.05f||audit.movingMeanDisplacementUnderLimbStress<.005f)
                throw new Exception("Hand/foot shell is locked instead of animated");
            var modelAssets=AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            var avatar=modelAssets.OfType<Avatar>().FirstOrDefault();
            if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("Dragon girl Humanoid avatar is invalid");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(!prefab)throw new Exception("Dragon girl prefab is missing");
            var instance=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var adapter=instance.GetComponent<DragonGirlMotionAdapter>();
                if(!adapter||adapter.DrivenBoneCount!=23)throw new Exception("Dragon girl motion mapping is incomplete");
                var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if(!skin||skin.sharedMesh.vertexCount<9000||skin.sharedMesh.blendShapeCount!=0)throw new Exception("Unexpected reviewed mesh");
                foreach(var review in reviewPoses)
                {
                    Sample(adapter,review);
                    var baked=new Mesh();skin.BakeMesh(baked);
                    var hips=instance.GetComponentsInChildren<Transform>(true).First(x=>x.name=="Hips");
                    Debug.Log("DRAGON_GIRL_POSE "+review.name+" bounds="+baked.bounds+" hipsLocal="+hips.localPosition+" hipsWorld="+hips.position+" hipsRotation="+hips.localRotation.eulerAngles);
                    if(baked.vertexCount!=skin.sharedMesh.vertexCount||!Finite(baked.bounds.center)||!Finite(baked.bounds.size)||baked.bounds.size.magnitude>6)
                        throw new Exception("Invalid skinned result in "+review.name);
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
            Debug.Log("DRAGON_GIRL_VALIDATION_PASSED humanoid, 23 driven bones, "+audit.lockedVertices+" locked vertices, moving max "+audit.movingMaxDisplacementUnderLimbStress+", 8 action poses");
        }

        [MenuItem("About Me/Dragon Girl/Render action review")]
        public static void Review()
        {
            Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.32f,.35f);
            var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.72f;light.transform.rotation=Quaternion.Euler(32,-35,0);
            var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.28f;fill.transform.rotation=Quaternion.Euler(18,145,0);
            var camera=new GameObject("Review Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.05f;camera.aspect=.8f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#343740");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);var instance=UnityEngine.Object.Instantiate(prefab);
            var adapter=instance.GetComponent<DragonGirlMotionAdapter>();var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var manifest=new List<string>();
            foreach(var review in reviewPoses)
            {
                Sample(adapter,review);Physics.SyncTransforms();
                Capture(camera,skin,review.name+"-front",new Vector3(0,.82f,-4),new Vector3(0,.82f,0));
                Capture(camera,skin,review.name+"-side",new Vector3(4,.82f,0),new Vector3(0,.82f,0));
                manifest.Add(review.name+" time="+review.time+" action="+review.action+" velocity="+review.velocity+" run="+review.runBlend+" clip="+adapter.LastMotionClip);
            }
            File.WriteAllLines(Output+"/action-review.txt",manifest);
            UnityEngine.Object.DestroyImmediate(instance);AssetDatabase.Refresh();
            Debug.Log("DRAGON_GIRL_REVIEW_RENDERED "+reviewPoses.Length+" poses x front/side");
        }

        static void Sample(DragonGirlMotionAdapter adapter,ReviewPose review)
        {
            adapter.ResetPose();int frames=Mathf.Max(1,Mathf.CeilToInt(review.time*60));
            for(int frame=0;frame<=frames;frame++)
            {
                float time=Mathf.Min(review.time,frame/60f);
                adapter.Apply(new DollCombatMotion.Input{dt=1/60f,time=time,localVelocity=review.velocity,runBlend=review.runBlend,
                    actionKind=review.action,actionElapsed=time,actionId=string.IsNullOrEmpty(review.action)?0:1,actionVariant=review.variant,
                    staggerRemaining=review.action=="fall"?Mathf.Max(0,1.10f-time):0});
            }
        }

        static void Capture(Camera camera,SkinnedMeshRenderer skin,string name,Vector3 position,Vector3 target)
        {
            var baked=new Mesh();skin.BakeMesh(baked);var copy=new GameObject("Baked Review",typeof(MeshFilter),typeof(MeshRenderer));
            copy.transform.SetParent(skin.transform,false);copy.GetComponent<MeshFilter>().sharedMesh=baked;copy.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
            Bounds bounds=copy.GetComponent<Renderer>().bounds;Vector3 direction=(position-target).normalized;
            camera.transform.position=bounds.center+direction*4;camera.transform.LookAt(bounds.center);
            Vector3 extents=bounds.extents;float horizontal=Mathf.Abs(camera.transform.right.x)*extents.x+Mathf.Abs(camera.transform.right.y)*extents.y+Mathf.Abs(camera.transform.right.z)*extents.z;
            float vertical=Mathf.Abs(camera.transform.up.x)*extents.x+Mathf.Abs(camera.transform.up.y)*extents.y+Mathf.Abs(camera.transform.up.z)*extents.z;
            camera.orthographicSize=Mathf.Max(vertical,horizontal/camera.aspect)*1.12f;
            var rt=new RenderTexture(720,900,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(720,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,720,900),0,0);image.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            skin.enabled=true;camera.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(copy);UnityEngine.Object.DestroyImmediate(baked);
        }

        static bool Finite(Vector3 value)
        {return !float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);}
    }
}
