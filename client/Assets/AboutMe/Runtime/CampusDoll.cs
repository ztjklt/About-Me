using System;
using System.Collections.Generic;
using UnityEngine;

namespace AboutMe
{
    public static class DollFactory
    {
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static Mesh sphere,cube,roundedBox,capMesh,skirt;
        public static Material Material(string color)
        {
            if(materials.TryGetValue(color,out var material)&&material)return material;
            material=new Material(Shader.Find("AboutMe/CampusFlat")){color=ToyFactory.ColorOf(color),name="Doll "+color};materials[color]=material;return material;
        }
        static Mesh Sphere()
        {
            if(sphere)return sphere;
            const int rings=8,segments=12;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int r=0;r<=rings;r++)for(int s=0;s<=segments;s++)
            {float latitude=r*Mathf.PI/rings,angle=s*2*Mathf.PI/segments;vertices.Add(new Vector3(Mathf.Sin(latitude)*Mathf.Cos(angle),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(angle))*.5f);}
            for(int r=0;r<rings;r++)for(int s=0;s<segments;s++)
            {int i=r*(segments+1)+s;triangles.AddRange(new[]{i,i+1,i+segments+1,i+1,i+segments+2,i+segments+1});}
            sphere=new Mesh{name="Doll low poly round form"};sphere.SetVertices(vertices);sphere.SetTriangles(triangles,0);sphere.RecalculateNormals();sphere.RecalculateBounds();return sphere;
        }
        static Mesh Box()
        {
            if(cube)return cube;var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=temp.GetComponent<MeshFilter>().sharedMesh;
            if(Application.isPlaying)UnityEngine.Object.Destroy(temp);else UnityEngine.Object.DestroyImmediate(temp);return cube;
        }
        static Mesh RoundedBox()
        {
            if(roundedBox)return roundedBox;
            var vertices=new List<Vector3>();var triangles=new List<int>();var shared=new Dictionary<Vector3,int>();
            foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                var u=Mathf.Abs(normal.x)>.5f?Vector3.forward:Vector3.right;var v=Vector3.Cross(normal,u);var grid=new int[5,5];
                for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
                {
                    var p=normal*.5f+u*(x/4f-.5f)+v*(y/4f-.5f);var inner=new Vector3(Mathf.Clamp(p.x,-.37f,.37f),Mathf.Clamp(p.y,-.37f,.37f),Mathf.Clamp(p.z,-.37f,.37f));p=inner+(p-inner).normalized*.13f;
                    p=new Vector3(Mathf.Round(p.x*10000)/10000,Mathf.Round(p.y*10000)/10000,Mathf.Round(p.z*10000)/10000);
                    if(!shared.TryGetValue(p,out int index)){index=vertices.Count;shared[p]=index;vertices.Add(p);}grid[x,y]=index;
                }
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)triangles.AddRange(new[]{grid[x,y],grid[x+1,y],grid[x,y+1],grid[x+1,y],grid[x+1,y+1],grid[x,y+1]});
            }
            roundedBox=new Mesh{name="Doll rounded clothing form"};roundedBox.SetVertices(vertices);roundedBox.SetTriangles(triangles,0);roundedBox.RecalculateNormals();roundedBox.RecalculateBounds();return roundedBox;
        }
        static Mesh Cap()
        {
            if(capMesh)return capMesh;capMesh=UnityEngine.Object.Instantiate(Sphere());capMesh.name="Solid cap dome";
            var points=capMesh.vertices;for(int i=0;i<points.Length;i++)points[i].y=Mathf.Max(0,points[i].y);capMesh.vertices=points;capMesh.RecalculateNormals();capMesh.RecalculateBounds();return capMesh;
        }
        static Mesh Skirt()
        {
            if(skirt)return skirt;skirt=UnityEngine.Object.Instantiate(RoundedBox());skirt.name="Trapezoid skirt";
            var points=skirt.vertices;for(int i=0;i<points.Length;i++){float taper=Mathf.Lerp(1.12f,.82f,points[i].y+.5f);points[i].x*=taper;points[i].z*=taper;}skirt.vertices=points;skirt.RecalculateNormals();skirt.RecalculateBounds();return skirt;
        }
        public static GameObject Part(string name,Transform parent,Vector3 position,Vector3 size,string color,bool round=true)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<MeshFilter>().sharedMesh=name=="Shirt silhouette"||name=="Shoe"||name=="Trouser thigh"||name=="Lower leg"||name=="Sock"?RoundedBox():name=="Skirt"?Skirt():name=="Cap crown"?Cap():round?Sphere():Box();go.GetComponent<Renderer>().sharedMaterial=Material(color);return go;
        }
        public static GameObject Asset(string name,Transform parent,Vector3 pos,Vector3 size,string color)
        {
            var source=Resources.Load<GameObject>("CampusDoll/"+name);if(!source)return null;
            var model=UnityEngine.Object.Instantiate(source,parent,false);model.transform.localPosition=pos;model.transform.localScale=size;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=Material(color);
            return model;
        }
    }
    public class ToyActor
    {
        public GameObject root,held;
        public Transform head,mouth,leftArm,rightArm,leftLeg,rightLeg,torso,leftBrow,rightBrow;
        public Transform leftForearm,rightForearm,leftShin,rightShin;
        public Transform pelvis,spine,chest,neck,leftClavicle,rightClavicle,leftHand,rightHand,leftFoot,rightFoot,leftToe,rightToe;
        public Transform RightHandAttachment {get;private set;}
        public Transform LeftHandAttachment {get;private set;}
        public Transform PersonaAttachment=>RightHandAttachment;
        public Transform GetAttachment(string name)=>name=="leftHand"?LeftHandAttachment:name=="head"?head:name=="back"?chest:RightHandAttachment;
        public int DeformationBoneCount=>rest.Count;
        readonly DollPhysicsPresentation physicsPresentation=new DollPhysicsPresentation();
        public Transform[] PhysicalBones {get;private set;}
        bool hasPhysicalPose;
        public bool ImmediatePhysicsPose;
        public bool PhysicsTarget;
        public float FreezeUntil;
        readonly DollCombatMotion combatMotion=new DollCombatMotion();
        readonly DollJointDynamics jointDynamics=new DollJointDynamics(22);
        Vector3 previousTarget; bool previousTargetValid;
        float latestServerTime=-1,latestServerArrival;
        public float BodyRebound=>combatPose.bodyOffset.magnitude;
        public float LeftFootContact=>combatPose.leftFootContact;
        public float RightFootContact=>combatPose.rightFootContact;
        public string LastTripoClip {get;private set;}="";
        DollCombatMotion.Pose combatPose;
        bool holdingContact;
        Transform fallPivot;
        public float GrabContactError {get;private set;}
        public SkinnedMeshRenderer Skin {get;private set;}
        public TextMesh label;
        public string visual="";
        public AppearanceData Appearance {get;private set;}
        readonly List<Rigidbody> bodies=new List<Rigidbody>();
        readonly List<(Transform t,Vector3 p,Quaternion q)> rest=new List<(Transform,Vector3,Quaternion)>();
        readonly List<GameObject> cosmetics=new List<GameObject>();
        DSGirlOutfit dsOutfit;
        ClaudeGirlOutfit claudeOutfit;
        bool UsesModularOutfit=>Appearance!=null&&(Appearance.character=="ds"||Appearance.character=="claude");
        float FaceMouthDrop=>Appearance?.character=="claude"?ClaudeGirlFaceDetails.MouthDrop:Appearance?.character=="ds"?DSGirlFaceDetails.MouthDrop:0;
        float FaceBrowDrop=>Appearance?.character=="claude"?ClaudeGirlFaceDetails.BrowDrop:Appearance?.character=="ds"?DSGirlFaceDetails.BrowDrop:0;
        Transform toyLeftHandAttachment,toyRightHandAttachment;
        bool ragdoll,positionInitialized;string appearanceKey="";
        float walkPhase,moveBlend,runBlend,eatBlend,grabBlend,chargeBlend,struggleBlend,recovery,eatProgress,swallowTime;
        string lastSwallowed="",lastNickname;int lastRemaining=-1;bool lastConnected;
        public float MouthOpen=>eatBlend;
        public float RunBlend=>runBlend;
        DollExpression face;
        public float EatProgress=>eatProgress;
        readonly DollMotionBuffer motion=new DollMotionBuffer();
        const string Ink="#202A3D",Paper="#FFF9EF";
        public ToyActor(string id,string color,int outfit,Transform parent,AppearanceData appearance=null)
        {
            root=new GameObject(id);root.transform.SetParent(parent,false);
            DollTripoMotion.Preload("idle","walk","run","box_01","hurt");
            torso=Bone("Torso",new Vector3(0,.03f,0),new Vector3(.74f,.78f,.48f),null).transform;
            var body=torso.GetComponent<Rigidbody>();
            head=Bone("Head",new Vector3(0,.88f,0),new Vector3(.93f,.94f,.85f),body,true).transform;
            leftArm=Bone("LeftUpperArm",new Vector3(-.405f,.31f,0),new Vector3(.28f,.29f,.3f),body,false,new Vector3(0,-.12f,0)).transform;
            rightArm=Bone("RightUpperArm",new Vector3(.405f,.31f,0),new Vector3(.28f,.29f,.3f),body,false,new Vector3(0,-.12f,0)).transform;
            leftForearm=Bone("LeftForearm",new Vector3(0,-.285f,0),new Vector3(.24f,.31f,.26f),leftArm.GetComponent<Rigidbody>(),false,new Vector3(0,-.14f,0)).transform;
            rightForearm=Bone("RightForearm",new Vector3(0,-.285f,0),new Vector3(.24f,.31f,.26f),rightArm.GetComponent<Rigidbody>(),false,new Vector3(0,-.14f,0)).transform;
            leftLeg=Bone("LeftThigh",new Vector3(-.22f,-.4f,0),new Vector3(.30f,.29f,.35f),body,false,new Vector3(0,-.12f,0)).transform;
            rightLeg=Bone("RightThigh",new Vector3(.22f,-.4f,0),new Vector3(.30f,.29f,.35f),body,false,new Vector3(0,-.12f,0)).transform;
            leftShin=Bone("LeftShin",new Vector3(0,-.25f,0),new Vector3(.29f,.31f,.4f),leftLeg.GetComponent<Rigidbody>(),false,new Vector3(0,-.15f,.05f)).transform;
            rightShin=Bone("RightShin",new Vector3(0,-.25f,0),new Vector3(.29f,.31f,.4f),rightLeg.GetComponent<Rigidbody>(),false,new Vector3(0,-.15f,.05f)).transform;
            // One shared rig for every outfit. Adjacent joints overlap intentionally; head/forearm collisions remain enabled.
            for(int i=0;i<bodies.Count;i++)
            {
                for(int j=0;j<i;j++)
                {
                    var joint=bodies[i].GetComponent<CharacterJoint>();bool adjacent=joint&&joint.connectedBody==bodies[j];
                    bool sleeveAgainstTorso=j==0&&bodies[i].name.IndexOf("arm",StringComparison.OrdinalIgnoreCase)>=0;
                    if(adjacent||sleeveAgainstTorso)Physics.IgnoreCollision(bodies[i].GetComponent<Collider>(),bodies[j].GetComponent<Collider>());
                }
            }
            BuildDeformationRig();
            // Soft combat uses the motor root as authority. These collider proxies never become free ragdolls.
            foreach(var b in bodies){b.isKinematic=true;b.detectCollisions=false;}
            rest.Clear();CollectRest(pelvis);
            for(int i=0;i<rest.Count;i++)
            {
                var t=rest[i].t;
                var profile=t==head||t==neck?DollJointDynamics.Profile.Head:t==leftArm||t==rightArm||t==leftClavicle||t==rightClavicle?DollJointDynamics.Profile.Shoulder:t==leftForearm||t==rightForearm?DollJointDynamics.Profile.Elbow:t==leftShin||t==rightShin?DollJointDynamics.Profile.Knee:t==leftLeg||t==rightLeg||t==leftFoot||t==rightFoot||t==leftHand||t==rightHand?DollJointDynamics.Profile.Hip:DollJointDynamics.Profile.Torso;
                jointDynamics.SetProfile(i,profile);jointDynamics.Reset(i,rest[i].q);
            }
            ApplyAppearance(AppearanceData.Resolve(appearance,color,outfit));BuildAttachments();
            toyLeftHandAttachment=LeftHandAttachment;toyRightHandAttachment=RightHandAttachment;
            RefreshCharacterVisual();PhysicalBones=ActiveDollRig.Bones(this);
            var text=new GameObject("Name");text.transform.SetParent(root.transform,false);text.transform.localPosition=Vector3.up*1.77f;
            label=text.AddComponent<TextMesh>();label.anchor=TextAnchor.MiddleCenter;label.characterSize=.05f;label.fontSize=64;label.color=ToyFactory.ColorOf(Ink);
            var font=Resources.Load<Font>("AboutMeFont");if(font){label.font=font;text.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
        }
        Transform Joint(string name,Transform parent,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        void CollectRest(Transform bone)
        {rest.Add((bone,bone.localPosition,bone.localRotation));for(int i=0;i<bone.childCount;i++)CollectRest(bone.GetChild(i));}
        void BuildDeformationRig()
        {
            fallPivot=Joint("SoftFallPresentation",root.transform,Vector3.zero);
            pelvis=Joint("Hips",fallPivot,new Vector3(0,-.37f,0));
            spine=Joint("Spine",pelvis,new Vector3(0,.2f,0));torso.SetParent(spine,true);
            chest=Joint("Chest",torso,new Vector3(0,.22f,0));neck=Joint("Neck",chest,new Vector3(0,.2f,0));head.SetParent(neck,true);
            leftClavicle=Joint("LeftShoulder",chest,new Vector3(-.20f,.09f,0));rightClavicle=Joint("RightShoulder",chest,new Vector3(.20f,.09f,0));
            leftArm.SetParent(leftClavicle,true);rightArm.SetParent(rightClavicle,true);
            leftLeg.SetParent(pelvis,true);rightLeg.SetParent(pelvis,true);
            leftHand=Joint("LeftHand",leftForearm,new Vector3(0,-.23f,.018f));rightHand=Joint("RightHand",rightForearm,new Vector3(0,-.23f,.018f));
            leftFoot=Joint("LeftFoot",leftShin,new Vector3(0,-.25f,.10f));rightFoot=Joint("RightFoot",rightShin,new Vector3(0,-.25f,.10f));
            leftToe=Joint("LeftToes",leftFoot,new Vector3(0,-.03f,.22f));rightToe=Joint("RightToes",rightFoot,new Vector3(0,-.03f,.22f));
        }
        void BuildAttachments()
        {
            LeftHandAttachment=Joint("LeftHandAttachment",leftHand,new Vector3(0,-.025f,.12f));
            RightHandAttachment=Joint("RightHandAttachment",rightHand,new Vector3(0,-.025f,.12f));
            LeftHandAttachment.localRotation=RightHandAttachment.localRotation=Quaternion.Euler(-90,0,0);
        }
        void RefreshCharacterVisual()
        {
            if(!toyLeftHandAttachment||!toyRightHandAttachment)return;
            if(Appearance.character=="ds"&&!dsOutfit&&Resources.Load<GameObject>("CampusDoll/DSGirlModular"))dsOutfit=DSGirlOutfit.Attach(this);
            if(Appearance.character=="claude"&&!claudeOutfit&&Resources.Load<GameObject>("CampusDoll/ClaudeGirlModular"))claudeOutfit=ClaudeGirlOutfit.Attach(this);
            LeftHandAttachment=toyLeftHandAttachment;RightHandAttachment=toyRightHandAttachment;
            if(Skin)Skin.enabled=true;
            if(held)held.transform.SetParent(RightHandAttachment,false);
        }
        void ClearDSOutfit()
        {
            if(!dsOutfit)return;
            dsOutfit.Release();dsOutfit.gameObject.SetActive(false);
            if(Application.isPlaying)UnityEngine.Object.Destroy(dsOutfit.gameObject);else UnityEngine.Object.DestroyImmediate(dsOutfit.gameObject);
            dsOutfit=null;
        }
        void ClearClaudeOutfit()
        {
            if(!claudeOutfit)return;
            claudeOutfit.Release();claudeOutfit.gameObject.SetActive(false);
            if(Application.isPlaying)UnityEngine.Object.Destroy(claudeOutfit.gameObject);else UnityEngine.Object.DestroyImmediate(claudeOutfit.gameObject);
            claudeOutfit=null;
        }
        Rigidbody Bone(string name,Vector3 position,Vector3 size,Rigidbody parent,bool round=false,Vector3 center=default)
        {
            var go=new GameObject(name);go.transform.SetParent(parent?parent.transform:root.transform,false);go.transform.localPosition=position;
            if(round){var c=go.AddComponent<SphereCollider>();c.radius=.46f;}else{var c=go.AddComponent<BoxCollider>();c.size=size;c.center=center;}
            var b=go.AddComponent<Rigidbody>();b.isKinematic=true;b.mass=!parent?2:round?.7f:.3f;b.solverIterations=10;b.solverVelocityIterations=4;b.maxAngularVelocity=12;
            if(parent)
            {
                var j=go.AddComponent<CharacterJoint>();j.connectedBody=parent;j.autoConfigureConnectedAnchor=false;j.anchor=Vector3.zero;j.connectedAnchor=parent.transform.InverseTransformPoint(go.transform.position);
                j.axis=Vector3.right;j.swingAxis=Vector3.forward;j.enableCollision=false;j.enableProjection=true;j.projectionDistance=.04f;j.projectionAngle=10;
                bool elbow=name.Contains("Forearm"),knee=name.Contains("Shin");
                j.swing1Limit=new SoftJointLimit{limit=elbow||knee?5:round?30:60};j.swing2Limit=new SoftJointLimit{limit=elbow||knee?5:35};
                j.lowTwistLimit=new SoftJointLimit{limit=knee?-5:elbow?-125:round?-30:-110};j.highTwistLimit=new SoftJointLimit{limit=knee?130:elbow?5:round?30:65};
            }
            bodies.Add(b);rest.Add((go.transform,position,Quaternion.identity));return b;
        }
        GameObject P(string name,Transform parent,Vector3 pos,Vector3 size,string color,bool round=true)
        {var go=DollFactory.Part(name,parent,pos,size,color,round);cosmetics.Add(go);return go;}
        bool Asset(string name,Transform parent,Vector3 pos,Vector3 size,string color)
        {var go=DollFactory.Asset(name,parent,pos,size,color);if(go)cosmetics.Add(go);return go;}
        public void ApplyAppearance(AppearanceData requested)
        {
            if(Appearance!=null&&Appearance.SameAs(requested))return;
            var a=AppearanceData.Resolve(requested);string key=a.character+"|"+JsonUtility.ToJson(a);if(key==appearanceKey)return;
            appearanceKey=key;Appearance=a;face=DollExpression.For(a.expression);ClearDSOutfit();ClearClaudeOutfit();
            bool ds=a.character=="ds"&&Resources.Load<GameObject>("CampusDoll/DSGirlModular");
            bool claude=a.character=="claude"&&Resources.Load<GameObject>("CampusDoll/ClaudeGirlModular");
            if(!ds&&a.character=="ds"){a=a.Copy();a.character="normal";}
            if(!claude&&a.character=="claude"){a=a.Copy();a.character="normal";}
            Appearance=a;
            if(ds){a=a.Copy();a.hairColor="#354D91";a.top="shirt";a.topColor="#263765";a.bottom="skirt";a.bottomColor="#263765";a.shoes="canvas";a.shoeColor="#263765";a.headAccessory="none";a.bag="none";}
            if(claude){a=a.Copy();a.hairColor="#D66B30";a.top="shirt";a.topColor="#F2E5CA";a.bottom="skirt";a.bottomColor="#302A29";a.shoes="canvas";a.shoeColor="#302A29";a.headAccessory="none";a.bag="none";}
            foreach(var go in cosmetics){if(!go)continue;go.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(go);else UnityEngine.Object.DestroyImmediate(go);}cosmetics.Clear();
            if(!Asset("Head",head,Vector3.zero,new Vector3(.97f,.98f,.9f),a.skinColor))P("Round head and chin",head,Vector3.zero,new Vector3(.97f,.98f,.9f),a.skinColor);
            P("Chin",head,new Vector3(0,-.29f,.015f),new Vector3(.65f,.35f,.66f),a.skinColor);
            for(int sign=-1;sign<=1;sign+=2)
            {
                P("Ear",head,new Vector3(sign*.46f,-.04f,0),new Vector3(.18f,.26f,.16f),a.skinColor);
                if(ds)DSGirlFaceDetails.AddEye(head,sign,face,cosmetics);
                else if(claude)ClaudeGirlFaceDetails.AddEye(head,sign,face,cosmetics);
                else
                {
                    P("Eye white",head,new Vector3(sign*.185f,.055f,.417f),new Vector3(.19f*face.eyeWidth,.205f*face.eyeHeight,.07f),Paper);
                    P("Pupil",head,new Vector3(sign*.17f,.044f,.454f),new Vector3(.079f*face.pupil,.112f*face.eyeHeight,.032f),Ink);
                    P("Eye glint",head,new Vector3(sign*.17f-.019f,.044f+.029f*face.eyeHeight,.475f),Vector3.one*.024f,Paper);
                }
                var brow=P("Eyebrow",head,new Vector3(sign*.185f,.215f-FaceBrowDrop,.412f),new Vector3(.19f,.038f,.06f),a.hairColor);
                if(sign<0)leftBrow=brow.transform;else rightBrow=brow.transform;
            }
            P("Nose",head,new Vector3(0,-.07f,.45f),new Vector3(.115f,.105f,.15f),"#"+ColorUtility.ToHtmlStringRGB(Color.Lerp(ToyFactory.ColorOf(a.skinColor),ToyFactory.ColorOf(Ink),.08f)));
            var mouthObject=DollMouth.Build(head,a.skinColor,face.smile);cosmetics.Add(mouthObject);mouth=mouthObject.transform;
            P("Neck",neck,Vector3.zero,new Vector3(.27f,.26f,.27f),a.skinColor);
            // Capture skin bind poses only in the neutral rest pose; keep the current animation intact.
            var current=new List<(Vector3 p,Quaternion q)>();
            Vector3 fallPosition=fallPivot.localPosition;Quaternion fallRotation=fallPivot.localRotation;fallPivot.localPosition=Vector3.zero;fallPivot.localRotation=Quaternion.identity;
            foreach(var bone in rest){current.Add((bone.t.localPosition,bone.t.localRotation));bone.t.localPosition=bone.p;bone.t.localRotation=bone.q;}
            var personalitySurface=root.GetComponent<PersonalitySkinSurface>()??root.AddComponent<PersonalitySkinSurface>();personalitySurface.Configure(a);
            BuildClothes(a);if(!ds&&!claude){BuildHair(a);BuildAccessories(a);}
            Skin=DollSkin.Build(root.transform,rest.ConvertAll(x=>x.t).ToArray(),cosmetics,mouth,leftBrow,rightBrow,leftArm,rightArm,leftForearm,rightForearm,torso);
            for(int i=0;i<rest.Count;i++){rest[i].t.localPosition=current[i].p;rest[i].t.localRotation=current[i].q;}
            fallPivot.localPosition=fallPosition;fallPivot.localRotation=fallRotation;
            UpdateFace(0);
            RefreshCharacterVisual();
        }
        void BuildClothes(AppearanceData a)
        {
            bool modular=a.character=="ds"||a.character=="claude";
            P("Shirt silhouette",torso,Vector3.zero,new Vector3(.88f,.88f,.59f),a.topColor);
            P("Shirt hem",torso,new Vector3(0,-.37f,0),new Vector3(.78f,.10f,.5f),a.top=="varsity"?Paper:a.topColor,false);
            P("Collar",torso,new Vector3(0,.39f,0),new Vector3(.35f,.085f,.32f),Paper);
            if(a.top=="hoodie")
            {
                P("Hood",torso,new Vector3(0,.37f,-.24f),new Vector3(.57f,.38f,.36f),a.topColor);
                P("Kangaroo pocket",torso,new Vector3(0,-.16f,.289f),new Vector3(.42f,.21f,.075f),a.topColor,false);
                for(int sign=-1;sign<=1;sign+=2)P("Drawcord",torso,new Vector3(sign*.1f,.17f,.3f),new Vector3(.027f,.23f,.022f),Paper,false);
            }
            if(!modular&&(a.top=="varsity"||a.top=="shirt"))
            {
                P("Placket",torso,new Vector3(0,0,.298f),new Vector3(.045f,.63f,.027f),Paper,false);
                for(int i=0;i<3;i++)P("Button",torso,new Vector3(.035f,.2f-i*.18f,.32f),Vector3.one*.04f,Ink);
                for(int sign=-1;sign<=1;sign+=2){var collar=P("Fold collar",torso,new Vector3(sign*.145f,.31f,.28f),new Vector3(.22f,.16f,.065f),Paper,false);collar.transform.localRotation=Quaternion.Euler(0,0,sign*28);}
                if(a.top=="varsity")P("Campus patch",torso,new Vector3(-.23f,.13f,.29f),new Vector3(.14f,.16f,.05f),a.accessoryColor,false);
            }
            if(a.top=="sport")
            {
                for(int sign=-1;sign<=1;sign+=2)P("Sport stripe",torso,new Vector3(sign*.27f,-.01f,.3f),new Vector3(.08f,.59f,.055f),Paper,false);
                P("Team patch",torso,new Vector3(0,.1f,.303f),new Vector3(.18f,.2f,.028f),Paper,false);
            }
            foreach(var arm in new[]{leftArm,rightArm})
            {
                var lower=arm==leftArm?leftForearm:rightForearm;bool shortSleeve=a.top=="sport";
                // A continuous weighted sleeve bridges shoulder, upper arm and elbow.
                var sleeve=DollSkin.Sleeve(arm,torso,lower,shortSleeve,a.top=="varsity"?Paper:a.topColor);cosmetics.Add(sleeve);
                if(shortSleeve)cosmetics.Add(DollSkin.Sleeve(arm,torso,lower,false,a.skinColor,true));
                P("Cuff",shortSleeve?arm:lower,new Vector3(0,shortSleeve?-.18f:-.16f,0),new Vector3(.29f,.075f,.31f),Paper);
                var hand=arm==leftArm?leftHand:rightHand;
                P("Wrist",hand,new Vector3(0,.03f,-.008f),new Vector3(.19f,.16f,.19f),a.skinColor);
                P("Mitten palm",hand,Vector3.zero,new Vector3(.27f,.24f,.25f),a.skinColor);
                float side=arm==leftArm?1:-1;P("Thumb",hand,new Vector3(side*.125f,.05f,.052f),new Vector3(.12f,.17f,.13f),a.skinColor);
            }
            P("Waist",pelvis,new Vector3(0,-.06f,0),new Vector3(.67f,.24f,.48f),a.bottomColor);
            if(a.bottom=="skirt"&&!modular)P("Skirt",pelvis,new Vector3(0,-.14f,0),new Vector3(.87f,.35f,.64f),a.bottomColor,false);
            foreach(var leg in new[]{leftLeg,rightLeg})
            {
                var shin=leg==leftLeg?leftShin:rightShin;
                P("Thigh",leg,new Vector3(0,-.13f,0),new Vector3(.29f,.32f,.32f),a.skinColor);
                P("Trouser thigh",leg,new Vector3(0,-.1f,0),new Vector3(.34f,.32f,.38f),a.bottomColor);
                P("Lower leg",shin,new Vector3(0,-.1f,0),new Vector3(.29f,.31f,.32f),modular?Paper:a.bottomColor==""?a.skinColor:a.bottom=="trousers"?a.bottomColor:a.skinColor);
                P("Sock",shin,new Vector3(0,-.14f,.02f),new Vector3(.30f,.16f,.34f),a.bottom=="trousers"?a.bottomColor:Paper);
                P("Shoe",leg==leftLeg?leftFoot:rightFoot,new Vector3(0,modular?0:a.shoes=="boots"?.10f:.04f,-.01f),new Vector3(.37f,modular?.17f:a.shoes=="boots"?.39f:.25f,.59f),a.shoeColor);
                P("Thick sole",leg==leftLeg?leftFoot:rightFoot,new Vector3(0,-.09f,0),new Vector3(.38f,.095f,.58f),a.character=="claude"?"#302A29":modular?"#1D294D":a.shoes=="boots"?Ink:Paper,false);
                if(a.shoes!="boots")for(int n=0;n<2;n++)P("Laces",leg==leftLeg?leftFoot:rightFoot,new Vector3(0,.132f,.02f+n*.09f),new Vector3(.23f,.028f,.025f),a.shoeColor==Paper?Ink:Paper,false);
                if(a.shoes=="sneakers")P("Sneaker toe",leg==leftLeg?leftToe:rightToe,new Vector3(0,.06f,-.02f),new Vector3(.34f,.15f,.17f),Paper);
            }
        }
        void BuildHair(AppearanceData a)
        {
            bool cap=a.headAccessory=="cap";
            P("Hair back",head,new Vector3(0,.1f,cap?-.15f:-.19f),new Vector3(cap?.91f:.97f,cap?.77f:.85f,cap?.58f:.64f),a.hairColor);
            P("Hair crown",head,new Vector3(0,cap?.27f:.32f,-.015f),new Vector3(cap?.82f:.96f,cap?.28f:.39f,cap?.70f:.83f),a.hairColor);
            if(a.hair=="curly")for(int i=0;i<3;i++)P("Curly fringe",head,new Vector3((i-1)*.25f,cap?.23f:.38f,cap?.22f:.27f),new Vector3(cap?.29f:.39f,cap?.19f:.31f,cap?.24f:.31f),a.hairColor);
            else if(a.hair=="short")for(int i=0;i<3;i++){var fringe=P("Short fringe",head,new Vector3((i-1)*.24f,(cap?.22f:.35f)+(i==0?.035f:0),cap?.23f:.27f),new Vector3(cap?.26f:.31f,cap?.17f:.23f,cap?.22f:.27f),a.hairColor);fringe.transform.localRotation=Quaternion.Euler(0,0,18);}
            else if(a.hair=="bob")for(int sign=-1;sign<=1;sign+=2)P("Bob side",head,new Vector3(sign*.4f,-.03f,-.025f),new Vector3(.28f,.71f,.55f),a.hairColor);
            else
            {
                P("Swept fringe",head,new Vector3(-.15f,cap?.23f:.32f,cap?.19f:.25f),new Vector3(cap?.47f:.58f,cap?.19f:.27f,cap?.25f:.32f),a.hairColor);
                P("Ponytail",head,new Vector3(0,.07f,-.62f),new Vector3(.39f,.67f,.42f),a.hairColor);
                P("Hair tie",head,new Vector3(0,.27f,-.48f),new Vector3(.35f,.13f,.17f),a.accessoryColor);
            }
        }
        void BuildAccessories(AppearanceData a)
        {
            if(a.headAccessory=="cap")
            {
                P("Cap crown",head,new Vector3(0,.25f,0),new Vector3(1.06f,.68f,1.02f),a.accessoryColor);
                P("Cap brim",head,new Vector3(0,.265f,.49f),new Vector3(.83f,.075f,.48f),a.accessoryColor);
                P("Cap badge",head,new Vector3(0,.38f,.49f),new Vector3(.16f,.15f,.025f),Paper,false);
            }
            if(a.headAccessory=="headphones"&&!Asset("Headphones",head,new Vector3(0,.22f,-.04f),new Vector3(1.30f,.89f,.38f),a.accessoryColor))
            {
                for(int side=-1;side<=1;side+=2)
                {
                    P("Headphone cup",head,new Vector3(side*.52f,0,0),new Vector3(.20f,.40f,.34f),a.accessoryColor);
                    var band=P("Headphone band side",head,new Vector3(side*.39f,.36f,-.03f),new Vector3(.13f,.43f,.16f),a.accessoryColor,false);band.transform.localRotation=Quaternion.Euler(0,0,side*24);
                }
                P("Headphone band top",head,new Vector3(0,.54f,-.03f),new Vector3(.77f,.12f,.16f),a.accessoryColor);
            }
            if(a.bag=="backpack")
            {
                P("Backpack",torso,new Vector3(0,.01f,-.43f),new Vector3(.57f,.65f,.28f),a.accessoryColor);
                P("Backpack pocket",torso,new Vector3(0,-.1f,-.59f),new Vector3(.39f,.26f,.09f),Paper,false);
                for(int side=-1;side<=1;side+=2)P("Backpack strap",torso,new Vector3(side*.22f,.08f,.27f),new Vector3(.09f,.55f,.075f),a.accessoryColor,false);
            }
            if(a.bag=="sling")
            {
                var strap=P("Crossbody strap",torso,new Vector3(0,.025f,.327f),new Vector3(.09f,.86f,.055f),a.accessoryColor,false);strap.transform.localRotation=Quaternion.Euler(0,0,-38);
                if(!Asset("SlingBag",torso,new Vector3(-.37f,-.3f,.3f),new Vector3(.44f,.37f,.21f),a.accessoryColor))
                {P("Sling pouch",torso,new Vector3(-.37f,-.3f,.3f),new Vector3(.44f,.37f,.21f),a.accessoryColor);P("Bag flap",torso,new Vector3(-.37f,-.25f,.42f),new Vector3(.37f,.20f,.055f),Paper,false);}
            }
        }
        void KeepLocomotionHandsClear()
        {
            SolveLocomotionArm(leftArm,leftForearm,-1);SolveLocomotionArm(rightArm,rightForearm,1);
        }
        void SolveLocomotionArm(Transform upper,Transform lower,float side)
        {
            var palmOffset=new Vector3(0,-.23f,.018f);
            var current=torso.InverseTransformPoint(lower.TransformPoint(palmOffset));var shoulder=torso.InverseTransformPoint(upper.position);
            var target=current;target.x=side*Mathf.Max(Mathf.Abs(target.x),.62f);
            if(side<0&&!UsesModularOutfit&&Appearance.bag=="sling")
            {
                float gap=BoxGap(target,new Vector3(-.37f,-.3f,.3f),new Vector3(.22f,.185f,.105f),.14f);
                float influence=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((gap+.14f)/.22f));
                target.y=Mathf.Lerp(target.y,Mathf.Max(target.y,.075f),influence);
                if(BoxGap(target,new Vector3(-.37f,-.3f,.3f),new Vector3(.22f,.185f,.105f),.16f)<0)target.y=Mathf.Max(target.y,.075f);
            }
            float first=Vector3.Distance(upper.position,lower.position),second=lower.TransformVector(palmOffset).magnitude;
            float length=(first+second-.004f)/torso.lossyScale.y,dx=target.x-shoulder.x;
            float maxZ=Mathf.Sqrt(Mathf.Max(.01f,length*length-dx*dx-.0064f));target.z=Mathf.Clamp(target.z,shoulder.z-maxZ,shoulder.z+maxZ);
            float dz=target.z-shoulder.z,maxY=Mathf.Sqrt(Mathf.Max(.001f,length*length-dx*dx-dz*dz));target.y=Mathf.Clamp(target.y,shoulder.y-maxY,shoulder.y+maxY);
            var worldTarget=torso.TransformPoint(target);var delta=worldTarget-upper.position;float distance=Mathf.Clamp(delta.magnitude,.06f,first+second-.004f);var direction=delta.normalized;
            var pole=Vector3.ProjectOnPlane(torso.right*side-torso.up*.15f,direction).normalized;
            float cosine=Mathf.Clamp((first*first+distance*distance-second*second)/(2*first*distance),-1,1);
            var elbow=upper.position+direction*(first*cosine)+pole*(first*Mathf.Sqrt(1-cosine*cosine));
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(lower.TransformVector(palmOffset),worldTarget-lower.position)*lower.rotation;
        }
        public void SetGrabContact(Vector3 worldAnchor)
        {
            if(hasPhysicalPose||!holdingContact||eatBlend>.01f)return;
            // A small, bounded stylized extension bridges a real contact; never reach across the arena.
            leftForearm.localPosition=new Vector3(0,-.345f,0);rightForearm.localPosition=new Vector3(0,-.345f,0);
            leftHand.localPosition=rightHand.localPosition=new Vector3(0,-.28f,.018f);
            AimContactArm(leftArm,leftForearm,worldAnchor-root.transform.right*.30f,-1,1);
            AimContactArm(rightArm,rightForearm,worldAnchor+root.transform.right*.30f,1,1);
            GrabContactError=Mathf.Max(Vector3.Distance(leftHand.position,worldAnchor-root.transform.right*.30f),Vector3.Distance(rightHand.position,worldAnchor+root.transform.right*.30f));
        }
        void AimContactArm(Transform upper,Transform lower,Vector3 target,float side,float weight,bool forwardOnly=true)
        {
            var hand=side<0?leftHand:rightHand;
            var local=torso.InverseTransformPoint(target);
            // In front of the shirt, the hands can move toward its centre without entering it.
            if(forwardOnly)local.z=Mathf.Max(.43f,local.z);target=torso.TransformPoint(local);
            var away=target-head.position;if(away.magnitude<.70f)target=head.position+(away.sqrMagnitude>.001f?away.normalized:torso.right*side)*.70f;
            float first=Vector3.Distance(upper.position,lower.position),second=Vector3.Distance(lower.position,hand.position);
            var delta=target-upper.position;float length=Mathf.Clamp(delta.magnitude,.08f,first+second-.005f);var direction=delta.normalized;
            var pole=Vector3.ProjectOnPlane(torso.right*side-torso.up*.1f,direction).normalized;
            float c=Mathf.Clamp((first*first+length*length-second*second)/(2*first*length),-1,1);
            var elbow=upper.position+direction*first*c+pole*first*Mathf.Sqrt(1-c*c);
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation,weight);
            var clamped=upper.position+direction*length;
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(hand.position-lower.position,clamped-lower.position)*lower.rotation,weight);
        }
        void StrikeTrajectory(string action,float elapsed,int variant)
        {
            if((action!="light"&&action!="heavy")||eatBlend>.01f||holdingContact)return;
            bool heavy=action=="heavy";float duration=heavy?.65f:.45f,hit=heavy?.16f:.10f;
            float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.025f))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(heavy?.29f:.20f,duration,elapsed)));
            if(weight<.001f)return;
            float side=(variant&1)==1?-1:1;var upper=side<0?leftArm:rightArm;var lower=side<0?leftForearm:rightForearm;var hand=side<0?leftHand:rightHand;
            // Preserve the soft target pose, then constrain the striking palm along a readable outside arc.
            // This bounded .11m stretch is visual only; it cannot extend authoritative hit reach.
            lower.localPosition=new Vector3(0,-.285f-.06f*weight,0);hand.localPosition=new Vector3(0,-.23f-.05f*weight,.018f);
            var windup=heavy?new Vector3(side*.80f,.77f,-.12f):new Vector3(side*.68f,.27f,-.06f);
            var hitPoint=heavy?new Vector3(side*.37f,.42f,.61f):new Vector3(side*.45f,.33f,.61f);
            float strike=Mathf.SmoothStep(0,1,Mathf.InverseLerp(heavy?.065f:.025f,hit,elapsed));
            Vector3 goal=Vector3.Lerp(windup,hitPoint,strike);
            AimContactArm(upper,lower,root.transform.TransformPoint(goal),side,weight,false);
        }
        public float StrikingReach(int variant)
        {return root.transform.InverseTransformPoint((variant&1)==1?leftHand.position:rightHand.position).z;}
        public float MaximumKneeBend=>Mathf.Max(Quaternion.Angle(Quaternion.identity,leftShin.localRotation),Quaternion.Angle(Quaternion.identity,rightShin.localRotation));
        void KeepCombatHandsClear()
        {
            for(int side=-1;side<=1;side+=2)
            {
                var upper=side<0?leftArm:rightArm;var lower=side<0?leftForearm:rightForearm;var hand=side<0?leftHand:rightHand;
                var palm=hand.position;float gap=Vector3.Distance(palm,head.position);
                for(int along=0;along<4;along++)gap=Mathf.Min(gap,Vector3.Distance(Vector3.Lerp(lower.position,palm,along/4f),head.position)+.04f);
                if(gap<.70f)
                {
                    // Move a raised fist to the side of the cheek, never through the large head.
                    var target=head.position+torso.right*(side*.70f)+torso.forward*.15f;
                    AimContactArm(upper,lower,target,side,1);
                }
                var local=torso.InverseTransformPoint(hand.position);
                bool blocked=BoxGap(local,Vector3.zero,new Vector3(.44f,.44f,.3f),.14f)<.01f;
                for(int sample=0;sample<=4;sample++)
                {
                    var point=torso.InverseTransformPoint(Vector3.Lerp(lower.position,hand.position,sample/4f));float radius=sample==4?.14f:.10f;
                    if(!UsesModularOutfit&&Appearance.bag=="sling")blocked|=BoxGap(point,new Vector3(-.37f,-.3f,.3f),new Vector3(.22f,.185f,.105f),radius)<.01f;
                    if(!UsesModularOutfit&&Appearance.bag=="backpack")blocked|=BoxGap(point,new Vector3(0,.01f,-.43f),new Vector3(.285f,.325f,.14f),radius)<.01f;
                }
                if(blocked)SolveLocomotionArm(upper,lower,side);
            }
        }
        public float LowestSole()
        {
            float low=100;
            for(int leg=0;leg<2;leg++)for(int x=-1;x<=1;x+=2)for(int z=0;z<2;z++)
                low=Mathf.Min(low,(leg==0?leftFoot:rightFoot).TransformPoint(new Vector3(x*.19f,-.1375f,z==0?-.29f:.29f)).y);
            return low;
        }
        static float BoxGap(Vector3 p,Vector3 center,Vector3 half,float radius)
        {
            var d=p-center;var outside=new Vector3(Mathf.Max(0,Mathf.Abs(d.x)-half.x),Mathf.Max(0,Mathf.Abs(d.y)-half.y),Mathf.Max(0,Mathf.Abs(d.z)-half.z));return outside.magnitude-radius;
        }
        public float LocomotionHandClearance()
        {
            float gap=100;
            foreach(var lower in new[]{leftForearm,rightForearm})
            {
                var hand=lower.TransformPoint(new Vector3(0,-.23f,.018f));var local=torso.InverseTransformPoint(hand);
                gap=Mathf.Min(gap,Vector3.Distance(hand,head.position)-.64f,BoxGap(local,Vector3.zero,new Vector3(.44f,.44f,.30f),.14f));
                if(!UsesModularOutfit&&Appearance.bag=="sling")gap=Mathf.Min(gap,BoxGap(local,new Vector3(-.37f,-.3f,.3f),new Vector3(.22f,.185f,.105f),.14f));
                for(int sample=1;sample<=3;sample++)
                {
                    var along=torso.InverseTransformPoint(Vector3.Lerp(lower.position,hand,sample/4f));
                    gap=Mathf.Min(gap,BoxGap(along,Vector3.zero,new Vector3(.44f,.44f,.30f),.10f));
                    if(!UsesModularOutfit&&Appearance.bag=="sling")gap=Mathf.Min(gap,BoxGap(along,new Vector3(-.37f,-.3f,.3f),new Vector3(.22f,.185f,.105f),.10f));
                }
                if(!UsesModularOutfit&&Appearance.bag=="backpack")gap=Mathf.Min(gap,BoxGap(local,new Vector3(0,.01f,-.43f),new Vector3(.285f,.325f,.14f),.14f));
            }
            return gap;
        }
        public void ConfirmSwallow(string instanceId)
        {if(instanceId==lastSwallowed)return;lastSwallowed=instanceId;swallowTime=.65f;}
        Vector3 FeedingTarget()
        {
            float insertion=Mathf.SmoothStep(0,1,Mathf.Clamp01((eatProgress-.48f)/.44f));
            return mouth.position+head.forward*Mathf.Lerp(.34f,-.025f,insertion);
        }
        Vector3 FeedingHandTarget(float side)
        {
            // Keep the entire mitten outside the lip silhouette, not just the wrist pivot.
            float sideClearance=mouth.localScale.x*.5f+.25f;
            // The visual mouth moves down only at rest; retain the original feeding-arm target.
            var anchor=mouth.position+head.TransformVector(Vector3.up*FaceMouthDrop*(1-eatBlend));
            return anchor+head.right*(side*sideClearance)+head.forward*.28f;
        }
        public float FeedingHandClearance()
        {
            float clearance=float.PositiveInfinity;
            foreach(var hand in new[]{leftForearm,rightForearm})
            {
                var palm=hand.TransformPoint(new Vector3(0,-.23f-eatBlend*.345f,.018f));
                for(int along=0;along<=5;along++)
                {
                    var center=Vector3.Lerp(hand.position,palm,along/5f);
                    for(int i=0;i<48;i++)
                    {
                        float angle=i*Mathf.PI*2/48;var rim=mouth.TransformPoint(new Vector3(Mathf.Cos(angle)*.5f,Mathf.Sin(angle)*.5f,.2f));clearance=Mathf.Min(clearance,Vector3.Distance(center,rim)-.15f);
                    }
                    clearance=Mathf.Min(clearance,Vector3.Distance(center,head.position)-.68f);
                }
            }
            return clearance;
        }
        void AimFeedingArm(Transform upper,Transform lower,Vector3 target,float side,float blend)
        {
            float first=Vector3.Distance(upper.position,lower.position),second=(.23f+eatBlend*.345f)*root.transform.lossyScale.y;
            var delta=target-upper.position;float distance=Mathf.Clamp(delta.magnitude,.08f,first+second-.008f);var direction=delta.normalized;
            var outwards=Vector3.ProjectOnPlane(root.transform.right*side+Vector3.down*.2f,direction).normalized;
            float cosine=Mathf.Clamp((first*first+distance*distance-second*second)/(2*first*distance),-1,1);
            var elbow=upper.position+direction*(first*cosine)+outwards*(first*Mathf.Sqrt(1-cosine*cosine));
            var upperRotation=Quaternion.FromToRotation(upper.TransformDirection(Vector3.down),(elbow-upper.position).normalized)*upper.rotation;
            upper.rotation=Quaternion.Slerp(upper.rotation,upperRotation,blend);
            var lowerRotation=Quaternion.FromToRotation(lower.TransformDirection(Vector3.down),(target-lower.position).normalized)*lower.rotation;
            lower.rotation=Quaternion.Slerp(lower.rotation,lowerRotation,blend);
        }
        public Vector3 CarriedPosition(bool eating)
        {
            var hands=(leftForearm.TransformPoint(new Vector3(0,-.23f-eatBlend*.345f,.09f))+rightForearm.TransformPoint(new Vector3(0,-.23f-eatBlend*.345f,.09f)))*.5f;
            if(!eating)return hands+root.transform.forward*.18f;
            return Vector3.Lerp(hands,FeedingTarget(),Mathf.SmoothStep(0,1,Mathf.Clamp01(eatProgress/.25f)));
        }
        public float CarriedScale(bool eating)=>eating?Mathf.Lerp(.58f,.035f,Mathf.SmoothStep(0,1,Mathf.Clamp01((eatProgress-.53f)/.47f))):.65f;
        void UpdateFace(float now)
        {
            float restingDepth=Appearance.expression=="joy"||Appearance.expression=="surprised"?.46f:.367f;
            mouth.localPosition=new Vector3(0,-.235f-eatBlend*.095f-FaceMouthDrop*(1-eatBlend),Mathf.Lerp(restingDepth,.495f,eatBlend));
            mouth.localScale=new Vector3(Mathf.Lerp(face.mouthWidth,1.25f,eatBlend),Mathf.Lerp(face.mouthHeight,1.06f,eatBlend),.10f+eatBlend*.14f);
            mouth.localRotation=Quaternion.Euler(0,0,face.mouthTilt*(1-eatBlend));
            DollMouth.SetOpening(mouth,eatBlend,Appearance.expression=="joy"||Appearance.expression=="surprised");
            float lift=face.browLift*(1-eatBlend)+eatBlend*.15f-FaceBrowDrop*(1-eatBlend);
            leftBrow.localPosition=new Vector3(-.185f-eatBlend*.085f,.215f+lift+face.asymmetry*(1-eatBlend),.412f-eatBlend*.03f);
            rightBrow.localPosition=new Vector3(.185f+eatBlend*.085f,.215f+lift,.412f-eatBlend*.03f);
            float angle=Mathf.Lerp(face.browAngle,-20,struggleBlend)*(1-eatBlend);
            leftBrow.localRotation=Quaternion.Euler(0,0,angle);rightBrow.localRotation=Quaternion.Euler(0,0,-angle);
            // A short deterministic blink, staggered by appearance, uses one morph and allocates nothing per frame.
            float clock=Mathf.Repeat(now+face.eyeHeight*.37f,3.6f);
            float blink=now<=0?0:Mathf.Clamp01(1-Mathf.Abs(clock-.10f)/.075f)*(1-eatBlend);
            Skin.SetBlendShapeWeight(3,blink*100);
        }
        static readonly Dictionary<string,string> mirroredBones=new Dictionary<string,string>{
            {"LeftShoulder","RightShoulder"},{"RightShoulder","LeftShoulder"},{"LeftUpperArm","RightUpperArm"},{"RightUpperArm","LeftUpperArm"},
            {"LeftForearm","RightForearm"},{"RightForearm","LeftForearm"},{"LeftHand","RightHand"},{"RightHand","LeftHand"},
            {"LeftThigh","RightThigh"},{"RightThigh","LeftThigh"},{"LeftShin","RightShin"},{"RightShin","LeftShin"},
            {"LeftFoot","RightFoot"},{"RightFoot","LeftFoot"},{"LeftToes","RightToes"},{"RightToes","LeftToes"}};
        Quaternion TripoTarget(string bone,Quaternion localTarget,Vector3 velocity,string action,float elapsed,int variant,float now,bool airborne)
        {
            // Tripo drives the target skeleton; bounded joints and final contact/feeding IK still own the displayed skeleton.
            if(eatBlend>.01f||holdingContact||airborne)return localTarget;
            float speed=new Vector2(velocity.x,velocity.z).magnitude;
            bool neutral=string.IsNullOrEmpty(action)||action=="idle";
            if(neutral)
            {
                if(speed<.10f&&DollTripoMotion.TrySample("idle",now,bone,out var idle))
                {LastTripoClip="idle";return Quaternion.Slerp(localTarget,idle,.60f);}
                float forward=speed>.1f?Mathf.Clamp01((velocity.z/speed-.55f)/.45f):0;
                if(forward>.01f)
                {
                    bool hasWalk=DollTripoMotion.TrySample("walk",now,bone,out var walk);
                    bool hasRun=DollTripoMotion.TrySample("run",now,bone,out var run);
                    if(hasWalk&&hasRun){LastTripoClip=runBlend>.5f?"run":"walk";return Quaternion.Slerp(localTarget,Quaternion.Slerp(walk,run,runBlend),forward*(PhysicsTarget?.25f:.78f));}
                }
                return localTarget; // Side/back locomotion is locally authored, never turned into forward running.
            }
            string clip=action=="light"?"box_01":action=="hurt"?"hurt":"";
            if(clip=="")return localTarget;
            float duration=action=="light"?.45f:.42f;
            float weight=(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-duration*.6f)/(duration*.4f))))*Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.025f));
            if(weight<=0)return localTarget;
            float sampleTime=DollTripoMotion.MapActionTime(clip,elapsed,duration,action=="light"?.10f:.10f);
            string sampleBone=bone;
            int desiredSide=(variant&1)==1?-1:1;bool mirror=action=="light"&&DollTripoMotion.SourceStrikeSide(clip)!=0&&DollTripoMotion.SourceStrikeSide(clip)!=desiredSide;
            if(mirror&&mirroredBones.TryGetValue(bone,out var opposite))sampleBone=opposite;
            if(!DollTripoMotion.TrySample(clip,sampleTime,sampleBone,out var sampled))return localTarget;
            if(mirror)sampled=new Quaternion(sampled.x,-sampled.y,-sampled.z,sampled.w);
            LastTripoClip=clip;
            return Quaternion.Slerp(localTarget,sampled,weight*(action=="light"?.45f:.60f));
        }
        public void Draw(PublicPlayer p,Camera camera,float snapshotTime=-1)
        {
            DrawAt(p,camera,Time.deltaTime,Time.unscaledTime,snapshotTime);
        }
        // Explicit clock enables deterministic frame-rate and transition validation in the editor.
        public void DrawAt(PublicPlayer p,Camera camera,float dt,float now,float snapshotTime=-1)
        {
            hasPhysicalPose=p.bodyPose!=null;
            root.SetActive(!p.eliminated);if(p.eliminated)return;
            var requested=p.appearance??Appearance;
            string character=p.characterSkin=="ds-girl"?"ds":p.characterSkin=="claude-girl"?"claude":"normal";
            if(requested!=null&&(Appearance==null||Appearance.character!=character||!Appearance.SameStyleAs(requested)))
            {var mapped=requested.Copy();mapped.character=character;ApplyAppearance(mapped);}
            dt=Mathf.Clamp(dt,0,.1f);
            Vector3 target;float yaw;
            if(snapshotTime>=0){motion.Push(snapshotTime,now,p.position,p.yaw);motion.Sample(now,out target,out yaw);}
            else{target=p.position;yaw=p.yaw;}
            ragdoll=false;
            var before=root.transform.position;
            if(!ragdoll)
            {
                bool teleport=!positionInitialized||Vector3.Distance(before,target)>8;
                if(teleport){combatMotion.Reset();for(int i=0;i<rest.Count;i++)jointDynamics.Reset(i,rest[i].q);}
                root.transform.position=teleport?target:recovery>0?Vector3.Lerp(before,target,1-Mathf.Exp(-dt*14)):target;
                root.transform.rotation=Quaternion.Slerp(root.transform.rotation,Quaternion.Euler(0,yaw,0),1-Mathf.Exp(-dt*22));
                positionInitialized=true;
                var distance=root.transform.position-before;distance.y=0;
                float velocity=p.velocity.sqrMagnitude>.001f?new Vector2(p.velocity.x,p.velocity.z).magnitude:teleport||dt<=0?0:distance.magnitude/dt;
                float allowed=p.airborne||p.stagger>0||p.grabbed||!string.IsNullOrEmpty(p.interaction)?0:Mathf.Min(velocity,p.locomotionSpeed);
                moveBlend=Mathf.Lerp(moveBlend,Mathf.Clamp01(allowed/3),1-Mathf.Exp(-dt*(allowed>0?10:7)));
                runBlend=Mathf.Lerp(runBlend,p.running&&allowed>.1f?1:0,1-Mathf.Exp(-dt/ .16f));
                if(!teleport&&allowed>.01f)walkPhase+=Mathf.Min(distance.magnitude,.6f)*Mathf.Lerp(2.05f,1.95f,runBlend);
            }
            if(p.simulationTime!=latestServerTime){latestServerTime=p.simulationTime;latestServerArrival=now;}
            float actionTime=p.simulationTime+Mathf.Clamp(now-latestServerArrival,0,.12f);
            float elapsedAction=Mathf.Max(0,actionTime-p.actionStartedAt);
            string activeAction=p.actionKind??"";
            float activeDuration=DollCombatMotion.Duration(activeAction);
            if(activeDuration>0&&!float.IsInfinity(activeDuration)&&elapsedAction>=activeDuration)activeAction="";
            if(p.interaction=="eat"||p.interaction=="recover")activeAction=p.interaction;
            else if(p.charge>0)activeAction="charge";
            bool eating=p.interaction=="eat";holdingContact=!string.IsNullOrEmpty(p.grabbing);GrabContactError=0;
            eatProgress=eating?Mathf.Lerp(eatProgress,p.progress,1-Mathf.Exp(-dt*20)):0;
            float opening=eating?Mathf.SmoothStep(0,1,Mathf.Clamp01(eatProgress/.24f)):0;
            eatBlend=Mathf.Lerp(eatBlend,opening,1-Mathf.Exp(-dt*15));
            swallowTime=Mathf.Max(0,swallowTime-dt);
            grabBlend=Mathf.Lerp(grabBlend,string.IsNullOrEmpty(p.grabbing)?p.interaction=="recover"?.65f:0:1,1-Mathf.Exp(-dt*16));
            chargeBlend=Mathf.Lerp(chargeBlend,p.charge>0?1:0,1-Mathf.Exp(-dt*14));
            struggleBlend=Mathf.Lerp(struggleBlend,p.grabbed||p.stability<30||activeAction=="hurt"||activeAction=="fall"?1:0,1-Mathf.Exp(-dt*12));
            if(!ragdoll)
            {
                recovery=Mathf.Max(0,recovery-dt/ .28f);
                float blend=1-Mathf.Exp(-dt*(recovery>0?12:22));
                Vector3 worldVelocity=p.velocity;
                if(worldVelocity.sqrMagnitude<.0001f&&previousTargetValid&&dt>0)worldVelocity=(target-previousTarget)/dt;
                if(!previousTargetValid||Vector3.Distance(target,previousTarget)>8)worldVelocity=Vector3.zero;
                previousTarget=target;previousTargetValid=true;
                string action=activeAction;
                var localVelocity=root.transform.InverseTransformDirection(worldVelocity);
                if(p.stagger>0||p.reveal||eating)localVelocity=Vector3.zero;
                combatPose=combatMotion.Sample(new DollCombatMotion.Input{dt=dt,time=now,runBlend=runBlend,strideMeters=PhysicsTarget?Mathf.Lerp(2.15f,2.55f,runBlend):0,localVelocity=localVelocity,actionKind=action,actionElapsed=Mathf.Max(0,actionTime-p.actionStartedAt),actionId=p.actionId,actionVariant=p.actionVariant,impactId=p.impactId,localImpact=root.transform.InverseTransformDirection(p.impactImpulse),grabTension=p.grabTension,escapeProgress=p.escapeProgress,airborne=p.airborne,holdingGrab=!string.IsNullOrEmpty(p.grabbing),staggerRemaining=p.stagger});
                fallPivot.localPosition=combatPose.fallOffset;
                fallPivot.localRotation=Quaternion.Euler(combatPose.fallEuler);
                LastTripoClip="";
                for(int index=0;index<rest.Count;index++)
                {
                    var r=rest[index];var pos=r.p;var angles=Vector3.zero;
                    if(r.t==pelvis){pos+=combatPose.bodyOffset;angles=combatPose.bodyEuler*.22f;}
                    else if(r.t==spine)angles=combatPose.bodyEuler*.28f;
                    else if(r.t==torso)angles=combatPose.bodyEuler*.30f+new Vector3(eatBlend*8,0,0);
                    else if(r.t==chest)angles=combatPose.bodyEuler*.20f;
                    else if(r.t==neck)angles=combatPose.headEuler*.3f;
                    else if(r.t==head)angles=combatPose.headEuler*.7f+new Vector3(-eatBlend*58,0,0);
                    else if(r.t==leftClavicle)angles=new Vector3(0,0,-combatPose.upperBodyWeight*5);
                    else if(r.t==rightClavicle)angles=new Vector3(0,0,combatPose.upperBodyWeight*5);
                    else if(r.t==leftLeg)angles=combatPose.leftThighEuler;
                    else if(r.t==rightLeg)angles=combatPose.rightThighEuler;
                    else if(r.t==leftShin)angles=combatPose.leftShinEuler;
                    else if(r.t==rightShin)angles=combatPose.rightShinEuler;
                    else if(r.t==leftArm)angles=Vector3.Lerp(combatPose.leftUpperArmEuler,new Vector3(-74,-16,-5),eatBlend);
                    else if(r.t==rightArm)angles=Vector3.Lerp(combatPose.rightUpperArmEuler,new Vector3(-74,16,5),eatBlend);
                    else if(r.t==leftForearm||r.t==rightForearm){pos.y-=eatBlend*.55f;angles=Vector3.Lerp(r.t==leftForearm?combatPose.leftForearmEuler:combatPose.rightForearmEuler,new Vector3(-51,0,0),eatBlend);}
                    else if(r.t==leftHand||r.t==rightHand)pos.y-=eatBlend*.345f;
                    else if(r.t==leftFoot)angles=new Vector3(-combatPose.leftShinEuler.x*.35f,0,0);
                    else if(r.t==rightFoot)angles=new Vector3(-combatPose.rightShinEuler.x*.35f,0,0);
                    r.t.localPosition=Vector3.Lerp(r.t.localPosition,pos,blend);
                    Vector3 torque=Vector3.zero;
                    if(p.grabTension>0&&(r.t==chest||r.t==spine||r.t==leftClavicle||r.t==rightClavicle))
                    {
                        var pull=root.transform.InverseTransformDirection(p.grabAnchor-root.transform.position);
                        pull.y=0;pull=pull.sqrMagnitude>.001f?pull.normalized:Vector3.forward;
                        torque=new Vector3(pull.z,0,-pull.x)*Mathf.Clamp01(p.grabTension)*(holdingContact?1.8f:2.4f);
                        torque=r.t.parent.InverseTransformDirection(root.transform.TransformDirection(torque));
                    }
                    bool fast=action=="light"||action=="heavy"||action=="throw"||holdingContact||eatBlend>.01f;
                    Quaternion targetRotation=r.q*Quaternion.Euler(angles);
                    targetRotation=TripoTarget(r.t.name,targetRotation,localVelocity,action,Mathf.Max(0,actionTime-p.actionStartedAt),p.actionVariant,now,p.airborne);
                    r.t.localRotation=jointDynamics.Sample(index,targetRotation,dt,torque,fast);
                }
            }
            float squash=ragdoll?1:p.reveal?.86f:p.stagger>0?.89f:1;
            root.transform.localScale=Vector3.Lerp(root.transform.localScale,new Vector3(1+(1-squash)*.3f,squash,1+(1-squash)*.2f),1-Mathf.Exp(-dt*14));
            if(!ragdoll)
            {
                if(eatBlend<.001f&&combatPose.upperBodyWeight<.05f&&grabBlend<.01f&&chargeBlend<.01f)KeepLocomotionHandsClear();
                if(eatBlend<.001f&&combatPose.upperBodyWeight>=.05f&&!holdingContact)KeepCombatHandsClear();
                if(!p.airborne)
                {
                    float lowest=LowestSole();if(combatPose.fallWeight>.01f)lowest=Mathf.Min(lowest,head.position.y-.49f*root.transform.lossyScale.y);
                    pelvis.position+=Vector3.up*Mathf.Max(0,root.transform.position.y-1+.005f-lowest);
                }
            }
            UpdateFace(now);
            Skin.SetBlendShapeWeight(0,eatBlend*100);
            Skin.SetBlendShapeWeight(1,Mathf.Sin(swallowTime/.65f*Mathf.PI)*100);
            Skin.SetBlendShapeWeight(2,eatBlend*100);
            if(!ragdoll&&eatBlend>.001f)
            {
                AimFeedingArm(leftArm,leftForearm,FeedingHandTarget(-1),-1,eatBlend);
                AimFeedingArm(rightArm,rightForearm,FeedingHandTarget(1),1,eatBlend);
            }
            StrikeTrajectory(activeAction,elapsedAction,p.actionVariant);
            if(holdingContact)SetGrabContact(p.grabAnchor);
            if(lastNickname!=p.nickname||lastRemaining!=p.remaining||lastConnected!=p.connected)
            {lastNickname=p.nickname;lastRemaining=p.remaining;lastConnected=p.connected;label.text=string.IsNullOrEmpty(p.nickname)?"":p.nickname+" · "+p.remaining+(p.connected?"":" [离线]");}
            if(hasPhysicalPose)
            {
                fallPivot.localPosition=Vector3.zero;fallPivot.localRotation=Quaternion.identity;
                foreach(var bone in rest){bone.t.localPosition=bone.p;bone.t.localRotation=bone.q;}
                physicsPresentation.Apply(this,p,now,ImmediatePhysicsPose);
            }
            if(dsOutfit)dsOutfit.Pose(dt,eatBlend,Mathf.Sin(swallowTime/.65f*Mathf.PI));
            if(claudeOutfit)claudeOutfit.Pose(dt,eatBlend,Mathf.Sin(swallowTime/.65f*Mathf.PI));
            if(camera)label.transform.rotation=camera.transform.rotation;
            var equipmentKey=p.equippedVisual+"|"+p.equippedEmblemPath;
            if(visual!=equipmentKey){if(held)UnityEngine.Object.Destroy(held);visual=equipmentKey;if(!string.IsNullOrEmpty(p.equippedVisual)){held=ToyFactory.Item(p.equippedVisual,p.equippedColor,RightHandAttachment,p.equippedName,p.equippedEmblemPath,p.equippedEmblemCrop,p.equippedGeneratedBy);held.transform.localScale=Vector3.one*.60f;}}
            if(held){held.transform.localPosition=new Vector3(0,0,.12f);held.transform.localRotation=Quaternion.identity;held.SetActive(string.IsNullOrEmpty(p.interaction)&&string.IsNullOrEmpty(p.publicProp));}
        }
    }
}
