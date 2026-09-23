using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class LocomotionTools
    {
        const string Out="../artifacts/characters-v2";
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        public static void Validate()
        {
            CharacterTools.Validate();
            var input=new LocomotionInput();
            Check(!input.Step(0,.016f,true,true,Vector2.up,false,true),"First W should walk");
            Check(!input.Step(1,1,false,true,Vector2.up,false,true),"Holding one W should not run");
            input.Step(1.1f,.1f,false,false,Vector2.zero,false,true);
            Check(!input.Step(1.2f,.1f,true,true,Vector2.up,false,true),"Slow second tap must walk");
            input.Step(1.25f,.05f,false,false,Vector2.zero,false,true);
            Check(input.Step(1.4f,.15f,true,true,Vector2.up,false,true),"Double W must run");
            Check(input.Step(2,.1f,false,true,Vector2.up,false,true),"Second W hold must retain run");
            Check(!input.Step(2.1f,.1f,false,false,Vector2.zero,false,true),"W release must stop run");
            input.Reset();Check(!input.Step(3,.05f,false,false,Vector2.up,true,true),"Touch should not run instantly");
            Check(input.Step(3.2f,.15f,false,false,Vector2.up,true,true),"Full forward touch must run");
            Check(input.Step(3.3f,.1f,false,false,new Vector2(0,.82f),true,true),"Touch hysteresis must prevent jitter");
            Check(!input.Step(3.4f,.1f,false,false,new Vector2(0,.6f),true,true),"Pullback must walk");
            Check(!input.Step(4,1,false,false,Vector2.right,true,true),"Sideways stick cannot start sprint");
            input.Step(5,1,false,false,Vector2.up,true,true);
            Check(!input.Step(6,.1f,false,false,Vector2.up,true,false),"Modal/focus lock must clear sprint");
            Check(!input.Step(6.1f,.02f,true,true,Vector2.up,false,true),"After reset first W must walk");
            var m=new Match("RUN",new MatchRules{revealDuration=.01f,handInterval=999,finalTime=999});
            foreach(string id in new[]{"a","b"})m.Add(new Profile{playerId=id,nickname=id,personas=new[]{new PersonaAsset{assetId=id,name=id,templateId="Melee",visualId="book"}}});
            m.Phase=MatchPhase.Reveal;m.Tick(.02f);var player=m.Players["a"];player.position=new Vector3(0,1,-10);m.Players["b"].position=new Vector3(10,1,-10);
            int seq=0;float Move(bool run,float x,float z){player.position=new Vector3(0,1,-10);player.impulse=Vector3.zero;float distance=0;for(int i=0;i<20;i++){var before=player.position;m.Receive("a",new Command{sequence=++seq,x=x,z=z,run=run});m.Tick(.02f);distance+=new Vector2(player.position.x-before.x,player.position.z-before.z).magnitude;}return distance;}
            float walk=Move(false,0,1),run=Move(true,0,1),diagonal=Move(true,99,99);
            Check(Mathf.Abs(walk/.4f-5.5f)<.02f&&run>walk*1.25f,"Distinct authoritative speeds");
            Check(diagonal/.4f<=m.Rules.runSpeed+.01f,"Diagonal/hostile input speed limit");
            var view=m.View("a").players.First(p=>p.id=="a");Check(view.running&&view.locomotionSpeed>7,"Run snapshot state missing");
            player.chargingSince=m.Time;Move(true,0,1);Check(!player.running&&player.locomotionSpeed<2.5f,"Heavy charge must suppress running");player.chargingSince=-1;
            Move(true,0,1);for(int i=0;i<25;i++)m.Tick(.02f);Check(!player.running&&player.locomotionSpeed==0,"Lost packets must stop sprint");
            foreach(int fps in new[]{30,60,120})
            {
                float minimum=100,maxRun=0,maxBend=0,handGap=100;
                foreach(int variant in Enumerable.Range(0,6))
                {
                    var a=AppearanceData.Demo(variant);if(variant>=3){a.bag="backpack";a.top="varsity";}
                    a.expression=AppearanceCatalog.Data.slots.First(s=>s.id=="expression").options[variant].id;
                    var actor=new ToyActor("Run validation",a.topColor,0,null,a);float z=0;
                    for(int frame=0;frame<fps*6;frame++)
                    {
                        float t=frame/(float)fps;bool running=t>=1&&t<3;bool eat=t>=3&&t<5;float speed=t<1?5.5f:running?7.8f:0;z+=speed/fps;
                        actor.DrawAt(new PublicPlayer{position=new Vector3(0,1,z),yaw=0,stability=100,running=running,locomotionSpeed=speed,interaction=eat?"eat":"",progress=eat?(t-3)/2:0},null,1f/fps,t);
                        if(t>.4f&&t<2.9f)handGap=Mathf.Min(handGap,actor.LocomotionHandClearance());
                        if(t>1.5f&&t<2.9f){maxRun=Mathf.Max(maxRun,actor.RunBlend);maxBend=Mathf.Max(maxBend,Mathf.Abs(Mathf.DeltaAngle(0,actor.leftForearm.localEulerAngles.x)));}
                        if(actor.MouthOpen>.3f){minimum=Mathf.Min(minimum,actor.FeedingHandClearance());Check(minimum>0,"Run to eat hand/face clipping "+variant+" "+minimum);}
                        Check(actor.LowestSole()>=-.002f,"Sole enters floor");
                        Check(!float.IsNaN(actor.head.position.y),"Animation non-finite");
                    }
                    Check(actor.RunBlend<.01f&&actor.MouthOpen<.01f,"Pose did not release");UnityEngine.Object.DestroyImmediate(actor.root);
                }
                Check(handGap>=-.002f,"Hand enters torso or bag "+handGap);
                Check(maxRun>.95f&&maxBend>50,"Running is not a distinct pose");Debug.Log("LOCOMOTION_PASS fps="+fps+" mouthClearance="+minimum+" elbow="+maxBend+" palmBodyBagGap="+handGap);
            }
            Debug.Log("LOCOMOTION_INPUT_AUTHORITY_EXPRESSIONS_PASSED");
        }
        static readonly string[] CombatCases={"light","heavy","grab","escape","side-left","side-right","back-run","eat","hurt","fall"};
        static PublicPlayer CombatPlayer(string action,float t,AppearanceData appearance)
        {
            bool locomotion=action.StartsWith("side-")||action=="back-run";
            Vector3 velocity=action=="side-left"?Vector3.left*7.8f:action=="side-right"?Vector3.right*7.8f:action=="back-run"?Vector3.back*7.8f:Vector3.zero;
            bool eat=action=="eat";float duration=DollCombatMotion.Duration(action);if(float.IsInfinity(duration)||duration<=0)duration=2.5f;
            float cycle=locomotion?t:Mathf.Repeat(t,duration+.65f);bool active=cycle<duration;
            var p=new PublicPlayer{appearance=appearance,position=Vector3.up*1.02f,connected=true,stability=100,yaw=0,velocity=velocity,running=locomotion,locomotionSpeed=locomotion?7.8f:0,simulationTime=t,actionKind=locomotion?"":active?action:"",actionStartedAt=t-cycle,actionId=(int)(t/(duration+.65f))+1,actionVariant=0};
            p.stagger=action=="fall"&&active?Mathf.Max(0,1.1f-cycle):0;
            p.interaction=eat&&active?"eat":"";p.progress=eat?Mathf.Clamp01(cycle/2.5f):0;
            p.grabbing=action=="grab"&&cycle>=.2f&&active?"contact-target":"";p.grabAnchor=p.position+new Vector3(0,.28f,.64f);p.grabTension=p.grabbing!=""?.6f:0;
            p.impactId=action=="hurt"?(int)(t/1.07f)+1:0;p.impactImpulse=new Vector3(4,0,-5);
            return p;
        }
        public static void CombatValidate()
        {
            CharacterTools.Validate();DollTripoMotion.ClearCache();
            foreach(string clip in new[]{"idle","walk","run","box_01","hurt"})
            {
                Check(DollTripoMotion.IsAvailable(clip)&&DollTripoMotion.Duration(clip)>0,"Missing real Tripo clip "+clip);
                var sourceActor=new ToyActor("Tripo source validation","#79CFC5",0,null,AppearanceData.Demo(0));
                for(int frame=0;frame<30;frame++)
                {
                    float t=frame/60f;var p=new PublicPlayer{position=Vector3.up,stability=100,simulationTime=t,actionKind=clip=="box_01"?"light":clip=="hurt"?"hurt":"",actionId=1,running=clip=="run",locomotionSpeed=clip=="walk"?5.5f:clip=="run"?7.8f:0};
                    p.velocity=Vector3.forward*p.locomotionSpeed;sourceActor.DrawAt(p,null,1/60f,t);
                    if(frame==6&&(clip=="box_01"||clip=="hurt"))Check(sourceActor.LastTripoClip==clip,"Tripo combat clip is not actually used: "+clip);
                }
                if(clip!="box_01"&&clip!="hurt")Check(sourceActor.LastTripoClip==clip,"Tripo locomotion clip is not actually used: "+clip);
                UnityEngine.Object.DestroyImmediate(sourceActor.root);
            }
            CombatAmplitudeValidate();
            foreach(int fps in new[]{30,60,120})
            {
                float rebound=0,contactError=0,mouthGap=100;int count=0;
                var appearances=new List<AppearanceData>();
                // Cover every selectable part and all expression options on the shared rig.
                foreach(var slot in AppearanceCatalog.Data.slots)foreach(var option in slot.options)
                {var a=AppearanceData.Demo(0);a.Set(slot.id,option.id);appearances.Add(a);}
                foreach(var appearance in appearances)
                {
                    var actor=new ToyActor("Combat validation",appearance.topColor,0,null,appearance);
                    Check(actor.DeformationBoneCount==22&&actor.Skin.bones.Length==22,"Incomplete deformation skeleton");
                    Check(actor.RightHandAttachment.parent==actor.rightHand,"Equipment is not bound to hand");
                    foreach(var action in CombatCases)
                    {
                        UnityEngine.Object.DestroyImmediate(actor.root);actor=new ToyActor("Combat validation",appearance.topColor,0,null,appearance);
                        for(int frame=0;frame<fps*3;frame++)
                        {
                            float t=frame/(float)fps;var player=CombatPlayer(action,t,appearance);actor.DrawAt(player,null,1f/fps,t);
                            if(player.grabbing!="")actor.SetGrabContact(player.grabAnchor);
                            rebound=Mathf.Max(rebound,actor.BodyRebound);contactError=Mathf.Max(contactError,actor.GrabContactError);
                            Check(actor.BodyRebound<=.121f,"Rebound exceeded presentation budget");
                            Check(actor.LowestSole()>=-.002f,"Final foot enters floor");
                            foreach(var bone in actor.Skin.bones)Check(!float.IsNaN(bone.position.x)&&!float.IsInfinity(bone.rotation.w),"Non-finite final pose");
                            if(action=="eat"&&actor.MouthOpen>.3f){mouthGap=Mathf.Min(mouthGap,actor.FeedingHandClearance());Check(actor.FeedingHandClearance()>=0,"Final feeding hand intersects mouth "+appearance.top+" gap="+actor.FeedingHandClearance());}
                            if(player.grabbing!=""&&Mathf.Repeat(t,1.1f)>.32f)Check(actor.GrabContactError<=.07f,"Hand cannot reach contact "+actor.GrabContactError);
                        }
                    }
                    // An out-of-reach .77m surface must remain visibly unreachable: IK must not stretch to fake it.
                    var boundary=CombatPlayer("grab",.36f,appearance);boundary.grabAnchor=boundary.position+new Vector3(0,.28f,.77f);
                    actor.DrawAt(boundary,null,1f/fps,.36f);actor.SetGrabContact(boundary.grabAnchor);
                    Check(actor.GrabContactError>.07f,"Out-of-range grab was hidden by stretching");
                    Check(Vector3.Distance(actor.rightArm.position,actor.rightForearm.position)<=.346f&&Vector3.Distance(actor.rightForearm.position,actor.rightHand.position)<=.282f,"Grab stretched beyond bounded limbs");
                    UnityEngine.Object.DestroyImmediate(actor.root);count++;
                }
                Debug.Log("COMBAT_POSE_PASS fps="+fps+" outfits="+count+" maxRebound="+rebound+" contactError="+contactError+" mouthGap="+mouthGap);
            }
            Debug.Log("CHARACTER_V3_COMBAT_VALIDATION_PASSED");
        }
        public static void CombatAmplitudeValidate()
        {
            DollTripoMotion.ClearCache();
            foreach(int rate in new[]{30,60,120})foreach(string kind in new[]{"light","heavy","fall"})
            {
                var actor=new ToyActor("Visible amplitude","#79CFC5",0,null,AppearanceData.Demo(0));float time=kind=="light"?.10f:kind=="heavy"?.16f:.50f;
                for(int frame=0;frame<=Mathf.CeilToInt(time*rate);frame++){float t=frame/(float)rate;actor.DrawAt(CombatPlayer(kind,t,actor.Appearance),null,1f/rate,t);}
                float reach=actor.StrikingReach(0),knee=actor.MaximumKneeBend;
                if(kind=="fall")Check(actor.MaximumKneeBend>95,"Soft fall did not visibly fold knees");
                else
                {
                    Check(actor.StrikingReach(0)>.50f,"Punch palm did not reach in front at hit time: "+kind+" "+actor.StrikingReach(0));
                    var mesh=new Mesh();actor.Skin.BakeMesh(mesh);var weights=actor.Skin.sharedMesh.boneWeights;var verts=mesh.vertices;int index=Array.IndexOf(actor.Skin.bones,actor.rightHand);float forward=0;int count=0;
                    for(int v=0;v<weights.Length;v++)if(weights[v].boneIndex0==index&&weights[v].weight0>.99f){forward+=verts[v].z;count++;}
                    Check(count>0&&forward/count>.46f,"Rendered palm skin failed visible forward reach");UnityEngine.Object.DestroyImmediate(mesh);
                }
                // Persistent authoritative events must expire into locomotion in presentation.
                for(int frame=0;frame<rate;frame++){float t=2+frame/(float)rate;actor.DrawAt(new PublicPlayer{position=Vector3.up,actionKind=kind,actionStartedAt=0,actionId=1,simulationTime=t,velocity=Vector3.forward*7.8f,running=true,locomotionSpeed=7.8f,stability=100},null,1f/rate,t);}
                Check(actor.LastTripoClip=="run","Expired action suppresses Tripo running forever: "+kind);Debug.Log("COMBAT_AMPLITUDE_PASS fps="+rate+" action="+kind+" reach="+reach+" knee="+knee+" recoveredClip="+actor.LastTripoClip);UnityEngine.Object.DestroyImmediate(actor.root);
            }
            Debug.Log("CHARACTER_COMBAT_AMPLITUDE_VALIDATION_PASSED");
        }
        public static void PoseDebug()
        {
            Setup();
            foreach(string action in new[]{"light","heavy","fall"})
            {
                var a=new ToyActor("Debug","#79CFC5",0,null,AppearanceData.Demo(0));
                for(int frame=0;frame<=36;frame++)
                {
                    float t=frame/60f;a.DrawAt(CombatPlayer(action,t,a.Appearance),null,1/60f,t);
                    if(frame==6||frame==10||frame==18||frame==30)
                    {
                        var pose=(DollCombatMotion.Pose)typeof(ToyActor).GetField("combatPose",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(a);
                        Debug.Log("POSE_DEBUG "+action+" t="+t+" targetArm="+pose.rightUpperArmEuler+" actualArm="+a.rightArm.localEulerAngles+" targetKnee="+pose.leftShinEuler+" actualKnee="+a.leftShin.localEulerAngles+" hand="+a.root.transform.InverseTransformPoint(a.rightHand.position)+" hip="+a.leftLeg.localEulerAngles);
                        var baked=new Mesh();a.Skin.BakeMesh(baked);var weights=a.Skin.sharedMesh.boneWeights;Vector3 palm=Vector3.zero,shin=Vector3.zero;int palmCount=0,shinCount=0;int palmIndex=Array.IndexOf(a.Skin.bones,a.rightHand),shinIndex=Array.IndexOf(a.Skin.bones,a.leftShin);var vertices=baked.vertices;
                        for(int i=0;i<weights.Length;i++){if(weights[i].boneIndex0==palmIndex&&weights[i].weight0>.99f){palm+=vertices[i];palmCount++;}if(weights[i].boneIndex0==shinIndex&&weights[i].weight0>.99f){shin+=vertices[i];shinCount++;}}
                        Debug.Log("SKIN_DEBUG "+action+" t="+t+" palmMesh="+(palm/Mathf.Max(1,palmCount))+" palmBone="+a.root.transform.InverseTransformPoint(a.rightHand.position)+" rightRigid="+a.rightArm.GetComponent<Rigidbody>().rotation.eulerAngles+" shinMesh="+(shin/Mathf.Max(1,shinCount))+" palmVerts="+palmCount);UnityEngine.Object.DestroyImmediate(baked);
                        if(frame==10||frame==30)
                        {
                            var rt=new RenderTexture(720,720,24);CombatView(rt,a,true);RenderTexture.active=rt;var tex=new Texture2D(720,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,720,720),0,0);tex.Apply();File.WriteAllBytes("../artifacts/characters-v3/debug-"+action+"-"+frame+".png",tex.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
                            Debug.Log("AFTER_RENDER "+action+" t="+t+" arm="+a.rightArm.localEulerAngles+" shin="+a.leftShin.localEulerAngles);
                        }


                    }
                }
                UnityEngine.Object.DestroyImmediate(a.root);
            }
        }
        static void CombatView(RenderTexture rt,ToyActor actor,bool side)
        {
            camera.targetTexture=rt;camera.orthographicSize=1.8f;
            camera.transform.position=side?new Vector3(6,1.7f,1.5f):new Vector3(.2f,1.8f,6);
            camera.transform.LookAt(new Vector3(0,1.2f,0));actor.label.transform.rotation=camera.transform.rotation;Render();
        }
        static bool reviewStillOnly;
        public static void CombatContactSheet(){reviewStillOnly=true;try{CombatReview();}finally{reviewStillOnly=false;}}
        public static void CombatReview()
        {
            const string directory="../artifacts/characters-v3";
            Directory.CreateDirectory(directory+"/combat-frames");Setup();DollTripoMotion.ClearCache();
            var sheet=new Texture2D(2160,CombatCases.Length*420,TextureFormat.RGB24,false);var rt=new RenderTexture(360,420,24){antiAliasing=4};
            var moments=new[]{new[]{.04f,.10f,.30f},new[]{.06f,.16f,.45f},new[]{.10f,.30f,.44f},new[]{.04f,.10f,.20f},new[]{.25f,.50f,.75f},new[]{.25f,.50f,.75f},new[]{.25f,.50f,.75f},new[]{.20f,1.2f,2.2f},new[]{.035f,.10f,.28f},new[]{.10f,.5f,.95f}};
            for(int index=0;index<CombatCases.Length;index++)for(int phase=0;phase<3;phase++)
            {
                var actor=new ToyActor("Combat review","#79CFC5",0,null,AppearanceData.Demo(0));actor.label.text="";string action=CombatCases[index];float capture=moments[index][phase];
                for(int frame=0;frame<=Mathf.CeilToInt(capture*60);frame++){float t=frame/60f;var p=CombatPlayer(action,t,actor.Appearance);actor.DrawAt(p,null,1/60f,t);if(p.grabbing!="")actor.SetGrabContact(p.grabAnchor);}
                actor.label.text=action+" "+capture.ToString("0.00")+"s";actor.label.characterSize=.033f;actor.label.transform.localPosition=new Vector3(0,1.85f,0);
                for(int view=0;view<2;view++){CombatView(rt,actor,view==1);RenderTexture.active=rt;sheet.ReadPixels(new Rect(0,0,360,420),phase*720+view*360,(CombatCases.Length-1-index)*420);RenderTexture.active=null;}
                UnityEngine.Object.DestroyImmediate(actor.root);
            }
            sheet.Apply();File.WriteAllBytes(directory+"/combat-contactsheet.png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);UnityEngine.Object.DestroyImmediate(rt);
            if(reviewStillOnly){camera.targetTexture=null;Debug.Log("CHARACTER_COMBAT_CONTACTSHEET_RENDERED");return;}
            var frameTarget=new RenderTexture(640,720,24){antiAliasing=4};var frameTexture=new Texture2D(1280,720,TextureFormat.RGB24,false);ToyActor moving=null;
            for(int frame=0;frame<CombatCases.Length*60;frame++)
            {
                if(frame%60==0){if(moving!=null)UnityEngine.Object.DestroyImmediate(moving.root);moving=new ToyActor("Combat review","#79CFC5",0,null,AppearanceData.Demo(0));}
                int actionIndex=frame/60;float t=(frame%60)/30f;string action=CombatCases[actionIndex];var p=CombatPlayer(action,t,moving.Appearance);moving.DrawAt(p,null,1/30f,t);if(p.grabbing!="")moving.SetGrabContact(p.grabAnchor);moving.label.text=action+" "+t.ToString("0.00")+"s";moving.label.characterSize=.035f;
                for(int view=0;view<2;view++){CombatView(frameTarget,moving,view==1);RenderTexture.active=frameTarget;frameTexture.ReadPixels(new Rect(0,0,640,720),view*640,0);RenderTexture.active=null;}
                frameTexture.Apply();File.WriteAllBytes(directory+"/combat-frames/"+frame.ToString("D4")+".png",frameTexture.EncodeToPNG());
            }
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(frameTexture);UnityEngine.Object.DestroyImmediate(frameTarget);if(moving!=null)UnityEngine.Object.DestroyImmediate(moving.root);Debug.Log("CHARACTER_V3_COMBAT_REVIEW_RENDERED");
        }
        static Camera camera;
        static void Setup()
        {
            Directory.CreateDirectory(Out);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var light=new GameObject("Review daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=Quaternion.Euler(35,155,0);
            camera=new GameObject("Review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.5f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#F5EFE3");camera.farClipPlane=200;
        }
        static void Render()
        {
            var temporary=new List<(SkinnedMeshRenderer skin,GameObject go,Mesh mesh)>();
            foreach(var skin in UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>())
            {if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh);var go=new GameObject("Evaluated skin",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(skin.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=skin.sharedMaterial;skin.enabled=false;temporary.Add((skin,go,mesh));}
            camera.rect=new Rect(0,0,1,1);camera.aspect=camera.targetTexture.width/(float)camera.targetTexture.height;camera.Render();foreach(var item in temporary){item.skin.enabled=true;UnityEngine.Object.DestroyImmediate(item.go);UnityEngine.Object.DestroyImmediate(item.mesh);}
        }
        static void Save(RenderTexture rt,string name)
        {
            RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);
        }
        public static void Expressions()
        {
            Setup();var faces=AppearanceCatalog.Data.slots.First(s=>s.id=="expression").options;
            var sheet=new Texture2D(1800,1200,TextureFormat.RGB24,false);var tile=new RenderTexture(600,600,24){antiAliasing=4};camera.targetTexture=tile;
            for(int i=0;i<faces.Length;i++)
            {
                var a=AppearanceData.Demo(0);a.headAccessory="none";a.expression=faces[i].id;var actor=new ToyActor(faces[i].id,a.topColor,0,null,a);actor.root.transform.position=Vector3.up;
                actor.label.text=faces[i].label;actor.label.transform.localPosition=new Vector3(0,.1f,.6f);actor.label.characterSize=.027f;actor.label.color=ToyFactory.ColorOf("#202A3D");
                camera.rect=new Rect(i%3/3f,(1-i/3)/2f,1/3f,.5f);camera.orthographicSize=.95f;camera.transform.position=new Vector3(0,1.8f,6);camera.transform.LookAt(new Vector3(0,1.8f,0));actor.label.transform.rotation=camera.transform.rotation;Render();RenderTexture.active=tile;sheet.ReadPixels(new Rect(0,0,600,600),i%3*600,(1-i/3)*600);RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(actor.root);
            }
            sheet.Apply();File.WriteAllBytes(Out+"/expressions.png",sheet.EncodeToPNG());camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(sheet);UnityEngine.Object.DestroyImmediate(tile);
        }
        public static void Review()
        {
            Validate();Expressions();Setup();
            var actors=new ToyActor[3];for(int i=0;i<3;i++){actors[i]=new ToyActor("Run demo "+i,"#79CFC5",0,null,AppearanceData.Demo(i));actors[i].label.text="";}
            for(int k=-8;k<80;k+=2)DollFactory.Part("Floor stripe",null,new Vector3(0,-.045f,k),new Vector3(18,.02f,.035f),"#CAB79E",false);
            var rt=new RenderTexture(1040,900,24){antiAliasing=4};var side=new RenderTexture(560,900,24){antiAliasing=4};var composite=new Texture2D(1600,900,TextureFormat.RGB24,false);camera.targetTexture=rt;Directory.CreateDirectory(Out+"/motion-frames");float z=0;
            var title=new GameObject("Review phase").AddComponent<TextMesh>();title.anchor=TextAnchor.MiddleCenter;title.fontSize=64;title.characterSize=.034f;title.color=ToyFactory.ColorOf("#202A3D");var font=Resources.Load<Font>("AboutMeFont");if(font){title.font=font;title.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
            for(int frame=0;frame<240;frame++)
            {
                float t=frame/30f;bool run=t>=2&&t<4.5f,eat=t>=5.2f&&t<7.3f;float speed=t<2?5.5f:run?7.8f:0;z+=speed/30;
                for(int i=0;i<3;i++)actors[i].DrawAt(new PublicPlayer{position=new Vector3((i-1)*2.3f,1.03f,z),yaw=0,stability=100,locomotionSpeed=speed,running=run,interaction=eat?"eat":"",progress=eat?(t-5.2f)/2.1f:0},null,1/30f,t);
                title.text=t<2?"行走 5.5 m/s":run?"奔跑 7.8 m/s":eat?"吞噬 · 手嘴避让":"停步 / 动作衔接";
                title.transform.position=new Vector3(0,3.2f,z);title.transform.rotation=Quaternion.identity;
                camera.targetTexture=rt;camera.rect=new Rect(0,0,.65f,1);camera.orthographicSize=2.9f;camera.transform.position=new Vector3(.4f,2.4f,z+9);camera.transform.LookAt(new Vector3(0,1.25f,z));title.transform.rotation=camera.transform.rotation;Render();RenderTexture.active=rt;composite.ReadPixels(new Rect(0,0,1040,900),0,0);RenderTexture.active=null;
                actors[0].root.SetActive(false);actors[2].root.SetActive(false);title.gameObject.SetActive(false);
                camera.targetTexture=side;camera.rect=new Rect(.65f,0,.35f,1);camera.orthographicSize=2.9f;camera.transform.position=new Vector3(8,1.8f,z);camera.transform.LookAt(new Vector3(0,1.25f,z));Render();RenderTexture.active=side;composite.ReadPixels(new Rect(0,0,560,900),1040,0);RenderTexture.active=null;
                actors[0].root.SetActive(true);actors[2].root.SetActive(true);title.gameObject.SetActive(true);
                composite.Apply();var bytes=composite.EncodeToPNG();File.WriteAllBytes(Out+"/motion-frames/"+frame.ToString("D4")+".png",bytes);if(frame==95)File.WriteAllBytes(Out+"/run-front-side.png",bytes);
            }
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(side);UnityEngine.Object.DestroyImmediate(composite);Debug.Log("LOCOMOTION_REVIEW_RENDERED");
        }
        public static void Release(){Review();CharacterTools.ExportDemos();BuildBinary();}
        public static void Build()
        {
            Validate();CharacterTools.ExportDemos();BuildBinary();
        }
        static void BuildBinary()
        {
            Directory.CreateDirectory(Out);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/AboutMe/Scenes/Bootstrap.unity"},locationPathName=Out+"/About Me Movement.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            Check(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded,"V2 build failed");
        }
    }
}
