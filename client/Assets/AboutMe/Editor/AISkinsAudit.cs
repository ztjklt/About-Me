using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace AboutMe.Editor
{
    public static class AISkinsAudit
    {
        [Serializable] public class Result {public string scope="Unity Editor actual render, deterministic motion parity, shared authoritative Match physics; no human/network/device claim";public int skins,poseFrames,switches,physicsTicks;public float maxBonePosition,maxBoneAngle,maxGrip;public string[] physicsStates;public bool passed;}
        static string Out=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/ai-girls-r5"));
        static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
        static PublicPlayer View(string action,float t,int id,string skin)
        {
            bool motion=action=="walk"||action=="run";
            return new PublicPlayer{id="review",nickname="",appearance=new AppearanceData(),characterSkin=skin,position=motion?new Vector3(0,0,t*(action=="run"?7:3)):Vector3.zero,
                connected=true,stability=100,simulationTime=id*3+t,actionId=id+1,actionStartedAt=id*3,actionKind=motion||action=="jump"||action=="eat"?"idle":action,
                running=action=="run",airborne=action=="jump",velocity=motion?Vector3.forward*(action=="run"?7:3):Vector3.zero,locomotionSpeed=7,
                grabbing=action=="grab"?"target":"",grabAnchor=new Vector3(0,.28f,.6f),interaction=action=="eat"?"eat":"",progress=action=="eat"?.9f:0,stagger=action=="fall"?1:0};
        }
        static void Parity(ToyActor a,ToyActor original,Result result)
        {
            Assert(a.DeformationBoneCount==22&&a.PhysicalBones.Length==15,"Body rig changed");
            for(int b=0;b<a.Skin.bones.Length;b++)
            {result.maxBonePosition=Mathf.Max(result.maxBonePosition,Vector3.Distance(a.Skin.bones[b].position,original.Skin.bones[b].position));result.maxBoneAngle=Mathf.Max(result.maxBoneAngle,Quaternion.Angle(a.Skin.bones[b].rotation,original.Skin.bones[b].rotation));}
            result.maxGrip=Mathf.Max(result.maxGrip,Vector3.Distance(a.RightHandAttachment.position,original.RightHandAttachment.position));
        }
        static void AppearancePresent(ToyActor actor,string skin)
        {
            var expected=AISkinRegistry.CharacterForSkin(skin);Assert(actor.Appearance.character==expected,"Skin silently fell back: "+skin);
            Assert(actor.Skin&&actor.Skin.enabled,"Original body hidden: "+skin);
            Assert(actor.root.GetComponentsInChildren<SkinnedMeshRenderer>().Length==(skin=="normal"?1:2),"Outfit count: "+skin);
            if(skin!="normal")
            {
                var outfit=actor.root.GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x!=actor.Skin);
                Assert(outfit.sharedMesh&&outfit.sharedMesh.vertexCount>100&&outfit.bones.All(x=>x),"Real outfit or mapped bone missing: "+skin);
                var mesh=new Mesh();outfit.BakeMesh(mesh);Assert(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Nonfinite mesh "+skin);UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        [InitializeOnLoadMethod] static void Resume()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&(SessionState.GetBool("AISkinsR5Audit",false)||SessionState.GetBool("AISkinsR5Images",false))){SessionState.EraseBool("AISkinsR5Audit");EditorApplication.delayCall+=()=>{try{if(SessionState.GetBool("AISkinsR5Images",false)){SessionState.EraseBool("AISkinsR5Images");CaptureModels();}else Review();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}};}};
        }
        public static void Begin(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("AISkinsR5Audit",true);EditorApplication.isPlaying=true;}
        public static void BeginModels(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("AISkinsR5Images",true);EditorApplication.isPlaying=true;}
        public static void CaptureModels()
        {
            Directory.CreateDirectory(Out+"/models");
            var camera=new GameObject("Clean model camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.7f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.9f,.94f,.95f);
            var light=new GameObject("Clean model light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(30,150,0);
            foreach(var skin in new[]{"normal"}.Concat(AISkinRegistry.All.Select(x=>x.skin)))
            {
                var actor=new ToyActor("Clean model","#79CFC5",0,null,new AppearanceData{character=AISkinRegistry.CharacterForSkin(skin)});actor.label.text="";
                // Box() creates its shared primitive mesh through a disposable independent Cube.
                // This synchronous capture runs before deferred Destroy; exclude that helper only.
                var primitive=GameObject.Find("Cube");if(primitive&&primitive.transform.parent==null)primitive.SetActive(false);
                AppearancePresent(actor,skin);
                foreach(var view in new[]{("front",new Vector3(0,.45f,6)),("side",new Vector3(6,.45f,0)),("back",new Vector3(0,.45f,-6))})DSGirlModularTools.Capture(camera,Out+"/models/"+skin+"-"+view.Item1+".png",view.Item2,new Vector3(0,.35f,0));
                foreach(var action in new[]{"light","grab","eat"})
                {
                    var p=View(action,.75f,3,skin);for(int f=0;f<45;f++){p.simulationTime=9+f/60f;actor.DrawAt(p,camera,1/60f,p.simulationTime);}actor.label.text="";
                    DSGirlModularTools.Capture(camera,Out+"/models/"+skin+"-"+action+".png",actor.root.transform.position+new Vector3(2,.6f,5),actor.root.transform.position+new Vector3(0,.35f,0));
                }
                UnityEngine.Object.DestroyImmediate(actor.root);
            }
            Debug.Log("AI_SKINS_R5_CLEAN_MODELS_PASSED count=66");
        }
        public static void Review()
        {
            Directory.CreateDirectory(Out);var result=new Result();SceneManager.SetActiveScene(SceneManager.CreateScene("AI skin integrated audit"));
            var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.7f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.9f,.94f,.95f);
            var light=new GameObject("Audit light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(30,150,0);
            var skins=new[]{"normal"}.Concat(AISkinRegistry.All.Select(x=>x.skin)).ToArray();result.skins=skins.Length;
            string[] actions={"idle","walk","run","jump","light","heavy","grab","eat","hurt","fall","recover"};
            foreach(var skin in skins)
            {
                // Authoritative whitelisting is exercised on genuine Match.Add/View, including forged local appearance.
                using(var m=new Match("AI-TRUST",new MatchRules()))
                {
                    var profile=GameNetwork.SampleProfile("trusted","Audit");profile.unlockedCharacterSkins=AISkinRegistry.All.Select(x=>x.skin).ToArray();profile.characterSkin=skin;m.Add(profile);
                    Assert(m.View("trusted").players.Single().characterSkin==skin,"Authority failed selected skin "+skin);
                }
                using(var m=new Match("AI-REJECT",new MatchRules()))
                {
                    var profile=GameNetwork.SampleProfile("locked","Locked");profile.characterSkin=skin;profile.appearance=new AppearanceData{character=AISkinRegistry.CharacterForSkin(skin)};m.Add(profile);
                    Assert(m.View("locked").players.Single().characterSkin=="normal","Forged skin reached public player");
                }
                var actor=new ToyActor("AI", "#79CFC5",0,null,new AppearanceData{character=AISkinRegistry.CharacterForSkin(skin)});actor.label.text="";AppearancePresent(actor,skin);
                foreach(var view in new[]{("front",new Vector3(0,.45f,6)),("side",new Vector3(6,.45f,0)),("back",new Vector3(0,.45f,-6))})DSGirlModularTools.Capture(camera,Out+"/models/"+skin+"-"+view.Item1+".png",view.Item2,new Vector3(0,.35f,0));
                UnityEngine.Object.DestroyImmediate(actor.root);
                for(int a=0;a<actions.Length;a++)
                {
                    var styled=new ToyActor("AI","#79CFC5",0,null,new AppearanceData{character=AISkinRegistry.CharacterForSkin(skin)});
                    var original=new ToyActor("Original","#79CFC5",0,null,new AppearanceData());
                    for(int f=0;f<60;f++)
                    {
                        var p=View(actions[a],f/60f,a,skin);styled.DrawAt(p,camera,1/60f,p.simulationTime);p.characterSkin="normal";original.DrawAt(p,camera,1/60f,p.simulationTime);Parity(styled,original,result);result.poseFrames++;
                        if(f==45){AppearancePresent(styled,skin);if(actions[a]=="eat"||actions[a]=="grab"||actions[a]=="light"){original.root.SetActive(false);styled.label.text="";DSGirlModularTools.Capture(camera,Out+"/models/"+skin+"-"+actions[a]+".png",styled.root.transform.position+new Vector3(2,.6f,5),styled.root.transform.position+new Vector3(0,.35f,0));original.root.SetActive(true);}}
                    }
                    UnityEngine.Object.DestroyImmediate(styled.root);UnityEngine.Object.DestroyImmediate(original.root);
                }
            }
            var switching=new ToyActor("Switching","#79CFC5",0,null,new AppearanceData());var bones=switching.Skin.bones.ToArray();var hand=switching.RightHandAttachment;var left=switching.LeftHandAttachment;int colliderCount=switching.root.GetComponentsInChildren<Collider>(true).Length;int neutralTransforms=switching.root.GetComponentsInChildren<Transform>().Length;
            for(int cycle=0;cycle<20;cycle++)foreach(var skin in skins)
            {
                switching.ApplyAppearance(new AppearanceData{character=AISkinRegistry.CharacterForSkin(skin)});AppearancePresent(switching,skin);
                Assert(bones.SequenceEqual(switching.Skin.bones)&&hand==switching.RightHandAttachment&&left==switching.LeftHandAttachment,"Original bones/hand anchors replaced");
                Assert(colliderCount==switching.root.GetComponentsInChildren<Collider>(true).Length,"Outfit added colliders");result.switches++;
                switching.ApplyAppearance(new AppearanceData());Assert(switching.root.GetComponentsInChildren<SkinnedMeshRenderer>().Length==1&&switching.root.GetComponentsInChildren<Transform>().Length==neutralTransforms,"Outfit helper leaked after normal switch");
            }
            UnityEngine.Object.DestroyImmediate(switching.root);
            using(var match=new Match("AI-PHYSICS",new MatchRules{revealDuration=.01f,handInterval=999,finalTime=999}))
            {
                for(int i=0;i<2;i++){var profile=GameNetwork.SampleProfile("phys-"+i,"Audit");match.Add(profile);match.Players[profile.playerId].position=new Vector3(i*3,1,-4);}
                match.EnableBodyPhysics();match.Phase=MatchPhase.Reveal;
                var actors=skins.Select(skin=>new ToyActor("AI physics","#79CFC5",0,null,new AppearanceData{character=AISkinRegistry.CharacterForSkin(skin)}){ImmediatePhysicsPose=true}).ToArray();var states=new HashSet<string>();var players=match.Players.Values.ToArray();
                for(int f=0;f<420;f++)
                {
                    var player=players[0];match.Receive(player.profile.playerId,new Command{sequence=player.sequence+1,z=f<95?.55f:0,run=f>40&&f<95,yaw=0});if(f==105||f==220)match.Hit(player,players[1],1,8,true,true);match.Tick(.02f);
                    var p=match.View(player.profile.playerId).players.First(x=>x.id==player.profile.playerId);states.Add(p.bodyMode.ToString());
                    for(int i=0;i<actors.Length;i++){p.characterSkin=skins[i];actors[i].DrawAt(p,camera,.02f,f*.02f);if(i>0)Parity(actors[i],actors[0],result);}
                    result.physicsTicks++;
                }
                result.physicsStates=states.ToArray();Assert(states.Contains("Grounded")&&states.Contains("GettingUp"),"Physics fall/get-up not covered");foreach(var actor in actors)UnityEngine.Object.DestroyImmediate(actor.root);
            }
            Assert(result.maxBonePosition<.0001f&&result.maxBoneAngle<.05f&&result.maxGrip<.0001f,"AI outfit altered body motion");result.passed=true;File.WriteAllText(Out+"/engine-audit.json",JsonUtility.ToJson(result,true));Debug.Log("AI_SKINS_R5_PASSED "+JsonUtility.ToJson(result));
        }
    }
}
