using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class ClaudeGirlModularTools
    {
        public const string Out="../artifacts/claude-girl-modular";
        [Serializable] public class BoneData {public string name,parent;public Vector3 position;public Quaternion rotation;public Vector3 scale;}
        [Serializable] public class BaseData {public Vector3[] vertices,normals;public int[] triangles;public Color[] colors;public BoneWeight[] weights;public BoneData[] bones;}
        public static void ExportBase()
        {
            Directory.CreateDirectory(Out);
            var actor=new ToyActor("Original toy neutral fitting copy","#79CFC5",0,null,new AppearanceData());
            var mesh=actor.Skin.sharedMesh;
            var data=new BaseData{vertices=mesh.vertices,normals=mesh.normals,triangles=mesh.triangles,colors=mesh.colors,weights=mesh.boneWeights,
                bones=actor.Skin.bones.Select(t=>new BoneData{name=t.name,parent=t.parent.name,position=t.position,rotation=t.rotation,scale=t.lossyScale}).ToArray()};
            File.WriteAllText(Out+"/base-neutral.json",JsonUtility.ToJson(data));
            Debug.Log("CLAUDE_MODULAR_BASE_EXPORTED bones="+data.bones.Length+" triangles="+data.triangles.Length/3);
            UnityEngine.Object.DestroyImmediate(actor.root);
        }
        const string Art="Assets/AboutMe/Art/ClaudeGirlModular";
        [Serializable] public class WeightData
        {
            public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;
            public float weight0,weight1,weight2,weight3;
            public BoneWeight Value=>new BoneWeight{boneIndex0=boneIndex0,boneIndex1=boneIndex1,boneIndex2=boneIndex2,boneIndex3=boneIndex3,weight0=weight0,weight1=weight1,weight2=weight2,weight3=weight3};
        }
        [Serializable] public class AssetData {public string[] boneNames;public Vector3[] bonePositions,vertices;public int[] triangles;public Color[] colors;public WeightData[] weights;public ClaudeGirlOutfit.Helper[] helpers;}
        [MenuItem("About Me/Claude Girl/Import modular outfit")]
        public static void Import()
        {
            AssetDatabase.Refresh();Directory.CreateDirectory(Art);
            var d=JsonUtility.FromJson<AssetData>(File.ReadAllText(Out+"/accessories.json"));
            if(d.triangles.Length/3>12000)throw new Exception("Accessory triangle budget exceeded");
            var mesh=new Mesh{name="DS independently bound accessories"};mesh.vertices=d.vertices;mesh.triangles=d.triangles;mesh.colors=d.colors;mesh.boneWeights=d.weights.Select(w=>w.Value).ToArray();
            mesh.bindposes=d.bonePositions.Select(p=>Matrix4x4.Translate(-p)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            var open=new Vector3[d.vertices.Length];var belly=new Vector3[d.vertices.Length];
            for(int i=0;i<d.vertices.Length;i++)
            {
                var p=d.vertices[i];var w=d.weights[i];float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                if(Mathf.Abs(sum-1)>.0001f)throw new Exception("Unnormalised accessory vertex "+i);
                if(p.y>.48f)open[i]=new Vector3(Mathf.Sign(p.x)*.035f,.15f,0)*Mathf.Clamp01((p.y-.48f)/.4f);
                if(p.z>.25f&&p.y>-.35f&&p.y<.3f){float influence=Mathf.Clamp01(1-Mathf.Abs(p.y+.15f)/.4f);belly[i]=new Vector3(p.x*.1f*influence,0,.11f*influence);}
            }
            mesh.AddBlendShapeFrame("Feeding clearance",100,open,new Vector3[open.Length],new Vector3[open.Length]);
            mesh.AddBlendShapeFrame("Swallow belly",100,belly,new Vector3[belly.Length],new Vector3[belly.Length]);
            SaveAsset(mesh,Art+"/Accessories.asset");mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/Accessories.asset");
            float meshDelta=mesh.vertices.Zip(d.vertices,(a,b)=>Vector3.Distance(a,b)).Max();Debug.Log("Claude_MESH_ROUNDTRIP maxDelta="+meshDelta);if(meshDelta>0.00001f)throw new Exception("Imported mesh does not match authored geometry");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Palette.mat");
            if(!material){material=new Material(Shader.Find("AboutMe/ClaudeGirlPalette"));AssetDatabase.CreateAsset(material,Art+"/Palette.mat");}
            var root=new GameObject("ClaudeGirl modular clothing");var skin=root.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterial=material;skin.quality=SkinQuality.Bone4;
            var outfit=root.AddComponent<ClaudeGirlOutfit>();outfit.skin=skin;outfit.boneNames=d.boneNames;outfit.bonePositions=d.bonePositions;outfit.helpers=d.helpers;
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/AboutMe/Resources/CampusDoll/ClaudeGirlModular.prefab");UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("CLAUDE_MODULAR_IMPORTED triangles="+d.triangles.Length/3);Review();
        }
        static void SaveAsset(UnityEngine.Object obj,string path)
        {
            var old=AssetDatabase.LoadMainAssetAtPath(path);
            if(old is Mesh target&&obj is Mesh source)
            {
                // Mesh buffers are native data; CopySerialized alone leaves stale geometry in this engine.
                target.Clear();target.ClearBlendShapes();target.indexFormat=source.indexFormat;
                target.vertices=source.vertices;target.normals=source.normals;target.colors=source.colors;
                target.triangles=source.triangles;target.boneWeights=source.boneWeights;target.bindposes=source.bindposes;target.bounds=source.bounds;
                for(int s=0;s<source.blendShapeCount;s++)for(int f=0;f<source.GetBlendShapeFrameCount(s);f++)
                {
                    var dv=new Vector3[source.vertexCount];var dn=new Vector3[source.vertexCount];var dt=new Vector3[source.vertexCount];
                    source.GetBlendShapeFrameVertices(s,f,dv,dn,dt);target.AddBlendShapeFrame(source.GetBlendShapeName(s),source.GetBlendShapeFrameWeight(s,f),dv,dn,dt);
                }
                EditorUtility.SetDirty(target);UnityEngine.Object.DestroyImmediate(source);
            }
            else if(old){EditorUtility.CopySerialized(obj,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(obj);}
            else AssetDatabase.CreateAsset(obj,path);
        }
        public static void ImportAndBuildMac(){Import();BuildMac();}
        public static void ImportFinal()
        {
            Import();ClaudeGirlFaceReview.ReviewFaceOnly();DSGirlModularTools.Review();
            File.Copy(DSGirlModularTools.Out+"/pose-parity.json",Out+"/ds-regression-parity.json",true);
            Debug.Log("CLAUDE_FINAL_ASSET_REVIEW_PASSED");
        }
        static Camera Setup()
        {
            if(!Application.isPlaying)UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            var camera=new GameObject("DS review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.57f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#E9ECF1");camera.farClipPlane=80;
            var light=new GameObject("Softbox").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.transform.rotation=Quaternion.Euler(25,-35,0);RenderSettings.ambientLight=Color.gray;
            var ground=ToyFactory.Shape("Studio floor",PrimitiveType.Cube,null,new Vector3(0,-1.055f,0),new Vector3(100,.1f,100),"#D3D9E3");
            return camera;
        }
        public static void Capture(Camera camera,string path,Vector3 from,Vector3 target,int width=840,int height=1000)
        {
            camera.transform.position=from;camera.transform.LookAt(target);camera.aspect=(float)width/height;
            var rt=new RenderTexture(width,height,24){antiAliasing=4};camera.targetTexture=rt;
            var baked=new System.Collections.Generic.List<(SkinnedMeshRenderer skin,GameObject go,Mesh mesh)>();
            foreach(var skin in UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                var mesh=new Mesh();skin.BakeMesh(mesh);var go=new GameObject("Baked capture",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(skin.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;baked.Add((skin,go,mesh));
            }
            camera.Render();RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,tex.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
            foreach(var b in baked){b.skin.enabled=true;UnityEngine.Object.DestroyImmediate(b.go);UnityEngine.Object.DestroyImmediate(b.mesh);}
        }
        static BaseData BodyData(ToyActor actor)
        {
            var mesh=actor.Skin.sharedMesh;var bones=actor.Skin.bones;
            var vertices=mesh.vertices.ToList();var normals=mesh.normals.ToList();var triangles=mesh.triangles.ToList();var colors=mesh.colors.ToList();var weights=mesh.boneWeights.ToList();
            foreach(var f in actor.root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=f.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled||!f.sharedMesh)continue;
                Transform parent=f.transform;int bone=-1;while(parent&&bone<0){bone=Array.IndexOf(bones,parent);parent=parent.parent;}if(bone<0)continue;
                var source=f.sharedMesh;int offset=vertices.Count;var matrix=actor.root.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                var tint=renderer.sharedMaterial.HasProperty("_Color")?renderer.sharedMaterial.color:Color.white;
                var points=source.vertices;var ns=source.normals;
                for(int i=0;i<points.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(points[i]));normals.Add(normalMatrix.MultiplyVector(ns[i]).normalized);colors.Add(tint);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});}
                triangles.AddRange(source.triangles.Select(i=>i+offset));
            }
            return new BaseData{vertices=vertices.ToArray(),normals=normals.ToArray(),triangles=triangles.ToArray(),colors=colors.ToArray(),weights=weights.ToArray(),bones=bones.Select(t=>new BoneData{name=t.name,parent=t.parent.name,position=t.position,rotation=t.rotation,scale=t.lossyScale}).ToArray()};
        }
        static AppearanceData DS()=>new AppearanceData{character="claude"};
        static PublicPlayer View(string action,float t,int id)
        {
            bool motion=action=="walk"||action=="run";return new PublicPlayer{id="review",nickname="",appearance=DS(),characterSkin="claude-girl",position=motion?new Vector3(0,0,t*(action=="run"?7:3)):Vector3.zero,
                connected=true,stability=100,simulationTime=id*3+t,actionId=id+1,actionStartedAt=id*3,actionKind=motion||action=="jump"||action=="eat"?"idle":action,
                running=action=="run",airborne=action=="jump",velocity=motion?Vector3.forward*(action=="run"?7:3):Vector3.zero,locomotionSpeed=7,
                grabbing=action=="grab"?"target":"",grabAnchor=new Vector3(0,.28f,.6f),interaction=action=="eat"?"eat":"",progress=action=="eat"?.9f:0,stagger=action=="fall"?1:0};
        }
        [MenuItem("About Me/Claude Girl/Review modular outfit")]
        public static void Review()
        {
            Directory.CreateDirectory(Out+"/review");var camera=Setup();var actor=new ToyActor("Claude modular", "#79CFC5",0,null,DS());actor.label.text="";
            foreach(var view in new[]{("front",new Vector3(0,.45f,6)),("side",new Vector3(6,.45f,0)),("back",new Vector3(0,.45f,-6)),("three-quarter",new Vector3(3,.6f,6))})
                Capture(camera,Out+"/review/"+view.Item1+".png",view.Item2,new Vector3(0,.35f,0));
            // Persist the assembled body skin in neutral pose as an editable Blender fitting companion.
            File.WriteAllText(Out+"/claude-base-neutral.json",JsonUtility.ToJson(BodyData(actor)));
            var toy=new ToyActor("Original", "#79CFC5",0,null,new AppearanceData());toy.label.text="";toy.root.transform.position=Vector3.left*.85f;actor.root.transform.position=Vector3.right*.85f;
            Capture(camera,Out+"/review/comparison.png",new Vector3(0,.5f,7),new Vector3(0,.35f,0),1400,1000);UnityEngine.Object.DestroyImmediate(toy.root);UnityEngine.Object.DestroyImmediate(actor.root);

            var extreme=new ToyActor("Extreme","#79CFC5",0,null,DS());extreme.label.text="";
            for(int frame=0;frame<90;frame++)
            {
                var p=View("grab",frame/60f,0);p.grabAnchor=new Vector3(0,1.1f,2.0f);extreme.DrawAt(p,camera,1/60f,p.simulationTime);
            }
            Capture(camera,Out+"/review/extreme-reach.png",new Vector3(2,.6f,5),new Vector3(0,.35f,0));
            extreme.root.transform.rotation=Quaternion.Euler(0,0,180);extreme.root.transform.position=Vector3.up*.6f;
            Capture(camera,Out+"/review/inverted.png",new Vector3(2,.6f,5),new Vector3(0,.35f,0));
            var baked=new Mesh();extreme.root.GetComponentInChildren<ClaudeGirlOutfit>().skin.BakeMesh(baked);
            if(baked.vertices.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.y)||v.magnitude>5))throw new Exception("Invalid cosmetic deformation");
            UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(extreme.root);
