using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class BodyPhysicsShowcase
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static Profile Profile(string id,int appearance)=>new Profile{playerId=id,nickname=id,appearance=AppearanceData.Demo(appearance),personas=Enumerable.Range(0,4).Select(i=>new PersonaAsset{assetId=id+i,name="Persona",visualId="book",templateId="Melee",color="#F5C0CF"}).ToArray()};
        public static void Render()
        {
            Directory.CreateDirectory("../artifacts/characters-v4/showcase");var old=Cache.GetValue(null);
            var camera=new GameObject("V4 camera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#F5EFE3");camera.farClipPlane=200;
            var light=new GameObject("V4 daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.transform.rotation=Quaternion.Euler(35,-35,0);RenderSettings.ambientLight=Color.gray;
            var ground=ToyFactory.Shape("Ground",PrimitiveType.Cube,null,new Vector3(0,-.06f,0),new Vector3(80,.10f,64),"#D4E3D7");ground.GetComponent<Renderer>().sharedMaterial=DollFactory.Material("#D4E3D7");
            var feedback=new GameObject("Impact feedback").AddComponent<ImpactFeedback>();
            var rt=new RenderTexture(640,720,24){antiAliasing=4};camera.targetTexture=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            int frameNumber=0;
            try
            {
                foreach(string kind in new[]{"walk-run","light","heavy","high-fall","wall","eat-interrupt","drag","comparison"})
                {
                    var floor=new CampusSolid{name="Ground",center=new Vector3(0,-.5f,0),size=new Vector3(80,1,64)};
                    Cache.SetValue(null,kind=="wall"?new[]{floor,new CampusSolid{name="Wall",center=new Vector3(-2,2,0),size=new Vector3(.4f,4,8)}}:new[]{floor});
                    GameObject wall=kind=="wall"?ToyFactory.Shape("Wall",PrimitiveType.Cube,null,new Vector3(-2,2,0),new Vector3(.4f,4,8),"#96BCDA"):null;
                    using(var match=new Match("V4",new MatchRules{revealDuration=.01f,handInterval=999,finalTime=999}))
                    {
                        match.Add(Profile("p0",0));match.Add(Profile("p1",1));match.Props.Clear();var p=match.Players["p0"];var attacker=match.Players["p1"];
                        p.position=kind=="high-fall"?new Vector3(0,5,0):Vector3.up;p.fallOriginHeight=kind=="high-fall"?4:0;p.airborne=kind=="high-fall";
                        attacker.position=kind=="light"||kind=="heavy"?new Vector3(0,1,1.7f):new Vector3(5,1,0);attacker.yaw=180;attacker.input.yaw=180;
                        match.EnableBodyPhysics();match.Phase=MatchPhase.Reveal;match.Tick(.02f);match.BodyWorld.Get(p);match.BodyWorld.Get(attacker);
                        var actors=new Dictionary<string,ToyActor>();foreach(var person in match.Players.Values){var actor=new ToyActor(person.profile.playerId,person.profile.color,0,null,person.profile.appearance);actor.ImmediatePhysicsPose=true;actors[person.profile.playerId]=actor;}
                        foreach(var temp in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(temp.name=="Cube"&&temp.transform.parent==null)UnityEngine.Object.DestroyImmediate(temp.gameObject);
                        var legacy=kind=="comparison"?new ToyActor("V3 pose replay","#77C8BD",0,null,AppearanceData.Demo(0)):null;
                        GameObject persona=null;
                        for(int frame=0;frame<240;frame++)
                        {
                            string action=kind=="light"&&(frame==40||frame==100)?"light":kind=="heavy"&&frame==5?"charge":kind=="heavy"&&frame==65?"heavy":"";
                            match.Receive("p1",new Command{sequence=attacker.sequence+1,yaw=180,action=action});
                            if(kind=="walk-run")match.Receive("p0",new Command{sequence=p.sequence+1,z=frame<160?1:0,run=frame>=65&&frame<160});
                            if((kind=="wall"||kind=="drag"||kind=="comparison")&&frame==40)match.Hit(p,attacker,1,7,true,true);
                            if(kind=="drag"&&frame==90){var chest=match.BodyWorld.Get(p).Bodies[1];attacker.position=new Vector3(chest.position.x,1,chest.position.z+.85f);attacker.yaw=180;match.BodyWorld.Reset(attacker);attacker.grabbing="p0";attacker.grabStartedAt=match.Time;attacker.bodyGrabBone=1;attacker.bodyGrabAnchor=Vector3.zero;}
                            if(kind=="drag"&&frame>=90)match.Receive("p1",new Command{sequence=attacker.sequence+1,yaw=180,z=1});
                            if(kind=="eat-interrupt")
                            {
                                if(frame==15){match.Drop(attacker,1);var item=match.Items.First(i=>i.ownerId=="p1"&&i.state==ItemState.Ground);item.position=p.position+Vector3.forward*.6f;item.velocity=Vector3.zero;match.Interact(p,item.id);persona=ToyFactory.Item("book","#F5C0CF",null);}
                                if(frame==105)match.Hit(p,attacker,1,5.8f,false);
                            }
                            match.Tick(.02f);if(kind=="walk-run"&&frame%15==0)Debug.Log("V4_WALK_DIAG "+frame+" airborne="+p.airborne+" velocity="+p.velocity+" speed="+p.locomotionSpeed+" "+match.BodyWorld.Get(p).MotionDiagnostic);var snapshot=match.View("p0");
                            foreach(var view in snapshot.players){view.equippedVisual="";actors[view.id].DrawAt(view,camera,.02f,match.Time);actors[view.id].label.text=view.id=="p0"?kind+" / "+view.bodyMode+" / "+match.Time.ToString("0.00")+"s":"";}
                            actors["p1"].root.SetActive(kind=="light"||kind=="heavy"||kind=="drag");
                            if(legacy!=null){var oldView=JsonUtility.FromJson<PublicPlayer>(JsonUtility.ToJson(snapshot.players.First(x=>x.id=="p0")));oldView.bodyPose=null;oldView.position=new Vector3(p.position.x,1,p.position.z);legacy.DrawAt(oldView,camera,.02f,match.Time);legacy.label.text="V3 preset pose (same event replay)";}
                            if(persona){persona.SetActive(p.interaction=="eat");persona.transform.position=actors["p0"].CarriedPosition(true);persona.transform.localScale=Vector3.one*actors["p0"].CarriedScale(true);}
                            feedback.Draw(snapshot,actors,null,match.Time);
                            if(frame%2!=0)continue;
                            for(int view=0;view<2;view++)
                            {
                                camera.orthographicSize=kind=="high-fall"?4.2f:2.5f;var focus=kind=="high-fall"?new Vector3(p.position.x,2.6f,p.position.z):p.position+Vector3.up*.2f;
                                if(legacy!=null){legacy.root.SetActive(view==0);actors["p0"].root.SetActive(view==1);actors["p0"].label.text="V4 physical body";legacy.label.transform.rotation=camera.transform.rotation;}
                                camera.transform.position=focus+(view==0?new Vector3(4,1.4f,6):new Vector3(7,1.0f,0));camera.transform.LookAt(focus);
                                foreach(var actor in actors.Values)actor.label.transform.rotation=camera.transform.rotation;
                                BakeRender(camera);RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,640,720),view*640,0);RenderTexture.active=null;
                            }
                            texture.Apply();File.WriteAllBytes("../artifacts/characters-v4/showcase/"+(frameNumber++).ToString("D4")+".png",texture.EncodeToPNG());
                        }
                        if(legacy!=null)UnityEngine.Object.DestroyImmediate(legacy.root);
                        foreach(var actor in actors.Values)UnityEngine.Object.DestroyImmediate(actor.root);if(persona)UnityEngine.Object.DestroyImmediate(persona);
                    }
                    if(wall)UnityEngine.Object.DestroyImmediate(wall);Debug.Log("V4_SHOWCASE_CASE "+kind);
                }
                Debug.Log("V4_SHOWCASE_RENDERED frames="+frameNumber);
            }
            finally{Cache.SetValue(null,old);}
        }
        static void BakeRender(Camera camera)
        {
            var temporary=new List<(SkinnedMeshRenderer skin,GameObject go,Mesh mesh)>();
            foreach(var skin in UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;var mesh=new Mesh();skin.BakeMesh(mesh);var go=new GameObject("Baked",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(skin.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=skin.sharedMaterial;skin.enabled=false;temporary.Add((skin,go,mesh));
            }
            camera.Render();foreach(var entry in temporary){entry.skin.enabled=true;UnityEngine.Object.DestroyImmediate(entry.go);UnityEngine.Object.DestroyImmediate(entry.mesh);}
        }
    }
}
