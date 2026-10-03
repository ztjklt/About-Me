using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace AboutMe.Editor
{
    public static class PartyMovementPhysicsTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        static void Input(Match m,Player p,string action="",float z=0)=>m.Receive(p.profile.playerId,new Command{sequence=p.sequence+1,action=action,yaw=p.yaw,z=z});
        static void Tick(Match m,int frames){for(int i=0;i<frames;i++){foreach(var p in m.Players.Values)Input(m,p);m.Tick(.02f);}}
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","movement");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);Cache.SetValue(null,new[]{new CampusSolid{name="Ground",center=new Vector3(0,-.5f,0),size=new Vector3(80,1,64)}});
            int passed=0;var failures=new System.Collections.Generic.List<string>();
            try
            {
                foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="movement"))using(var m=new Match("MOVE-PHYSICS",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999}))
                {
                    try
                    {
                        for(int i=0;i<3;i++)m.Add(new Profile{playerId="p"+i,nickname="p"+i,appearance=AppearanceData.Demo(i),personas=Enumerable.Range(0,4).Select(n=>new PersonaAsset{assetId="p"+i+n,name=d.label,templateId="SpecialAbility",visualId=i==0?d.visualId:"book",color="#6CD2CA"}).ToArray()});
                        m.Props.Clear();m.EnableBodyPhysics();m.Phase=MatchPhase.Reveal;m.Tick(.02f);var a=m.Players["p0"];var b=m.Players["p1"];var c=m.Players["p2"];
                        a.position=new Vector3(0,1,0);b.position=new Vector3(10,1,0);c.position=new Vector3(-10,1,0);a.yaw=0;b.yaw=180;c.yaw=0;foreach(var p in m.Players.Values){p.motorHeight=1;m.BodyWorld.Reset(p);}Tick(m,30);
                        if(d.id=="uppercut-piston"||d.id=="inflate-shove"||d.id=="stomp-pulse"){b.position=new Vector3(d.id=="inflate-shove"?.9f:0,1,1.3f);b.motorHeight=1;m.BodyWorld.Reset(b);}
                        if(d.id=="glide-open"||d.id=="stomp-pulse"){a.position=new Vector3(0,d.id=="glide-open"?8:3,0);a.motorHeight=a.position.y;a.airborne=true;a.verticalSpeed=0;a.fallOriginHeight=0;m.BodyWorld.Reset(a);}
                        if(d.id=="grapple-anchor")m.Props.Add(new PublicProp{id="anchor",visual="box",holder="",position=new Vector3(0,.6f,6)});
                        var beforeA=a.position;var beforeB=b.position;Input(m,a,"use_item");Tick(m,Mathf.CeilToInt(d.windup/.02f)+1);var e=m.Effects.Last(x=>x.kind=="PartyMechanic");
                        if(d.id=="spring-pad"||d.id=="belt-floor"){b.position=e.position+Vector3.up*.85f;b.motorHeight=1;b.airborne=false;b.verticalSpeed=0;m.BodyWorld.Reset(b);beforeB=b.position;}
                        if(d.id=="paired-teleport")
                        {
                            Tick(m,25);a.position=new Vector3(0,1,4);a.motorHeight=1;m.BodyWorld.Reset(a);Input(m,a,"use_item");Tick(m,Mathf.CeilToInt(d.windup/.02f)+1);b.position=e.position+Vector3.up*.85f+Vector3.back*.4f;b.motorHeight=1;m.BodyWorld.Reset(b);beforeB=b.position;Input(m,b,"",1);m.Tick(.02f);
                        }
                        float peak=0,heightA=a.position.y,heightB=b.position.y,travelA=0,travelB=0;
                        for(int f=0;f<100;f++){Tick(m,1);heightA=Mathf.Max(heightA,a.position.y);heightB=Mathf.Max(heightB,b.position.y);travelA=Mathf.Max(travelA,Vector3.Distance(beforeA,a.position));travelB=Mathf.Max(travelB,Vector3.Distance(beforeB,b.position));peak=Mathf.Max(peak,m.BodyWorld.Get(a).Bodies.Concat(m.BodyWorld.Get(b).Bodies).Max(x=>x.velocity.magnitude));}
                        Debug.Log("MOVEMENT_REAL_PHYSICS "+d.id+" ownerTravel="+travelA.ToString("F3")+" targetTravel="+travelB.ToString("F3")+" ownerHeight="+heightA.ToString("F3")+" targetHeight="+heightB.ToString("F3")+" peak="+peak.ToString("F3"));
                        Check(peak<25&&m.View("p0").players.All(x=>x.bodyPose.positions.Length==15),"invalid body speed/pose");Check(m.Players.Keys.All(id=>m.Remaining(id)==4),"movement changed Persona life");
                        if(d.id=="spring-pad"||d.id=="uppercut-piston")Check(heightB>1.8f,"real target did not launch");
                        else if(d.id=="pogo-hop")Check(heightA>1.5f,"real pogo did not hop");
                        else if(d.id=="stomp-pulse")Check(m.CombatEvents.Any(x=>x.kind=="party_stomp")&&b.stability<100,"real landing pulse failed");
                        else if(d.id=="glide-open")Check(a.motorHeight>3&&a.verticalSpeed>=-2.01f,"real glide did not slow descent");
                        else if(d.id=="short-dash"||d.id=="grapple-anchor")Check(travelA>1,"real movement did not travel");
                        else Check(travelB>.25f,"real target displacement absent");
                        m.Finish("timeout");Check(!m.Effects.Any(x=>x.kind=="PartyMechanic"),"finish retained movement device");passed++;
                    }
                    catch(Exception ex){failures.Add(d.id+": "+ex.Message);Debug.LogError("MOVEMENT_REAL_PHYSICS_FAIL "+d.id+": "+ex);}
                }
            }
            finally{Cache.SetValue(null,old);}
            if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("MOVEMENT_REAL_PHYSICS_PASSED "+passed+" mechanisms / real 15-body rigs / finite velocity and conserved Persona life");
        }
    }
}