float maxPosition=0,maxAngle=0,maxGrip=0;int frames=0;
            string[] actions={"idle","walk","run","jump","light","heavy","grab","eat","hurt","fall","recover"};
            for(int a=0;a<actions.Length;a++)
            {
                var ds=new ToyActor("DS", "#79CFC5",0,null,DS());var original=new ToyActor("Original", "#79CFC5",0,null,new AppearanceData());
                for(int f=0;f<60;f++)
                {
                    float t=f/60f;var p=View(actions[a],t,a);ds.DrawAt(p,camera,1/60f,p.simulationTime);p.appearance=new AppearanceData();p.characterSkin="normal";original.DrawAt(p,camera,1/60f,p.simulationTime);
                    for(int b=0;b<ds.Skin.bones.Length;b++){maxPosition=Mathf.Max(maxPosition,Vector3.Distance(ds.Skin.bones[b].position,original.Skin.bones[b].position));maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(ds.Skin.bones[b].rotation,original.Skin.bones[b].rotation));}
                    maxGrip=Mathf.Max(maxGrip,Vector3.Distance(ds.RightHandAttachment.position,original.RightHandAttachment.position));frames++;
                    int peak=actions[a]=="light"?8:actions[a]=="heavy"?14:actions[a]=="hurt"?8:actions[a]=="fall"?22:45;
                    if(f==peak)
                    {
                        original.root.SetActive(false);ds.label.text="";
                        Capture(camera,Out+"/review/action-"+actions[a]+".png",ds.root.transform.position+new Vector3(2,.6f,5),ds.root.transform.position+new Vector3(0,.35f,0));
                        if(actions[a]=="eat"||actions[a]=="grab")Capture(camera,Out+"/review/action-"+actions[a]+"-side.png",ds.root.transform.position+new Vector3(5,.4f,0),ds.root.transform.position+new Vector3(0,.35f,0));
                    }
                }
                ds.root.GetComponentInChildren<ClaudeGirlOutfit>()?.Release();UnityEngine.Object.DestroyImmediate(ds.root);UnityEngine.Object.DestroyImmediate(original.root);
            }
            var switching=new ToyActor("Switching", "#79CFC5",0,null,new AppearanceData());
            for(int i=0;i<20;i++)
            {
                switching.ApplyAppearance(DS());if(!switching.Skin.enabled||switching.root.GetComponentsInChildren<ClaudeGirlOutfit>().Length!=1||switching.DeformationBoneCount!=22||switching.PhysicalBones.Length!=15)throw new Exception("DS switch altered base rig");
                switching.ApplyAppearance(new AppearanceData());if(switching.root.GetComponentInChildren<ClaudeGirlOutfit>()||switching.root.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("Claude_")))throw new Exception("Accessory leaked on switch");
            }

            // Switching must retain the exact original transforms and all collider identities.
            var switchingBones=switching.Skin.bones;var originalGrip=switching.RightHandAttachment;
            int colliders=switching.root.GetComponentsInChildren<Collider>(true).Length;
            for(int cycle=0;cycle<20;cycle++)foreach(var character in new[]{"claude","ds","toy"})
            {
                switching.ApplyAppearance(new AppearanceData{character=character});
                if(!switching.Skin.bones.SequenceEqual(switchingBones)||switching.RightHandAttachment!=originalGrip)throw new Exception("Switch replaced an original body/grip transform");
                if(switching.root.GetComponentsInChildren<Collider>(true).Length!=colliders)throw new Exception("Cosmetics changed collision shapes");
                int cl=switching.root.GetComponentsInChildren<ClaudeGirlOutfit>().Length,ds=switching.root.GetComponentsInChildren<DSGirlOutfit>().Length;
                if(cl!=(character=="claude"?1:0)||ds!=(character=="ds"?1:0))throw new Exception("Cross-character outfit residue");
                int helpers=switching.root.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Claude_")||t.name.StartsWith("DS_"));
                if(helpers!=(character=="claude"?7:character=="ds"?8:0))throw new Exception("Cross-character helper residue");
            }
            File.WriteAllText(Out+"/switch-validation.json","{\"cycles\":20,\"characters\":[\"toy\",\"ds\",\"claude\"],\"gripIdentityPreserved\":true,\"bodyIdentityPreserved\":true,\"collidersUnchanged\":true}");
