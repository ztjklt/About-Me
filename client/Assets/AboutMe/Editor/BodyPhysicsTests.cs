using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace AboutMe.Editor
{
    public static class BodyPhysicsTests
    {
        [InitializeOnLoadMethod] static void Resume()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state!=PlayModeStateChange.EnteredPlayMode)return;string job=SessionState.GetString("V4Job","");if(job=="")return;SessionState.EraseString("V4Job");EditorApplication.delayCall+=()=>{try{if(job=="review")RenderReview();else if(job=="validate")Validate();else if(job=="feet")ValidateFeet();else if(job=="controls")ValidateLocomotionControls();else Diagnose();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}};};
        }
        static bool Enter(string job){if(Application.isPlaying)return false;SessionState.SetString("V4Job",job);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return true;}
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static readonly CampusSolid[] Flat={new CampusSolid{name="Test ground",center=new Vector3(0,-.5f,0),size=new Vector3(80,1,64)}};
        static Profile Profile(string id)=>new Profile{playerId=id,nickname=id,appearance=AppearanceData.Demo(0),personas=Enumerable.Range(0,5).Select(i=>new PersonaAsset{assetId=id+i,name="Glove",visualId="book",templateId="Melee",color="#77C8BD"}).ToArray()};
        static Match Create(int players=2)
        {
            var match=new Match("V4",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999});match.Props.Clear();
            for(int i=0;i<players;i++){match.Add(Profile("p"+i));match.Players["p"+i].position=new Vector3(i*5,1,0);}
            match.EnableBodyPhysics();match.Phase=MatchPhase.Reveal;match.Tick(.02f);Check(match.View("p0").players.All(p=>p.bodyPose?.positions?.Length==15),"First battle snapshot missing physics pose");return match;
        }
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        public static void ValidateLocomotionControls()
        {
            if(Enter("controls"))return;
            var facing=new LocomotionInput();
            Check(Mathf.DeltaAngle(facing.FacingYaw(20,Vector2.up),20)==0,"Forward facing changed");
            Check(Mathf.Abs(Mathf.DeltaAngle(facing.FacingYaw(20,Vector2.down),200))<.01f,"S did not face the camera");
            Check(Mathf.Abs(Mathf.DeltaAngle(facing.FacingYaw(20,Vector2.zero),200))<.01f,"S facing did not persist after release");
            Check(Mathf.Abs(Mathf.DeltaAngle(facing.FacingYaw(20,Vector2.up),20))<.01f,"Forward input did not restore facing");
            var old=Cache.GetValue(null);Cache.SetValue(null,Flat);
            try
            {
                using(var m=Create(1))
                {
                    var p=m.Players["p0"];
                    for(int i=0;i<70;i++){m.Receive("p0",new Command{sequence=p.sequence+1,z=1,run=true,yaw=0});m.Tick(.02f);}
                    Debug.Log("V4_CONTROL_RUN height="+p.position.y+" motor="+p.motorHeight+" floor="+Match.Floor(p.position)+" airborne="+p.airborne+" speed="+p.locomotionSpeed);
                    Check(p.running&&p.locomotionSpeed>7,"Run fixture did not reach speed");
                    m.Receive("p0",new Command{sequence=p.sequence+1,z=1,run=true,yaw=0,action="jump"});
                    Debug.Log("V4_CONTROL_JUMP_ACCEPT vertical="+p.verticalSpeed+" action="+p.actionKind);
                    Check(p.verticalSpeed==m.Rules.jumpSpeed&&p.actionKind=="jump","Running jump was rejected");
                    for(int i=0;i<8;i++){m.Receive("p0",new Command{sequence=p.sequence+1,z=1,run=true,yaw=0});m.Tick(.02f);}
                    Debug.Log("V4_CONTROL_JUMP_FLIGHT height="+p.motorHeight+" airborne="+p.airborne+" speed="+p.locomotionSpeed);
                    Check(p.airborne&&p.motorHeight>1.25f&&p.locomotionSpeed>5.5f,"Running jump lost forward flight");
                    float flightSpeed=p.verticalSpeed;
                    m.Receive("p0",new Command{sequence=p.sequence+1,z=1,run=true,yaw=0,action="jump"});
                    Check(p.verticalSpeed==flightSpeed,"Midair jump restarted flight");
                    for(int i=0;i<70;i++){m.Receive("p0",new Command{sequence=p.sequence+1,z=-1,yaw=180});m.Tick(.02f);if(i==4||i==9||i==19)Debug.Log("V4_CONTROL_BACK_TURN tick="+i+" headForward="+(m.BodyWorld.Get(p).Bodies[2].rotation*Vector3.forward));}
                    var rig=m.BodyWorld.Get(p);
                    Debug.Log("V4_CONTROL_BACK yaw="+p.yaw+" headForward="+(rig.Bodies[2].rotation*Vector3.forward)+" chestForward="+(rig.Bodies[1].rotation*Vector3.forward)+" pelvisForward="+(rig.Bodies[0].rotation*Vector3.forward)+" velocity="+p.velocity);
                    Check(Vector3.Dot(rig.Bodies[2].rotation*Vector3.forward,Vector3.back)>.9f,"S physics face did not look toward camera");
                }
            }
            finally{Cache.SetValue(null,old);}
            Debug.Log("V4_LOCOMOTION_CONTROLS_PASSED");
        }
        public static void ValidateFeet()
        {
            if(Enter("feet"))return;
            var old=Cache.GetValue(null);Cache.SetValue(null,Flat);
            try
            {
                using(var m=Create(1))
                {
                    var p=m.Players["p0"];
                    foreach(string phase in new[]{"idle","walk","run","stop","strafe","reverse","turn"})
                    {
                        float left=0,right=0,leftContact=0,rightContact=0,leftPlant=0,rightPlant=0,maxSlip=0,minGap=100,maxGap=-100;
                        for(int frame=0;frame<80;frame++)
                        {
                            float x=phase=="strafe"?1:0,z=phase=="walk"||phase=="run"||phase=="turn"?1:phase=="reverse"?-1:0;
                            m.Receive("p0",new Command{sequence=p.sequence+1,x=x,z=z,run=phase=="run",yaw=phase=="turn"?frame*1.125f:0});m.Tick(.02f);
                            var rig=m.BodyWorld.Get(p);float a=rig.FootSoleGap(0),b=rig.FootSoleGap(1);
                            left+=a;right+=b;minGap=Mathf.Min(minGap,a,b);maxGap=Mathf.Max(maxGap,a,b);
                            if(rig.FootContactAge(0)<.04f)leftContact++;if(rig.FootContactAge(1)<.04f)rightContact++;
                            if(rig.FootPlanted(0))leftPlant++;if(rig.FootPlanted(1))rightPlant++;
                            maxSlip=Mathf.Max(maxSlip,rig.FootPlantSlip(0),rig.FootPlantSlip(1));
                        }
                        Debug.Log("V4_FEET phase="+phase+" leftGap="+left/80+" rightGap="+right/80+" minGap="+minGap+" maxGap="+maxGap+" leftContact="+leftContact/80+" rightContact="+rightContact/80+" leftPlant="+leftPlant/80+" rightPlant="+rightPlant/80+" maxSlip="+maxSlip+" speed="+p.velocity.magnitude);
                        Check(left/80<.03f&&right/80<.03f&&maxGap<.08f&&minGap>-.04f,"Shoe lost the floor in "+phase);
                        Check(maxSlip<.18f,"Stance foot dragged in "+phase+": "+maxSlip);
                        Check(leftPlant+rightPlant>(phase=="idle"||phase=="stop"?120:12),"No stable foot support in "+phase);
                    }
                }
            }
            finally{Cache.SetValue(null,old);}
            Debug.Log("V4_FEET_VALIDATION_PASSED");
        }
        public static void Diagnose()
        {
            if(Enter("diagnose"))return;
            var old=Cache.GetValue(null);Cache.SetValue(null,Flat.Concat(new[]{new CampusSolid{name="Wall",center=new Vector3(-2,2,0),size=new Vector3(.4f,4,8)}}).ToArray());
            try
            {
                using(var match=Create())
                {
                    var p=match.Players["p0"];var a=match.Players["p1"];a.position=Vector3.right*5+Vector3.up;
                    for(int frame=0;frame<400;frame++)
                    {
                        
                        if(frame==40)match.Hit(p,a,1,8,true,true);
                        match.Tick(.02f);
                        if(frame%10==0)
                        {var rig=match.BodyWorld.Get(p);Debug.Log("V4_DIAG frame="+frame+" mode="+p.bodyMode+" pos="+p.position+" head="+rig.Bodies[2].position+" chestUp="+Vector3.Dot(rig.Bodies[1].rotation*Vector3.up,Vector3.up)+" vel="+p.velocity+" pelvisRot="+rig.Bodies[0].rotation.eulerAngles+" getUp="+p.getUpPosition+" targetChest="+rig.TargetChest+" bones="+rig.Bodies.Length+" stepMs="+match.BodyWorld.LastStepMs);}
                    }
                }
            }
            finally{Cache.SetValue(null,old);}
        }
        static void Ticks(Match m,int count){for(int i=0;i<count;i++)m.Tick(.02f);}
        static int Dropped(Match m,Player p)=>m.Items.Count(i=>i.ownerId==p.profile.playerId&&i.state==ItemState.Ground);
        static void AssertRig(ActiveDollRig rig)
        {
            foreach(var body in rig.Bodies){Check(!float.IsNaN(body.position.x)&&body.position.magnitude<200,"Body nonfinite or escaped");Check(body.velocity.magnitude<24,"Unbounded body velocity");}
            foreach(var b in rig.Bodies){var joint=b.GetComponent<ConfigurableJoint>();if(joint)Check(Vector3.Distance(b.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor))<.22f,"Joint stretched apart: "+b.transform.parent.name+" / "+b.name+" gap="+Vector3.Distance(b.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)));}
            Check(rig.Bodies.Length==15&&rig.Bodies.Count(b=>b.GetComponent<ConfigurableJoint>())==14,"Rig body/joint counts");
        }
        public static void Validate()
        {
            if(Enter("validate"))return;
            var old=Cache.GetValue(null);Cache.SetValue(null,Flat);
            try
            {
                foreach(bool heavy in new[]{false,true})using(var m=Create())
                {
                    var p=m.Players["p0"];Ticks(m,60);var start=p.position;m.Hit(p,m.Players["p1"],1,7,heavy);Ticks(m,50);
                    float distance=Vector2.Distance(new Vector2(start.x,start.z),new Vector2(p.position.x,p.position.z));
                    Debug.Log("V4_RECOIL heavy="+heavy+" distance="+distance);
                    Check(distance>=(heavy?1.3f:.65f)&&distance<=(heavy?2.4f:1.15f),"Recoil distance outside target");
                    Check(Dropped(m,p)==0,"Non-depleting strike dropped persona");Check(p.bodyMode==BodyMode.Standing,"Ordinary strike did not recover");AssertRig(m.BodyWorld.Get(p));
                }
                foreach(Vector3 direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})using(var m=Create())
                {
                    var p=m.Players["p0"];var a=m.Players["p1"];a.position=direction*5+Vector3.up;m.BodyWorld.Reset(a);Ticks(m,60);var start=p.position;
                    m.Hit(p,a,1,8,true,true);bool lay=false,getup=false;float minUp=1,groundAt=-1;int drops=0;bool pushed=false;
                    for(int f=0;f<220;f++)
                    {
                        m.Tick(.02f);var rig=m.BodyWorld.Get(p);AssertRig(rig);
                        if(p.bodyMode==BodyMode.Grounded){lay=true;minUp=Mathf.Min(minUp,Mathf.Abs(Vector3.Dot(rig.Bodies[1].rotation*Vector3.up,Vector3.up)));if(groundAt<0)groundAt=p.bodyStateAt;
                            if(!pushed){float hp=p.stability;float at=p.bodyStateAt;m.Hit(p,a,999,12,true,true);Check(p.stability==hp&&p.bodyStateAt==at,"Ground followup extends control or damages");pushed=true;}}
                        if(p.bodyMode==BodyMode.GettingUp){getup=true;Check(m.Time-groundAt>=.98f,"Got up before ground timer");}
                        drops=Mathf.Max(drops,Dropped(m,p));
                    }
                    Check(lay&&minUp<.5f,"Body did not actually lie flat");Check(getup&&p.bodyMode==BodyMode.Standing,"Get-up did not restore control: mode="+p.bodyMode+" direction="+direction+" chestUp="+Vector3.Dot(m.BodyWorld.Get(p).Bodies[1].rotation*Vector3.up,Vector3.up)+" footAge="+m.BodyWorld.Get(p).FootContactAge(0)+"/"+m.BodyWorld.Get(p).FootContactAge(1)+" footGap="+m.BodyWorld.Get(p).FootSoleGap(0)+"/"+m.BodyWorld.Get(p).FootSoleGap(1)+" position="+p.position+" goal="+p.getUpPosition);Check(drops==0,"Non-depleting knockdown dropped persona");
                    Debug.Log("V4_FALL_PASS direction="+direction+" minChestUp="+minUp+" displacement="+Vector3.Distance(start,p.position));
                }
                using(var m=Create())
                {
                    var p=m.Players["p0"];Ticks(m,50);p.stability=1;m.Hit(p,m.Players["p1"],2,4,false);Check(Dropped(m,p)==1&&p.stability==65,"Zero stability must drop once and restore 65");
                    for(int i=0;i<100;i++){m.Hit(p,m.Players["p1"],99,9,true,true);m.Tick(.02f);}Check(Dropped(m,p)==1,"Repeated ground contacts duplicated drop");
                }
                using(var m=Create())
                {
                    var p=m.Players["p0"];p.position=new Vector3(0,5,0);m.BodyWorld.Reset(p);p.fallOriginHeight=4;p.airborne=true;float minUp=1;bool landed=false;
                    for(int f=0;f<190;f++){m.Tick(.02f);if(p.bodyMode==BodyMode.Grounded){landed=true;Check(p.stability==0,"High landing did not clear stability");minUp=Mathf.Min(minUp,Mathf.Abs(Vector3.Dot(m.BodyWorld.Get(p).Bodies[1].rotation*Vector3.up,Vector3.up)));}}
                    Debug.Log("V4_HIGH_DETAILS landed="+landed+" drops="+Dropped(m,p)+" up="+minUp+" position="+p.position+" state="+p.bodyMode);Check(landed&&Dropped(m,p)==1&&minUp<.5f,"High fall must lie and drop exactly one");Check(new Vector2(p.position.x,p.position.z).magnitude<6,"High fall teleported away");Debug.Log("V4_HIGH_FALL_PASS");
                }
                using(var m=Create(6))
                {
                    double maximum=0,total=0;for(int f=0;f<220;f++)
                    {
                        foreach(var p in m.Players.Values)m.Receive(p.profile.playerId,new Command{sequence=p.sequence+1,x=f<100?1:0,z=f<100?.2f:0,run=true,yaw=90});
                        if(f==110)foreach(var p in m.Players.Values)m.Hit(p,m.Players.Values.First(a=>a!=p),1,8,true,true);
                        m.Tick(.02f);foreach(var p in m.Players.Values)AssertRig(m.BodyWorld.Get(p));
                        if(f%20==0)
                        {
                            var snapshot=m.View("p0");Check(snapshot.players.Count()==6,"Six-player snapshot lost a body");
                            foreach(var visible in snapshot.players)
                            {
                                Check(visible.bodyPose?.positions?.Length==15&&visible.bodyPose.rotations?.Length==15,"Six-player snapshot lost physical bones");
                                Check(visible.bodyPose.positions.All(position=>!float.IsNaN(position.x)&&position.magnitude<200),"Six-player snapshot has invalid foot/body positions");
                            }
                        }
                        if(f>20){maximum=Math.Max(maximum,m.BodyWorld.LastStepMs);total+=m.BodyWorld.LastStepMs;}
                    }
                    Debug.Log("V4_SIX_PHYSICS meanMs="+total/199+" maxMs="+maximum);
                }
                ExtraValidate();
                Debug.Log("V4_BODY_VALIDATION_PASSED");
            }
            finally{Cache.SetValue(null,old);}
        }
        static void ExtraValidate()
        {
            using(var m=Create())
            {
                var p=m.Players["p0"];Ticks(m,40);m.Receive("p0",new Command{sequence=1,action="jump"});Ticks(m,65);Check(Dropped(m,p)==0&&!m.BodyLocked(p),"Normal jump caused a fall");Debug.Log("V4_JUMP_PASS");
            }
            Cache.SetValue(null,Flat.Concat(new[]{new CampusSolid{name="Wall",center=new Vector3(-2,2,0),size=new Vector3(.4f,4,8)}}).ToArray());
            using(var m=Create())
            {
                var p=m.Players["p0"];Ticks(m,40);m.Hit(p,m.Players["p1"],1,8,true,true);
                for(int i=0;i<180;i++){m.Tick(.02f);foreach(var b in m.BodyWorld.Get(p).Bodies)Check(b.position.x> -2.3f,"Ragdoll tunneled through wall");}
                Check(p.bodyMode==BodyMode.Standing,"Wall fall failed to get up: "+p.bodyMode+" at="+p.bodyStateAt+" now="+m.Time+" position="+p.position+" goal="+p.getUpPosition);Debug.Log("V4_WALL_PASS");
            }
            Cache.SetValue(null,Flat);
            using(var m=Create())
            {
                var p=m.Players["p0"];var a=m.Players["p1"];Ticks(m,40);m.Hit(p,a,1,8,true,true);
                while(p.bodyMode!=BodyMode.Grounded&&m.Time<4)m.Tick(.02f);
                var chest=m.BodyWorld.Get(p).Bodies[1];a.position=chest.position+Vector3.forward*.85f;a.position=new Vector3(a.position.x,1,a.position.z);a.yaw=180;m.BodyWorld.Reset(a);a.grabbing=p.profile.playerId;a.grabStartedAt=m.Time;a.bodyGrabAnchor=Vector3.zero;
                float at=p.bodyStateAt;Ticks(m,70);Check(p.bodyStateAt!=at&&a.grabbing=="","Get-up failed to release lying drag");Debug.Log("V4_DRAG_RELEASE_PASS");
            }
            foreach(bool run in new[]{false,true})foreach(Vector3 direction in new[]{Vector3.forward,Vector3.right,Vector3.back})using(var m=Create(1))
            {
                var p=m.Players["p0"];Ticks(m,30);var start=p.position;
                for(int i=0;i<100;i++){m.Receive("p0",new Command{sequence=p.sequence+1,x=direction.x,z=direction.z,run=run});m.Tick(.02f);}
                float speed=Vector2.Distance(new Vector2(start.x,start.z),new Vector2(p.position.x,p.position.z))/2;
                Debug.Log("V4_MOVEMENT run="+run+" direction="+direction+" averageSpeed="+speed);Check(speed>3&&speed<9.5f,"Physical locomotion too slow or too fast");
            }
            Cache.SetValue(null,null);
            using(var m=Create())
            {
                var p=m.Players["p0"];p.position=new Vector3(21,1,-20);m.BodyWorld.Reset(p);
                for(int i=0;i<220&&p.position.z< -8.8f;i++){m.Receive("p0",new Command{sequence=p.sequence+1,z=1});m.Tick(.02f);}
                Debug.Log("V4_STAIRS position="+p.position+" drops="+Dropped(m,p));Check(p.position.y>4.8f&&Dropped(m,p)==0,"Physical stairs ascent failed");
                for(int i=0;i<240&&p.position.z> -20;i++){m.Receive("p0",new Command{sequence=p.sequence+1,z=-1});m.Tick(.02f);}
                Check(p.position.y<1.4f&&Dropped(m,p)==0,"Stairs descent incorrectly fell");Debug.Log("V4_STAIRS_DOWN_PASS");
            }
            Cache.SetValue(null,Flat);
            using(var m=Create(6))
            {
                int index=0;foreach(var p in m.Players.Values){p.position=new Vector3((index%2)*.25f,1+index*2.8f,0);index++;m.BodyWorld.Reset(p);m.Hit(p,m.Players.Values.First(a=>a!=p),1,7,true,true);}
                for(int f=0;f<180;f++){m.Tick(.02f);foreach(var p in m.Players.Values)AssertRig(m.BodyWorld.Get(p));}
                Debug.Log("V4_SIX_STACK_PASS");
            }
            using(var m=Create())
            {
                var p=m.Players["p0"];p.position=new Vector3(0,5,0);p.fallOriginHeight=4;p.airborne=true;m.BodyWorld.Reset(p);p.stability=1;m.Hit(p,m.Players["p1"],2,7,true,true);
                bool protectedChecked=false;for(int f=0;f<200;f++){m.Tick(.02f);if(p.bodyMode==BodyMode.GettingUp){float hp=p.stability;m.Hit(p,m.Players["p1"],999,20,true,true);Check(p.stability==hp,"Get-up immunity failed");protectedChecked=true;}}
                Check(Dropped(m,p)==1&&protectedChecked,"Hit plus high landing duplicated drop or missed recovery");Debug.Log("V4_COMBINED_DROP_PROTECTION_PASS");
            }
            // Capture actual final authority poses once, then apply every catalog part at 30/60/120 render FPS.
            var frames=new System.Collections.Generic.List<PublicPlayer>();
            using(var m=Create())
            {
                var p=m.Players["p0"];for(int f=0;f<220;f++){if(f==40)m.Hit(p,m.Players["p1"],1,8,true,true);m.Tick(.02f);var v=m.View("p0").players.First(x=>x.id=="p0");v.bodyPose=new BodyPose{tick=v.bodyPose.tick,positions=v.bodyPose.positions.ToArray(),rotations=v.bodyPose.rotations.ToArray()};v.equippedVisual="";frames.Add(v);}
            }
            float low=100;string lowPart="";int tested=0;
            foreach(var slot in AppearanceCatalog.Data.slots)foreach(var option in slot.options)
            {
                var appearance=AppearanceData.Demo(0);appearance.Set(slot.id,option.id);var actor=new ToyActor("Skin check","#77C8BD",0,null,appearance);
                foreach(int fps in new[]{30,60,120})
                {
                    var mesh=new Mesh();
                    for(int frame=0;frame<fps*4;frame++)
                    {
                        float t=frame/(float)fps;var v=frames[Mathf.Min(frames.Count-1,Mathf.FloorToInt(t/.02f))];v.appearance=appearance;actor.DrawAt(v,null,1f/fps,t);
                        if(frame%Mathf.Max(1,fps/15)!=0)continue;actor.Skin.BakeMesh(mesh);
                        var vertices=mesh.vertices;var weights=mesh.boneWeights;for(int i=0;i<vertices.Length;i++){float y=actor.Skin.transform.TransformPoint(vertices[i]).y;if(y<low){low=y;var skinWeights=actor.Skin.sharedMesh.boneWeights;lowPart=slot.id+"/"+option.id+"/"+v.bodyMode+" bone="+actor.Skin.bones[skinWeights[i].boneIndex0].name+" t="+t+" fps="+fps+" local="+actor.Skin.bones[skinWeights[i].boneIndex0].InverseTransformPoint(actor.Skin.transform.TransformPoint(vertices[i]));}}
                    }
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                UnityEngine.Object.DestroyImmediate(actor.root);tested++;
            }
            Debug.Log("V4_RENDER_POSE_CATALOG count="+tested+" lowestVertex="+low+" part="+lowPart);
            Check(low>-.035f,"Visible mesh penetrates ground: "+lowPart+" depth="+low);
        }
        public static void RenderReview(){if(Enter("review"))return;BodyPhysicsShowcase.Render();}
        public static void RenderSingleReview()
        {
            if(Enter("review"))return;
            var old=Cache.GetValue(null);Cache.SetValue(null,Flat);Directory.CreateDirectory("../artifacts/characters-v4/frames");
            var camera=new GameObject("Review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2.8f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.96f,.94f,.89f);
            var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,-30,0);RenderSettings.ambientLight=Color.gray;
            ToyFactory.Shape("Ground",PrimitiveType.Cube,null,new Vector3(0,-.04f,0),new Vector3(80,.08f,64),"#C9DCD3");
            foreach(var temporary in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(temporary.name=="Cube"&&temporary.transform.parent==null)UnityEngine.Object.DestroyImmediate(temporary.gameObject);
            var rt=new RenderTexture(960,720,24);camera.targetTexture=rt;var tex=new Texture2D(960,720,TextureFormat.RGB24,false);
            var actor=new ToyActor("Physical review","#77C8BD",0,null,AppearanceData.Demo(0));actor.ImmediatePhysicsPose=true;
            try
            {
                using(var match=Create())
                {
                    var p=match.Players["p0"];var attacker=match.Players["p1"];
                    match.BodyWorld.Get(p);match.BodyWorld.Get(attacker);
                    foreach(var temporary in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(temporary.name=="Cube"&&temporary.transform.parent==null)UnityEngine.Object.DestroyImmediate(temporary.gameObject);
                    for(int frame=0;frame<240;frame++)
                    {
                        if(frame==40)match.Hit(p,attacker,1,8,true,true);
                        match.Tick(.02f);var view=match.View("p0").players.First(x=>x.id=="p0");actor.DrawAt(view,camera,.02f,match.Time);
                        if(frame%2!=0)continue;
                        camera.transform.position=p.position+new Vector3(4.5f,2,5);camera.transform.LookAt(p.position+Vector3.up*.15f);actor.label.text=p.bodyMode+" "+match.Time.ToString("0.00");actor.label.transform.rotation=camera.transform.rotation;
                        var mesh=new Mesh();actor.Skin.BakeMesh(mesh);var temp=new GameObject("Baked",typeof(MeshFilter),typeof(MeshRenderer));temp.transform.SetParent(actor.Skin.transform,false);temp.GetComponent<MeshFilter>().sharedMesh=mesh;temp.GetComponent<MeshRenderer>().sharedMaterial=actor.Skin.sharedMaterial;actor.Skin.enabled=false;camera.Render();actor.Skin.enabled=true;
                        RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,720),0,0);tex.Apply();File.WriteAllBytes("../artifacts/characters-v4/frames/"+(frame/2).ToString("D4")+".png",tex.EncodeToPNG());RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(temp);UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
                Debug.Log("V4_PHYSICS_REVIEW_RENDERED");
            }
            finally{Cache.SetValue(null,old);}
        }
    }
}
