using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AboutMe.Editor
{
    public static class PartyMechanicTests
    {
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static CampusSolid[] Flat=>new[]{new CampusSolid{name="test_floor",center=new Vector3(0,-.5f,0),size=new Vector3(60,1,60)}};
        static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        static Match Make(string mechanic)
        {
            var d=PartyMechanicRegistry.Find(mechanic);var m=new Match("PARTY",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999});
            for(int i=0;i<3;i++)m.Add(new Profile{playerId="p"+i,nickname="p"+i,personas=Enumerable.Range(0,4).Select(n=>new PersonaAsset{assetId="p"+i+"-"+n,name=d.label,visualId=i==0?d.visualId:"book",templateId="SpecialAbility",color="#F49A6B"}).ToArray()});
            m.Phase=MatchPhase.Reveal;m.Tick(.02f);m.Time=10;m.Props.Clear();
            for(int i=0;i<3;i++){var p=m.Players["p"+i];p.position=new Vector3(i*10,1,0);p.yaw=0;p.input=new Command();p.lastInput=m.Time;}
            return m;
        }
        static void Input(Match m,string id,string action="",bool escape=false)
        {var p=m.Players[id];m.Receive(id,new Command{sequence=p.sequence+1,action=action,yaw=p.yaw,escapeHeld=escape});}
        static void Tick(Match m,float seconds,string escape=null)
        {for(int i=0;i<Mathf.CeilToInt(seconds/.02f-.001f);i++){foreach(var p in m.Players.Values)Input(m,p.profile.playerId,"",p.profile.playerId==escape);m.Tick(.02f);}}
        static EffectState Deploy(Match m)
        {Input(m,"p0","use_item");Tick(m,PartyMechanicRegistry.ForVisual(m.Item(m.Players["p0"].equipped).asset.visualId).windup+.02f);return m.Effects.Last(e=>e.kind=="PartyMechanic");}
        static void Near(Match m,EffectState e,string id="p1",float x=1)
        {var p=m.Players[id];p.position=e.position+new Vector3(x,0,0);p.position.y=1;p.impulse=Vector3.zero;p.verticalSpeed=0;p.airborne=false;}
        [MenuItem("About Me/Tests/Party blast mechanics authority")]
        public static void Run()
        {
            var old=Cache.GetValue(null);var failures=new List<string>();int passed=0;
            void Test(string title,Action run){Cache.SetValue(null,Flat);try{run();passed++;Debug.Log("PARTY_MECHANIC_PASS "+title);}catch(Exception e){failures.Add(title+": "+e.Message);Debug.LogError("PARTY_MECHANIC_FAIL "+title+": "+e);}}
            try
            {
                Test("ten bounded authoritative definitions with unique bindings",()=>{
                    Check(Resources.Load<Shader>("PartyWarning")!=null,"warning ring shader missing");var all=PartyMechanicRegistry.All;Check(all.Length==20&&all.Select(x=>x.id).Distinct().Count()==20&&all.Select(x=>x.visualId).Distinct().Count()==20,"registry count/duplicates");
                    Check(all.All(x=>x.windup>=.2f&&x.cooldown>=4&&x.force<=8&&x.radius<=8),"budget exceeded");
                    foreach(var d in all.Where(x=>x.family=="blast")){var m=Make(d.id);Check(m.Item(m.Players["p0"].equipped).PublicCopy().asset.mechanicId==d.id,"public mechanic binding lost");Deploy(m);Check(m.Remaining("p0")==4,"deployment invented/consumed a life");}
                });
                Test("contact burst sweeps player and harms owner only within radius",()=>{
                    var m=Make("impact-burst");m.Players["p1"].position=new Vector3(0,1,2.3f);Deploy(m);Tick(m,.25f);
                    Check(m.CombatEvents.Any(e=>e.kind=="party_burst"),"contact did not explode");Check(m.Players["p1"].stability<100&&m.Players["p0"].stability<100,"radial owner/target damage missing");Check(m.Players["p2"].stability==100,"distant player hit");
                });
                Test("fuse countdown stays visible and can be grabbed and thrown",()=>{
                    var m=Make("fuse-burst");var e=Deploy(m);Check(m.View("p0").players.Single(x=>x.id=="p0").mechanicCooldown>7,"cooldown not shown to owner");Check(m.View("p1").effects.Any(x=>x.id==e.id&&x.remaining>2),"countdown not public");
                    Tick(m,.65f);var prop=m.Props.Single();prop.velocity=Vector3.zero;var p=m.Players["p1"];p.position=prop.position-Vector3.forward*.9f;p.position.y=1;p.yaw=0;Input(m,"p1","grab");Tick(m,.24f);Check(p.publicProp==prop.id,"device not grabbable");
                    Input(m,"p1","use_item");Tick(m,.18f);Check(p.publicProp==""&&prop.thrower=="p1"&&prop.velocity.magnitude>5,"device did not throw back");Tick(m,2.5f);Check(m.Props.Count==0&&m.CombatEvents.Any(x=>x.kind=="party_burst"),"fuse failed or stale prop");
                });
                Test("remote requires second input and device can be dismantled",()=>{
                    var m=Make("remote-burst");var e=Deploy(m);Near(m,e);Tick(m,.5f);Check(m.Players["p1"].stability==100,"remote auto detonated");Input(m,"p0","use_item");Tick(m,.25f);Check(m.Players["p1"].stability<100&&e.stage=="burst","remote second press failed");
                    var n=Make("remote-burst");var device=Deploy(n);var prop=n.Props.Single();n.Players["p1"].position=prop.position-Vector3.forward*1.4f;n.Players["p1"].yaw=0;n.Item(n.Players["p1"].equipped).asset.templateId="Melee";
                    Input(n,"p1","use_item");Tick(n,.18f);Check(device.health==1,"first strike did not damage device");Tick(n,1.1f);Input(n,"p1","use_item");Tick(n,.18f);Check(!n.Effects.Contains(device)&&n.Props.Count==0,"second strike did not dismantle");
                });
                Test("proximity warns then cancels when player leaves",()=>{
                    var m=Make("proximity-burst");var e=Deploy(m);Near(m,e);Tick(m,.25f);Check(e.stage=="sensing"&&e.triggerAt>m.Time,"missing 0.6 second warning");m.Players["p1"].position=new Vector3(10,1,0);Tick(m,.45f);Check(e.stage=="watching"&&m.Players["p1"].stability==100,"leaving circle did not cancel");Near(m,e);Tick(m,.65f);Check(e.stage=="burst"&&m.Players["p1"].stability<100,"standing in circle did not trigger");
                });
                Test("proximity ignores jumping player",()=>{
                    var m=Make("proximity-burst");var e=Deploy(m);var p=m.Players["p1"];for(int i=0;i<40;i++){p.position=e.position+Vector3.up*2;p.airborne=true;p.verticalSpeed=1;Input(m,"p1");m.Tick(.02f);}Check(e.stage=="watching"&&p.stability==100,"airborne player triggered device");
                });
                Test("cone covers front 60 degrees with owner recoil and safe sides",()=>{
                    var m=Make("cone-burst");m.Players["p1"].position=new Vector3(0,1,3);m.Players["p2"].position=new Vector3(3,1,0);Deploy(m);Check(m.Players["p1"].stability<100&&m.Players["p2"].stability==100,"cone direction incorrect");Check(m.Players["p0"].impulse.z<0,"owner recoil missing");
                });
                Test("sticky follows victim and held Q removes before explosion",()=>{
                    var m=Make("sticky-burst");m.Players["p1"].position=new Vector3(0,1,2.3f);var e=Deploy(m);Tick(m,.18f);Check(e.stage=="attached"&&m.View("p1").players.Single(p=>p.id=="p1").sticky,"sticky failed to attach/publish");m.Players["p1"].position+=Vector3.right;Tick(m,.02f);Check(Vector3.Distance(e.position,m.Players["p1"].position)<.2f,"sticky does not follow");Tick(m,.72f,"p1");Check(!m.Effects.Contains(e)&&m.Players["p1"].stability==100&&m.Players["p1"].actionKind=="escape","Q failed to remove sticky");
                });
                Test("sticky unattended explodes and protected target cannot be attached",()=>{
                    var m=Make("sticky-burst");m.Players["p1"].position=new Vector3(0,1,2.3f);var e=Deploy(m);Tick(m,.18f);Tick(m,1.65f);Check(m.Players["p1"].stability<100,"unattended sticker never exploded");
                    var n=Make("sticky-burst");n.Players["p1"].position=new Vector3(0,1,2.3f);n.Players["p1"].bodyProtectedUntil=n.Time+4;var other=Deploy(n);Tick(n,.2f);Check(other.stage!="attached","sticker bypassed getup immunity");
                });
                Test("vacuum pulls before exploding",()=>{
                    var m=Make("vacuum-burst");var e=Deploy(m);for(int i=0;i<100&&e.stage!="pulling";i++)Tick(m,.02f);Check(e.stage=="pulling","no vacuum phase");Near(m,e);float before=m.Players["p1"].position.x;Tick(m,.24f);Check(m.Players["p1"].position.x<before&&m.Players["p1"].stability==100,"vacuum phase failed or damaged early");Tick(m,.3f);Check(m.Players["p1"].stability<100,"outburst missing");
                });
                Test("relay limited to three with ordered delayed chain",()=>{
                    var m=Make("relay-burst");Deploy(m);Tick(m,.5f);m.Players["p0"].position=new Vector3(2,1,0);Deploy(m);Tick(m,.5f);m.Players["p0"].position=new Vector3(4,1,0);Deploy(m);Check(m.Effects.Count(e=>e.kind=="PartyMechanic")==3,"relay did not deploy 3");Input(m,"p0","use_item");Tick(m,.1f);Check(m.Props.Count==3,"fourth relay bypassed cap");Tick(m,1.5f);Check(m.CombatEvents.Count(e=>e.kind=="party_burst"&&e.reason=="relay-burst")==3,"chain order did not finish");
                });
                Test("moving middle relay breaks chain; untriggered nodes expire safely",()=>{
                    var m=Make("relay-burst");Deploy(m);Tick(m,.5f);m.Players["p0"].position=new Vector3(2,1,0);Deploy(m);Tick(m,.5f);m.Players["p0"].position=new Vector3(4,1,0);Deploy(m);var middle=m.Effects.Single(e=>e.chainIndex==2);var prop=m.Props.Single(x=>x.id==middle.targetId);prop.position=new Vector3(15,.6f,0);Tick(m,3.5f);Check(m.CombatEvents.Count(e=>e.kind=="party_burst")==1&&m.Props.Count==0,"broken relay still chained or leaked");
                });
                Test("cluster has three distinct points and bounded cumulative force",()=>{
                    var m=Make("cluster-burst");Deploy(m);var nodes=m.Effects.Where(e=>e.kind=="PartyMechanic").ToArray();Check(nodes.Length==3&&nodes.Select(e=>e.position).Distinct().Count()==3,"cluster landing duplicate");Check(nodes.Sum(e=>e.force)<=8,"cluster force exceeds single blast budget");Tick(m,2);Check(m.CombatEvents.Count(e=>e.kind=="party_burst")==3,"cluster failed to fire separately");
                });
                Test("ground ring hits once and jumping is safe",()=>{
                    var m=Make("ring-burst");m.Players["p1"].position=new Vector3(0,1,3);m.Players["p2"].position=new Vector3(3,1,0);var e=Deploy(m);m.Players["p2"].position=new Vector3(3,2.5f,0);m.Players["p2"].verticalSpeed=8;m.Players["p2"].airborne=true;Tick(m,.55f);Check(m.Players["p1"].stability==72&&m.Players["p2"].stability==100,"ring hit count or jump immunity wrong");Tick(m,.5f);Check(m.Players["p1"].stability==72,"ring repeatedly hit same target");
                });
                Test("raised floor blocks ground ring",()=>{
                    Cache.SetValue(null,Flat.Concat(new[]{new CampusSolid{name="raised",center=new Vector3(3,.6f,0),size=new Vector3(2,1.2f,2)}}).ToArray());
                    var m=Make("ring-burst");m.Players["p1"].position=new Vector3(3,2.2f,0);Deploy(m);Tick(m,1.25f);Check(m.Players["p1"].stability==100,"ground ring crossed raised floor");
                });
                Test("get-up immunity blocks blast and disconnected held device is released",()=>{
                    var m=Make("remote-burst");var e=Deploy(m);Near(m,e);m.Players["p1"].bodyProtectedUntil=m.Time+5;Tick(m,.5f);Input(m,"p0","use_item");Tick(m,.25f);Check(m.Players["p1"].stability==100,"burst bypassed get-up immunity");
                    var n=Make("fuse-burst");Deploy(n);var prop=n.Props.Single();prop.holder="p1";n.Players["p1"].publicProp=prop.id;n.Disconnect("p1");Check(prop.holder==""&&n.Players["p1"].publicProp=="","disconnect kept held live device");
                });
                Test("wall blocks radial blast; one depleted hit drops only one Persona",()=>{
                    var m=Make("fuse-burst");var e=Deploy(m);var prop=m.Props.Single();prop.position=new Vector3(0,.6f,0);prop.velocity=Vector3.zero;e.deadline=m.Time+.2f;m.Players["p0"].position=new Vector3(-5,1,0);m.Players["p1"].position=new Vector3(1.5f,1,0);m.Players["p2"].position=new Vector3(-1.5f,1,0);m.Players["p2"].stability=10;m.Players["p2"].lastHit=m.Time;
                    Cache.SetValue(null,Flat.Concat(new[]{new CampusSolid{name="wall",center=new Vector3(.75f,1,0),size=new Vector3(.2f,2,4)}}).ToArray());Tick(m,.25f);Check(m.Players["p1"].stability==100,"blast bypassed wall");Check(m.Items.Count(i=>i.ownerId=="p2"&&i.state==ItemState.Ground)==1&&m.Remaining("p2")==4,"blast broke one-drop/life conservation");
                });
                Test("consume source, eliminated owner and finish clear hazards and held devices",()=>{
                    foreach(var mode in new[]{"consume","eliminate","finish"}){var m=Make("remote-burst");var e=Deploy(m);var p=m.Players["p1"];var prop=m.Props.Single();prop.holder="p1";p.publicProp=prop.id;
                        if(mode=="consume")m.Item(e.sourceInstance).state=ItemState.Consumed;else if(mode=="eliminate")m.Players["p0"].eliminated=true;else m.Finish("timeout");Tick(m,.02f);Check(!m.Effects.Contains(e)&&m.Props.Count==0&&p.publicProp=="","hazard/holder leaked on "+mode);}
                });
                Test("cooldown belongs to source instance; light compatibility and no duplicate input",()=>{
                    var m=Make("fuse-burst");var e=Deploy(m);for(int i=0;i<8;i++){Input(m,"p0","use_item");Tick(m,.04f);}Check(m.Effects.Count(x=>x.kind=="PartyMechanic")==1,"spam duplicated fuse");var source=m.Item(e.sourceInstance);Tick(m,3);m.Players["p0"].nextAttack=0;Input(m,"p0","use_item");Tick(m,.25f);Check(!m.Effects.Any(x=>x.kind=="PartyMechanic"),"source cooldown bypassed");Check(source.state!=ItemState.Consumed,"use consumed life");
                });
            }
            finally{Cache.SetValue(null,old);}
            if(failures.Count>0)throw new Exception(string.Join("\n",failures));Debug.Log("PARTY_MECHANICS_PASSED "+passed+" cases / 10 distinct mechanisms / authority, counters, lifecycle, force and occlusion");
        }
    }
}