UnityEngine.Object.DestroyImmediate(switching.root);
            File.WriteAllText(Out+"/pose-parity.json","{\"frames\":"+frames+",\"maxBonePositionDelta\":"+maxPosition.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"maxBoneAngleDelta\":"+maxAngle.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"maxGripDelta\":"+maxGrip.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"switchCycles\":20}");
            if(maxPosition>.0001f||maxAngle>.05f||maxGrip>.0001f)throw new Exception("Original-body motion changed");
            Debug.Log("CLAUDE_MODULAR_REVIEW_PASSED frames="+frames+" maxPosition="+maxPosition+" maxAngle="+maxAngle);
        }
        [InitializeOnLoadMethod] static void ResumePhysics()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("ClaudeModularPhysics",false))return;
                SessionState.EraseBool("ClaudeModularPhysics");EditorApplication.delayCall+=()=>{try{PhysicsReview();BodyPhysicsTests.Validate();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}};
            };
        }
        public static void BeginPhysicsReview()
        {
            SessionState.SetBool("ClaudeModularPhysics",true);
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            EditorApplication.isPlaying=true;
        }
        public static void BuildMac()
        {
            ProjectTools.TestRules();
            Build(UnityEditor.BuildTarget.StandaloneOSX,"../builds/claude-girl-modular-mac/About Me.app");
        }
        public static void BuildWeb()
        {
            PlayerSettings.WebGL.template="PROJECT:AboutMe";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
            Build(UnityEditor.BuildTarget.WebGL,"../builds/claude-girl-modular-web");
        }
        static void Build(UnityEditor.BuildTarget target,string path)
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=path,target=target,options=BuildOptions.Development});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("DS build failed: "+target);
            Debug.Log("CLAUDE_MODULAR_BUILD_OK "+target);
        }
        public static void PhysicsReview()
        {
            var camera=Setup();camera.orthographicSize=1.8f;Directory.CreateDirectory(Out+"/physics-frames");
            using(var match=new Match("DS-MODULAR-REVIEW",new MatchRules{revealDuration=.01f,handInterval=999,finalTime=999}))
            {
                for(int i=0;i<2;i++){var p=GameNetwork.SampleProfile("ds-test-"+i,"Review");p.appearance=DS();match.Add(p);match.Players[p.playerId].position=new Vector3(i*3,1,-4);}
                match.EnableBodyPhysics();match.Phase=MatchPhase.Reveal;
                var ds=new ToyActor("DS","#79CFC5",0,null,DS()){ImmediatePhysicsPose=true};var toy=new ToyActor("Original","#79CFC5",0,null,new AppearanceData()){ImmediatePhysicsPose=true};
                var players=match.Players.Values.ToArray();var states=new System.Collections.Generic.HashSet<string>();float maxPosition=0,maxAngle=0;
                for(int f=0;f<420;f++)
                {
                    float t=f*.02f;var player=players[0];match.Receive(player.profile.playerId,new Command{sequence=player.sequence+1,z=f<95?.55f:0,run=f>40&&f<95,yaw=0});
                    if(f==105||f==220)match.Hit(player,players[1],1,8,true,true);
                    match.Tick(.02f);var p=match.View(player.profile.playerId).players.First(x=>x.id==player.profile.playerId);states.Add(p.bodyMode.ToString());
                    p.appearance=DS();ds.DrawAt(p,camera,.02f,t);p.appearance=new AppearanceData();toy.DrawAt(p,camera,.02f,t);toy.root.SetActive(false);ds.label.text="";
                    for(int b=0;b<ds.Skin.bones.Length;b++){maxPosition=Mathf.Max(maxPosition,Vector3.Distance(ds.Skin.bones[b].position,toy.Skin.bones[b].position));maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(ds.Skin.bones[b].rotation,toy.Skin.bones[b].rotation));}
                    if(f%4==0)Capture(camera,Out+"/physics-frames/"+(f/4).ToString("D4")+".png",p.position+new Vector3(3,1.3f,6),p.position+new Vector3(0,.1f,0),960,720);
                }
                File.WriteAllText(Out+"/physics-parity.txt","420 actual Match ticks; states="+string.Join(",",states)+"; max bone position delta="+maxPosition+"; max rotation delta="+maxAngle);
                if(maxPosition>.0001f||maxAngle>.05f||!states.Contains("Grounded")||!states.Contains("GettingUp"))throw new Exception("Physical pose parity or state coverage failed");
                UnityEngine.Object.DestroyImmediate(ds.root);UnityEngine.Object.DestroyImmediate(toy.root);
            }
            Debug.Log("CLAUDE_MODULAR_PHYSICS_REVIEW_PASSED");
        }
    }
}
