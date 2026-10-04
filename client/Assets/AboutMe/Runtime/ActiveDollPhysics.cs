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
        readonly Dictionary<string,GameObject> machines=new Dictionary<string,GameObject>();
        public int MachineColliderCount=>machines.Count;
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
            var geometry=match.MachineGeometry().ToArray();
            foreach(var key in machines.Keys.Where(id=>!geometry.Any(b=>b.id==id)).ToArray()){Destroy(machines[key]);machines.Remove(key);}
            foreach(var shape in geometry){if(!machines.TryGetValue(shape.id,out var go)){go=Box("Public machine "+shape.id,shape.center,shape.size);machines[shape.id]=go;}go.transform.SetPositionAndRotation(shape.center,shape.rotation);go.GetComponent<BoxCollider>().size=shape.size;}
            Physics.SyncTransforms();
            // Keep authority at 50 Hz; fast collisions / a body pile receive two solver substeps within that tick.
            int substeps=rigs.Values.Any(r=>r.Bodies.Any(b=>b.velocity.sqrMagnitude>100))||match.Players.Values.Count(p=>match.BodyDown(p))>=3?2:1;
            for(int substep=0;substep<substeps;substep++)
            {
            foreach(var p in match.Players.Values)if(!p.eliminated)Get(p).Prepare(p,match,dt/substeps,dt);
            foreach(var p in match.Players.Values)if(p.grabbing!=""&&match.Players.TryGetValue(p.grabbing,out var other)&&!other.eliminated)
            {
                var a=Get(p);var b=Get(other);var grabbedBody=b.Bodies[Mathf.Clamp(p.bodyGrabBone,0,14)];var contact=grabbedBody.position+grabbedBody.rotation*p.bodyGrabAnchor;
                // Drive the grip from the smoothed torso anchor; reaching arms remain visual/physical contacts,
                // but their stretch must not absorb the entire dragging force.
                var hand=a.Bodies[0].position+Quaternion.Euler(0,p.grabAnchorYaw,0)*p.grabberLocalAnchor;
                var rules=match.Rules;
                var away=other.position-p.position;away.y=0;var movement=new Vector3(other.input.x,0,other.input.z);
                float resistance=movement.sqrMagnitude>.01f&&Vector3.Dot(movement.normalized,away.normalized)>.5f?.8f:1;
                var raw=((contact-hand)*rules.bodyGrabSpring+(grabbedBody.velocity-a.Bodies[0].velocity)*rules.bodyGrabDamping)*resistance;
                float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((match.Time-p.grabStartedAt)/rules.grabBlendDuration));
                var force=Vector3.ClampMagnitude(raw,rules.bodyGrabMaxForce)*blend;
                a.Bodies[0].AddForce(force);var targetForce=force;if(match.GuardAnchored(other))targetForce=new Vector3(0,force.y,0);grabbedBody.AddForceAtPosition(-targetForce*.35f,contact);b.Bodies[0].AddForce(-targetForce*.65f);
                p.grabForceDemand=raw.magnitude;
                a.Reach(contact);p.grabTension=other.grabTension=Mathf.Clamp01(raw.magnitude/rules.bodyGrabMaxForce)*blend;
            }
            physics.Simulate(dt/substeps);
            }
            foreach(var p in match.Players.Values)if(!p.eliminated)Get(p).ReadBack(p,match,dt);
            clock.Stop();LastStepMs=clock.Elapsed.TotalMilliseconds;
        }
        public void RemoveMachine(string id)
        {foreach(var key in machines.Keys.Where(key=>key.StartsWith(id+"/",StringComparison.Ordinal)).ToArray()){machines[key].SetActive(false);Destroy(machines[key]);machines.Remove(key);}}
        public void PublishInitial(Match match){foreach(var p in match.Players.Values)if(!p.eliminated)Get(p).PublishPose(p,match.serverTick);}
        public void Impact(Player p,Vector3 velocity,Vector3 point,bool topple)
        {Get(p).Impact(velocity,point,topple);}
        public Vector3 Contact(Player p,Vector3 from)=>Get(p).ClosestContact(from);
        public void Translate(Player p,Vector3 delta)=>Get(p).Translate(delta);
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
            bool supportSurface=c.collider.attachedRigidbody==null;
            for(int i=0;i<c.contactCount;i++)
            {
                var contact=c.GetContact(i);
                if(contact.normal.y>.45f)owner.Contact(index,c.relativeVelocity.magnitude,contact.point,contact.normal,supportSurface);
            }
        }
    }
    public sealed class ActiveDollRig : IDisposable
    {
        const float SoleBelowAnkle=.1375f;
        sealed class FootSupport
        {
            public bool planted;
            public Vector3 anchor,contactPoint,contactNormal=Vector3.up,desiredAnkle;
            public float contactAge=99,plantAge,swingAge;
        }
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
        readonly FootSupport[] feet={new FootSupport(),new FootSupport()};
        readonly BoxCollider[] footColliders=new BoxCollider[2];
        readonly PhysicMaterial bodyMaterial,stanceMaterial,swingMaterial;
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
            bodyMaterial=new PhysicMaterial("Doll friction"){dynamicFriction=.35f,staticFriction=.45f,bounciness=.05f,frictionCombine=PhysicMaterialCombine.Minimum,bounceCombine=PhysicMaterialCombine.Minimum};
            stanceMaterial=new PhysicMaterial("Doll planted sole"){dynamicFriction=.75f,staticFriction=.95f,bounciness=0,frictionCombine=PhysicMaterialCombine.Maximum,bounceCombine=PhysicMaterialCombine.Minimum};
            swingMaterial=new PhysicMaterial("Doll swing sole"){dynamicFriction=.04f,staticFriction=.06f,bounciness=0,frictionCombine=PhysicMaterialCombine.Minimum,bounceCombine=PhysicMaterialCombine.Minimum};
            for(int i=0;i<15;i++)
            {
                var go=new GameObject(target[i].name+" body");go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(target[i].position,target[i].rotation);
                var collider=go.AddComponent<BoxCollider>();collider.sharedMaterial=bodyMaterial;
                if(i==0){collider.size=new Vector3(.62f,.25f,.40f);collider.center=Vector3.up*.08f;
                    var hem=go.AddComponent<BoxCollider>();hem.center=new Vector3(0,-.14f,0);hem.size=new Vector3(1.02f,.38f,.70f);hem.sharedMaterial=bodyMaterial;}
                else if(i==1){collider.size=new Vector3(.86f,.86f,.62f);collider.center=Vector3.up*.01f;
                    // Canonical envelope includes every supported bag; cosmetics never alter reach or physics size.
                    var bag=go.AddComponent<BoxCollider>();bag.center=new Vector3(0,-.03f,-.43f);bag.size=new Vector3(.60f,.70f,.32f);bag.sharedMaterial=bodyMaterial;
                    var sling=go.AddComponent<BoxCollider>();sling.center=new Vector3(-.31f,-.28f,.32f);sling.size=new Vector3(.38f,.38f,.24f);sling.sharedMaterial=bodyMaterial;}
                else if(i==2){UnityEngine.Object.DestroyImmediate(collider);var sphere=go.AddComponent<SphereCollider>();sphere.radius=.73f;sphere.sharedMaterial=bodyMaterial;}
                else if(i==3||i==6){collider.size=new Vector3(.26f,.28f,.28f);collider.center=Vector3.down*.14f;}
                else if(i==4||i==7){collider.size=new Vector3(.23f,.24f,.25f);collider.center=Vector3.down*.115f;}
                else if(i==5||i==8){collider.size=new Vector3(.25f,.20f,.25f);collider.center=new Vector3(0,-.055f,.03f);}
                else if(i==9||i==12){collider.size=new Vector3(.30f,.25f,.34f);collider.center=Vector3.down*.12f;}
                else if(i==10||i==13){collider.size=new Vector3(.29f,.25f,.34f);collider.center=new Vector3(0,-.12f,.04f);}
                else{collider.size=new Vector3(.40f,.48f,.60f);collider.center=new Vector3(0,.1025f,0);collider.sharedMaterial=swingMaterial;footColliders[i==11?0:1]=collider;}
                foreach(var shape in go.GetComponents<Collider>())shape.contactOffset=i==11||i==14?.015f:.035f;
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
        public void Contact(int i,float speed,Vector3 point,Vector3 normal,bool supportSurface)
        {
            contactAge=0;
            if(supportSurface&&normal.y>.8f&&(i==11||i==14))
            {
                feetContactAge=0;
                var foot=feet[i==11?0:1];foot.contactAge=0;foot.contactPoint=point;foot.contactNormal=normal;
            }
            if(i<3)bodyContactAge=0;
            landSpeed=Mathf.Max(landSpeed,speed);
        }
        public float FootContactAge(int side)=>feet[side].contactAge;
        public bool FootPlanted(int side)=>feet[side].planted;
        public float FootPlantSlip(int side)
        {
            var foot=feet[side];if(!foot.planted)return 0;
            var sole=SoleCenter(Bodies[side==0?11:14]);var delta=sole-foot.anchor;delta.y=0;return delta.magnitude;
        }
        static Vector3 SoleCenter(Rigidbody foot)=>foot.position+foot.rotation*Vector3.down*SoleBelowAnkle;
        public float FootSoleGap(int side)
        {
            var foot=Bodies[side==0?11:14].transform;float lowest=float.MaxValue;
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var corner=foot.TransformPoint(new Vector3(x*.19f,-SoleBelowAnkle,z*.29f));
                float ground=CampusGeometry.Floor(corner+Vector3.up);
                if(ground>-50)lowest=Mathf.Min(lowest,corner.y-ground);
            }
            return lowest;
        }
        public int ClosestBone(Vector3 from)
        {int index=0;float best=float.MaxValue;for(int i=0;i<15;i++){float distance=(Bodies[i].GetComponent<Collider>().ClosestPoint(from)-from).sqrMagnitude;if(distance<best){best=distance;index=i;}}return index;}
        public Vector3 ClosestContact(Vector3 from)=>Bodies[ClosestBone(from)].GetComponent<Collider>().ClosestPoint(from);
        public void Translate(Vector3 delta)
        {foreach(var b in Bodies)b.position+=delta;lastPublished+=delta;if(previousMode==BodyMode.GettingUp){standingPoint+=delta;for(int i=0;i<getUpPositions.Length;i++)getUpPositions[i]+=delta;}foreach(var foot in feet){foot.anchor+=delta;foot.contactPoint+=delta;foot.desiredAnkle+=delta;}}
        public void Reset(Vector3 position,float yaw)
        {
            donor.DrawAt(new PublicPlayer{position=position,yaw=yaw,stability=100,connected=true},null,.02f,0);donor.root.transform.localScale=Vector3.one;
            for(int i=0;i<15;i++){Bodies[i].position=target[i].position;Bodies[i].rotation=target[i].rotation;Bodies[i].velocity=Vector3.zero;Bodies[i].angularVelocity=Vector3.zero;}
            foreach(var foot in feet){foot.planted=false;foot.contactAge=99;foot.plantAge=0;foot.swingAge=0;}
            footColliders[0].sharedMaterial=bodyMaterial;footColliders[1].sharedMaterial=bodyMaterial;

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
            contactAge+=dt;bodyContactAge+=dt;feetContactAge+=dt;feet[0].contactAge+=dt;feet[1].contactAge+=dt;landSpeed=0;
            if((p.position-lastPublished).sqrMagnitude>64)Reset(p.position,p.yaw);
            bool down=p.bodyMode==BodyMode.Falling||p.bodyMode==BodyMode.Grounded;
            bool getting=p.bodyMode==BodyMode.GettingUp;
            bool beingDragged=match.Players.Values.Any(other=>other.grabbing==p.profile.playerId)&&!match.GuardAnchored(p);
            if(getting&&previousMode!=BodyMode.GettingUp)
            {
                for(int i=0;i<15;i++){getUpPositions[i]=Bodies[i].position;getUpRotations[i]=Bodies[i].rotation;}
                standingPoint=p.getUpPosition;
            }
            var view=new PublicPlayer{position=getting?standingPoint:p.position,yaw=p.grabbing!=""?p.grabAnchorYaw:p.yaw,stability=100,connected=true,velocity=down||getting?Vector3.zero:p.velocity,locomotionSpeed=down||getting?0:p.locomotionSpeed,running=p.running,airborne=p.airborne&&!down,
                simulationTime=match.Time,actionKind=down||getting?"":p.actionKind,actionStartedAt=p.actionStartedAt,actionId=p.actionId,actionVariant=p.actionVariant,interaction=down||getting?"":p.interaction,progress=p.interaction=="eat"?Mathf.Clamp01((match.Time-p.interactionStart)/match.Rules.eatDuration):0,
                charge=p.chargingSince<0?0:Mathf.Clamp01((match.Time-p.chargingSince)/1.2f),grabbing=p.grabbing,grabAnchor=match.BodyGrabPoint(p)};
            donor.DrawAt(view,null,dt,match.Time);donor.root.transform.localScale=Vector3.one;

            if(getting)ApplyGetUpTarget(p,match.Time-p.bodyStateAt);
            PrepareFeet(p,match,dt,match.Time,beingDragged);
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
                if(!p.airborne)
                    // Stance has a world-space goal; only the swing foot follows the moving body.
                    for(int side=0;side<2;side++)
                    {
                        var foot=feet[side];var body=Bodies[side==0?11:14];
                        var force=(foot.desiredAnkle-body.position)*(foot.planted?190:210)-body.velocity*(foot.planted?18:13);
                        if(beingDragged){force.x*=.12f;force.z*=.12f;}
                        body.AddForce(Vector3.ClampMagnitude(force,foot.planted?90:100));
                        var facing=Quaternion.LookRotation(Vector3.ProjectOnPlane(donor.root.transform.forward,Vector3.up).normalized,Vector3.up);
                        var turn=facing*Quaternion.Inverse(body.rotation);turn.ToAngleAxis(out float degrees,out var axis);if(degrees>180)degrees-=360;
                        if(!float.IsNaN(axis.x))body.AddTorque(Vector3.ClampMagnitude(axis*(degrees*Mathf.Deg2Rad*420)-body.angularVelocity*32,330),ForceMode.Acceleration);
                    }
                if(p.interaction=="eat")foreach(int hand in new[]{5,8})
                    Bodies[hand].AddForce(Vector3.ClampMagnitude((target[hand].position-Bodies[hand].position)*80-Bodies[hand].velocity*5,40));
            }
            if(getting&&match.Time-p.bodyStateAt>.45f)
                for(int side=0;side<2;side++)
                {
                    var body=Bodies[side==0?11:14];
                    body.AddForce(Vector3.ClampMagnitude((feet[side].desiredAnkle-body.position)*170-body.velocity*14,75));
                }
            if(!down)
            {
                Vector3 gravityTorque=Vector3.zero;foreach(var body in Bodies)gravityTorque+=Vector3.Cross(body.worldCenterOfMass-Bodies[0].worldCenterOfMass,Vector3.down*match.Rules.gravity*body.mass);
                Bodies[0].AddTorque(Vector3.ClampMagnitude(-gravityTorque,220)*(p.bodyMode==BodyMode.Stagger?.65f:1),ForceMode.Force);
                Vector3 goal=target[0].position;
                if(getting)goal=Vector3.Lerp(getUpPositions[0],goal-Vector3.up*.055f,getUpBlend);
                var movement=getting?Vector3.zero:(p.position-lastPublished)/Mathf.Max(.001f,motorDt);
                movement=Vector3.ClampMagnitude(movement,10);
                var accel=(goal-Bodies[0].position)*180+(movement-Bodies[0].velocity)*22+Vector3.up*(match.Rules.gravity*5);
                // Keep vertical support while letting an external hand pull the body along the floor.
                if(beingDragged){accel.x*=.12f;accel.z*=.12f;}
                if(p.bodyMode==BodyMode.Standing&&movement.sqrMagnitude>.01f)
                {
                    var planarError=movement-Bodies[0].velocity;planarError.y=0;
                    accel+=planarError*(beingDragged?2.4f:20);
                }
                Bodies[0].AddForce(Vector3.ClampMagnitude(accel,p.bodyMode==BodyMode.Standing&&movement.sqrMagnitude>.01f?240:180),ForceMode.Acceleration);
                var rotation=getting?Quaternion.Slerp(getUpRotations[0],target[0].rotation,getUpBlend):target[0].rotation;
                var delta=rotation*Quaternion.Inverse(Bodies[0].rotation);delta.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;
                if(!float.IsNaN(axis.x)&&!float.IsInfinity(axis.x))Bodies[0].AddTorque(Vector3.ClampMagnitude((axis*(angle*Mathf.Deg2Rad*2500)-Bodies[0].angularVelocity*90)/(1+90*dt+2500*dt*dt),1200),ForceMode.Acceleration);
                if(!getting){var chestDelta=target[1].rotation*Quaternion.Inverse(Bodies[1].rotation);chestDelta.ToAngleAxis(out float chestAngle,out var chestAxis);if(chestAngle>180)chestAngle-=360;
                    if(!float.IsNaN(chestAxis.x))Bodies[1].AddTorque(Vector3.ClampMagnitude((chestAxis*(chestAngle*Mathf.Deg2Rad*2500)-Bodies[1].angularVelocity*90)/(1+90*dt+2500*dt*dt),1200)*(p.bodyMode==BodyMode.Stagger?.3f:1),ForceMode.Acceleration);}
            }
        }
        void PrepareFeet(Player p,Match match,float dt,float now,bool beingDragged)
        {
            bool locomoting=p.bodyMode==BodyMode.Standing&&!p.airborne&&!beingDragged;
            bool settling=p.bodyMode==BodyMode.GettingUp&&now-p.bodyStateAt>.45f;
            float speed=p.locomotionSpeed;
            for(int side=0;side<2;side++)
            {
                var state=feet[side];int hipIndex=side==0?9:12,footIndex=side==0?11:14;
                var body=Bodies[footIndex];var animatedFoot=target[footIndex];
                state.desiredAnkle=animatedFoot.position;
                if(!locomoting)
                {
                    state.planted=false;state.plantAge=0;state.swingAge=0;footColliders[side].sharedMaterial=beingDragged?swingMaterial:bodyMaterial;
                    if(settling)
                    {
                        var settlingAnkle=animatedFoot.position;
                        float floor=match.SurfaceFloor(new Vector3(settlingAnkle.x,p.getUpPosition.y,settlingAnkle.z));
                        if(floor>-50)
                        {
                            settlingAnkle.y=floor+SoleBelowAnkle;
                            var settlingForward=Vector3.ProjectOnPlane(donor.root.transform.forward,Vector3.up).normalized;
                            SolveLeg(hipIndex,footIndex,settlingAnkle,Quaternion.LookRotation(settlingForward,Vector3.up),Mathf.Clamp01((now-p.bodyStateAt-.45f)/.15f));
                            state.desiredAnkle=settlingAnkle;
                        }
                    }
                    continue;
                }

                float intent=side==0?donor.LeftFootContact:donor.RightFootContact;
                var hip=target[hipIndex].position;
                var center=SoleCenter(body);
                float gap=FootSoleGap(side);
                bool touching=state.contactAge<.06f&&gap<.085f;
                if(state.planted)
                {
                    state.plantAge+=dt;
                    var reach=hip-state.anchor;reach.y=0;
                    bool step=speed>.15f&&(reach.magnitude>.24f||state.plantAge>.07f&&intent<.25f);
                    if(step||state.contactAge>.10f||Mathf.Abs(hip.y-state.anchor.y)>.72f)
                    {state.planted=false;state.swingAge=0;}
                }
                if(!state.planted)
                {
                    state.swingAge+=dt;
                    if(touching&&(speed<.15f||state.swingAge>.06f&&(intent>.58f||state.swingAge>.18f)))
                    {
                        state.planted=true;state.plantAge=0;
                        state.anchor=new Vector3(center.x,state.contactPoint.y,center.z);
                    }
                }

                Vector3 desired;
                if(state.planted)
                {
                    desired=state.anchor+state.contactNormal*SoleBelowAnkle;
                }
                else
                {
                    desired=animatedFoot.position;
                    float ground=match.SurfaceFloor(new Vector3(desired.x,p.motorHeight,desired.z));
                    if(ground>-50)
                    {
                        float lift=speed<.15f?0:Mathf.Lerp(.01f,.105f,1-intent)*Mathf.Clamp01((.26f-state.swingAge)/.10f);
                        desired.y=ground+SoleBelowAnkle+lift;
                    }
                    var displacement=desired-hip;displacement.y=0;
                    float legLength=Vector3.Distance(target[hipIndex].position,target[hipIndex+1].position)+Vector3.Distance(target[hipIndex+1].position,animatedFoot.position);
                    float vertical=Mathf.Abs(desired.y-hip.y);
                    float horizontalReach=Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0,(legLength-.012f)*(legLength-.012f)-vertical*vertical)),.08f,.27f);
                    if(displacement.magnitude>horizontalReach)desired=hip+displacement.normalized*horizontalReach+Vector3.up*(desired.y-hip.y);
                }
                state.desiredAnkle=desired;
                footColliders[side].sharedMaterial=state.planted?stanceMaterial:swingMaterial;
                var normal=state.planted?state.contactNormal:Vector3.up;
                var forward=Vector3.ProjectOnPlane(donor.root.transform.forward,normal).normalized;
                var flatRotation=Quaternion.LookRotation(forward,normal);
                SolveLeg(hipIndex,footIndex,desired,flatRotation,state.planted?1:.85f);
            }
        }
        void SolveLeg(int hipIndex,int footIndex,Vector3 ankleGoal,Quaternion footRotation,float weight)
        {
            var hip=target[hipIndex];var knee=target[hipIndex+1];var ankle=target[footIndex];
            var first=knee.position-hip.position;var second=ankle.position-knee.position;
            float upperLength=first.magnitude,lowerLength=second.magnitude;
            if(upperLength<.01f||lowerLength<.01f)return;
            ankleGoal=Vector3.Lerp(ankle.position,ankleGoal,weight);
            var delta=ankleGoal-hip.position;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upperLength-lowerLength)+.01f,upperLength+lowerLength-.005f);
            var direction=delta.sqrMagnitude>.0001f?delta.normalized:Vector3.down;
            var pole=Vector3.ProjectOnPlane(first,direction);
            if(pole.sqrMagnitude<.0001f)pole=Vector3.ProjectOnPlane(donor.root.transform.forward,direction);
            if(pole.sqrMagnitude<.0001f)pole=Vector3.ProjectOnPlane(donor.root.transform.right,direction);
            pole.Normalize();
            float along=(upperLength*upperLength+distance*distance-lowerLength*lowerLength)/(2*distance);
            var kneeGoal=hip.position+direction*along+pole*Mathf.Sqrt(Mathf.Max(0,upperLength*upperLength-along*along));
            var upperTurn=Quaternion.FromToRotation(knee.position-hip.position,kneeGoal-hip.position)*hip.rotation;
            hip.rotation=Quaternion.RotateTowards(hip.rotation,upperTurn,65);
            var lowerTurn=Quaternion.FromToRotation(ankle.position-knee.position,ankleGoal-knee.position)*knee.rotation;
            knee.rotation=Quaternion.RotateTowards(knee.rotation,lowerTurn,85);
            ankle.rotation=Quaternion.RotateTowards(ankle.rotation,footRotation,45);
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
            if(down&&p.fallOriginHeight-match.SurfaceFloor(p.position+Vector3.up*.7f)>2)p.highFall=true;
            if(down&&bodyContactAge<.05f&&p.bodyMode==BodyMode.Falling)match.BodyLanded(p,landSpeed);
            // A supported stride may lift both feet; that is not a jump. Use the motor floor trajectory.
            p.airborne=down?bodyContactAge>.08f:p.motorHeight>match.SurfaceFloor(new Vector3(p.position.x,p.motorHeight,p.position.z))+1.15f;
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
        {
            if(root){if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);}
            if(Application.isPlaying){UnityEngine.Object.Destroy(bodyMaterial);UnityEngine.Object.Destroy(stanceMaterial);UnityEngine.Object.Destroy(swingMaterial);}
            else{UnityEngine.Object.DestroyImmediate(bodyMaterial);UnityEngine.Object.DestroyImmediate(stanceMaterial);UnityEngine.Object.DestroyImmediate(swingMaterial);}
        }
    }
}
