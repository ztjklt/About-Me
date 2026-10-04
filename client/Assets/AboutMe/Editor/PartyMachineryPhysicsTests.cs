using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class PartyMachineryPhysicsTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        public static void Regression()
        {if(!Application.isPlaying){SessionState.SetString("V4Job","machinery-regression");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}Validate();PartyLootPhysicsTests.Regression();}
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","machinery");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);int passed=0;var failures=new System.Collections.Generic.List<string>();
            try
            {
                foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="machinery"))
                {
                    Cache.SetValue(null,PartyMachineryTests.Flat);
                    using(var m=PartyFieldTests.Make(d.id))try
                    {
                        m.EnableBodyPhysics();var a=m.Players["p0"];var b=m.Players["p1"];var c=m.Players["p2"];PartyMachineryTests.Place(m,a,new Vector3(0,1,0));PartyMachineryTests.Place(m,b,new Vector3(10,1,0));PartyMachineryTests.Place(m,c,new Vector3(-10,1,0));PartyFieldTests.Tick(m,.6f);
                        var e=PartyFieldTests.Deploy(m);PartyFieldTests.Tick(m,.08f);var pivot=e.position;var button=PartyMachineryTests.Button(e);float top=(d.id=="turntable-wheel"||d.id=="balance-plank"||d.id=="floor-elevator"||d.id=="seesaw-flip")?PartyMachineryTests.Top(m,e):0;
                        if(d.id=="step-switch")
                        {PartyMachineryTests.Place(m,b,button+Vector3.up*.85f);var item=PartyFieldTests.Ground(m,pivot+Vector3.forward*2+Vector3.up*.2f);float z=item.position.z;PartyFieldTests.Tick(m,.35f);Check(e.machine.powered&&item.position.z>z+.15f,"real pressure fan did not move Ground life");PartyMachineryTests.Place(m,b,new Vector3(10,1,0));PartyFieldTests.Tick(m,.1f);Check(!e.machine.powered,"real fan retained signal");}
                        if(d.id=="weight-switch")
                        {PartyMachineryTests.Place(m,b,button+Vector3.up*.85f);PartyFieldTests.Tick(m,.12f);Check(!e.machine.powered,"real player powered object plate");var prop=new PublicProp{id="crate",visual="box",position=button+Vector3.up*.45f};m.Props.Add(prop);PartyFieldTests.Tick(m,.16f);Check(e.machine.powered&&m.BodyWorld.MachineColliderCount==0,"real door collider remained with powered plate");prop.position=new Vector3(10,.6f,6);PartyFieldTests.Tick(m,.16f);Check(!e.machine.powered&&m.BodyWorld.MachineColliderCount==1,"real door collider not restored");}
                        if(d.id=="timed-door")
                        {PartyMachineryTests.Place(m,b,pivot+Vector3.back*1.5f+Vector3.up*.85f);PartyFieldTests.Tick(m,.5f,"p1",0,1);Check(b.position.z<pivot.z-.45f&&m.BodyWorld.MachineColliderCount==1,"real closed door passed through");PartyMachineryTests.Place(m,b,button+Vector3.up*.85f);PartyFieldTests.Input(m,"p1","interact");PartyFieldTests.Tick(m,.1f);Check(e.machine.powered&&m.BodyWorld.MachineColliderCount==0,"real E did not disable solid door");PartyMachineryTests.Place(m,b,pivot+Vector3.up*.85f);PartyFieldTests.Tick(m,1.1f);Check(e.machine.powered&&m.BodyWorld.MachineColliderCount==0&&b.stability==100,"real closing door trapped/hit occupant");}
                        if(d.id=="turntable-wheel")
                        {PartyMachineryTests.Place(m,b,button+Vector3.up*.85f);PartyMachineryTests.Place(m,c,new Vector3(pivot.x+1,top+1,pivot.z));var item=PartyFieldTests.Ground(m,new Vector3(pivot.x+1.3f,top+.35f,pivot.z+.3f));PartyFieldTests.Tick(m,.12f);PartyFieldTests.Input(m,"p1","interact");float z=c.position.z;PartyFieldTests.Tick(m,.5f);Check(e.machine.angle>20&&Mathf.Abs(c.position.z-z)>.2f&&Mathf.Abs(item.position.z-pivot.z)>.2f,"real wheel failed to carry player and life "+c.position+" angle="+e.machine.angle+" operator="+e.machine.operatorId+" cargo="+item.position);PartyFieldTests.Input(m,"p1","cancel");float angle=e.machine.angle;PartyFieldTests.Tick(m,.1f);Check(Mathf.Abs(e.machine.angle-angle)<.01f,"real Q did not brake");}
                        if(d.id=="rotating-sweep")
                        {PartyMachineryTests.Place(m,b,pivot+Vector3.right+Vector3.up*.85f);PartyFieldTests.Tick(m,1.12f);Check(m.CombatEvents.Any(x=>x.kind=="party_sweep"&&x.targetId=="p1")&&b.stability<100&&m.BodyWorld.MachineColliderCount==1,"real sweep never made contact");}
                        if(d.id=="balance-plank")
                        {PartyMachineryTests.Place(m,b,new Vector3(pivot.x+1,top+1,pivot.z));PartyFieldTests.Tick(m,.4f);Check(e.machine.angle< -3&&Mathf.Abs(e.machine.angle)<=12&&b.position.x>pivot.x+.8f,"real plank did not respond to rider "+e.machine.angle+" body="+b.position);Check(m.SurfaceFloor(new Vector3(pivot.x+1,b.motorHeight,pivot.z))<m.SurfaceFloor(new Vector3(pivot.x-1,b.motorHeight+1,pivot.z)),"real tilt floor absent");}
                        if(d.id=="floor-elevator")
                        {PartyMachineryTests.Place(m,b,button+Vector3.up*.85f);PartyFieldTests.Input(m,"p1","interact");PartyMachineryTests.Place(m,b,new Vector3(pivot.x,top+1,pivot.z));var item=PartyFieldTests.Ground(m,new Vector3(pivot.x+.7f,top+.35f,pivot.z));PartyFieldTests.Tick(m,2.05f);Check(e.machine.height==1.2f&&b.motorHeight>2.2f&&item.position.y>1.5f&&m.BodyWorld.MachineColliderCount==1,"real elevator failed passenger/cargo "+b.position+" motor="+b.motorHeight+" cargo="+item.position);PartyFieldTests.Input(m,"p1","interact");Check(item.state==ItemState.Carried,"E could not reach real life on moving deck");PartyFieldTests.Tick(m,3f);Check(e.machine.height==0&&b.motorHeight<1.35f&&item.state==ItemState.Consumed&&m.Eaten.Count==1,"real lift did not return or finish original Eat");}
                        if(d.id=="rotary-gate")
                        {PartyMachineryTests.Place(m,b,pivot+Vector3.right+Vector3.up*.85f);var prop=new PublicProp{id="crate",visual="box",position=pivot+Vector3.left+Vector3.up*.45f};m.Props.Add(prop);PartyFieldTests.Tick(m,.24f,"p1",0,-1);Check(e.machine.angle>5&&m.BodyWorld.MachineColliderCount==2&&Mathf.Abs(prop.position.z-pivot.z)>.01f,"real turnstile did not transfer turn to other arm "+e.machine.angle+" prop="+prop.position);}
                        if(d.id=="wind-up-cart")
                        {var prop=m.Props.First();var source=m.Item(b.equipped);var target=new EffectState{id="lift",kind="PartyMechanic",mechanicId="floor-elevator",owner="p1",sourceInstance=source.id,velocity=Vector3.forward,position=prop.position+Vector3.forward*2+Vector3.right*2.35f-Vector3.up*.45f,deadline=m.Time+7,machine=new MachineState(),health=2};m.Effects.Add(target);PartyFieldTests.Tick(m,.5f);PartyFieldTests.Input(m,"p0","use_item");PartyFieldTests.Tick(m,1.2f);Check(target.machine.used&&e.machine.mode=="spent"&&m.CombatEvents.Count(x=>x.kind=="party_cart_button")==1,"real cart signal failed");}
                        if(d.id=="seesaw-flip")
                        {PartyMachineryTests.Place(m,b,new Vector3(pivot.x-1,top+1,pivot.z));m.Props.Add(new PublicProp{id="crate",visual="box",position=new Vector3(pivot.x+1.6f,top+.6f,pivot.z)});PartyFieldTests.Tick(m,.18f);Check(b.motorHeight>top+1.3f&&m.CombatEvents.Count(x=>x.kind=="party_seesaw_launch")==1,"real opposite weight did not launch rider "+b.position);}
                        float peak=m.Players.Values.SelectMany(p=>m.BodyWorld.Get(p).Bodies).Max(x=>x.velocity.magnitude);Check(peak<25&&m.View("p0").players.All(x=>x.bodyPose.positions.Length==15),"invalid real rigid body pose/velocity");Check(m.Players.Keys.All(id=>m.Remaining(id)==(d.id=="floor-elevator"&&id=="p2"?3:4)),"machinery duplicated/consumed original life");m.Finish("timeout");Check(!m.Effects.Any(x=>x.kind=="PartyMechanic")&&!m.Props.Any(x=>x.id.StartsWith("device-"))&&m.BodyWorld.MachineColliderCount==0,"finish leaked machine");passed++;Debug.Log("MACHINERY_REAL_PHYSICS_PASS "+d.id+" peak="+peak.ToString("F3"));
                    }
                    catch(Exception ex){failures.Add(d.id+": "+ex.Message);Debug.LogError("MACHINERY_REAL_PHYSICS_FAIL "+d.id+": "+ex);}
                }
            }
            finally{Cache.SetValue(null,old);}if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("MACHINERY_REAL_PHYSICS_PASSED "+passed+" mechanisms / real moving colliders and 15-body rigs / original life conservation");
        }
    }
}
