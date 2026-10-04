using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class PartyLinkPhysicsTests
    {
        static void Check(bool ok,string why)=>PartyLinkTests.Check(ok,why);
        public static void Regression(){if(!Application.isPlaying){SessionState.SetString("V4Job","link-regression");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}Validate();PartyVirtualPhysicsTests.Regression();}
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","links");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=PartyLinkTests.Cache.GetValue(null);var failures=new System.Collections.Generic.List<string>();int passed=0;
            try{foreach(var d in PartyMechanicRegistry.All.Where(d=>d.family=="link"))
            {
                PartyLinkTests.Cache.SetValue(null,PartyControlTests.Flat);using(var m=PartyFieldTests.Make(d.id))try
                {
                    if(d.id=="wall-cling")PartyLinkTests.Cache.SetValue(null,PartyControlTests.Flat.Concat(new[]{new CampusSolid{name="climb wall",center=new Vector3(0,2,1.1f),size=new Vector3(6,4,.2f)}}).ToArray());
                    if(d.id=="safety-tether")PartyLinkTests.Cache.SetValue(null,PartyControlTests.Flat.Concat(new[]{new CampusSolid{name="high platform",center=new Vector3(0,1.5f,0),size=new Vector3(2.8f,3,5)}}).ToArray());
                    m.EnableBodyPhysics();var a=m.Players["p0"];var b=m.Players["p1"];var c=m.Players["p2"];foreach(var p in m.Players.Values)PartyControlTests.Place(m,p,p.position);if(d.id=="safety-tether")PartyControlTests.Place(m,a,new Vector3(0,4,0));PartyFieldTests.Tick(m,.6f);EffectState e;try{e=PartyLinkTests.Deploy(m,d.id);}catch{Debug.LogError("LINK_DEPLOY_FAILED "+d.id+" p="+a.position+" motor="+a.motorHeight+" events="+string.Join(" / ",m.Events.Select(x=>x.text)));throw;}
                    if(d.id=="zipline-build")
                    {PartyControlTests.Place(m,b,e.path[0]+Vector3.up*.85f);PartyFieldTests.Input(m,"p1","interact");PartyFieldTests.Tick(m,.45f);Check(b.linkRide==e.id&&b.linkProgress>.3f&&b.linkProgress<.8f&&b.position.x>1.2f&&b.position.y>1,"real continuous ride path "+b.position+" progress="+b.linkProgress);PartyFieldTests.Input(m,"p1","cancel");PartyFieldTests.Tick(m,.1f);Check(b.linkRide==""&&m.Effects.Contains(e),"real Q ride release");}
                    if(d.id=="wall-cling")
                    {float y=e.height;PartyFieldTests.Tick(m,.3f,"p0",1,0);Check(a.position.x>.3f&&Mathf.Abs(a.motorHeight-y)<.03f&&Mathf.Abs(a.position.y-y)<.4f,"real wall control/hold "+a.position+" height="+y);PartyFieldTests.Input(m,"p0","jump");PartyFieldTests.Tick(m,.12f);Check(!m.Effects.Contains(e)&&a.airborne,"real wall jump release");}
                    if(d.id=="safety-tether")
                    {PartyControlTests.Place(m,a,new Vector3(1.8f,4,0));a.airborne=true;a.fallOriginHeight=3;PartyFieldTests.Tick(m,.65f);Check(a.position.x<.1f&&a.motorHeight>3.9f&&m.CombatEvents.Count(x=>x.kind=="party_tether_rescue")==1&&m.Items.Count(x=>x.ownerId=="p0"&&x.state==ItemState.Ground)==1,"real anchor rescue/fall cost "+a.position+" floor="+a.motorHeight);}
                    if(d.id=="item-scale")
                    {var pan=Match.MachineShapes(e).First();var life=PartyFieldTests.Ground(m,pan.center+Vector3.up*.4f);PartyControlTests.Place(m,b,pan.center+Vector3.up*1.05f);float y=b.motorHeight;PartyFieldTests.Tick(m,.45f);Check(m.BodyWorld.MachineColliderCount==2&&e.machine.height>.2f&&b.motorHeight<y-.2f&&life.position.y<.8f,"real scale lowering rider+life "+b.position+" motor="+b.motorHeight+" life="+life.position+" height="+e.machine.height);PartyControlTests.Place(m,c,life.position+Vector3.up*.65f+Vector3.back*.6f);PartyFieldTests.Input(m,"p2","interact");Check(life.state==ItemState.Carried&&c.interaction=="recover","real scale cannot recover own life");}
                    if(d.id=="rope-bridge")
                    {var start=e.path[0]+Vector3.up*1.05f;PartyControlTests.Place(m,b,start);b.yaw=Mathf.Atan2(e.velocity.x,e.velocity.z)*Mathf.Rad2Deg;PartyFieldTests.Tick(m,.4f,"p1",e.velocity.x,e.velocity.z);Check(m.BodyWorld.MachineColliderCount==6&&b.motorHeight>1.1f&&Vector3.Distance(b.position,start)>1,"real bridge walk/support "+b.position+" motor="+b.motorHeight);}
                    if(d.id=="public-clamp")
                    {var box=m.Props[0];var oldPos=box.position;PartyVirtualTests.Auxiliary(m,"fan-volume",box.position-Vector3.forward);PartyFieldTests.Tick(m,.4f);Check(box.position==oldPos&&m.BodyWorld.MachineColliderCount==0,"real clamp moved target or made actor wall");box.holder="p1";b.publicProp=box.id;PartyFieldTests.Tick(m,.02f);Check(!m.Effects.Contains(e)&&m.Props.Contains(box),"real pickup detach");}
                    if(d.id=="projectile-fork")
                    {PartyGuardTests.Shot(m,e.position+Vector3.up*.85f+Vector3.forward*.1f,Vector3.back*10);PartyFieldTests.Tick(m,.02f);var shots=m.Effects.Where(x=>x.kind=="Projectile").ToArray();Check(shots.Length==2&&shots.Sum(x=>x.damage)==10&&shots.Sum(x=>x.force)==4&&shots.All(x=>x.owner=="p1"),"real fork conservation");}
                    if(d.id=="shot-vault")
                    {var shot=PartyGuardTests.Shot(m,e.position+Vector3.up*.85f+Vector3.forward*.1f,Vector3.back*10);PartyFieldTests.Tick(m,.04f);Check(e.stage=="stored"&&!m.Effects.Contains(shot),"real vault capture");PartyControlTests.Place(m,c,e.position+Vector3.up*.85f+Vector3.left*.7f);c.yaw=90;PartyFieldTests.Input(m,"p2","interact");PartyFieldTests.Tick(m,.04f);Check(m.Effects.Contains(shot)&&shot.owner=="p1"&&shot.velocity.x>9.9f&&e.stage=="spent","real manual release");}
                    if(d.id=="public-twin-rope")
                    {var first=m.Props[0];var second=m.Props[1];first.position+=Vector3.left;PartyFieldTests.Tick(m,.02f);Check(first.velocity.x>0&&second.velocity.x<0&&Mathf.Abs(first.velocity.x+second.velocity.x)<.001f,"real reciprocal public force");first.holder="p1";b.publicProp=first.id;PartyControlTests.Place(m,b,new Vector3(-1.5f,1,1));float x=second.position.x;PartyFieldTests.Tick(m,.35f);Check(second.position.x<x-.05f&&m.Effects.Contains(e),"held box did not drag other "+second.position);}
                    if(d.id=="consent-swap")
                    {PartyControlTests.Place(m,b,e.position+Vector3.left*1.3f+Vector3.up*.85f);PartyControlTests.Place(m,c,e.position+Vector3.right*1.3f+Vector3.up*.85f);float left=b.position.x,right=c.position.x;PartyFieldTests.Input(m,"p1","interact");PartyFieldTests.Input(m,"p2","interact");PartyFieldTests.Tick(m,.52f);Check(e.stage=="spent"&&Mathf.Abs(b.position.x-right)<.12f&&Mathf.Abs(c.position.x-left)<.12f&&m.CombatEvents.Count(x=>x.kind=="party_consent_swap")==1,"real swap did not reset bodies "+e.stage+" b="+b.position+" c="+c.position);}
                    float peak=m.Players.Values.SelectMany(p=>m.BodyWorld.Get(p).Bodies).Max(x=>x.velocity.magnitude);Check(peak<25&&m.View("p0").players.All(p=>p.bodyPose.positions.Length==15),"real rig runaway "+peak);Check(m.Players.Keys.All(id=>m.Remaining(id)==4),"link lost/created life");m.Finish("timeout");Check(!m.Effects.Any(x=>x.kind=="PartyMechanic")&&m.BodyWorld.MachineColliderCount==0&&m.Players.Values.All(p=>p.linkRide==""),"link finish leak");passed++;Debug.Log("LINK_REAL_PHYSICS_PASS "+d.id+" peak="+peak.ToString("F3"));
                }catch(Exception ex){failures.Add(d.id+": "+ex.Message);Debug.LogError("LINK_REAL_PHYSICS_FAIL "+d.id+": "+ex);}
            }}finally{PartyLinkTests.Cache.SetValue(null,old);}if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("LINK_REAL_PHYSICS_PASSED "+passed+" link mechanisms / real 15-body rigs / authoritative public-prop motion / original life conservation");
        }
    }
}
