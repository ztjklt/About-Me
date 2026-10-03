using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace AboutMe.Editor
{
    public static class PartyGuardPhysicsTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        static void Place(Match m,Player p,Vector3 pos){p.position=pos;p.motorHeight=pos.y;p.verticalSpeed=0;p.airborne=false;p.impulse=Vector3.zero;m.BodyWorld.Reset(p);}
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        static PersonaInstance Incoming(Match m,bool rear=false,bool far=false)
        {var a=m.Players["p0"];return PartyGuardTests.Throw(m,a.position+new Vector3(0,far?1:.25f,rear?-3:far?5:3),new Vector3(0,far?4:1,rear?10:-10));}
        public static void Regression()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","guard-regression");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            Validate();PartyFieldPhysicsTests.Validate();PartyMovementPhysicsTests.Validate();BodyPhysicsTests.ValidateGrab();
        }
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","guards");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);Cache.SetValue(null,new[]{new CampusSolid{name="Ground",center=new Vector3(0,-.5f,0),size=new Vector3(80,1,64)}});
            int passed=0;var failures=new System.Collections.Generic.List<string>();
            try
            {
                foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="guard"))using(var m=PartyFieldTests.Make(d.id))
                {
                    try
                    {
                        m.EnableBodyPhysics();var a=m.Players["p0"];var b=m.Players["p1"];var c=m.Players["p2"];
                        Place(m,a,new Vector3(0,1,0));Place(m,b,new Vector3(10,1,0));Place(m,c,new Vector3(-10,1,0));PartyFieldTests.Tick(m,.6f);
                        EffectState wet=null,glue=null;
                        if(d.id=="sponge-dry")
                        {
                            wet=new EffectState{id="wet",kind="PartyMechanic",mechanicId="wet-trail",owner="p2",sourceInstance=c.equipped,position=new Vector3(10,1,0),deadline=m.Time+5,path=new[]{new Vector3(0,.15f,1.8f),new Vector3(.3f,.15f,1.8f),new Vector3(.6f,.15f,1.8f)},nextHit=m.Time+5};
                            glue=new EffectState{id="glue",kind="PartyMechanic",mechanicId="sticky-step",owner="p2",sourceInstance=c.equipped,position=new Vector3(0,.15f,1.8f),deadline=m.Time+5,radius=2};m.Effects.Add(wet);m.Effects.Add(glue);
                        }
                        var e=PartyFieldTests.Deploy(m);var before=a.position;PersonaInstance shot=null;
                        if(d.id=="front-shield"||d.id=="timed-reflect"||d.id=="ribbon-cover"||d.id=="recover-screen"||d.id=="calm-dome"||d.id=="wind-deflector"||d.id=="rhythm-gate")
                        {
                            if(d.id=="rhythm-gate")PartyFieldTests.Tick(m,.4f);
                            shot=Incoming(m,d.id=="ribbon-cover",d.id=="calm-dome");PartyFieldTests.Tick(m,d.id=="timed-reflect"?.16f:d.id=="wind-deflector"?.04f:d.id=="calm-dome"?.6f:.24f);
                            if(d.id=="timed-reflect")Check(shot.thrownBy=="p0"&&shot.velocity.z>0&&e.stage=="parried","real parry did not reverse actual asset");
                            else if(d.id=="calm-dome")
                            {var hit=m.CombatEvents.LastOrDefault(x=>x.kind=="hit"&&x.targetId=="p0");Check(hit!=null&&Mathf.Abs(new Vector2(hit.impulse.x,hit.impulse.z).magnitude-2.45f)<.05f&&Mathf.Abs(hit.impulse.y-3)<.01f&&m.Items.Count(x=>x.ownerId=="p0"&&x.state==ItemState.Ground)==1,"real dome changed damage/drop/vertical force");}
                            else if(d.id=="wind-deflector")Check(shot.velocity.x< -5&&shot.thrownBy=="p1"&&FlatDistance(before,a.position)<.03f,"real deflector pushed person or failed to bend shot");
                            else Check(shot.state==ItemState.Ground&&a.stability==100&&m.CombatEvents.Count(x=>x.kind=="party_block")==1,"real plane did not intercept");
                            Check(shot.ownerId=="p1"&&m.Remaining("p1")==4,"real interception changed source ownership/lives");
                        }
                        if(d.id=="ground-anchor")
                        {
                            Place(m,b,a.position+Vector3.back*.85f);PartyFieldTests.Input(m,"p1","grab");PartyFieldTests.Tick(m,.24f);Check(b.grabbing=="p0","real grip did not connect before dragging");before=a.position;PartyFieldTests.Tick(m,.3f,"p1",0,-1);
                            Check(FlatDistance(before,a.position)<.3f&&b.grabbing=="p0","real anchor grip was unstable travel="+FlatDistance(before,a.position)+" grabbing="+b.grabbing+" events="+string.Join(",",m.CombatEvents.Select(x=>x.kind))+" anchored="+m.GuardAnchored(a));
                            PartyFieldTests.Input(m,"p0","cancel");Check(!m.GuardAnchored(a),"real Q did not stop brace");
                            PartyFieldTests.Tick(m,.75f,"p1",0,-1);Check(FlatDistance(before,a.position)>.3f,"real drag did not resume after releasing brace");
                        }
                        if(d.id=="impact-bank")
                        {
                            m.Hit(a,b,20,4,false);Check(a.stability==80&&e.stage=="banked"&&e.force<=6,"real bank removed damage or failed to charge");
                            PartyFieldTests.Tick(m,.45f);Place(m,b,a.position+Vector3.forward*2);var targetBefore=b.position;
                            PartyFieldTests.Input(m,"p0","use_item");PartyFieldTests.Tick(m,.24f);
                            Check(!m.Effects.Contains(e)&&b.stability==100&&FlatDistance(targetBefore,b.position)>.05f&&m.CombatEvents.Count(x=>x.kind=="party_bank_return")==1,"real bank did not return single bounded impulse");
                        }
                        if(d.id=="sponge-dry")Check(wet.path.Length==1&&e.chainIndex==2&&m.Effects.Contains(glue)&&m.Remaining("p2")==4,"real drying removed wrong zone/life");
                        float peak=m.Players.Values.SelectMany(p=>m.BodyWorld.Get(p).Bodies).Max(x=>x.velocity.magnitude);
                        Check(peak<25&&m.View("p0").players.All(p=>p.bodyPose.positions.Length==15)&&m.Players.Keys.All(id=>m.Remaining(id)==4),"body velocity/pose/life invalid");
                        Debug.Log("GUARD_REAL_PHYSICS "+d.id+" defenderTravel="+FlatDistance(before,a.position).ToString("F3")+" peak="+peak.ToString("F3")+" stability="+a.stability);
                        m.Finish("timeout");Check(!m.Effects.Any(x=>x.kind=="PartyMechanic"),"finish retained guard");passed++;
                    }
                    catch(Exception ex){failures.Add(d.id+": "+ex.Message);Debug.LogError("GUARD_REAL_PHYSICS_FAIL "+d.id+": "+ex);}
                }
            }
            finally{Cache.SetValue(null,old);}
            if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("GUARD_REAL_PHYSICS_PASSED "+passed+" mechanisms / actual 15-body rigs / real Persona owner and life conservation");
        }
    }
}
