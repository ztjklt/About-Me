using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class PartyVirtualPhysicsTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static void Check(bool ok,string why)=>PartyVirtualTests.Check(ok,why);
        public static void Regression(){if(!Application.isPlaying){SessionState.SetString("V4Job","virtual-regression");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}Validate();PartyControlPhysicsTests.Regression();}
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","virtual");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);int passed=0;var failures=new System.Collections.Generic.List<string>();
            try{foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="virtual"))
            {
                Cache.SetValue(null,PartyControlTests.Flat);using(var m=PartyFieldTests.Make(d.id))try
                {
                    m.EnableBodyPhysics();foreach(var p in m.Players.Values)PartyControlTests.Place(m,p,p.position);PartyFieldTests.Tick(m,.6f);var a=m.Players["p0"];var b=m.Players["p1"];EffectState e;
                    if(d.id=="shot-portal")
                    {e=PartyVirtualTests.PortalPair(m);var shot=PartyGuardTests.Shot(m,e.position+Vector3.up*.85f+Vector3.forward*.3f,Vector3.back*10);PartyFieldTests.Tick(m,.1f);Check(shot.position.x>7&&shot.velocity.x>9.9f&&m.CombatEvents.Count(x=>x.kind=="party_shot_portal")==1,"real portal route");}
                    else e=PartyVirtualTests.Deploy(m,d.id);
                    if(d.id=="pixel-stamp")
                    {PartyControlTests.Place(m,b,e.position+Vector3.up*.85f);PartyFieldTests.Tick(m,.18f);Check(b.airborne&&b.motorHeight>1.25f&&m.CombatEvents.Any(x=>x.kind=="party_pixel_hop"),"real grounded jump failed "+b.motorHeight);}
                    if(d.id=="vector-arrow")
                    {var shot=PartyGuardTests.Shot(m,e.position+Vector3.up*.85f+Vector3.right*1.5f,Vector3.left*10);PartyFieldTests.Tick(m,.06f);Check(shot.velocity.z>9.9f,"real vector turn");}
                    if(d.id=="quantum-echo")
                    {PartyFieldTests.Tick(m,.8f,"p0",1,0);var box=new PublicProp{id="echo-box",visual="box",position=new Vector3(1,.6f,0)};m.Props.Add(box);PartyFieldTests.Tick(m,.9f);Check(box.position.x>1.1f&&m.CombatEvents.Any(x=>x.kind=="party_echo_push")&&e.path.Length<=7,"real echo/record movement");}
                    if(d.id=="light-lattice")
                    {var box=new PublicProp{id="box",visual="box",position=e.position+Vector3.up*.45f+Vector3.forward*.2f,velocity=Vector3.back*8};m.Props.Add(box);PartyControlTests.Place(m,b,e.position+Vector3.up*.85f+Vector3.right);PartyFieldTests.Tick(m,.15f,"p1",0,-1);Check(box.position.z>=e.position.z&&b.position.z<e.position.z-.3f&&m.BodyWorld.MachineColliderCount==0,"lattice blocked real actor or passed box");}
                    if(d.id=="virtual-gravity")
                    {var box=new PublicProp{id="box",visual="box",position=e.position+Vector3.up*.45f};m.Props.Add(box);PartyControlTests.Place(m,b,e.position+Vector3.up*.85f+Vector3.right*1.5f);float z=b.position.z;PartyFieldTests.Tick(m,.4f);Check(box.position.z>e.position.z+.2f&&Mathf.Abs(b.position.z-z)<.06f&&box.velocity.magnitude<=6,"gravity affected body/filter");}
                    if(d.id=="solid-shadow")
                    {PartyControlTests.Place(m,b,e.position+Vector3.up*1.35f);PartyFieldTests.Tick(m,.5f);Check(m.BodyWorld.MachineColliderCount==1&&b.motorHeight>1.45f&&b.position.y>1.42f,"real board support "+b.position+" motor="+b.motorHeight);e.deadline=m.Time+.85f;PartyFieldTests.Tick(m,.12f);Check(e.stage=="shadow_fading","real board no warning");PartyFieldTests.Tick(m,1.1f);Check(!m.Effects.Contains(e)&&m.BodyWorld.MachineColliderCount==0&&b.motorHeight<1.1f,"real board expiry fall "+b.position);}
                    if(d.id=="weather-vane")
                    {var table=m.Effects.First(x=>x.id==e.targetId);var fan=PartyVirtualTests.Auxiliary(m,"fan-volume",e.position-Vector3.forward*1.5f);PartyControlTests.Place(m,b,table.position+Vector3.up*1.03f+Vector3.right*.7f);var before=b.position;PartyFieldTests.Tick(m,.3f);Check(table.machine.angle>10&&Mathf.Abs(b.position.z-before.z)>.08f&&m.BodyWorld.MachineColliderCount==1,"real wind powered platform carry angle="+table.machine.angle+" body="+b.position);m.Effects.Remove(fan);float angle=table.machine.angle;PartyFieldTests.Tick(m,.15f);Check(Mathf.Abs(table.machine.angle-angle)<.01f,"real wind did not stop");}
                    if(d.id=="heartbeat-tempo")
                    {var lift=PartyVirtualTests.Auxiliary(m,"floor-elevator",e.position+Vector3.right*2.35f);PartyControlTests.Place(m,b,PartyMachineryTests.Button(lift)+Vector3.up*.85f);m.Time=e.createdAt+.6f;PartyFieldTests.Input(m,"p1","interact");Check(!lift.machine.used,"real offbeat accepts");m.Time=e.createdAt+1.41f;PartyFieldTests.Input(m,"p1","interact");PartyFieldTests.Tick(m,.3f);Check(lift.machine.used&&lift.machine.height>.1f,"real beat elevator not moving");}
                    if(d.id=="frost-lock")
                    {PartyControlTests.Place(m,a,new Vector3(-4,1,0));PartyControlTests.Place(m,b,new Vector3(0,1,0));PartyFieldTests.Tick(m,.2f,"p1",1,0);float x=b.position.x;PartyFieldTests.Tick(m,.2f);Check(b.position.x>x+.04f&&b.fieldStatus.Contains("凝结"),"real ice inertia x="+x+" position="+b.position+" motor="+b.motorHeight+" field="+b.fieldStatus+" ice="+string.Join("/",e.path.Select(v=>v.ToString()))+" floor="+m.SurfaceFloor(b.position));PartyFieldTests.Input(m,"p1","jump");PartyFieldTests.Tick(m,.1f);Check(b.airborne&&b.fieldStatus=="","real ice jump counter");}
                    var max=m.Players.Values.SelectMany(p=>m.BodyWorld.Get(p).Bodies).Max(x=>x.velocity.magnitude);Check(max<25&&m.View("p0").players.All(p=>p.bodyPose.positions.Length==15),"rig runaway "+max);Check(m.Players.Keys.All(id=>m.Remaining(id)==4),"life duplicated/lost");m.Finish("timeout");Check(!m.Effects.Any(x=>x.kind=="PartyMechanic")&&m.BodyWorld.MachineColliderCount==0,"finish leaked");passed++;Debug.Log("VIRTUAL_REAL_PHYSICS_PASS "+d.id+" peak="+max.ToString("F3"));
                }catch(Exception ex){failures.Add(d.id+": "+ex.Message);Debug.LogError("VIRTUAL_REAL_PHYSICS_FAIL "+d.id+": "+ex);}
            }}finally{Cache.SetValue(null,old);}if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("VIRTUAL_REAL_PHYSICS_PASSED "+passed+" mechanisms / real 15-body rigs / original life conservation");
        }
    }
}
