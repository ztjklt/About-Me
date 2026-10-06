using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class CombatImpactTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static readonly CampusSolid[] Floor={new CampusSolid{name="impact_floor",center=new Vector3(0,-.5f,0),size=new Vector3(80,1,64)}};
        static void Check(bool value,string why){if(!value)throw new Exception(why);}
        static int Drops(Match m)=>m.Items.Count(x=>x.ownerId=="p1"&&x.state==ItemState.Ground);
        static void Place(Match m,Player p,Vector3 position)
        {p.position=position;p.motorHeight=position.y;p.verticalSpeed=0;p.airborne=false;p.impulse=Vector3.zero;p.velocity=Vector3.zero;m.BodyWorld?.Reset(p);}
        static Match Basic(string template,string visual,bool physics=false)
        {
            var m=PartyFieldTests.Make("flame-fan");m.Item(m.Players["p0"].equipped).asset.templateId=template;m.Item(m.Players["p0"].equipped).asset.visualId=visual;
            if(physics)m.EnableBodyPhysics();
            Place(m,m.Players["p0"],new Vector3(0,1,0));Place(m,m.Players["p1"],new Vector3(0,1,1.6f));Place(m,m.Players["p2"],new Vector3(10,1,0));return m;
        }
        static void Use(Match m,float duration=.22f){PartyFieldTests.Input(m,"p0","use_item");PartyFieldTests.Tick(m,duration);}
        public static void Run()
        {
            var old=Cache.GetValue(null);int passed=0;var failures=new List<string>();
            void Test(string name,Action run){Cache.SetValue(null,Floor);try{run();passed++;Debug.Log("IMPACT_PASS "+name);}catch(Exception e){failures.Add(name+": "+e.Message);Debug.LogError("IMPACT_FAIL "+name+": "+e);}}
            try
            {
                Test("ordinary F melee is a light action; two hits drop exactly one",()=>{using(var m=Basic("Melee","book")){Place(m,m.Players["p1"],new Vector3(0,1,2.6f));Use(m);Check(Drops(m)==0&&m.Players["p1"].stability==40&&m.Players["p0"].actionKind=="light","first hit is not 60-point light attack");PartyFieldTests.Tick(m,.4f);Place(m,m.Players["p0"],Vector3.up);Place(m,m.Players["p1"],new Vector3(0,1,1.6f));Use(m);Check(Drops(m)==1&&m.BodyDown(m.Players["p1"]),"second melee failed to knock down/drop");m.Hit(m.Players["p1"],m.Players["p0"],100,7,true);Check(Drops(m)==1&&m.Remaining("p1")==4,"same fall lost extra life");}});
                foreach(var template in new[]{"HeavyWeapon","Utility","SpecialAbility"})
                    Test(template+" one F hit drops a full-stability victim",()=>{using(var m=Basic(template,template=="HeavyWeapon"?"computer":template=="Utility"?"camera":"robot")){Use(m);Check(Drops(m)==1&&m.BodyDown(m.Players["p1"]),"did not drop on one hit");}});
                foreach(var entry in new[]{new[]{"Projectile","basketball"},new[]{"Disc","school-disc"},new[]{"Ricochet","bowling-ball"},new[]{"Deployable","plant"},new[]{"Summon","cat"}})
                    Test(entry[0]+" throws original life and drops target once",()=>{using(var m=Basic(entry[0],entry[1])){string source=m.Players["p0"].equipped;Use(m,.5f);Check(Drops(m)==1&&m.Item(source).state==ItemState.Ground&&m.Players["p0"].equipped==""&&m.Remaining("p0")==4,"throw failed or duplicated source");}});
                Test("public box throw knocks down and drops once",()=>{using(var m=Basic("Melee","book")){var p=m.Players["p0"];m.Props.Add(new PublicProp{id="box",visual="box",holder="p0",position=p.position});p.publicProp="box";Use(m,.35f);Check(Drops(m)==1&&p.publicProp=="","public throw did not deplete");}});
                Test("barricade contact drops once and cannot farm another after protection",()=>{using(var m=Basic("Barricade","book-stack")){Place(m,m.Players["p1"],new Vector3(8,1,0));Use(m);var e=m.Effects.Single(x=>x.kind=="Barricade");Place(m,m.Players["p1"],new Vector3(e.position.x,1,e.position.z-.9f));PartyFieldTests.Tick(m,.2f,"p1",0,1);Check(Drops(m)==1&&e.hit.Contains("p1"),"walking into barrier did not trigger");var p=m.Players["p1"];p.bodyMode=BodyMode.Standing;p.bodyProtectedUntil=0;p.stability=100;p.staggerUntil=0;Place(m,p,new Vector3(e.position.x,1,e.position.z-.9f));PartyFieldTests.Tick(m,.2f,"p1",0,1);Check(Drops(m)==1,"same barrier duplicated drop");}});
                foreach(var id in new[]{"wet-trail","sand-sink","sticky-step","frost-lock"})
                    Test(id+" ground contact, protection, owner and jump boundaries",()=>{using(var m=PartyFieldTests.Make(id)){EffectState e;if(id=="frost-lock"){var wet=new EffectState{id="wet",kind="PartyMechanic",mechanicId="wet-trail",owner="p2",sourceInstance=m.Players["p2"].equipped,path=new[]{new Vector3(0,.15f,1.8f)},deadline=m.Time+5};m.Effects.Add(wet);}e=PartyFieldTests.Deploy(m);var point=id=="wet-trail"||id=="frost-lock"?e.path[0]:e.position;var b=m.Players["p1"];Place(m,b,new Vector3(point.x,1,point.z));b.bodyProtectedUntil=m.Time+.2f;PartyFieldTests.Tick(m,.1f);Check(Drops(m)==0&&!e.hit.Contains("p1"),"protection consumed trigger");b.bodyProtectedUntil=0;PartyFieldTests.Tick(m,.02f);Check(Drops(m)==1&&e.hit.Count(x=>x=="p1")==1&&m.Remaining("p0")==4,"ground trigger/drop failed");var c=m.Players["p2"];Place(m,c,new Vector3(point.x,3,point.z));c.airborne=true;c.verticalSpeed=2;PartyFieldTests.Tick(m,.1f);Check(!e.hit.Contains("p2"),"hazard hit airborne target");}});
                Test("flame two 60-point pulses drop once; shield/cushion counters still work",()=>{using(var m=PartyFieldTests.Make("flame-fan")){Place(m,m.Players["p1"],new Vector3(0,1,2));PartyFieldTests.Deploy(m);PartyFieldTests.Tick(m,.8f);Check(Drops(m)==1&&m.CombatEvents.Count(x=>x.kind=="party_flame")==2,"flame failed to drop");}using(var m=Basic("HeavyWeapon","computer")){m.Players["p1"].cushionUntil=m.Time+1;Use(m);Check(Drops(m)==0&&m.Players["p1"].stability==100,"cushion no longer counters heavy hit");}});
                Test("walls and get-up protection still reject stronger hits",()=>{foreach(bool wall in new[]{false,true})using(var m=Basic("Utility","camera")){if(wall)Cache.SetValue(null,Floor.Concat(new[]{new CampusSolid{name="wall",center=new Vector3(0,1,.8f),size=new Vector3(5,2,.1f)}}).ToArray());else m.Players["p1"].bodyProtectedUntil=m.Time+3;Use(m);Check(Drops(m)==0&&m.Players["p1"].stability==100,"attack crossed wall/protection");}});
                Test("regression SoftCombatTests",()=>SoftCombatTests.Run());Test("regression PartyMechanicTests",()=>PartyMechanicTests.Run());Test("regression PartyMovementTests",()=>PartyMovementTests.Run());Test("regression PartyFieldTests",()=>PartyFieldTests.Run());Test("regression PartyGuardTests",()=>PartyGuardTests.Run());Test("regression PartyLootTests",()=>PartyLootTests.Run());Test("regression PartyMachineryTests",()=>PartyMachineryTests.Run());Test("regression PartyControlTests",()=>PartyControlTests.Run());Test("regression PartyVirtualTests",()=>PartyVirtualTests.Run());Test("regression PartyLinkTests",()=>PartyLinkTests.Run());Test("regression TraitWordRuleTests",()=>TraitWordRuleTests.Run());
            }
            finally{Cache.SetValue(null,old);}
            if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("COMBAT_IMPACT_PASSED "+passed);
        }
        public static void Physics()
        {
            if(!Application.isPlaying){SessionState.SetString("V4Job","combat-impact");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;return;}
            var old=Cache.GetValue(null);Cache.SetValue(null,Floor);int passed=0;
            try
            {
                foreach(var template in new[]{"Melee","HeavyWeapon","Utility","Projectile","Disc","Ricochet","Deployable"})using(var m=Basic(template,template=="Melee"?"book":template=="HeavyWeapon"?"computer":template=="Utility"?"camera":template=="Projectile"?"basketball":template=="Disc"?"school-disc":template=="Ricochet"?"bowling-ball":"plant",true))
                {
                    Use(m,.25f);if(template=="Melee"){Check(Drops(m)==0,"first physical melee dropped");PartyFieldTests.Tick(m,.4f);Place(m,m.Players["p0"],Vector3.up);Place(m,m.Players["p1"],new Vector3(0,1,1.6f));Use(m,.25f);}
                    PartyFieldTests.Tick(m,.3f);Check(Drops(m)==1&&m.Remaining("p1")==4,"physical "+template+" did not drop exactly one");Check(m.BodyWorld.Get(m.Players["p1"]).Bodies.All(b=>b.velocity.magnitude<25),"unsafe physical velocity");Check(m.View("p0").players.All(p=>p.bodyPose.positions.Length==15),"invalid physical pose");Debug.Log("COMBAT_IMPACT_REAL_PHYSICS_PASS "+template);passed++;
                }
                foreach(var id in new[]{"wet-trail","sand-sink","sticky-step","frost-lock"})using(var m=PartyFieldTests.Make(id))
                {
                    m.EnableBodyPhysics();foreach(var player in m.Players.Values)Place(m,player,player.position);
                    if(id=="frost-lock")m.Effects.Add(new EffectState{id="wet",kind="PartyMechanic",mechanicId="wet-trail",owner="p2",sourceInstance=m.Players["p2"].equipped,path=new[]{new Vector3(0,.15f,1.8f)},deadline=m.Time+5,nextHit=m.Time+5});
                    var e=PartyFieldTests.Deploy(m);var point=id=="wet-trail"||id=="frost-lock"?e.path[0]:e.position;
                    Place(m,m.Players["p0"],new Vector3(-4,1,0));Place(m,m.Players["p1"],new Vector3(point.x,1,point.z));
                    PartyFieldTests.Tick(m,.4f);Check(Drops(m)==1&&m.Remaining("p1")==4,"physical contact failed "+id);Check(m.BodyWorld.Get(m.Players["p1"]).Bodies.All(b=>b.velocity.magnitude<25),"unsafe hazard velocity");Debug.Log("COMBAT_IMPACT_REAL_PHYSICS_PASS "+id);passed++;
                }
            }
            finally{Cache.SetValue(null,old);}
            Debug.Log("COMBAT_IMPACT_REAL_PHYSICS_PASSED "+passed);
        }
    }
}
