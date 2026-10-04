using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class PartyControlPhysicsTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        public static void Regression()
        {if(!Application.isPlaying){SessionState.SetString("V4Job","control-regression");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}Validate();PartyMachineryPhysicsTests.Regression();}
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","control");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);var failures=new System.Collections.Generic.List<string>();int passed=0;
            try
            {
                foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="control"))
                {
                    Cache.SetValue(null,PartyControlTests.Flat);
                    using(var m=PartyFieldTests.Make(d.id))try
                    {
                        m.EnableBodyPhysics();var a=m.Players["p0"];var b=m.Players["p1"];var c=m.Players["p2"];PartyControlTests.Place(m,a,new Vector3(0,1,0));PartyControlTests.Place(m,b,new Vector3(10,1,0));PartyControlTests.Place(m,c,new Vector3(-10,1,0));PartyFieldTests.Tick(m,.6f);EffectState e=null;
                        if(d.id=="lasso-loop")
                        {PartyControlTests.Place(m,b,new Vector3(0,1,3));e=PartyFieldTests.Deploy(m);float z=a.position.z;PartyFieldTests.Tick(m,.4f,"p1",1,0);Check(b.position.x>.4f&&a.position.z>z+.01f&&m.Effects.Contains(e),"real reciprocal rope locked/not pulled a="+a.position+" b="+b.position);PartyControlTests.Escape(m,"p1",.72f);Check(!m.Effects.Contains(e),"real held Q failed");}
                        if(d.id=="break-net")
                        {e=PartyControlTests.Attached(m,"net");var start=b.position;PartyFieldTests.Tick(m,.7f,"p1",1,0);float distance=Vector2.Distance(new Vector2(b.position.x,b.position.z),new Vector2(e.position.x,e.position.z));Check(b.position.x>start.x+.3f&&distance<1.35f,"real net boundary drift "+distance+" body="+b.position);PartyControlTests.Escape(m,"p1",.72f);Check(!m.Effects.Contains(e),"real net Q failed");}
                        if(d.id=="bubble-lift")
                        {e=PartyControlTests.Attached(m,"bubble");float y=b.motorHeight;PartyFieldTests.Tick(m,.45f,"p1",1,0);Check(b.position.x>.7f&&b.motorHeight>y+.25f&&b.motorHeight<e.height+.73f&&b.stability==100,"real bubble failed height/drift "+b.position+" motor="+b.motorHeight+" base="+e.height);PartyControlTests.Escape(m,"p1",.72f);Check(!m.Effects.Contains(e),"real bubble Q failed");}
                        if(d.id=="gapped-cage")
                        {e=PartyFieldTests.Deploy(m);PartyControlTests.Place(m,b,e.position+Vector3.up*.85f);PartyFieldTests.Tick(m,.5f,"p1",1,0);Check(b.position.x<e.position.x+1.2f&&m.BodyWorld.MachineColliderCount==3,"real cage side wall absent "+b.position);PartyControlTests.Place(m,b,e.position+Vector3.up*.85f);PartyFieldTests.Tick(m,.6f,"p1",0,1);Check(b.position.z>e.position.z+2&&b.stability==100,"real cage front gap blocked");}
                        if(d.id=="water-jet")
                        {PartyControlTests.Place(m,b,new Vector3(0,1,2.6f));var life=PartyFieldTests.Ground(m,new Vector3(.2f,.35f,3));e=PartyFieldTests.Deploy(m);float z=b.position.z,az=a.position.z,lz=life.position.z;PartyFieldTests.Tick(m,.4f);Check(b.position.z>z+.12f&&a.position.z<az-.04f&&life.position.z>lz+.12f&&b.stability==100&&life.ownerId=="p2","real jet/recoil failed a="+a.position+" b="+b.position+" ground="+life.position);PartyFieldTests.Input(m,"p0","cancel");Check(!m.Effects.Contains(e),"real water Q failed");}
                        if(d.id=="team-sling")
                        {e=PartyFieldTests.Deploy(m);PartyControlTests.Place(m,b,e.position+Vector3.up*.85f);PartyFieldTests.Tick(m,.25f);Check(!b.airborne&&e.stage=="ready","real forced lift");PartyFieldTests.Input(m,"p1","interact");PartyFieldTests.Tick(m,.65f);Check(m.CombatEvents.Count(x=>x.kind=="party_team_launch")==1&&b.motorHeight>1.5f,"real consent launch failed stage="+e.stage+" body="+b.position+" motor="+b.motorHeight);}
                        if(d.id=="body-bank")
                        {Cache.SetValue(null,PartyControlTests.Flat.Concat(new[]{new CampusSolid{name="bank_wall",center=new Vector3(0,1,3.1f),size=new Vector3(5,2,.2f)}}).ToArray());m.Dispose();using(var n=PartyFieldTests.Make(d.id)){n.EnableBodyPhysics();var rider=n.Players["p0"];PartyControlTests.Place(n,rider,new Vector3(0,1,0));PartyFieldTests.Tick(n,.15f,"p0",0,1);PartyFieldTests.Input(n,"p0","use_item",0,1);PartyFieldTests.Tick(n,.8f,"p0",0,1);Check(n.CombatEvents.Count(x=>x.kind=="party_body_reflect")==1&&n.Players.Values.All(p=>n.Remaining(p.profile.playerId)==4),"real one collision reflection missing");var peak=n.Players.Values.SelectMany(p=>n.BodyWorld.Get(p).Bodies).Max(x=>x.velocity.magnitude);Check(peak<25,"real reflected body runaway");n.Finish("timeout");}passed++;Debug.Log("CONTROL_REAL_PHYSICS_PASS body-bank");continue;}
                        if(d.id=="momentum-pass")
                        {var prop=new PublicProp{id="crate",visual="box",position=new Vector3(0,.6f,3)};m.Props.Add(prop);e=PartyFieldTests.Deploy(m);PartyFieldTests.Tick(m,.4f);Check(prop.position.z>3.3f&&m.CombatEvents.Count(x=>x.kind=="party_momentum_transfer")==1&&e.velocity.magnitude<2,"real scene transfer missing");}
                        if(d.id=="skate-arc")
                        {e=PartyFieldTests.Deploy(m);PartyFieldTests.Tick(m,.3f,"p0",1,0);Check(a.position.z>.4f&&e.velocity.z>e.velocity.x&&e.velocity.x>0,"real arc instant turned "+a.position);PartyFieldTests.Input(m,"p0","cancel");float z=a.position.z;PartyFieldTests.Tick(m,.18f);Check(!m.Effects.Contains(e)&&a.position.z-z<.3f,"real Q brake coast excessive");}
                        if(d.id=="anchor-swing")
                        {m.Props.Add(new PublicProp{id="anchor",visual="box",position=new Vector3(0,.6f,3)});e=PartyFieldTests.Deploy(m);float radius=e.radius;PartyFieldTests.Tick(m,.6f,"p0",1,0);float dist=Vector2.Distance(new Vector2(a.position.x,a.position.z),new Vector2(0,3));Check(a.position.x>.5f&&Mathf.Abs(dist-radius)<.3f,"real swing radius drift "+dist+" desired="+radius);m.Props[0].holder="p1";b.publicProp="anchor";PartyFieldTests.Tick(m,.04f);Check(!m.Effects.Contains(e),"real moved anchor did not break");}
                        float max=m.Players.Values.SelectMany(p=>m.BodyWorld.Get(p).Bodies).Max(x=>x.velocity.magnitude);Check(max<25&&m.View("p0").players.All(x=>x.bodyPose.positions.Length==15),"invalid real rig/velocity "+max);Check(m.Players.Keys.All(id=>m.Remaining(id)==4),"control consumed/duplicated life");m.Finish("timeout");Check(m.BodyWorld.MachineColliderCount==0&&!m.Effects.Any(x=>x.kind=="PartyMechanic"),"finish leaked control/collider");passed++;Debug.Log("CONTROL_REAL_PHYSICS_PASS "+d.id+" peak="+max.ToString("F3"));
                    }
                    catch(Exception ex){failures.Add(d.id+": "+ex.Message);Debug.LogError("CONTROL_REAL_PHYSICS_FAIL "+d.id+": "+ex);}
                }
            }
            finally{Cache.SetValue(null,old);}if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("CONTROL_REAL_PHYSICS_PASSED "+passed+" mechanisms / real 15-body rigs / original life conservation");
        }
    }
}
