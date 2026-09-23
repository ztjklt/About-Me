using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AboutMe
{
    public enum BodyMode { Standing, Stagger, Falling, Grounded, GettingUp }
    [Serializable] public class BodyPose
    {
        public int tick;
        public Vector3[] positions;
        public Quaternion[] rotations;
    }
    // One authority-only physics scene per match; visual clients do not simulate competing ragdolls.
    public sealed class ActiveDollWorld : IDisposable
    {
        readonly Scene scene;
        readonly PhysicsScene physics;
        readonly GameObject container;
        readonly Dictionary<string,ActiveDollRig> rigs=new Dictionary<string,ActiveDollRig>();
        readonly Dictionary<string,GameObject> barriers=new Dictionary<string,GameObject>();
        public int RigCount=>rigs.Count;
        public double LastStepMs {get;private set;}
        public ActiveDollWorld()
        {
            scene=SceneManager.CreateScene("Match physics "+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics=scene.GetPhysicsScene();container=new GameObject("Authority collision world");SceneManager.MoveGameObjectToScene(container,scene);
            foreach(var solid in CampusGeometry.Solids)Box(solid.name,solid.center,solid.size);
        }
        GameObject Box(string name,Vector3 center,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(container.transform,false);go.transform.position=center;go.AddComponent<BoxCollider>().size=size;return go;}
        public ActiveDollRig Get(Player p)
        {
            if(!rigs.TryGetValue(p.profile.playerId,out var rig))rigs[p.profile.playerId]=rig=new ActiveDollRig(p,container.transform);
            return rig;
        }
        public void WarmOne(IEnumerable<Player> players){foreach(var p in players)if(!rigs.ContainsKey(p.profile.playerId)){Get(p);break;}}
        public void Step(Match match,float dt)
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            foreach(var id in rigs.Keys.Where(id=>!match.Players.ContainsKey(id)||match.Players[id].eliminated).ToArray()){rigs[id].Dispose();rigs.Remove(id);}
            var activeBarriers=match.Effects.Where(e=>e.kind=="Barricade").ToArray();
            foreach(var key in barriers.Keys.Where(id=>!activeBarriers.Any(e=>e.id==id)).ToArray()){Destroy(barriers[key]);barriers.Remove(key);}
            foreach(var b in activeBarriers)if(!barriers.ContainsKey(b.id))barriers[b.id]=Box("Temporary barrier",b.position+Vector3.up*.25f,new Vector3(b.radius*2,1.5f,b.radius*2));
            // Keep authority at 50 Hz; fast collisions / a body pile receive two solver substeps within that tick.
            int substeps=rigs.Values.Any(r=>r.Bodies.Any(b=>b.velocity.sqrMagnitude>100))||match.Players.Values.Count(p=>match.BodyDown(p))>=3?2:1;
            for(int substep=0;substep<substeps;substep++)
            {
            foreach(var p in match.Players.Values)if(!p.eliminated)Get(p).Prepare(p,match,dt/substeps,dt);
            foreach(var p in match.Players.Values)if(p.grabbing!=""&&match.Players.TryGetValue(p.grabbing,out var other)&&!other.eliminated)
            {
                var a=Get(p);var b=Get(other);var grabbedBody=b.Bodies[Mathf.Clamp(p.bodyGrabBone,0,14)];var contact=grabbedBody.position+grabbedBody.rotation*p.bodyGrabAnchor;
                var hand=(a.Bodies[5].position+a.Bodies[8].position)*.5f;
                var force=Vector3.ClampMagnitude((contact-hand)*110+(grabbedBody.velocity-a.Bodies[0].velocity)*12,90);
                a.Bodies[0].AddForce(force);grabbedBody.AddForceAtPosition(-force,contact);
                a.Reach(contact);p.grabTension=other.grabTension=Mathf.Clamp01(force.magnitude/90);
            }
            physics.Simulate(dt/substeps);
            }
            foreach(var p in match.Players.Values)if(!p.eliminated)Get(p).ReadBack(p,match,dt);
            clock.Stop();LastStepMs=clock.Elapsed.TotalMilliseconds;
        }
        public void PublishInitial(Match match){foreach(var p in match.Players.Values)if(!p.eliminated)Get(p).PublishPose(p,match.serverTick);}
        public void Impact(Player p,Vector3 velocity,Vector3 point,bool topple)
        {Get(p).Impact(velocity,point,topple);}
        public Vector3 Contact(Player p,Vector3 from)=>Get(p).ClosestContact(from);
        public void Reset(Player p){Get(p).Reset(p.position,p.yaw);p.motorHeight=p.position.y;}
        public void Dispose()
        {
            foreach(var rig in rigs.Values)rig.Dispose();rigs.Clear();Destroy(container);
            if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
        }
        static void Destroy(UnityEngine.Object o){if(!o)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
    public sealed class ActiveDollContact : MonoBehaviour
    {
        public ActiveDollRig owner;public int index;
        void OnCollisionStay(Collision c)=>Record(c);
        void OnCollisionEnter(Collision c)=>Record(c);
        void Record(Collision c)
        {
            if(c.collider.GetComponent<ActiveDollContact>()!=null)return;
            for(int i=0;i<c.contactCount;i++)if(c.GetContact(i).normal.y>.45f)owner.Contact(index,c.relativeVelocity.magnitude);
        }
    }
    public sealed class ActiveDollRig : IDisposable
    {
        public readonly Rigidbody[] Bodies=new Rigidbody[15];
        readonly ConfigurableJoint[] joints=new ConfigurableJoint[15];
        readonly Quaternion[] bindRelative=new Quaternion[15];
        public Vector3 TargetChest=>target[1].rotation.eulerAngles;
        public string MotionDiagnostic=>"target chest="+target[1].rotation.eulerAngles+" phys chest="+Bodies[1].rotation.eulerAngles+" target arm="+target[3].localEulerAngles+" leg="+target[9].localEulerAngles+" physical arm="+(Quaternion.Inverse(Bodies[1].rotation)*Bodies[3].rotation).eulerAngles+" leg="+(Quaternion.Inverse(Bodies[0].rotation)*Bodies[9].rotation).eulerAngles;
        readonly Transform[] target;
        readonly ToyActor donor;
        readonly GameObject root;
        readonly Vector3[] getUpPositions=new Vector3[15];
        readonly Quaternion[] getUpRotations=new Quaternion[15];
        readonly int[] parent={-1,0,1,1,3,4,1,6,7,0,9,10,0,12,13};
        readonly float[] masses={3,3,1.5f,.5f,.4f,.3f,.5f,.4f,.3f,.8f,.6f,.4f,.8f,.6f,.4f};
        readonly BodyPose pose=new BodyPose{positions=new Vector3[15],rotations=new Quaternion[15]};
        float contactAge=99,bodyContactAge=99,feetContactAge=99,landSpeed;
        BodyMode previousMode;
        Vector3 lastPublished,standingPoint;
        float lastYaw;
        public static Transform[] Bones(ToyActor a)=>new[]{a.pelvis,a.torso,a.head,a.leftArm,a.leftForearm,a.leftHand,a.rightArm,a.rightForearm,a.rightHand,a.leftLeg,a.leftShin,a.leftFoot,a.rightLeg,a.rightShin,a.rightFoot};
        public ActiveDollRig(Player p,Transform container)
        {
            root=new GameObject("Physical body "+p.profile.playerId);root.transform.SetParent(container,false);
            donor=new ToyActor("Animation target",p.profile.color,p.profile.outfit,root.transform,p.profile.appearance);
            donor.PhysicsTarget=true;
            donor.DrawAt(new PublicPlayer{position=p.position,yaw=p.yaw,stability=100,connected=true},null,.02f,0);
            donor.root.transform.localScale=Vector3.one;
            foreach(var renderer in donor.root.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            target=Bones(donor);
            var material=new PhysicMaterial("Doll friction"){dynamicFriction=.35f,staticFriction=.45f,bounciness=.05f,frictionCombine=PhysicMaterialCombine.Minimum,bounceCombine=PhysicMaterialCombine.Minimum};
            for(int i=0;i<15;i++)
            {
                var go=new GameObject(target[i].name+" body");go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(target[i].position,target[i].rotation);
                var collider=go.AddComponent<BoxCollider>();collider.sharedMaterial=material;
                if(i==0){collider.size=new Vector3(.62f,.25f,.40f);collider.center=Vector3.up*.08f;
                    var hem=go.AddComponent<BoxCollider>();hem.center=new Vector3(0,-.14f,0);hem.size=new Vector3(1.02f,.38f,.70f);hem.sharedMaterial=material;}
                else if(i==1){collider.size=new Vector3(.86f,.86f,.62f);collider.center=Vector3.up*.01f;
                    // Canonical envelope includes every supported bag; cosmetics never alter reach or physics size.
                    var bag=go.AddComponent<BoxCollider>();bag.center=new Vector3(0,-.03f,-.43f);bag.size=new Vector3(.60f,.70f,.32f);bag.sharedMaterial=material;
                    var sling=go.AddComponent<BoxCollider>();sling.center=new Vector3(-.31f,-.28f,.32f);sling.size=new Vector3(.38f,.38f,.24f);sling.sharedMaterial=material;}
                else if(i==2){UnityEngine.Object.DestroyImmediate(collider);var sphere=go.AddComponent<SphereCollider>();sphere.radius=.73f;sphere.sharedMaterial=material;}
                else if(i==3||i==6){collider.size=new Vector3(.26f,.28f,.28f);collider.center=Vector3.down*.14f;}
                else if(i==4||i==7){collider.size=new Vector3(.23f,.24f,.25f);collider.center=Vector3.down*.115f;}
                else if(i==5||i==8){collider.size=new Vector3(.25f,.20f,.25f);collider.center=new Vector3(0,-.055f,.03f);}
                else if(i==9||i==12){collider.size=new Vector3(.30f,.25f,.34f);collider.center=Vector3.down*.12f;}
                else if(i==10||i==13){collider.size=new Vector3(.29f,.25f,.34f);collider.center=new Vector3(0,-.12f,.04f);}
                else{collider.size=new Vector3(.48f,.54f,.77f);collider.center=new Vector3(0,.075f,.025f);}
                foreach(var shape in go.GetComponents<Collider>())shape.contactOffset=.035f;
                var b=go.AddComponent<Rigidbody>();Bodies[i]=b;b.mass=masses[i];b.useGravity=false;b.drag=.12f;b.angularDrag=.35f;b.maxAngularVelocity=16;b.maxDepenetrationVelocity=3;b.solverIterations=32;b.solverVelocityIterations=12;b.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;if(i==0)b.inertiaTensor=Vector3.one*1.5f;if(i==1)b.inertiaTensor=Vector3.one*.8f;
                var observer=go.AddComponent<ActiveDollContact>();observer.owner=this;observer.index=i;
                if(i==0)continue;
                var j=go.AddComponent<ConfigurableJoint>();joints[i]=j;j.connectedBody=Bodies[parent[i]];j.autoConfigureConnectedAnchor=false;j.connectedAnchor=Bodies[parent[i]].transform.InverseTransformPoint(b.position);j.anchor=i==2?new Vector3(0,-.34f,0):Vector3.zero;if(i==2)j.connectedAnchor=Bodies[parent[i]].transform.InverseTransformPoint(b.position+b.rotation*j.anchor);
                bindRelative[i]=Quaternion.Inverse(Bodies[parent[i]].rotation)*b.rotation;
                j.axis=Vector3.right;j.secondaryAxis=Vector3.up;j.xMotion=j.yMotion=j.zMotion=ConfigurableJointMotion.Locked;
                j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Limited;j.rotationDriveMode=RotationDriveMode.XYAndZ;
                bool knee=i==10||i==13,elbow=i==4||i==7;
                j.lowAngularXLimit=new SoftJointLimit{limit=knee?-5:elbow?-125:i==1?-35:i==2?-75:-100};j.highAngularXLimit=new SoftJointLimit{limit=knee?130:elbow?5:i==1?40:i==2?55:100};
                j.angularYLimit=new SoftJointLimit{limit=knee||elbow?0:i==1?30:i==2?55:65};j.angularZLimit=new SoftJointLimit{limit=knee||elbow?0:i==1?25:i==2?40:80};
                if(knee||elbow)j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Locked;
                j.enablePreprocessing=false;j.projectionMode=JointProjectionMode.PositionAndRotation;j.projectionDistance=.015f;j.projectionAngle=12;
            }
            for(int i=0;i<15;i++)for(int k=0;k<i;k++)
            {
                bool near=parent[i]==k||parent[k]==i||parent[i]>=0&&parent[i]==parent[k]||i==1&&k==0||i==11&&k==9||i==14&&k==12||i>=9&&k>=9;
                if(near)foreach(var a in Bodies[i].GetComponents<Collider>())foreach(var b in Bodies[k].GetComponents<Collider>())Physics.IgnoreCollision(a,b);
            }
            var hemGuard=Bodies[0].GetComponents<Collider>()[1];for(int i=1;i<15;i++)foreach(var own in Bodies[i].GetComponents<Collider>())Physics.IgnoreCollision(hemGuard,own);
            lastPublished=p.position;lastYaw=p.yaw;p.motorHeight=p.position.y;
        }
        public void Contact(int i,float speed){contactAge=0;if(i==11||i==14)feetContactAge=0;if(i<3)bodyContactAge=0;landSpeed=Mathf.Max(landSpeed,speed);}
        public int ClosestBone(Vector3 from)
        {int index=0;float best=float.MaxValue;for(int i=0;i<15;i++){float distance=(Bodies[i].GetComponent<Collider>().ClosestPoint(from)-from).sqrMagnitude;if(distance<best){best=distance;index=i;}}return index;}
        public Vector3 ClosestContact(Vector3 from)=>Bodies[ClosestBone(from)].GetComponent<Collider>().ClosestPoint(from);
        public void Reset(Vector3 position,float yaw)
        {
            donor.DrawAt(new PublicPlayer{position=position,yaw=yaw,stability=100,connected=true},null,.02f,0);donor.root.transform.localScale=Vector3.one;
            for(int i=0;i<15;i++){Bodies[i].position=target[i].position;Bodies[i].rotation=target[i].rotation;Bodies[i].velocity=Vector3.zero;Bodies[i].angularVelocity=Vector3.zero;}

            lastPublished=position;lastYaw=yaw;
        }
        public void Impact(Vector3 velocity,Vector3 point,bool topple)
        {
            if(topple){bodyContactAge=99;contactAge=99;}
            foreach(var b in Bodies)b.AddForce(velocity,ForceMode.VelocityChange);
            var closest=Bodies.OrderBy(b=>(b.position-point).sqrMagnitude).First();
            var horizontal=new Vector3(velocity.x,0,velocity.z);if(topple&&horizontal.sqrMagnitude<.01f)horizontal=Vector3.forward;var torque=Vector3.Cross(Vector3.up,horizontal.normalized)*(topple?5.2f:1.4f);
            closest.AddTorque(torque,ForceMode.VelocityChange);Bodies[0].AddTorque(torque*.7f,ForceMode.VelocityChange);Bodies[1].AddTorque(torque,ForceMode.VelocityChange);
        }
        public void Reach(Vector3 contact)
        {foreach(int i in new[]{5,8})Bodies[i].AddForce(Vector3.ClampMagnitude((contact-Bodies[i].position)*110-Bodies[i].velocity*8,65));}
        public void Prepare(Player p,Match match,float dt,float motorDt)
        {
            contactAge+=dt;bodyContactAge+=dt;feetContactAge+=dt;landSpeed=0;
            if((p.position-lastPublished).sqrMagnitude>64)Reset(p.position,p.yaw);
            bool down=p.bodyMode==BodyMode.Falling||p.bodyMode==BodyMode.Grounded;
            bool getting=p.bodyMode==BodyMode.GettingUp;
            if(getting&&previousMode!=BodyMode.GettingUp)
            {
                for(int i=0;i<15;i++){getUpPositions[i]=Bodies[i].position;getUpRotations[i]=Bodies[i].rotation;}
                standingPoint=p.getUpPosition;
            }
            var view=new PublicPlayer{position=getting?standingPoint:p.position,yaw=p.yaw,stability=100,connected=true,velocity=down||getting?Vector3.zero:p.velocity,locomotionSpeed=down||getting?0:p.locomotionSpeed,running=p.running,airborne=p.airborne&&!down,
                simulationTime=match.Time,actionKind=down||getting?"":p.actionKind,actionStartedAt=p.actionStartedAt,actionId=p.actionId,actionVariant=p.actionVariant,interaction=down||getting?"":p.interaction,progress=p.interaction=="eat"?Mathf.Clamp01((match.Time-p.interactionStart)/match.Rules.eatDuration):0,
                charge=p.chargingSince<0?0:Mathf.Clamp01((match.Time-p.chargingSince)/1.2f),grabbing=p.grabbing,grabAnchor=match.BodyGrabPoint(p)};
            donor.DrawAt(view,null,dt,match.Time);donor.root.transform.localScale=Vector3.one;

            if(getting)ApplyGetUpTarget(p,match.Time-p.bodyStateAt);
            previousMode=p.bodyMode;
            float getUpBlend=getting?Mathf.SmoothStep(0,1,Mathf.Clamp01((match.Time-p.bodyStateAt)/.10f)):1;
            for(int i=0;i<15;i++)
            {
                var b=Bodies[i];b.AddForce(Vector3.down*match.Rules.gravity,ForceMode.Acceleration);
                b.velocity=Vector3.ClampMagnitude(b.velocity,16);b.angularVelocity=Vector3.ClampMagnitude(b.angularVelocity,16);
                if(i>0)
                {
                    var j=joints[i];var relative=Quaternion.Inverse(target[parent[i]].rotation)*target[i].rotation;
                    if(getting)
                    {
                        var a=Quaternion.Slerp(getUpRotations[parent[i]],target[parent[i]].rotation,getUpBlend);
                        var c=Quaternion.Slerp(getUpRotations[i],target[i].rotation,getUpBlend);relative=Quaternion.Inverse(a)*c;
                    }
                    j.targetRotation=Quaternion.Inverse(relative)*bindRelative[i];
                    // Feeding extension changes the target anchor smoothly, never a dynamic Transform.
                    var anchor=target[parent[i]].InverseTransformPoint(target[i].position+target[i].rotation*j.anchor);
                    j.connectedAnchor=Vector3.Lerp(j.connectedAnchor,anchor,Mathf.Clamp01(dt*12));
                    bool precise=p.interaction=="eat"||p.actionKind=="light"&&match.Time-p.actionStartedAt<.45f||p.actionKind=="heavy"&&match.Time-p.actionStartedAt<.65f;
                    float muscle=down?0:p.bodyMode==BodyMode.Stagger?.30f:getting?.8f:precise?1.6f:1;
                    float strength=i==1?400:i==2?100:i==9||i==12?220:i==10||i==13?120:i==3||i==6?120:80;
                    j.angularXDrive=j.angularYZDrive=new JointDrive{positionSpring=strength*muscle,positionDamper=down?1.4f:Mathf.Sqrt(strength)*(i<=2?1.1f:.45f),maximumForce=down?3:strength*1.2f};
                }
            }
            // A wall can catch the body upright on both soles. A bounded collapse impulse tips it into free space;
            // ground contacts and the joint solver still determine the resulting pose.
            if(p.bodyMode==BodyMode.Falling&&match.Time-p.bodyStateAt>.45f&&contactAge<.1f&&bodyContactAge>.15f&&Bodies[0].velocity.magnitude<1.2f&&Vector3.Dot(Bodies[1].rotation*Vector3.up,Vector3.up)>.6f)
            {
                var away=-new Vector3(p.impactImpulse.x,0,p.impactImpulse.z).normalized;if(away.sqrMagnitude<.1f)away=Vector3.forward;
                if(!CampusGeometry.ClearSegment(p.position,p.position+away))away=Vector3.Cross(Vector3.up,away);
                var axis=Vector3.Cross(Vector3.up,away);Bodies[1].AddTorque(axis*120,ForceMode.Acceleration);Bodies[0].AddTorque(axis*60,ForceMode.Acceleration);Bodies[1].AddForce(away*30);
            }
            if(!down&&!getting&&p.bodyMode!=BodyMode.Stagger)
            {
                // End-effector muscles lift the swing foot against floor contact instead of dragging both soles.
                var travel=new Vector3(p.velocity.x,0,p.velocity.z);
                foreach(int foot in new[]{11,14})
                    Bodies[foot].AddForce(Vector3.ClampMagnitude((target[foot].position-Bodies[foot].position)*140+(travel-Bodies[foot].velocity)*9,65));
                if(p.interaction=="eat")foreach(int hand in new[]{5,8})
                    Bodies[hand].AddForce(Vector3.ClampMagnitude((target[hand].position-Bodies[hand].position)*80-Bodies[hand].velocity*5,40));
            }
            if(!down)
            {
                Vector3 gravityTorque=Vector3.zero;foreach(var body in Bodies)gravityTorque+=Vector3.Cross(body.worldCenterOfMass-Bodies[0].worldCenterOfMass,Vector3.down*match.Rules.gravity*body.mass);
                Bodies[0].AddTorque(Vector3.ClampMagnitude(-gravityTorque,220)*(p.bodyMode==BodyMode.Stagger?.65f:1),ForceMode.Force);
                Vector3 goal=target[0].position;
                if(getting)goal=Vector3.Lerp(getUpPositions[0],goal,getUpBlend);
                var movement=getting?Vector3.zero:(p.position-lastPublished)/Mathf.Max(.001f,motorDt);
                movement=Vector3.ClampMagnitude(movement,10);
                var accel=(goal-Bodies[0].position)*180+(movement-Bodies[0].velocity)*22+Vector3.up*(match.Rules.gravity*5);
                Bodies[0].AddForce(Vector3.ClampMagnitude(accel,180),ForceMode.Acceleration);
                var rotation=getting?Quaternion.Slerp(getUpRotations[0],target[0].rotation,getUpBlend):target[0].rotation;
                var delta=rotation*Quaternion.Inverse(Bodies[0].rotation);delta.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;
                if(!float.IsNaN(axis.x)&&!float.IsInfinity(axis.x))Bodies[0].AddTorque(Vector3.ClampMagnitude((axis*(angle*Mathf.Deg2Rad*2500)-Bodies[0].angularVelocity*90)/(1+90*dt+2500*dt*dt),1200),ForceMode.Acceleration);
                if(!getting){var chestDelta=target[1].rotation*Quaternion.Inverse(Bodies[1].rotation);chestDelta.ToAngleAxis(out float chestAngle,out var chestAxis);if(chestAngle>180)chestAngle-=360;
                    if(!float.IsNaN(chestAxis.x))Bodies[1].AddTorque(Vector3.ClampMagnitude((chestAxis*(chestAngle*Mathf.Deg2Rad*2500)-Bodies[1].angularVelocity*90)/(1+90*dt+2500*dt*dt),1200)*(p.bodyMode==BodyMode.Stagger?.3f:1),ForceMode.Acceleration);}
            }
        }
        void ApplyGetUpTarget(Player p,float age)
        {
            float t=Mathf.Clamp01(age/.6f),settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,t));
            float pitch=p.getUpFaceUp?Mathf.Lerp(-65,12,Mathf.Clamp01(t/.7f)):Mathf.Lerp(65,12,Mathf.Clamp01(t/.7f));
            pitch=Mathf.Lerp(pitch,0,settle);
            donor.pelvis.rotation=Quaternion.Euler(0,p.yaw,0)*Quaternion.Euler(pitch,0,0);
            var hip=donor.pelvis.position;hip.y=p.getUpPosition.y-1+Mathf.Lerp(.28f,.65f,Mathf.SmoothStep(0,1,t));donor.pelvis.position=hip;
            donor.leftLeg.localRotation=donor.rightLeg.localRotation=Quaternion.Euler(Mathf.Lerp(-65,0,settle),0,0);
            donor.leftShin.localRotation=donor.rightShin.localRotation=Quaternion.Euler(Mathf.Lerp(110,0,settle),0,0);
            donor.leftArm.localRotation=Quaternion.Euler(Mathf.Lerp(p.getUpFaceUp?25:-65,0,settle),0,22*(1-settle));
            donor.rightArm.localRotation=Quaternion.Euler(Mathf.Lerp(p.getUpFaceUp?25:-65,0,settle),0,-22*(1-settle));
            donor.leftForearm.localRotation=donor.rightForearm.localRotation=Quaternion.Euler(Mathf.Lerp(-65,0,settle),0,0);
            donor.head.localRotation=Quaternion.Euler(-12*(1-settle),0,0);
        }
        public void ReadBack(Player p,Match match,float dt)
        {
            p.position=Bodies[0].position+Vector3.up*.37f;
            p.velocity=Bodies[0].velocity;
            if(p.bodyMode==BodyMode.Falling||p.bodyMode==BodyMode.Grounded)p.verticalSpeed=p.velocity.y;
            bool down=p.bodyMode==BodyMode.Falling||p.bodyMode==BodyMode.Grounded;
            if(down&&p.fallOriginHeight-CampusGeometry.Floor(p.position+Vector3.up*.7f)>2)p.highFall=true;
            if(down&&bodyContactAge<.05f&&p.bodyMode==BodyMode.Falling)match.BodyLanded(p,landSpeed);
            // A supported stride may lift both feet; that is not a jump. Use the motor floor trajectory.
            p.airborne=down?bodyContactAge>.08f:p.motorHeight>CampusGeometry.Floor(new Vector3(p.position.x,p.motorHeight,p.position.z))+1.15f;
            if(p.bodyMode==BodyMode.Grounded&&match.Time-p.bodyStateAt>=1)match.TryBodyGetUp(p,Bodies[1].rotation*Vector3.forward);
            if(p.bodyMode==BodyMode.GettingUp&&match.Time-p.bodyStateAt>=.6f&&Vector3.Dot(Bodies[1].rotation*Vector3.up,Vector3.up)>.82f&&feetContactAge<.1f&&Vector2.Distance(new Vector2(p.position.x,p.position.z),new Vector2(p.getUpPosition.x,p.getUpPosition.z))<.35f&&Mathf.Abs(p.position.y-p.getUpPosition.y)<.55f)match.CompleteBodyGetUp(p);
            lastPublished=p.position;lastYaw=p.yaw;
            PublishPose(p,match.serverTick);
        }
        public void PublishPose(Player p,int tick)
        {
            pose.tick=tick;
            for(int i=0;i<15;i++){pose.positions[i]=Bodies[i].position-p.position;pose.rotations[i]=Bodies[i].rotation;}
            p.bodyPose=pose;
        }
        public void Dispose()
        {if(root){if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);}}
    }
}
