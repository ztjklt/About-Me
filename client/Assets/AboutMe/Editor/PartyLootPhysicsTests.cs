using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class PartyLootPhysicsTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        static void Place(Match m,Player p,Vector3 at){p.position=at;p.motorHeight=at.y;p.impulse=Vector3.zero;p.airborne=false;p.verticalSpeed=0;m.BodyWorld.Reset(p);}
        public static void Regression()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","loot-regression");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            Validate();PartyGuardPhysicsTests.Regression();
        }
        public static void Validate()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","loot");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);int passed=0;var failures=new System.Collections.Generic.List<string>();
            try
            {
                foreach(var d in PartyMechanicRegistry.All.Where(x=>x.family=="loot"))using(var m=PartyFieldTests.Make(d.id))
                {
                    Cache.SetValue(null,PartyLootTests.Flat);
                    try
                    {
                        m.EnableBodyPhysics();var a=m.Players["p0"];var b=m.Players["p1"];var c=m.Players["p2"];Place(m,a,new Vector3(0,1,0));Place(m,b,new Vector3(10,1,0));Place(m,c,new Vector3(-10,1,0));PartyFieldTests.Tick(m,.6f);
                        PersonaInstance item=null;
                        if(d.id=="own-item-beacon"||d.id=="loose-radar"||d.id=="tongs-place"||d.id=="rescue-tow"||d.id=="float-lift"||d.id=="carry-cradle")item=PartyLootTests.Loose(m,new Vector3(0,.35f,d.id=="rescue-tow"?4:d.id=="own-item-beacon"||d.id=="loose-radar"?3:1.3f),d.id=="rescue-tow"||d.id=="own-item-beacon"?"p0":"p2");
                        PublicProp pa=null,pb=null;
                        if(d.id=="mischief-swap-props"){pa=new PublicProp{id="a",visual="box",position=new Vector3(2,.6f,2)};pb=new PublicProp{id="b",visual="trash",position=new Vector3(-2,.6f,2)};m.Props.Add(pa);m.Props.Add(pb);}
                        float downTime=0;
                        if(d.id=="gentle-push-clear"){Place(m,b,new Vector3(0,.6f,1.5f));b.bodyMode=BodyMode.Grounded;b.bodyStateAt=m.Time;b.staggerUntil=m.Time+3;downTime=b.bodyStateAt;}
                        var e=d.id=="trip-line"?PartyLootTests.Line(m):PartyFieldTests.Deploy(m);
                        if(d.id=="own-item-beacon"||d.id=="loose-radar")Check(e.hit.Contains(item.id)&&m.View("p1").effects.All(x=>x.id!=e.id),"real private direction did not filter per recipient");
                        if(d.id=="tongs-place"){PartyFieldTests.Tick(m,.35f);Check(item.position.x>.5f&&item.state==ItemState.Ground,"real tongs did not place exact life beside caster");}
                        if(d.id=="rescue-tow"){PartyFieldTests.Tick(m,1.9f);Check(item.position.z<1.1f&&item.position.z>.5f&&item.state==ItemState.Ground,"real tow did not stop near caster "+item.position);}
                        if(d.id=="float-lift")
                        {PartyFieldTests.Tick(m,.12f);Check(item.position.y>.9f,"real float did not hold lift");Place(m,b,item.position+Vector3.back*.8f);PartyFieldTests.Input(m,"p1","interact");PartyFieldTests.Tick(m,2.6f);Check(item.state==ItemState.Consumed&&m.Eaten.Count==1&&m.Remaining("p2")==3,"real floating life could not be swallowed");}
                        if(d.id=="carry-cradle")
                        {var prop=m.Props.First();PartyFieldTests.Tick(m,.5f);Place(m,a,new Vector3(0,1,.35f));PartyFieldTests.Input(m,"p0","grab");PartyFieldTests.Tick(m,.22f);Check(prop.holder=="p0","real G did not lift cradle");var before=item.position;PartyFieldTests.Tick(m,.4f,"p0",1,0);Check(item.position.x>before.x+.2f&&item.state==ItemState.Ground,"real cradle failed to move original Ground cargo");PartyFieldTests.Input(m,"p0","cancel");PartyFieldTests.Tick(m,.04f);Check(prop.holder==""&&item.ownerId=="p2","real Q could not set cradle down");}
                        if(d.id=="public-hot-potato")
                        {var prop=m.Props.First();PartyFieldTests.Tick(m,.5f);Place(m,a,new Vector3(0,1,.35f));PartyFieldTests.Input(m,"p0","grab");PartyFieldTests.Tick(m,.6f);Check(prop.holder=="p0","real G did not take potato");Place(m,b,a.position+Vector3.forward*1.7f);b.yaw=180;PartyFieldTests.Input(m,"p0","use_item");PartyFieldTests.Tick(m,.18f);Check(e.stage=="offered"&&prop.holder=="p0","real potato forcibly transferred");PartyFieldTests.Input(m,"p1","grab");PartyFieldTests.Tick(m,.22f);Check(prop.holder=="p1","real voluntary handoff failed");PartyFieldTests.Tick(m,4.6f);Check(!m.Effects.Contains(e)&&!m.Props.Contains(prop)&&m.CombatEvents.Count(x=>x.kind=="party_burst")==1,"real fuse leaked or repeated explosion");}
                        if(d.id=="mischief-swap-props"){PartyFieldTests.Tick(m,.62f);Check(pa.position.x<0&&pb.position.x>0&&m.CombatEvents.Count(x=>x.kind=="party_prop_swap")==1,"real exchange did not switch two public boxes");}
                        if(d.id=="gentle-push-clear"){PartyFieldTests.Tick(m,.16f);Check(b.stability==100&&b.bodyStateAt==downTime&&m.CombatEvents.Count(x=>x.kind=="party_gentle_rescue")==1,"real rescue damaged/deferred recovery");}
                        if(d.id=="trip-line")
                        {Place(m,b,new Vector3(1.5f,1,.5f));for(int i=0;i<25;i++){foreach(var p in m.Players.Values)m.Receive(p.profile.playerId,new Command{sequence=p.sequence+1,yaw=0,z=p==b?1:0,run=p==b});m.Tick(.02f);}Check(b.stability==96&&m.CombatEvents.Count(x=>x.kind=="party_trip")==1,"real running crossing did not trip once pos="+b.position+" health="+b.stability);}
                        float peak=m.Players.Values.SelectMany(p=>m.BodyWorld.Get(p).Bodies).Max(x=>x.velocity.magnitude);Check(peak<25&&m.View("p0").players.All(x=>x.bodyPose.positions.Length==15),"invalid velocity or physical pose");
                        Check(m.Players.Keys.All(id=>m.Remaining(id)==(d.id=="float-lift"&&id=="p2"?3:4)),"loot duplicated original lives");
                        m.Finish("timeout");Check(!m.Effects.Any(x=>x.kind=="PartyMechanic")&&!m.Props.Any(x=>x.id.StartsWith("device-")),"finish retained loot device");passed++;Debug.Log("LOOT_REAL_PHYSICS_PASS "+d.id+" peak="+peak.ToString("F3"));
                    }
                    catch(Exception ex){failures.Add(d.id+": "+ex.Message);Debug.LogError("LOOT_REAL_PHYSICS_FAIL "+d.id+": "+ex);}
                }
            }
            finally{Cache.SetValue(null,old);}
            if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("LOOT_REAL_PHYSICS_PASSED "+passed+" mechanics / real 15-body rigs / exact life conservation and E/G/Q/F interaction");
        }
    }
}
