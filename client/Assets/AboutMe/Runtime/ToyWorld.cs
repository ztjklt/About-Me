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
            go.GetComponent<Renderer>().sharedMaterial=Material(color);if(!collider){if(Application.isPlaying)UnityEngine.Object.Destroy(go.GetComponent<Collider>());else UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}return go;
        }
        public static GameObject Item(string visual,string color,Transform parent,string label="")
        {
            var imported=PersonaVisualCatalog.Create(visual,parent);
            if(imported)return imported;
            var root=new GameObject(visual);root.transform.SetParent(parent,false);var t=root.transform;
            switch(visual)
            {
                case "chair":
                    Shape("seat",PrimitiveType.Cube,t,Vector3.zero,new Vector3(.9f,.15f,.9f),color);
                    Shape("back",PrimitiveType.Cube,t,new Vector3(0,.45f,-.4f),new Vector3(.9f,.8f,.13f),color);
                    for(int i=0;i<4;i++)Shape("leg",PrimitiveType.Cube,t,new Vector3(i%2==0?-.34f:.34f,-.35f,i<2?-.34f:.34f),new Vector3(.12f,.65f,.12f),color);break;
                case "box":Shape("box",PrimitiveType.Cube,t,Vector3.zero,Vector3.one*.9f,"#C99D70");Shape("tape",PrimitiveType.Cube,t,new Vector3(0,.46f,0),new Vector3(.15f,.02f,.88f),"#F1DDAD");break;
                case "trash":Shape("bin",PrimitiveType.Cube,t,Vector3.zero,new Vector3(.9f,1.2f,.9f),"#79CFC5");Shape("lid",PrimitiveType.Cube,t,Vector3.up*.62f,new Vector3(1,.12f,1),"#202A3D");break;
                case "basketball":
                    Shape("ball",PrimitiveType.Sphere,t,Vector3.zero,Vector3.one*.65f,color);
                    for(int i=0;i<2;i++){var seam=Shape("seam",PrimitiveType.Cylinder,t,Vector3.zero,new Vector3(.66f,.009f,.66f),"#483E41");seam.transform.localRotation=Quaternion.Euler(i*90,0,0);}break;
                case "school-disc":
                    Shape("disc",PrimitiveType.Cylinder,t,Vector3.zero,new Vector3(.8f,.08f,.8f),color);
                    Shape("crest",PrimitiveType.Cylinder,t,Vector3.up*.095f,new Vector3(.48f,.018f,.48f),"#FFF1D2");
                    var school=new GameObject("school name");school.transform.SetParent(t,false);school.transform.localPosition=new Vector3(0,.122f,0);school.transform.localRotation=Quaternion.Euler(90,0,0);
                    var schoolText=school.AddComponent<TextMesh>();var shortName=(label??"").Replace("校徽圆盘","").Replace("校徽","");if(shortName.Length>6)shortName=shortName.Substring(0,6);
                    schoolText.text=shortName;schoolText.anchor=TextAnchor.MiddleCenter;schoolText.alignment=TextAlignment.Center;schoolText.characterSize=.075f;schoolText.fontSize=52;schoolText.color=ColorOf("#263248");
                    var schoolFont=Resources.Load<Font>("AboutMeFont");if(schoolFont){schoolText.font=schoolFont;school.GetComponent<MeshRenderer>().sharedMaterial=schoolFont.material;}break;
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
    public class ToyWorld : MonoBehaviour
    {
        public Camera ViewCamera;
        public AppearanceData PreviewAppearance=AppearanceData.Demo(0);
        public float CameraYaw=20,CameraPitch=32;
        readonly Dictionary<string,ToyActor> actors=new Dictionary<string,ToyActor>();
        readonly Dictionary<string,(SkinnedMeshRenderer skin,string equipped,Renderer[] renderers,bool hidden)> cameraSkins=new Dictionary<string,(SkinnedMeshRenderer,string,Renderer[],bool)>();
        readonly Dictionary<string,GameObject> objects=new Dictionary<string,GameObject>();
        Transform actorRoot,objectRoot,hand;
        GameObject handOpen,handFist;
        HandPresentation handPresentation;
        ImpactFeedback impactFeedback;
        string spectate="";
        bool menuPreviewVisible=true;
        public void SetMenuPreviewVisible(bool visible)
        {
            if(menuPreviewVisible==visible)return;menuPreviewVisible=visible;
            if(GameNetwork.Game?.State==null&&ViewCamera)ViewCamera.enabled=visible;
        }
        public void Build()
        {
            RenderSettings.ambientLight=new Color(.72f,.76f,.8f);RenderSettings.fog=true;RenderSettings.fogColor=ToyFactory.ColorOf("#EEE8DC");RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=80;RenderSettings.fogEndDistance=180;
            ViewCamera=Camera.main;
            if(!ViewCamera){var c=new GameObject("Main Camera");c.tag="MainCamera";ViewCamera=c.AddComponent<Camera>();c.AddComponent<AudioListener>();}
            ViewCamera.backgroundColor=ToyFactory.ColorOf("#EEE8DC");ViewCamera.clearFlags=CameraClearFlags.SolidColor;ViewCamera.fieldOfView=48;ViewCamera.farClipPlane=220;
            DeskBackdrop.Create();DeskBackdrop.ConfigureView(ViewCamera);
            var background=new GameObject("Backdrop").AddComponent<Camera>();background.depth=-2;background.cullingMask=0;background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=ViewCamera.backgroundColor;
            var sun=new GameObject("Warm studio light").AddComponent<Light>();sun.type=LightType.Directional;sun.cullingMask=~(1<<30);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            QualitySettings.shadowDistance=65;
            DeskBackdrop.ConfigureLight(sun);
            var campus=Resources.Load<GameObject>("CampusV04/Campus");
            if(!campus)throw new InvalidOperationException("Campus V0.4 prefab missing. Run CampusImport.Build.");
            var campusRoot=Instantiate(campus);campusRoot.name="Remember About Me · Campus 80×64m";
            // One set of boxes drives the visible closure and the authoritative
            // collision representation of every upper classroom seam.
            foreach(var seal in CampusFacadeSeals.Solids)
            {
                var color=seal.name.EndsWith("cheek")?"#FFF9EF":"#F5EFE3";
                var shape=ToyFactory.Shape(seal.name,PrimitiveType.Cube,campusRoot.transform,
                    seal.center,seal.size,color,true);
                shape.isStatic=true;
            }
            // These flush tiled aprons bridge the narrow gaps between each
            // second-floor wing and the club buildings beside the stair routes.
            foreach(int side in new[]{-1,1})foreach(float z in new[]{-14f,10f})
            {
                var apron=ToyFactory.Shape("Club wing threshold",PrimitiveType.Cube,campusRoot.transform,
                    new Vector3(side*30.375f,4.25f,z),new Vector3(1f,.5f,8f),"#F5EFE3",true);
                apron.name=(side<0?"West":"East")+" club wing threshold "+z;
                ToyFactory.Shape("Threshold mint inlay",PrimitiveType.Cube,campusRoot.transform,
                    new Vector3(side*30.02f,4.505f,z),new Vector3(.055f,.01f,7.75f),"#79CFC5");
                ToyFactory.Shape("Threshold gold inlay",PrimitiveType.Cube,campusRoot.transform,
                    new Vector3(side*30.45f,4.506f,z),new Vector3(.045f,.012f,6.4f),"#FFD773");
            }
            impactFeedback=gameObject.AddComponent<ImpactFeedback>();
            actorRoot=new GameObject("Actors").transform;objectRoot=new GameObject("Persona & effects").transform;
            hand=new GameObject("The Hand").transform;handPresentation=gameObject.AddComponent<HandPresentation>();
            handOpen=Instantiate(Resources.Load<GameObject>("CampusV04/HandOpen"),hand,false);
            handFist=Instantiate(Resources.Load<GameObject>("CampusV04/HandFist"),hand,false);
            hand.gameObject.SetActive(false);
        }
        void LateUpdate()
        {
            var state=GameNetwork.Game?.State;
            if(state==null)
            {
                if(!menuPreviewVisible){if(ViewCamera)ViewCamera.enabled=false;return;}
                if(ViewCamera&&!ViewCamera.enabled)ViewCamera.enabled=true;
                handPresentation.Draw(null,hand,handOpen,handFist,ViewCamera);
                if(actors.Keys.Any(k=>!k.StartsWith("preview-")))
                {foreach(var actor in actors.Values)Destroy(actor.root);actors.Clear();cameraSkins.Clear();foreach(var obj in objects.Values)Destroy(obj);objects.Clear();hand.gameObject.SetActive(false);}
                var safe=Screen.safeArea;float fit=Mathf.Min(safe.width/1600f,safe.height/900f);
                var origin=safe.center-new Vector2(800,450)*fit;
                ViewCamera.rect=new Rect((origin.x+40*fit)/Screen.width,(origin.y+102*fit)/Screen.height,830*fit/Screen.width,345*fit/Screen.height);ViewCamera.transform.position=new Vector3(6,5,-9);ViewCamera.transform.LookAt(new Vector3(0,1,0));
                if(actors.Count==0)
                {
                    string[] colors={"#77C8BD","#F5C0CF","#FFE08D"};
                    for(int i=0;i<3;i++){var actor=new ToyActor("preview-"+i,colors[i],i,actorRoot);actors["preview-"+i]=actor;actor.root.transform.position=new Vector3((i-1)*3,1,0);}
                }
                int index=0;foreach(var a in actors.Values)a.Draw(new PublicPlayer{id="preview",nickname="",color="#77C8BD",remaining=0,position=new Vector3((index++-1)*3,1,0),yaw=180,connected=true,stability=100,appearance=index==1?PreviewAppearance:AppearanceData.Demo(index-1),equippedVisual=index==1?"basketball":index==2?"cat":"computer",equippedColor="#FF934F",interaction="",grabbing=""},ViewCamera);
                return;
            }
            if(ViewCamera&&!ViewCamera.enabled)ViewCamera.enabled=true;
            ViewCamera.rect=new Rect(0,0,1,1);
            foreach(var key in actors.Keys.Where(k=>!state.players.Any(p=>p.id==k)).ToArray()){Destroy(actors[key].root);actors.Remove(key);cameraSkins.Remove(key);}
            foreach(var p in state.players)
            {
                if(!actors.TryGetValue(p.id,out var actor)){actor=new ToyActor(p.id,p.color,p.outfit,actorRoot,p.appearance);actor.root.transform.position=p.position;actors[p.id]=actor;}
                if(state.eaten!=null){var consumed=state.eaten.LastOrDefault(x=>x.eaterId==p.id);if(consumed!=null)actor.ConfirmSwallow(consumed.instanceId);}
                actor.Draw(p,ViewCamera,state.simulationTime);
                if(!cameraSkins.TryGetValue(p.id,out var cached)||cached.skin!=actor.Skin||cached.equipped!=p.equippedVisual)
                    cameraSkins[p.id]=(actor.Skin,p.equippedVisual,actor.root.GetComponentsInChildren<Renderer>(true),cached.hidden);
            }
            // Both bodies have now sampled the same snapshot clock. Resolve contact in that rendered space.
            if(state.grabLinks!=null)foreach(var link in state.grabLinks)
                if(actors.TryGetValue(link.grabberId,out var grabber)&&actors.TryGetValue(link.targetId,out var grabbed))
                    grabber.SetGrabContact(grabbed.root.transform.TransformPoint(link.targetLocalAnchor));
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
                if(state.phase==MatchPhase.Lobby||state.phase==MatchPhase.Reveal){target=Vector3.zero;offset=new Vector3(0,48,-70);}
                var desired=target+offset;bool orbit=state.phase!=MatchPhase.Lobby&&state.phase!=MatchPhase.Reveal;
                if(orbit)desired=CampusGeometry.CameraPosition(target,desired);
                var cameraPosition=Vector3.Lerp(ViewCamera.transform.position,desired,1-Mathf.Exp(-UnityEngine.Time.deltaTime*14));
                ViewCamera.transform.position=orbit?CampusGeometry.CameraPosition(target,cameraPosition):cameraPosition;ViewCamera.transform.LookAt(target);
            }
            // Wall avoidance may move the camera into either fighter. Cache the full
            // visual hierarchy so faces/equipment cannot float after the skin is hidden.
            foreach(var pair in actors)
            {
                var actor=pair.Value;var visual=cameraSkins[pair.Key];
                bool hidden=Vector3.Distance(ViewCamera.transform.position,actor.head.position)<(visual.hidden?2.8f:2.5f);
                foreach(var renderer in visual.renderers)
                    if(renderer&&renderer.gameObject!=actor.label.gameObject)renderer.forceRenderingOff=hidden;
                visual.hidden=hidden;cameraSkins[pair.Key]=visual;
            }
            impactFeedback.Draw(state,actors,ViewCamera);
            var present=new HashSet<string>();
            if(state.revealed!=null)foreach(var item in state.revealed)
            {
                present.Add(item.id);if(!objects.TryGetValue(item.id,out var display)){display=ToyFactory.Item(item.asset.visualId,item.asset.color,objectRoot,item.asset.name);objects[item.id]=display;}
                display.transform.position=item.position+Vector3.up*(.2f*Mathf.Sin(UnityEngine.Time.time*3));display.transform.Rotate(Vector3.up,70*UnityEngine.Time.deltaTime);
            }
            if(state.props!=null)foreach(var prop in state.props)
            {
                present.Add(prop.id);if(!objects.TryGetValue(prop.id,out var go)){go=ToyFactory.Item(prop.visual,"#91ABA9",objectRoot);objects[prop.id]=go;}
                go.transform.position=Vector3.Lerp(go.transform.position,prop.position,1-Mathf.Exp(-UnityEngine.Time.deltaTime*20));
            }
            foreach(var item in state.ground)
            {
                present.Add(item.id);if(!objects.TryGetValue(item.id,out var go)){go=ToyFactory.Item(item.asset.visualId,item.asset.color,objectRoot,item.asset.name);objects[item.id]=go;}
                bool carried=item.state==ItemState.Carried;
                if(carried&&actors.TryGetValue(item.carrierId,out var carrier))
                {
                    var eater=state.players.FirstOrDefault(x=>x.id==item.carrierId);bool eating=eater!=null&&eater.interaction=="eat";
                    go.transform.position=carrier.CarriedPosition(eating);go.transform.localScale=Vector3.one*carrier.CarriedScale(eating);
                    go.transform.rotation=Quaternion.Slerp(go.transform.rotation,carrier.head.rotation,1-Mathf.Exp(-Time.deltaTime*10));
                    var itemLabel=go.GetComponentInChildren<TextMesh>(true);if(itemLabel)itemLabel.gameObject.SetActive(false);
                }
                else
                {
                    go.transform.position=Vector3.Lerp(go.transform.position,item.position,1-Mathf.Exp(-Time.deltaTime*20));go.transform.Rotate(Vector3.up,45*Time.deltaTime);go.transform.localScale=Vector3.one;
                    var itemLabel=go.GetComponentInChildren<TextMesh>(true);if(itemLabel)itemLabel.gameObject.SetActive(true);
                }
            }
            foreach(var effect in state.effects)
            {
                present.Add(effect.id);
                if(!objects.TryGetValue(effect.id,out var go))
                {go=effect.kind=="Pulse"?ToyFactory.Shape("impact",PrimitiveType.Sphere,objectRoot,effect.position,Vector3.one,effect.color):ToyFactory.Item(effect.visual,effect.color,objectRoot);objects[effect.id]=go;}
                go.transform.position=effect.position;
                if(effect.kind=="Pulse")go.transform.localScale=Vector3.one*Mathf.Max(.1f,effect.remaining*3);
                else go.transform.Rotate(Vector3.up,UnityEngine.Time.deltaTime*(effect.kind=="ThrownDisc"?(effect.landed?32:900):200));
            }
            foreach(var key in objects.Keys.Where(k=>!present.Contains(k)).ToArray()){Destroy(objects[key]);objects.Remove(key);}
            handPresentation.Draw(state,hand,handOpen,handFist,ViewCamera);
        }
    }
}
