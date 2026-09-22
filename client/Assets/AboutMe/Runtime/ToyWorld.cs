using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    public static class ToyFactory
    {
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        public static Color ColorOf(string hex) {return ColorUtility.TryParseHtmlString(hex??"",out var c)?c:new Color(.47f,.78f,.74f);}
        public static Material Material(string hex)
        {
            if(materials.TryGetValue(hex,out var m)&&m!=null)return m;
            var basis=Resources.Load<Material>("ToyBaseMaterial");
            m=basis?new Material(basis):new Material(Shader.Find("Standard"));m.color=ColorOf(hex);m.SetFloat("_Glossiness",.3f);materials[hex]=m;return m;
        }
        public static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,string color,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=Material(color);if(!collider)UnityEngine.Object.Destroy(go.GetComponent<Collider>());return go;
        }
        public static GameObject Item(string visual,string color,Transform parent)
        {
            var root=new GameObject(visual);root.transform.SetParent(parent,false);var t=root.transform;
            switch(visual)
            {
                case "chair":
                    Shape("seat",PrimitiveType.Cube,t,Vector3.zero,new Vector3(.9f,.15f,.9f),color);
                    Shape("back",PrimitiveType.Cube,t,new Vector3(0,.45f,-.4f),new Vector3(.9f,.8f,.13f),color);
                    for(int i=0;i<4;i++)Shape("leg",PrimitiveType.Cube,t,new Vector3(i%2==0?-.34f:.34f,-.35f,i<2?-.34f:.34f),new Vector3(.12f,.65f,.12f),color);break;
                case "box":Shape("box",PrimitiveType.Cube,t,Vector3.zero,Vector3.one*.9f,"#C99D70");Shape("tape",PrimitiveType.Cube,t,new Vector3(0,.46f,0),new Vector3(.15f,.02f,.88f),"#F1DDAD");break;
                case "trash":Shape("bin",PrimitiveType.Cylinder,t,Vector3.zero,new Vector3(.8f,.55f,.8f),color);Shape("lid",PrimitiveType.Cylinder,t,Vector3.up*.55f,new Vector3(.9f,.06f,.9f),"#617B80");break;
                case "basketball":
                    Shape("ball",PrimitiveType.Sphere,t,Vector3.zero,Vector3.one*.65f,color);
                    for(int i=0;i<2;i++){var seam=Shape("seam",PrimitiveType.Cylinder,t,Vector3.zero,new Vector3(.66f,.009f,.66f),"#483E41");seam.transform.localRotation=Quaternion.Euler(i*90,0,0);}break;
                case "school-disc":
                    Shape("disc",PrimitiveType.Cylinder,t,Vector3.zero,new Vector3(.8f,.08f,.8f),color);
                    Shape("crest",PrimitiveType.Cube,t,Vector3.up*.095f,new Vector3(.3f,.02f,.3f),"#FFF1D2");break;
                case "book":
                    Shape("cover",PrimitiveType.Cube,t,Vector3.zero,new Vector3(.65f,.8f,.2f),color);
                    Shape("pages",PrimitiveType.Cube,t,new Vector3(.035f,0,-.005f),new Vector3(.59f,.73f,.21f),"#FFF1D2");break;
                case "computer":
                    Shape("monitor",PrimitiveType.Cube,t,Vector3.up*.15f,new Vector3(.85f,.65f,.28f),color);
                    Shape("screen",PrimitiveType.Cube,t,new Vector3(0,.16f,.15f),new Vector3(.68f,.48f,.025f),"#283347");
                    Shape("keyboard",PrimitiveType.Cube,t,new Vector3(0,-.25f,.17f),new Vector3(.8f,.1f,.5f),color);break;
                case "camera":
                    Shape("body",PrimitiveType.Cube,t,Vector3.zero,new Vector3(.75f,.48f,.32f),color);
                    var lens=Shape("lens",PrimitiveType.Cylinder,t,Vector3.forward*.23f,new Vector3(.34f,.14f,.34f),"#27354B");lens.transform.localRotation=Quaternion.Euler(90,0,0);
                    Shape("flash",PrimitiveType.Cube,t,new Vector3(-.18f,.19f,.18f),new Vector3(.13f,.09f,.04f),"#FFF8E9");break;
                case "plant":
                    Shape("pot",PrimitiveType.Cylinder,t,new Vector3(0,-.15f,0),new Vector3(.42f,.23f,.42f),"#E59377");
                    for(int i=0;i<3;i++){var leaf=Shape("leaf",PrimitiveType.Sphere,t,new Vector3((i-1)*.17f,.2f,0),new Vector3(.25f,.55f,.18f),color);leaf.transform.localRotation=Quaternion.Euler(0,0,(i-1)*30);}break;
                case "cat":
                    Shape("cat",PrimitiveType.Sphere,t,Vector3.zero,new Vector3(.56f,.55f,.47f),color);
                    for(int i=-1;i<=1;i+=2){Shape("ear",PrimitiveType.Cube,t,new Vector3(i*.18f,.26f,0),new Vector3(.18f,.23f,.16f),color).transform.localRotation=Quaternion.Euler(0,0,i*15);Shape("eye",PrimitiveType.Sphere,t,new Vector3(i*.12f,.07f,.22f),Vector3.one*.06f,"#283347");}break;
                default:
                    Shape("robot",PrimitiveType.Cube,t,Vector3.zero,new Vector3(.55f,.55f,.45f),color);
                    Shape("visor",PrimitiveType.Cube,t,new Vector3(0,.07f,.23f),new Vector3(.41f,.2f,.02f),"#283347");
                    Shape("antenna",PrimitiveType.Capsule,t,new Vector3(0,.36f,0),new Vector3(.07f,.15f,.07f),"#FFE08D");break;
            }
            return root;
        }
    }
    public class ToyActor
    {
        public GameObject root, held;
        public Transform head,mouth,leftArm,rightArm,leftLeg,rightLeg;
        public TextMesh label;
        public string visual="";
        readonly List<Rigidbody> bodies=new List<Rigidbody>();
        readonly List<(Transform t,Vector3 p,Quaternion q)> rest=new List<(Transform,Vector3,Quaternion)>();
        bool ragdoll;
        Vector3 previous;
        public ToyActor(string id,string color,int outfit,Transform parent)
        {
            root=new GameObject(id);root.transform.SetParent(parent,false);
            var torso=Part("body",new Vector3(0,-.1f,0),new Vector3(.78f,.8f,.52f),outfit==1?"#283347":color,null);
            head=Part("head",new Vector3(0,.65f,0),new Vector3(1.05f,1.0f,.87f),color,torso).transform;
            leftArm=Part("left arm",new Vector3(-.58f,-.08f,0),new Vector3(.32f,.65f,.32f),color,torso).transform;
            rightArm=Part("right arm",new Vector3(.58f,-.08f,0),new Vector3(.32f,.65f,.32f),color,torso).transform;
            leftLeg=Part("left leg",new Vector3(-.25f,-.65f,0),new Vector3(.34f,.55f,.4f),outfit==2?"#283347":color,torso).transform;
            rightLeg=Part("right leg",new Vector3(.25f,-.65f,0),new Vector3(.34f,.55f,.4f),outfit==2?"#283347":color,torso).transform;
            for(int i=-1;i<=1;i+=2)ToyFactory.Shape("eye",PrimitiveType.Sphere,head,new Vector3(i*.2f,.04f,.46f),new Vector3(.08f,.12f,.06f),"#263248");
            mouth=ToyFactory.Shape("mouth",PrimitiveType.Sphere,head,new Vector3(0,-.22f,.47f),new Vector3(.16f,.07f,.025f),"#263248").transform;
            var text=new GameObject("Name");text.transform.SetParent(root.transform,false);text.transform.localPosition=Vector3.up*1.55f;
            label=text.AddComponent<TextMesh>();label.anchor=TextAnchor.MiddleCenter;label.characterSize=.055f;label.fontSize=64;label.color=ToyFactory.ColorOf("#263248");
            var font=Resources.Load<Font>("AboutMeFont");if(font){label.font=font;text.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
        }
        Rigidbody Part(string name,Vector3 p,Vector3 size,string color,Rigidbody parent)
        {
            var go=ToyFactory.Shape(name,PrimitiveType.Sphere,root.transform,p,size,color,true);
            var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.mass=parent==null?2:.35f;body.interpolation=RigidbodyInterpolation.None;
            if(parent!=null){var joint=go.AddComponent<CharacterJoint>();joint.connectedBody=parent;joint.enableCollision=false;joint.swing1Limit=new SoftJointLimit{limit=35};joint.swing2Limit=new SoftJointLimit{limit=35};joint.lowTwistLimit=new SoftJointLimit{limit=-25};joint.highTwistLimit=new SoftJointLimit{limit=25};}
            bodies.Add(body);rest.Add((go.transform,p,Quaternion.identity));return body;
        }
        public void Draw(PublicPlayer p,Camera camera)
        {
            root.SetActive(!p.eliminated);
            if(p.eliminated)return;
            float dt=UnityEngine.Time.deltaTime;
            float speed=(p.position-previous).magnitude;previous=p.position;
            root.transform.position=Vector3.Lerp(root.transform.position,p.position,1-Mathf.Exp(-dt*18));
            root.transform.rotation=Quaternion.Slerp(root.transform.rotation,Quaternion.Euler(0,p.yaw,0),1-Mathf.Exp(-dt*16));
            root.transform.localScale=Vector3.Lerp(root.transform.localScale,new Vector3(p.reveal?1.3f:1,p.reveal?.3f:1,p.reveal?1.3f:1),1-Mathf.Exp(-dt*10));
            bool tumble=p.stagger>.65f;
            if(tumble!=ragdoll)
            {
                ragdoll=tumble;
                foreach(var b in bodies){b.interpolation=tumble?RigidbodyInterpolation.Interpolate:RigidbodyInterpolation.None;b.isKinematic=!tumble;if(tumble)b.AddForce((root.transform.forward*-2+Vector3.up)*2,ForceMode.Impulse);}
                if(!tumble)foreach(var r in rest){r.t.localPosition=r.p;r.t.localRotation=r.q;}
            }
            if(!ragdoll)
            {
                // Kinematic parts follow the network root; physics interpolation must not retain their old world pose.
                foreach(var r in rest){r.t.localPosition=r.p;r.t.localRotation=r.q;}
                float walk=Mathf.Sin(UnityEngine.Time.time*12)*Mathf.Clamp01(speed*30)*25;
                leftLeg.localRotation=Quaternion.Euler(walk,0,0);rightLeg.localRotation=Quaternion.Euler(-walk,0,0);
                leftArm.localRotation=Quaternion.Euler(p.grabbing!=""?-70:-walk,0,-8);
                rightArm.localRotation=Quaternion.Euler(p.interaction!=""?-85:p.charge>0?-120:walk,0,8);
            }
            mouth.localScale=new Vector3(.16f+p.progress*.7f,.07f+p.progress*.7f,.04f);
            label.text=p.nickname==""?"":p.nickname+" · "+p.remaining+(p.connected?"":" [离线]");label.transform.rotation=camera.transform.rotation;
            if(visual!=p.equippedVisual)
            {if(held)UnityEngine.Object.Destroy(held);visual=p.equippedVisual;if(visual!=""){held=ToyFactory.Item(visual,p.equippedColor,root.transform);held.transform.localScale=Vector3.one*.75f;}}
            if(held){held.transform.localPosition=new Vector3(.72f,p.charge>0?.9f:.05f,.45f);held.transform.localRotation=Quaternion.Euler(-p.charge*65,UnityEngine.Time.time*10,0);held.SetActive(p.interaction==""&&string.IsNullOrEmpty(p.publicProp));}
        }
    }
    public class ToyWorld : MonoBehaviour
    {
        public Camera ViewCamera;
        public float CameraYaw=20,CameraPitch=32;
        readonly Dictionary<string,ToyActor> actors=new Dictionary<string,ToyActor>();
        readonly Dictionary<string,GameObject> objects=new Dictionary<string,GameObject>();
        Transform actorRoot,objectRoot,hand;
        string spectate="";
        public void Build()
        {
            RenderSettings.ambientLight=new Color(.72f,.76f,.8f);RenderSettings.fog=true;RenderSettings.fogColor=ToyFactory.ColorOf("#EEE8DC");RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=35;RenderSettings.fogEndDistance=75;
            ViewCamera=Camera.main;
            if(!ViewCamera){var c=new GameObject("Main Camera");c.tag="MainCamera";ViewCamera=c.AddComponent<Camera>();c.AddComponent<AudioListener>();}
            ViewCamera.backgroundColor=ToyFactory.ColorOf("#EEE8DC");ViewCamera.clearFlags=CameraClearFlags.SolidColor;ViewCamera.fieldOfView=48;ViewCamera.farClipPlane=100;
            var background=new GameObject("Backdrop").AddComponent<Camera>();background.depth=-2;background.cullingMask=0;background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=ViewCamera.backgroundColor;
            var sun=new GameObject("Warm studio light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            QualitySettings.shadowDistance=35;
            var arena=new GameObject("Small Arena · 桌上自我介绍").transform;
            ToyFactory.Shape("table edge",PrimitiveType.Cube,arena,new Vector3(0,-.65f,0),new Vector3(28.5f,1.2f,28.5f),"#C49575",true);
            ToyFactory.Shape("play mat",PrimitiveType.Cube,arena,new Vector3(0,-.055f,0),new Vector3(28,.1f,28),"#E0D9C7",true);
            for(int i=-6;i<=6;i++)
            {
                ToyFactory.Shape("grid",PrimitiveType.Cube,arena,new Vector3(i*2,.004f,0),new Vector3(.025f,.01f,27.7f),"#CEC6B4");
                ToyFactory.Shape("grid",PrimitiveType.Cube,arena,new Vector3(0,.004f,i*2),new Vector3(27.7f,.01f,.025f),"#CEC6B4");
            }
            foreach(var p in Match.Obstacles)
            {ToyFactory.Shape("toy building block",PrimitiveType.Cube,arena,p+Vector3.up*.7f,new Vector3(3,1.4f,3),"#BCB0D0",true);ToyFactory.Shape("block top",PrimitiveType.Cube,arena,p+Vector3.up*1.405f,new Vector3(2.65f,.03f,2.65f),"#D4C6E6");}
            for(int i=0;i<6;i++)
            {var ring=ToyFactory.Shape("spawn marker",PrimitiveType.Cylinder,arena,Match.Spawn(i)-Vector3.up*.99f,new Vector3(2,.01f,2),i%2==0?"#B3D2C4":"#E8BCAE");}
            for(int i=0;i<4;i++)ToyFactory.Shape("table leg",PrimitiveType.Cube,arena,new Vector3((i%2==0?-1:1)*12,-5,(i<2?-1:1)*12),new Vector3(1.4f,8,1.4f),"#B78668");
            actorRoot=new GameObject("Actors").transform;objectRoot=new GameObject("Persona & effects").transform;
            hand=new GameObject("The Hand").transform;
            ToyFactory.Shape("palm",PrimitiveType.Sphere,hand,Vector3.zero,new Vector3(5,1.4f,4.5f),"#ECAE8E");
            for(int i=0;i<4;i++)ToyFactory.Shape("finger",PrimitiveType.Capsule,hand,new Vector3((i-1.5f)*1.05f,0,2.5f),new Vector3(.9f,2,1),"#ECAE8E").transform.localRotation=Quaternion.Euler(90,0,0);
            hand.gameObject.SetActive(false);
        }
        void LateUpdate()
        {
            var state=GameNetwork.Game?.State;
            if(state==null)
            {
                if(actors.Keys.Any(k=>!k.StartsWith("preview-")))
                {foreach(var actor in actors.Values)Destroy(actor.root);actors.Clear();foreach(var obj in objects.Values)Destroy(obj);objects.Clear();hand.gameObject.SetActive(false);}
                ViewCamera.rect=new Rect(.03f,.10f,.52f,.37f);ViewCamera.transform.position=new Vector3(6,5,-9);ViewCamera.transform.LookAt(new Vector3(0,1,0));
                if(actors.Count==0)
                {
                    string[] colors={"#77C8BD","#F5C0CF","#FFE08D"};
                    for(int i=0;i<3;i++){var actor=new ToyActor("preview-"+i,colors[i],i,actorRoot);actors["preview-"+i]=actor;actor.root.transform.position=new Vector3((i-1)*3,1,0);}
                }
                int index=0;foreach(var a in actors.Values)a.Draw(new PublicPlayer{id="preview",nickname="",color="#77C8BD",remaining=0,position=new Vector3((index++-1)*3,1,0),yaw=180,connected=true,equippedVisual=index==1?"basketball":index==2?"cat":"computer",equippedColor="#FF934F",interaction="",grabbing=""},ViewCamera);
                return;
            }
            ViewCamera.rect=new Rect(0,0,1,1);
            foreach(var key in actors.Keys.Where(k=>!state.players.Any(p=>p.id==k)).ToArray()){Destroy(actors[key].root);actors.Remove(key);}
            foreach(var p in state.players)
            {
                if(!actors.TryGetValue(p.id,out var actor)){actor=new ToyActor(p.id,p.color,p.outfit,actorRoot);actor.root.transform.position=p.position;actors[p.id]=actor;}
                actor.Draw(p,ViewCamera);
            }
            var me=state.players.FirstOrDefault(p=>p.id==state.you);
            var follow=me;
            if(me!=null&&me.eliminated)
            {
                if(Input.GetKeyDown(KeyCode.Tab)||!state.players.Any(p=>p.id==spectate&&!p.eliminated))
                {var alive=state.players.Where(p=>!p.eliminated).ToArray();if(alive.Length>0)spectate=alive[(Array.FindIndex(alive,p=>p.id==spectate)+1)%alive.Length].id;}
                follow=state.players.FirstOrDefault(p=>p.id==spectate);
            }
            if(follow!=null)
            {
                var target=actors[follow.id].root.transform.position+Vector3.up*.5f;
                var offset=Quaternion.Euler(CameraPitch,CameraYaw,0)*new Vector3(0,0,-8);
                if(state.phase==MatchPhase.Lobby||state.phase==MatchPhase.Reveal){target=Vector3.zero;offset=new Vector3(17,21,-23);}
                ViewCamera.transform.position=Vector3.Lerp(ViewCamera.transform.position,target+offset,1-Mathf.Exp(-UnityEngine.Time.deltaTime*8));ViewCamera.transform.LookAt(target);
            }
            var present=new HashSet<string>();
            if(state.revealed!=null)foreach(var item in state.revealed)
            {
                present.Add(item.id);if(!objects.TryGetValue(item.id,out var display)){display=ToyFactory.Item(item.asset.visualId,item.asset.color,objectRoot);objects[item.id]=display;}
                display.transform.position=item.position+Vector3.up*(.2f*Mathf.Sin(UnityEngine.Time.time*3));display.transform.Rotate(Vector3.up,70*UnityEngine.Time.deltaTime);
            }
            if(state.props!=null)foreach(var prop in state.props)
            {
                present.Add(prop.id);if(!objects.TryGetValue(prop.id,out var go)){go=ToyFactory.Item(prop.visual,"#91ABA9",objectRoot);objects[prop.id]=go;}
                go.transform.position=Vector3.Lerp(go.transform.position,prop.position,1-Mathf.Exp(-UnityEngine.Time.deltaTime*20));
            }
            foreach(var item in state.ground)
            {
                present.Add(item.id);if(!objects.TryGetValue(item.id,out var go)){go=ToyFactory.Item(item.asset.visualId,item.asset.color,objectRoot);objects[item.id]=go;}
                go.transform.position=Vector3.Lerp(go.transform.position,item.position,1-Mathf.Exp(-UnityEngine.Time.deltaTime*20));
                go.transform.Rotate(Vector3.up,45*UnityEngine.Time.deltaTime);
                float scale=1;
                if(item.state==ItemState.Carried){var eater=state.players.FirstOrDefault(x=>x.id==item.carrierId);if(eater!=null&&eater.interaction=="eat")scale=1-eater.progress*.75f;}
                go.transform.localScale=Vector3.one*scale;
            }
            foreach(var effect in state.effects)
            {
                present.Add(effect.id);
                if(!objects.TryGetValue(effect.id,out var go))
                {go=effect.kind=="Pulse"?ToyFactory.Shape("impact",PrimitiveType.Sphere,objectRoot,effect.position,Vector3.one,effect.color):ToyFactory.Item(effect.visual,effect.color,objectRoot);objects[effect.id]=go;}
                go.transform.position=effect.position;
                if(effect.kind=="Pulse")go.transform.localScale=Vector3.one*Mathf.Max(.1f,effect.remaining*3);else go.transform.Rotate(Vector3.up,UnityEngine.Time.deltaTime*200);
            }
            foreach(var key in objects.Keys.Where(k=>!present.Contains(k)).ToArray()){Destroy(objects[key]);objects.Remove(key);}
            hand.gameObject.SetActive(state.handAnimation>0);
            if(state.handAnimation>0)hand.position=state.handPosition+new Vector3(0,2+Mathf.Abs(Mathf.Cos((1.5f-state.handAnimation)*Mathf.PI))*15,0);
        }
    }
}
