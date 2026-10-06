using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class PartyMovementTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static CampusSolid[] Flat=>new[]{new CampusSolid{name="test_floor",center=new Vector3(0,-.5f,0),size=new Vector3(60,1,60)}};
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        static Match Make(string id)
        {
            var d=PartyMechanicRegistry.Find(id);var m=new Match("MOVEMENT",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999});
            for(int i=0;i<3;i++)m.Add(new Profile{playerId="p"+i,nickname="p"+i,appearance=AppearanceData.Demo(i),personas=Enumerable.Range(0,4).Select(n=>new PersonaAsset{assetId="p"+i+n,name=d.label,templateId="SpecialAbility",visualId=i==0?d.visualId:"book",color="#6CD2CA"}).ToArray()});
            m.Phase=MatchPhase.Reveal;m.Tick(.02f);m.Time=10;m.Props.Clear();
            for(int i=0;i<3;i++){var p=m.Players["p"+i];p.position=new Vector3(i*10,1,0);p.motorHeight=1;p.input=new Command();p.lastInput=m.Time;p.yaw=0;}
            return m;
        }
        static void Input(Match m,string id,string action="",float x=0,float z=0)
        {var p=m.Players[id];m.Receive(id,new Command{sequence=p.sequence+1,action=action,x=x,z=z,yaw=p.yaw});}
        static void Tick(Match m,float seconds,float z=0)
        {for(int i=0;i<Mathf.CeilToInt(seconds/.02f-.001f);i++){foreach(var p in m.Players.Values)Input(m,p.profile.playerId,"",0,p.profile.playerId=="p0"?z:0);m.Tick(.02f);}}
        static EffectState Deploy(Match m)
        {Input(m,"p0","use_item");Tick(m,PartyMechanicRegistry.ForVisual(m.Item(m.Players["p0"].equipped).asset.visualId).windup+.02f);return m.Effects.Last(e=>e.kind=="PartyMechanic");}
        [MenuItem("About Me/Tests/Party movement authority")]
        public static void Run()
        {
            var old=Cache.GetValue(null);var failures=new List<string>();int passed=0;
            void Test(string name,Action run){Cache.SetValue(null,Flat);try{run();passed++;Debug.Log("PARTY_MOVEMENT_PASS "+name);}catch(Exception e){failures.Add(name+": "+e.Message);Debug.LogError("PARTY_MOVEMENT_FAIL "+name+": "+e);}}
            try
            {
                Test("ground pad launches once without creating Persona life",()=>{var m=Make("spring-pad");var e=Deploy(m);var b=m.Players["p1"];b.position=e.position+Vector3.up*.85f;Tick(m,.04f);Check(b.airborne&&b.verticalSpeed>7,"pad failed to launch");Check(e.touchTimes.Count==1&&m.Remaining("p1")==4,"retrigger/life mutation");b.position=e.position+Vector3.up*.85f;b.airborne=false;b.verticalSpeed=0;Tick(m,.04f);Check(!b.airborne,"pad retriggered within 1s");});
                Test("uppercut front only and immunity",()=>{var m=Make("uppercut-piston");var b=m.Players["p1"];var c=m.Players["p2"];b.position=new Vector3(0,1,1.3f);c.position=new Vector3(0,1,-1.3f);Deploy(m);Check(m.BodyDown(b)&&m.Items.Count(x=>x.ownerId=="p1"&&x.state==ItemState.Ground)==1&&c.stability==100,"front lift failed");var n=Make("uppercut-piston");n.Players["p1"].position=new Vector3(0,1,1.3f);n.Players["p1"].bodyProtectedUntil=n.Time+5;Deploy(n);Check(!n.Players["p1"].airborne&&n.Players["p1"].stability==100,"immunity bypass");});
                Test("pogo hops and Q ends motion",()=>{var m=Make("pogo-hop");Deploy(m);Tick(m,.24f);Check(m.Players["p0"].position.y>1.5f,"pogo no vertical travel");Input(m,"p0","cancel");Check(!m.Effects.Any(x=>x.mechanicId=="pogo-hop"),"Q failed");});
                Test("inflate pushes without damage and is interrupted by equipment switch",()=>{var m=Make("inflate-shove");m.Players["p1"].position=new Vector3(.9f,1,0);Deploy(m);Tick(m,.8f);Check(m.Players["p1"].position.x>1&&m.Players["p1"].stability==100,"inflate lacked safe displacement");var other=m.Items.First(i=>i.ownerId=="p0"&&i.state==ItemState.Inventory);var p=m.Players["p0"];m.Receive("p0",new Command{sequence=p.sequence+1,action="equip",itemId=other.id});Tick(m,.04f);Check(!m.Effects.Any(x=>x.mechanicId=="inflate-shove"),"old equipment movement leaked");});
                Test("stomp requires jumping and landing triggers one pulse",()=>{var m=Make("stomp-pulse");Input(m,"p0","use_item");Tick(m,.25f);Check(m.Effects.Count==0,"ground stomp accepted");Tick(m,.5f);var a=m.Players["p0"];a.position=new Vector3(0,3,0);a.airborne=true;a.verticalSpeed=0;m.Players["p1"].position=new Vector3(1.5f,1,0);Deploy(m);Tick(m,.7f);Check(m.CombatEvents.Count(x=>x.kind=="party_stomp")==1&&m.Players["p1"].stability<100,"landing pulse failed");});
                Test("dash travels with fixed direction and stops before wall",()=>{var m=Make("short-dash");Deploy(m);Tick(m,.35f);Check(m.Players["p0"].position.z>3&&m.Players["p0"].position.z<5,"dash distance wrong");Check(m.Players["p0"].interactBlockedUntil>m.Time,"post dash interaction lock absent");var n=Make("short-dash");Cache.SetValue(null,Flat.Concat(new[]{new CampusSolid{name="wall",center=new Vector3(0,1,2),size=new Vector3(5,2,.2f)}}).ToArray());Deploy(n);Tick(n,.35f);Check(n.Players["p0"].position.z<1.9f,"dash crossed wall");});
                Test("grapple requires public visible anchor and cancels when anchor is taken",()=>{var m=Make("grapple-anchor");Input(m,"p0","use_item");Tick(m,.3f);Check(m.Effects.Count==0,"grapple invented anchor");Tick(m,.4f);var prop=new PublicProp{id="anchor",visual="box",holder="",position=new Vector3(0,.6f,6)};m.Props.Add(prop);var e=Deploy(m);Tick(m,.25f);Check(m.Players["p0"].position.z>1&&e.targetId==prop.id,"grapple no travel");prop.holder="p1";Tick(m,.02f);Check(!m.Effects.Contains(e),"taken anchor still pulled");});
                Test("glide limits descent and blocks item use until Q",()=>{var m=Make("glide-open");var a=m.Players["p0"];a.position=new Vector3(0,8,0);a.airborne=true;a.verticalSpeed=-8;var e=Deploy(m);Tick(m,.4f);Check(a.verticalSpeed>=-2.01f&&a.position.y>3,"glide fell normally");Input(m,"p0","use_item");Tick(m,.04f);Check(m.Effects.Count(x=>x.mechanicId=="glide-open")==1,"glide duplicated");Input(m,"p0","cancel");Check(!m.Effects.Contains(e),"glide cancel failed");});
                Test("belt moves grounded players and loose Persona but not airborne",()=>{var m=Make("belt-floor");var e=Deploy(m);var b=m.Players["p1"];var c=m.Players["p2"];b.position=e.position+Vector3.up*.85f;c.position=e.position+Vector3.up*3;c.airborne=true;c.verticalSpeed=2;var item=m.Items.First(i=>i.ownerId=="p2"&&i.state==ItemState.Inventory);item.state=ItemState.Ground;item.position=e.position+Vector3.up*.2f;item.velocity=Vector3.zero;float before=item.position.z;Tick(m,.2f);Check(b.position.z>e.position.z+.3f&&Mathf.Abs(c.position.z-e.position.z)<.05f,"belt grounding wrong");Check(item.position.z>before,"loose Persona ignored belt");});
                Test("paired portals require two placements and voluntary preparation",()=>{var m=Make("paired-teleport");var entrance=Deploy(m);Check(entrance.stage=="portal_wait","first portal active early");Tick(m,.5f);m.Players["p0"].position=new Vector3(0,1,4);var exit=Deploy(m);Check(entrance.stage=="portal_in"&&exit.stage=="portal_out","pair not linked");var b=m.Players["p1"];b.position=entrance.position+Vector3.up*.85f+Vector3.back*.4f;Input(m,"p1","",0,1);m.Tick(.02f);Check(b.portalEntry==entrance.id,"voluntary entry ignored");Tick(m,.62f);Check(b.position.z>exit.position.z+.5f&&m.CombatEvents.Any(x=>x.kind=="party_teleport"),"portal failed to transport");Check(m.Remaining("p1")==4,"portal consumed life");m.Item(exit.sourceInstance).state=ItemState.Consumed;Tick(m,.02f);Check(!m.Effects.Contains(entrance)&&!m.Effects.Contains(exit),"paired cleanup leaked");});
                Test("portal exit obstruction and leaving preparation both cancel",()=>{foreach(bool blocked in new[]{false,true}){var m=Make("paired-teleport");var entrance=Deploy(m);Tick(m,.5f);m.Players["p0"].position=new Vector3(0,1,4);var exit=Deploy(m);var b=m.Players["p1"];b.position=entrance.position+Vector3.up*.85f+Vector3.back*.4f;Input(m,"p1","",0,1);m.Tick(.02f);Check(b.portalEntry==entrance.id,"preparation never began");if(blocked)Cache.SetValue(null,Flat.Concat(new[]{new CampusSolid{name="exit_wall",center=exit.position+Vector3.up*.85f+Vector3.forward*.95f,size=new Vector3(1,2,1)}}).ToArray());else b.position=new Vector3(4,1,0);Tick(m,.7f);Check(b.portalEntry==""&&!m.CombatEvents.Any(x=>x.kind=="party_teleport"),"counter did not prevent teleport");}});
                Test("ground deployment rejects blocked location without spending full cooldown",()=>{var m=Make("spring-pad");Cache.SetValue(null,Flat.Concat(new[]{new CampusSolid{name="wall",center=new Vector3(0,1,1.8f),size=new Vector3(2,2,2)}}).ToArray());Input(m,"p0","use_item");Tick(m,.3f);Check(m.Effects.Count==0&&m.View("p0").players.First(x=>x.id=="p0").mechanicCooldown<1,"blocked deployment created pad or spent cooldown");});
                Test("ability cooldown does not prevent grabbing after action lock",()=>{var m=Make("remote-burst");Deploy(m);Tick(m,.5f);m.Props.Clear();var b=m.Players["p1"];b.position=m.Players["p0"].position+Vector3.forward;Input(m,"p0","grab");Tick(m,.25f);Check(m.Players["p0"].grabbing=="p1","ability cooldown locked ordinary grab");});
                Test("disconnect and finished match remove sustained movement",()=>{foreach(var mode in new[]{"disconnect","finish","consume"}){var m=Make("pogo-hop");var e=Deploy(m);if(mode=="disconnect")m.Disconnect("p0");else if(mode=="finish")m.Finish("timeout");else m.Item(e.sourceInstance).state=ItemState.Consumed;Tick(m,.02f);Check(!m.Effects.Contains(e),mode+" retained movement");}});
            }
            finally{Cache.SetValue(null,old);}
            if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("PARTY_MOVEMENT_PASSED "+passed+" authority cases / ten distinct movement mechanisms");
        }
    }
}
