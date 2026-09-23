using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class CharacterTools
    {
        const string Out="../artifacts/characters-v1";
        [Serializable] class MeshData {public Vector3[] vertices;public int[] triangles;}
        [MenuItem("About Me/Characters/Import reviewed Tripo parts")]
        public static void Import()
        {
            const string art="Assets/AboutMe/Art/CharactersV1",resources="Assets/AboutMe/Resources/CampusDoll";
            Directory.CreateDirectory(art);Directory.CreateDirectory(resources);AssetDatabase.Refresh();
            foreach(string name in new[]{"Head","Headphones","SlingBag"})
            {
                var data=JsonUtility.FromJson<MeshData>(File.ReadAllText(Out+"/"+name+".json"));var mesh=new Mesh{name="Tripo normalized "+name};mesh.vertices=data.vertices;mesh.triangles=data.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=art+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.GetComponent<MeshFilter>().sharedMesh=mesh;
                string materialPath=art+"/SourceClay.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!material){material=new Material(Shader.Find("AboutMe/CampusFlat")){color=Color.white};AssetDatabase.CreateAsset(material,materialPath);}go.GetComponent<MeshRenderer>().sharedMaterial=material;
                PrefabUtility.SaveAsPrefabAsset(go,resources+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("CHARACTER_TRIPO_IMPORTED");Review();
        }
        static Camera camera;
        static void Setup()
        {
            Directory.CreateDirectory(Out);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var light=new GameObject("Soft daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=Quaternion.Euler(35,155,0);light.shadows=LightShadows.Soft;
            camera=new GameObject("Character review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.5f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#F5EFE3");camera.farClipPlane=220;
        }
        static void RenderFrame()
        {
            // Batch editor has no animation player loop between captures. Bake the evaluated rig for this render only.
            var temporary=new System.Collections.Generic.List<(SkinnedMeshRenderer skin,GameObject go,Mesh mesh)>();
            foreach(var skin in UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh);var go=new GameObject("Capture evaluated skin",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(skin.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=skin.sharedMaterial;skin.enabled=false;temporary.Add((skin,go,mesh));
            }
            camera.Render();
            foreach(var item in temporary){item.skin.enabled=true;UnityEngine.Object.DestroyImmediate(item.go);UnityEngine.Object.DestroyImmediate(item.mesh);}
        }
        static void Capture(string name,Vector3 pos,Vector3 target,int width=900,int height=1000)
        {
            camera.transform.position=pos;camera.transform.LookAt(target);
            var rt=new RenderTexture(width,height,24){antiAliasing=4};camera.targetTexture=rt;RenderFrame();RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Out+"/"+name+".png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
        [MenuItem("About Me/Characters/Render design turnarounds")]
        public static void Preview()
        {
            AssetDatabase.Refresh();Setup();var parent=new GameObject("Characters").transform;
            string[] names={"curly-hoodie","sport-cap","tied-shirt"};var poses=new[]{new Vector3(0,1.3f,6),new Vector3(6,1.3f,0),new Vector3(0,1.3f,-6)};
            for(int i=0;i<3;i++)
            {
                var actor=new ToyActor(names[i],"#79CFC5",0,parent,AppearanceData.Demo(i));actor.root.transform.position=Vector3.up;actor.label.text="";
                for(int v=0;v<3;v++)Capture(names[i]+"-"+new[]{"front","side","back"}[v],poses[v],new Vector3(0,1.2f,0));
                UnityEngine.Object.DestroyImmediate(actor.root);
            }
            // Contact sheet renders nine independent cameras into one target; no generated image editing.
            var sheet=new RenderTexture(1800,1800,24){antiAliasing=4};camera.targetTexture=sheet;
            for(int i=0;i<3;i++)
            {
                var actor=new ToyActor(names[i],"#79CFC5",0,parent,AppearanceData.Demo(i));actor.root.transform.position=Vector3.up;actor.label.text="";
                for(int v=0;v<3;v++){camera.rect=new Rect(v/3f,(2-i)/3f,1/3f,1/3f);camera.transform.position=poses[v];camera.transform.LookAt(new Vector3(0,1.2f,0));RenderFrame();}
                UnityEngine.Object.DestroyImmediate(actor.root);
            }
            RenderTexture.active=sheet;var image=new Texture2D(1800,1800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1800,1800),0,0);image.Apply();File.WriteAllBytes(Out+"/character-turnarounds.png",image.EncodeToPNG());camera.targetTexture=null;camera.rect=new Rect(0,0,1,1);RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(sheet);
            for(int i=0;i<6;i++){var a=AppearanceData.Demo(i);if(i>=3){a.top="varsity";a.bag="backpack";a.topColor=new[]{"#FFD773","#FFF9EF","#F3AFC2"}[i-3];a.hair="bob";}var actor=new ToyActor("Player "+i,a.topColor,0,parent,a);actor.root.transform.position=new Vector3((i-2.5f)*1.7f,1,0);actor.label.text="";}
            camera.orthographicSize=1.9f;Capture("six-player-lineup",new Vector3(0,1.9f,10),new Vector3(0,1.15f,0),1800,660);
            if(Resources.Load<GameObject>("CampusV04/Campus"))
            {
                PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("CampusV04/Campus"));
                parent.position=new Vector3(0,0,-10);camera.orthographic=false;camera.fieldOfView=50;Capture("campus-character-scale",new Vector3(11,6,-24),new Vector3(0,1.6f,-4),1800,1000);
            }
            PersistObjects(parent.gameObject,"Assets/AboutMe/Art/CharactersV1/ScenePreview");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/AboutMe/Scenes/CharacterV1Preview.unity");
            Debug.Log("CHARACTER_TURNAROUNDS_RENDERED");
        }
        static void PersistObjects(GameObject root,string directory)
        {
            Directory.CreateDirectory(directory);AssetDatabase.Refresh();int index=0;
            Mesh SaveMesh(Mesh mesh)
            {
                if(!mesh||AssetDatabase.Contains(mesh))return mesh;
                string path=directory+"/Mesh-"+(index++)+".asset";var copy=UnityEngine.Object.Instantiate(mesh);copy.hideFlags=HideFlags.None;
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(copy,existing);UnityEngine.Object.DestroyImmediate(copy);return existing;}AssetDatabase.CreateAsset(copy,path);return copy;
            }
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())filter.sharedMesh=SaveMesh(filter.sharedMesh);
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()){var generated=skin.sharedMesh;skin.sharedMesh=SaveMesh(generated);var owner=skin.GetComponent<DollMeshOwner>();if(owner){owner.mesh=null;UnityEngine.Object.DestroyImmediate(owner);}if(generated&&!AssetDatabase.Contains(generated))UnityEngine.Object.DestroyImmediate(generated);}
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.sharedMaterial||AssetDatabase.Contains(renderer.sharedMaterial))continue;
                string safe=renderer.sharedMaterial.name.Replace('#','_').Replace(' ','_');string path="Assets/AboutMe/Art/CharactersV1/"+safe+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(renderer.sharedMaterial);AssetDatabase.CreateAsset(material,path);}renderer.sharedMaterial=material;
            }
        }
        public static void ExportDemos()
        {
            Directory.CreateDirectory("Assets/AboutMe/Resources/CampusDoll");AssetDatabase.Refresh();
            string materialPath="Assets/AboutMe/Resources/CampusDoll/Palette.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(materialPath))AssetDatabase.CreateAsset(new Material(Shader.Find("AboutMe/DollPalette")),materialPath);
            for(int i=0;i<3;i++)
            {
                var actor=new ToyActor("CampusDoll-"+i,"#79CFC5",0,null,AppearanceData.Demo(i));actor.label.text="";
                PersistObjects(actor.root,"Assets/AboutMe/Art/CharactersV1/Demo"+i);
                PrefabUtility.SaveAsPrefabAsset(actor.root,"Assets/AboutMe/Art/CharactersV1/Demo"+i+".prefab");UnityEngine.Object.DestroyImmediate(actor.root);
            }
            AssetDatabase.SaveAssets();
        }
        public static void MotionPreview()
        {
            Setup();Directory.CreateDirectory(Out+"/motion-frames");
            camera.orthographicSize=2.1f;
            var floor=new GameObject("Review floor",typeof(BoxCollider));floor.GetComponent<BoxCollider>().size=new Vector3(30,.1f,30);floor.transform.position=Vector3.down*.08f;
            var actors=new ToyActor[3];var items=new GameObject[3];
            for(int i=0;i<3;i++)
            {
                actors[i]=new ToyActor("Motion "+i,"#79CFC5",0,null,AppearanceData.Demo(i));actors[i].label.text="";
                items[i]=ToyFactory.Item(new[]{"camera","book","basketball"}[i],"#FFD773",null);
            }
            bool previous=Physics.autoSimulation;Physics.autoSimulation=false;
            try
            {
                for(int frame=0;frame<240;frame++)
                {
                    float time=frame/30f;bool eating=time>=3&&time<5.5f;
                    for(int i=0;i<3;i++)
                    {
                        var p=new PublicPlayer{position=new Vector3((i-1)*2.5f,1.02f,Mathf.Min(time,2)*1.5f-1.5f),yaw=0,stability=time>=6&&time<7?0:100,interaction=eating?"eat":"",progress=eating?(time-3)/2.5f:0,grabbing=time>=2&&time<3?"other":"",grabbed=time>=2&&time<2.6f,stagger=time>=6&&time<7?1.5f-(time-6):0,appearance=AppearanceData.Demo(i)};
                        if(frame==165)actors[i].ConfirmSwallow("preview-persona");actors[i].DrawAt(p,camera,1/30f,time);
                        items[i].SetActive(eating);if(eating){items[i].transform.position=actors[i].CarriedPosition(true);items[i].transform.localScale=Vector3.one*actors[i].CarriedScale(true);}
                    }
                    Physics.SyncTransforms();Physics.Simulate(1/30f);
                    Capture("motion-frames/"+frame.ToString("D4"),new Vector3(.6f,1.8f,8),new Vector3(0,1.25f,.5f),1280,720);
                    if(frame==129)Capture("giant-mouth-all-characters",new Vector3(.5f,1.7f,8),new Vector3(0,1.22f,1.4f),1800,1000);
                    if(frame==129)
                    {
                        actors[1].root.SetActive(false);actors[2].root.SetActive(false);items[1].SetActive(false);items[2].SetActive(false);camera.orthographicSize=1.65f;
                        Capture("giant-mouth-side-check",new Vector3(3.5f,2.3f,4),new Vector3(-2.5f,1.35f,1.4f),1400,1000);camera.orthographicSize=2.1f;
                    }
                    if(frame==64)Capture("shoulder-grab-check",new Vector3(3.4f,1.8f,7),new Vector3(0,1.23f,1.4f),1800,1000);
                }
            }
            finally{Physics.autoSimulation=previous;}
            Debug.Log("CHARACTER_MOTION_PREVIEW_RENDERED 240 frames / 30 fps");
        }
        public static void Review()
        {
            ExportDemos();Preview();Validate();MotionPreview();
        }

        public static void BuildPlayer()
        {
            Validate();
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/AboutMe/Scenes/Bootstrap.unity"},locationPathName=Out+"/About Me Characters.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Character build failed");
            Debug.Log("CHARACTER_PLAYER_BUILT");
        }
        public static void Validate()
        {
            ProjectTools.TestRules();
            var profile=new Profile{playerId="avatar-a",nickname="a",appearance=AppearanceData.Demo(2),personas=new[]{new PersonaAsset{assetId="one",name="One",templateId="Melee",visualId="book"},new PersonaAsset{assetId="two",name="Two",templateId="Melee",visualId="book"}}};
            var match=new Match("APPEARANCE",new MatchRules());match.Add(profile);string initial=JsonUtility.ToJson(match.View("avatar-a").players[0].appearance);match.Drop(match.Players["avatar-a"],1);
            if(initial!=JsonUtility.ToJson(match.View("avatar-a").players[0].appearance))throw new Exception("Persona drop changed outfit");
            match.Phase=MatchPhase.Battle;match.Disconnect("avatar-a");match.Add(new Profile{playerId="avatar-a",appearance=AppearanceData.Demo(0)});
            if(initial!=JsonUtility.ToJson(match.View("avatar-a").players[0].appearance))throw new Exception("Reconnect changed battle appearance");
            var serialized=JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(match.View("avatar-a")));
            if(serialized.players[0].appearance.bag!="sling")throw new Exception("Appearance missing from snapshot");
            var eatingMatch=new Match("EAT-COSMETICS",new MatchRules{revealDuration=.01f,eatDuration=.10f,handInterval=10000,finalTime=20000});
            eatingMatch.Add(new Profile{playerId="eater",appearance=AppearanceData.Demo(0),personas=profile.personas});eatingMatch.Add(new Profile{playerId="owner",appearance=AppearanceData.Demo(1),personas=profile.personas});eatingMatch.Phase=MatchPhase.Reveal;eatingMatch.Tick(.02f);
            var eater=eatingMatch.Players["eater"];var owner=eatingMatch.Players["owner"];eater.position=new Vector3(0,1,0);owner.position=new Vector3(4,1,0);eatingMatch.Drop(owner,1);
            var dropped=eatingMatch.Items.First(x=>x.ownerId=="owner"&&x.state==ItemState.Ground);dropped.position=eater.position;dropped.velocity=Vector3.zero;eatingMatch.Interact(eater);
            for(int i=0;i<10;i++)eatingMatch.Tick(.02f);
            if(eatingMatch.Eaten.Count!=1||!eater.profile.appearance.SameAs(AppearanceData.Demo(0))||!owner.profile.appearance.SameAs(AppearanceData.Demo(1)))throw new Exception("Eating changed outfit or failed completion");
            var root=new GameObject("Visual validation");
            var actor=new ToyActor("validation","#79CFC5",0,root.transform,AppearanceData.Demo(0));
            foreach(var slot in AppearanceCatalog.Data.slots)foreach(var option in slot.options)
            {
                var a=AppearanceData.Demo(0);a.Set(slot.id,option.id);actor.ApplyAppearance(a);
                if(actor.root.GetComponentsInChildren<Rigidbody>().Length!=10||actor.DeformationBoneCount!=22)throw new Exception("Outfit changed body rig");
                foreach(var renderer in actor.root.GetComponentsInChildren<Renderer>())if(!(renderer is SkinnedMeshRenderer)&&renderer.bounds.size.magnitude>4)throw new Exception("Unbounded cosmetic "+renderer.name);
            }
            if(Vector3.Distance(actor.torso.InverseTransformPoint(actor.leftArm.position),new Vector3(-.405f,.31f,0))>.001f)throw new Exception("Cosmetic changed bone position");
            // Same walk distance and eat poses must agree at 30/60/120 rendering FPS.
            foreach(int fps in new[]{30,60,120})
            {
                var test=new ToyActor("motion", "#79CFC5",0,root.transform,AppearanceData.Demo(0));
                float maximumOpen=0,minimumClearance=100;
                for(int frame=0;frame<fps*4;frame++)
                {
                    float time=frame/(float)fps;var player=new PublicPlayer{position=new Vector3(time*3,1,0),yaw=0,stability=100,appearance=AppearanceData.Demo(0),interaction=time>1&&time<3?"eat":"",progress=Mathf.Clamp01((time-1)/2)};
                    test.DrawAt(player,null,1f/fps,time,Mathf.Floor(time*20)/20);
                    maximumOpen=Mathf.Max(maximumOpen,test.MouthOpen);if(test.MouthOpen>.3f)minimumClearance=Mathf.Min(minimumClearance,test.FeedingHandClearance());
                    if(test.MouthOpen>.3f&&test.FeedingHandClearance()<0)throw new Exception("Hand intersects mouth or face: "+test.FeedingHandClearance()+" at "+time);
                    if(float.IsNaN(test.head.position.x)||float.IsInfinity(test.head.position.x))throw new Exception("Non-finite animation");
                }
                if(maximumOpen<.95f||test.MouthOpen>.02f)throw new Exception("Exaggerated mouth failed to open or release");
                Debug.Log("CHARACTER_CLEARANCE fps="+fps+" minimum="+minimumClearance);
                if(test.Skin.sharedMesh.blendShapeCount!=4)throw new Exception("Missing mouth, belly or arm stretch morph");
                if(test.Skin.bones.Length!=22||test.Skin.sharedMesh.boneWeights.Any(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f))throw new Exception("Invalid shared skin weights");
                UnityEngine.Object.DestroyImmediate(test.root);
            }
            var buffer=new DollMotionBuffer();buffer.Push(1,1,new Vector3(1,0,0),350);buffer.Push(1.05f,1.05f,new Vector3(1.05f,0,0),10);buffer.Sample(1.1f,out var interpolated,out var angle);
            if(Mathf.Abs(interpolated.x-1.025f)>.002f||Mathf.Abs(Mathf.DeltaAngle(angle,0))>1)throw new Exception("Snapshot interpolation or wrapped yaw failed");
            buffer.Sample(20,out var stalled,out _);if(stalled.x>1.10f)throw new Exception("Packet loss extrapolates indefinitely");
            UnityEngine.Object.DestroyImmediate(root);Debug.Log("CHARACTER_VALIDATION_PASSED catalog, rig, stable persona appearance, reconnect, snapshot");
        }
        public static void References()
        {
            Setup();var root=new GameObject("Reference").transform;
            DollFactory.Part("Head",root,Vector3.zero,new Vector3(.97f,.98f,.9f),"#F3CDA9");
            DollFactory.Part("Chin",root,new Vector3(0,-.29f,.015f),new Vector3(.65f,.35f,.66f),"#F3CDA9");
            camera.orthographicSize=.75f;Capture("head-reference",new Vector3(1,.35f,3),Vector3.zero,1024,1024);UnityEngine.Object.DestroyImmediate(root.gameObject);
            root=new GameObject("Reference").transform;
            for(int side=-1;side<=1;side+=2)
            {
                DollFactory.Part("Cup",root,new Vector3(side*.52f,0,0),new Vector3(.20f,.40f,.34f),"#FFD773");
                var band=DollFactory.Part("Band",root,new Vector3(side*.39f,.36f,-.03f),new Vector3(.13f,.43f,.16f),"#FFD773",false);band.transform.localRotation=Quaternion.Euler(0,0,side*24);
            }
            DollFactory.Part("Band top",root,new Vector3(0,.54f,-.03f),new Vector3(.77f,.12f,.16f),"#FFD773");camera.orthographicSize=.82f;Capture("headphones-reference",new Vector3(1,.8f,3),new Vector3(0,.2f,0),1024,1024);UnityEngine.Object.DestroyImmediate(root.gameObject);
            root=new GameObject("Reference").transform;DollFactory.Part("Pouch",root,Vector3.zero,new Vector3(.44f,.37f,.21f),"#FFD773");DollFactory.Part("Flap",root,new Vector3(0,.05f,.12f),new Vector3(.37f,.2f,.055f),"#FFF9EF",false);DollFactory.Part("Clasp",root,new Vector3(0,-.02f,.155f),new Vector3(.07f,.065f,.025f),"#202A3D",false);
            camera.orthographicSize=.34f;Capture("sling-reference",new Vector3(.6f,.3f,2),Vector3.zero,1024,1024);
            Debug.Log("CHARACTER_REFERENCES_READY");
        }
    }
}
