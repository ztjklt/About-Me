using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace AboutMe.Editor
{
    public static class AnimalPersonaTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static CampusSolid[] Floor=>new[]{new CampusSolid{name="test_floor",center=new Vector3(0,-.5f,0),size=new Vector3(60,1,60)}};
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        static Match Make(string visual="cat",string template="Summon")
        {
            var m=new Match("ANIMAL",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999,eatDuration=.1f,recoverDuration=.1f});
            for(int i=0;i<3;i++)m.Add(new Profile{playerId="p"+i,nickname="p"+i,personas=Enumerable.Range(0,2).Select(j=>new PersonaAsset{assetId="asset"+i+j,name="toy",templateId=template,visualId=visual,color="#FFD773"}).ToArray()});
            m.Phase=MatchPhase.Reveal;m.Tick(.02f);m.Time=10;m.Props.Clear();
            for(int i=0;i<3;i++){var p=m.Players["p"+i];p.position=new Vector3(i==0?0:2*i,1,i==0?0:5);p.yaw=0;p.input=new Command{yaw=0};p.lastInput=m.Time;}
            return m;
        }
        static void Tick(Match m,float t){for(int i=0;i<Mathf.CeilToInt(t/.02f);i++)m.Tick(.02f);}
        static PersonaInstance Throw(Match m)
        {var p=m.Players["p0"];var item=m.Item(p.equipped);m.Receive("p0",new Command{sequence=p.sequence+1,action="use_item",yaw=0});Tick(m,.2f);return item;}
        public static void ValidateAll(){Run();Regression();}
        public static void Regression()
        {
            SoftCombatTests.Run();
            PartyMechanicTests.Run();
            PartyControlTests.Run();
            PartyGuardTests.Run();
            Debug.Log("ANIMAL_REGRESSION_PASSED");
        }
        [MenuItem("About Me/Tests/Animal Persona single use")]
        public static void Run()
        {
            var old=Cache.GetValue(null);var random=UnityEngine.Random.state;int passed=0;
            void Test(string name,Action test){Cache.SetValue(null,Floor);test();passed++;Debug.Log("ANIMAL_RULE_PASS "+name);}
            try
            {
                foreach(var visual in AnimalPersonaCatalog.Visuals)Test(visual+" chase hit same life and single knockback",()=>{
                    var m=Make(visual);m.Players["p2"].position=new Vector3(20,1,20);var item=Throw(m);
                    Check(item.state==ItemState.Thrown&&m.Players["p0"].equipped==""&&m.Remaining("p0")==2,"did not throw original life");
                    Check(m.View("p1").ground.Any(x=>x.id==item.id&&x.asset.visualId==visual),"peer missing actual animal");
                    Tick(m,1.2f);Check(item.state==ItemState.Ground&&item.ownerId=="p0"&&m.Items.Count(x=>x.id==item.id)==1,"landing lost/copied life");
                    Check(m.Players["p1"].stability==0&&m.Players["p1"].bodyMode!=BodyMode.Standing,"animal missed knockback");
                    int impacts=m.Players["p1"].impactId;Tick(m,1);Check(m.Players["p1"].impactId==impacts,"landed animal attacked again");
                });
                Test("random chooses only nearby eligible players",()=>{
                    var seen=new System.Collections.Generic.HashSet<string>();
                    for(int seed=0;seed<24;seed++){UnityEngine.Random.InitState(seed);var m=Make();m.Players["p2"].position=new Vector3(-2,1,5);var item=Throw(m);Tick(m,.2f);seen.Add(item.chaseTarget);}
                    Check(seen.SetEquals(new[]{"p1","p2"}),"target not random or selected owner");
                });
                Test("no target lands without returning",()=>{var m=Make();m.Players["p1"].position=m.Players["p2"].position=new Vector3(20,1,20);var item=Throw(m);Tick(m,.4f);Check(item.state==ItemState.Ground&&m.Players["p0"].equipped==""&&m.Remaining("p0")==2,"empty arena recalled animal");});
                Test("protected and disconnected players not chased",()=>{var m=Make();m.Players["p1"].bodyProtectedUntil=m.Time+10;m.Players["p2"].connected=false;var item=Throw(m);Tick(m,.5f);Check(item.state==ItemState.Ground&&m.Players["p1"].stability==100,"protected/disconnected target");});
                Test("wall blocks animal",()=>{var m=Make();m.Players["p1"].position=new Vector3(0,1,5);m.Players["p2"].position=new Vector3(20,1,20);Cache.SetValue(null,Floor.Concat(new[]{new CampusSolid{name="wall",center=new Vector3(0,1,2),size=new Vector3(20,2,.2f)}}).ToArray());var item=Throw(m);Tick(m,2);Check(item.state==ItemState.Ground&&m.Players["p1"].stability==100&&item.position.z<2,"animal crossed wall");});
                Test("chase times out and preserves last owner life",()=>{var m=Make();var item=Throw(m);item.thrownAt=m.Time-4;Tick(m,.02f);Check(item.state==ItemState.Ground&&m.Remaining("p0")==2,"timeout destroyed life");});
                foreach(var template in new[]{"Projectile","Disc","Ricochet","Deployable"})Test(template+" never recalls and original can be consumed",()=>{
                    var m=Make("basketball",template);m.Players["p1"].position=m.Players["p2"].position=new Vector3(20,1,20);var item=Throw(m);Tick(m,2);
                    Check(item.state==ItemState.Ground&&m.Players["p0"].equipped==""&&m.Remaining("p0")==2&&!m.Effects.Any(x=>x.kind==template),"copy or recall");
                    item.velocity=Vector3.zero;var eater=m.Players["p1"];eater.position=item.position+Vector3.up*.65f;m.Interact(eater,item.id);Tick(m,.15f);
                    Check(item.state==ItemState.Consumed&&m.Remaining("p0")==1&&m.Eaten.Count(x=>x.instanceId==item.id)==1,"opponent cannot pick up/eat same instance");
                });
                Test("owner recovers original animal explicitly",()=>{var m=Make();m.Players["p1"].position=m.Players["p2"].position=new Vector3(20,1,20);var item=Throw(m);Tick(m,.5f);var owner=m.Players["p0"];owner.position=item.position+Vector3.up*.65f;m.Interact(owner,item.id);Tick(m,.15f);Check((item.state==ItemState.Inventory||item.state==ItemState.Equipped)&&item.ownerId=="p0"&&m.Remaining("p0")==2,"explicit recover failed");});
                foreach(var id in new[]{"impact-burst","sticky-burst","vacuum-burst","fuse-burst","break-net","momentum-pass"})Test(id+" actual thrown mechanic lands original life",()=>{
                    using(var m=PartyFieldTests.Make(id)){var item=m.Item(m.Players["p0"].equipped);PartyFieldTests.Deploy(m);Check(item.state==ItemState.Thrown&&m.Players["p0"].equipped=="","blast kept equipped copy");PartyFieldTests.Tick(m,8);Check(item.state==ItemState.Ground&&m.Items.Count(x=>x.id==item.id)==1&&m.Remaining("p0")==4,"blast lost/copied life");}
                });
            }
            finally{Cache.SetValue(null,old);UnityEngine.Random.state=random;}
            Debug.Log("ANIMAL_PERSONA_TESTS_PASSED "+passed);
        }
    }
}
