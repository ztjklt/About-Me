using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AboutMe.Editor
{
    // Exercises the same pure-data authority as local play and the dedicated server.
    // The geometry fixture replaces only the cached collision data and is always restored.
    public static class SoftCombatTests
    {
        const float Step = .02f;
        static readonly FieldInfo GeometryCache = typeof(CampusGeometry).GetField("solids", BindingFlags.Static | BindingFlags.NonPublic);

        static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
        }

        static CampusSolid[] FlatGround() => new[]
        {
            new CampusSolid { name="test_floor", center=new Vector3(0,-.5f,0), size=new Vector3(60,1,60) }
        };

        static Profile Profile(string id) => new Profile
        {
            playerId=id, nickname=id,
            personas=Enumerable.Range(0,3).Select(i=>new PersonaAsset
            { assetId=id+i, name="Training glove", templateId="Melee", visualId="book", color="#77C8BD" }).ToArray()
        };

        static Match Make(bool reversed=false)
        {
            var m=new Match("SOFT",new MatchRules { revealDuration=.01f, handInterval=9999, finalTime=9999 });
            foreach(var id in reversed ? new[]{"b","a"} : new[]{"a","b"}) Check(m.Add(Profile(id)),"player fixture rejected");
            m.Phase=MatchPhase.Reveal;
            m.Tick(Step);
            m.Time=10;
            m.Props.Clear();
            Place(m);
            return m;
        }

        static void Place(Match m)
        {
            foreach(var p in m.Players.Values)
            {
                p.position=new Vector3(0,1,p.profile.playerId=="a"?0:1.10f);
                p.impulse=Vector3.zero;
                p.verticalSpeed=0;
                p.yaw=p.profile.playerId=="a"?0:180;
                p.input=new Command { yaw=p.yaw };
                p.lastInput=m.Time;
            }
        }

        static void Send(Match m,string id,string action="",bool escape=false,float x=0,float z=0)
        {
            var p=m.Players[id];
            m.Receive(id,new Command { sequence=p.sequence+1, action=action, yaw=p.yaw, escapeHeld=escape, x=x, z=z });
        }

        static void Tick(Match m,float seconds,string escapePlayer=null)
        {
            int frames=Mathf.CeilToInt(seconds/Step-.0001f);
            for(int i=0;i<frames;i++)
            {
                if(escapePlayer!=null) Send(m,escapePlayer,"",true);
                m.Tick(Step);
            }
        }

        static void Connect(Match m,string kind="grab")
        {
            Send(m,"a",kind);
            Tick(m,.24f);
            Check(m.Players["a"].grabbing=="b","valid forward contact did not establish "+kind);
        }

        [MenuItem("About Me/Tests/Soft combat authority and directional input")]
        public static void Run()
        {
            Check(GeometryCache!=null,"CampusGeometry fixture cache not found");
            object previous=GeometryCache.GetValue(null);
            var failures=new List<string>();
            int passed=0;
            void Test(string name,Action test)
            {
                GeometryCache.SetValue(null,FlatGround());
                try { test(); passed++; Debug.Log("SOFT_COMBAT_PASS "+name); }
                catch(Exception e) { failures.Add(name+": "+e.Message); Debug.LogError("SOFT_COMBAT_FAILED "+name+": "+e); }
            }
            try
            {
                Test("camera stops before the nearest occluding wall with clear sightline",()=>
                {
                    GeometryCache.SetValue(null,FlatGround().Concat(new[]
                    {
                        new CampusSolid {name="far_camera_wall",center=new Vector3(0,2,7),size=new Vector3(4,4,.5f)},
                        new CampusSolid {name="near_camera_wall",center=new Vector3(0,2,3),size=new Vector3(4,4,.5f)}
                    }).ToArray());
                    var target=new Vector3(0,2,0); var desired=new Vector3(0,2,10);
                    Check(!CampusGeometry.ClearSegment(target,desired),"camera fixture does not obstruct the desired view");
                    var actual=CampusGeometry.CameraPosition(target,desired);
                    Check(actual.z>0&&actual.z<2.55f,"camera failed to stop ahead of nearest wall and padding: "+actual);
                    Check(CampusGeometry.ClearSegment(target,actual),"constrained camera still sees through a wall");
                });
                Test("unobstructed camera retains its desired orbit position",()=>
                {
                    var target=new Vector3(0,2,0); var desired=new Vector3(4,3,-7);
                    var actual=CampusGeometry.CameraPosition(target,desired);
                    Check(Vector3.Distance(actual,desired)<.00001f,"unobstructed camera was pulled toward character");
                    Check(CampusGeometry.ClearSegment(target,actual),"unobstructed fixture unexpectedly blocked");
                });
                Test("grab has a visible windup before authority contact",()=>
                {
                    var m=Make(); Send(m,"a","grab");
                    Check(m.Players["a"].grabbing=="","grab connected on command receipt");
                    Tick(m,.16f); Check(m.Players["a"].grabbing=="","grab connected before contact window");
                    Tick(m,.08f); Check(m.Players["a"].grabbing=="b","contact window never connected");
                });
                Test("grab rejects rear, distance and cross-floor targets",()=>
                {
                    foreach(var position in new[]{new Vector3(0,1,-1.10f),new Vector3(0,1,1.4f),new Vector3(0,2,1)})
                    {
                        var m=Make(); m.Players["b"].position=position;
                        if(position.y>1) m.Players["b"].verticalSpeed=m.Rules.gravity*.24f;
                        Send(m,"a","grab"); Tick(m,.24f);
                        Check(m.Players["a"].grabbing=="","invalid grab accepted at "+position);
                    }
                });
                Test("attempt range alone cannot establish unreachable hand contact",()=>
                {
                    var m=Make(); m.Players["b"].position=new Vector3(0,1,1.18f);
                    Send(m,"a","grab"); Tick(m,.24f);
                    Check(m.Players["a"].grabbing=="","attempt range accepted a target beyond real hand contact");
                });
                Test("wall occlusion prevents a nearby grab",()=>
                {
                    GeometryCache.SetValue(null,FlatGround().Concat(new[]{new CampusSolid
                    {name="test_wall",center=new Vector3(0,1,.55f),size=new Vector3(4,2,.08f)}}).ToArray());
                    var m=Make(); Send(m,"a","grab"); Tick(m,.24f);
                    Check(m.Players["a"].grabbing=="","grab passed through wall");
                });
                Test("elastic grip moves both players in opposite directions",()=>
                {
                    var m=Make(); Connect(m);
                    var a=m.Players["a"]; var b=m.Players["b"];
                    a.impulse=b.impulse=Vector3.zero;
                    b.position+=Vector3.forward*.35f;
                    var beforeA=a.position; var beforeB=b.position;
                    Tick(m,Step);
                    Check(a.position.z>beforeA.z+.00001f,"grabber receives no pulling reaction");
                    Check(b.position.z<beforeB.z-.00001f,"target receives no pull toward contact");
                    Check(Mathf.Abs((a.position.z-beforeA.z)+(b.position.z-beforeB.z))<.005f,"equal-mass pair gained unexplained net displacement");
                });
                Test("pair pull is independent of dictionary insertion order",()=>
                {
                    var first=Make(); var reversed=Make(true);
                    foreach(var m in new[]{first,reversed})
                    {
                        Connect(m); m.Players["b"].position+=Vector3.forward*.3f;
                        Send(m,"a",x:-.4f); Send(m,"b",x:.4f); Tick(m,.28f);
                    }
                    foreach(var id in new[]{"a","b"})
                        Check(Vector3.Distance(first.Players[id].position,reversed.Players[id].position)<.002f,"iteration order changed "+id+" displacement");
                });
                Test("Q must be held and releases the incoming grip",()=>
                {
                    var m=Make(); Connect(m); Send(m,"b","escape",true);
                    Tick(m,.3f,"b"); Check(m.Players["a"].grabbing=="b","escape completed too early");
                    Send(m,"b","escape_end"); Tick(m,.1f);
                    Send(m,"b","escape",true); Tick(m,.44f,"b");
                    Check(m.Players["a"].grabbing=="b","released Q retained old progress");
                    Tick(m,.3f,"b"); Check(m.Players["a"].grabbing=="","Q did not release another player's grip");
                    Check(m.Players["b"].grabImmuneUntil>m.Time+.8f,"escape did not give regrab immunity");
                });
                Test("expired held input cannot finish an unattended escape",()=>
                {
                    var m=Make(); Connect(m); Send(m,"b","escape",true); Tick(m,.76f);
                    Check(m.Players["a"].grabbing=="b","stale held input completed escape after disconnect-like silence");
                });
                Test("grip expires and enforces release cooldown",()=>
                {
                    var m=Make(); Connect(m); Tick(m,2.02f);
                    var a=m.Players["a"];
                    Check(a.grabbing=="","grip lasted more than two seconds");
                    Check(a.nextGrab>m.Time+1.2f,"release cooldown absent");
                    Place(m); Send(m,"a","grab"); Tick(m,.24f);
                    Check(a.grabbing=="","grab restarted during cooldown");
                    Tick(m,1.6f); Place(m); Connect(m);
                });
                Test("escape immunity blocks a second eligible grabber",()=>
                {
                    var m=Make(); m.Phase=MatchPhase.Lobby; Check(m.Add(Profile("c")),"third player fixture"); m.Phase=MatchPhase.Battle;
                    m.Players["c"].position=new Vector3(10,1,10);
                    Connect(m); Send(m,"b","escape",true); Tick(m,.74f,"b");
                    var c=m.Players["c"]; var b=m.Players["b"];
                    c.position=b.position+Vector3.right*1.10f; c.yaw=270; c.impulse=Vector3.zero;
                    Send(m,"c","grab"); Tick(m,.24f);
                    Check(c.grabbing=="","second grabber bypassed victim immunity");
                });
                Test("grabbing an equipped Persona does not transfer ownership",()=>
                {
                    var m=Make(); var original=m.Players["b"].equipped; Connect(m,"grab_item");
                    Check(m.Item(original).ownerId=="b"&&m.Item(original).state==ItemState.Equipped,"grip stole persistent owner or equipped state");
                    Send(m,"a","cancel"); Check(m.Players["a"].grabbing=="","cancel retained outgoing item grip");
                });
                Test("grabber attacks only after releasing the grip",()=>
                {
                    var m=Make(); Connect(m); Send(m,"a","use_item");
                    Check(m.Players["a"].grabbing=="","grabber can attack while retaining victim");
                });
                Test("grabbed player can counterattack and interrupt grabber",()=>
                {
                    var m=Make(); Connect(m); var before=m.Players["a"].stability;
                    Send(m,"b","use_item"); Tick(m,.20f);
                    Check(m.Players["a"].stability<before,"victim's counterattack was suppressed");
                    Check(m.Players["a"].grabbing=="","hit grabber kept grip");
                });
                Test("item use waits for contact and deals damage only once",()=>
                {
                    var m=Make(); var victim=m.Players["b"]; var before=victim.stability;
                    Send(m,"a","use_item"); Check(victim.stability==before,"item use hit immediately");
                    Tick(m,.12f); Check(victim.stability==before,"item use hit during anticipation");
                    Tick(m,.08f); var hit=victim.stability;
                    Check(hit<before,"item use missed valid contact window");
                    Tick(m,.5f); Check(Mathf.Abs(victim.stability-hit)<.001f,"one strike dealt repeated damage");
                });
                Test("old attack intents and empty hands cannot attack",()=>
                {
                    foreach(var action in new[]{"light","charge","heavy"})
                    {
                        var m=Make(); Send(m,"a",action); Tick(m,.24f);
                        Check(m.Players["b"].stability==100&&m.Players["a"].pendingAction==""&&m.Players["a"].chargingSince<0,"legacy intent attacked: "+action);
                    }
                    var empty=Make();var actor=empty.Players["a"];
                    empty.Item(actor.equipped).state=ItemState.Inventory;actor.equipped="";
                    Send(empty,"a","use_item");Tick(empty,.24f);
                    Check(empty.Players["b"].stability==100&&actor.pendingAction=="","empty hands attacked");
                });
                Test("cancel and equipment changes discard pending contact",()=>
                {
                    foreach(var attack in new[]{"use_item","grab"})
                    {
                        var m=Make(); Send(m,"a",attack); Tick(m,.04f); Send(m,"a","cancel"); Tick(m,.3f);
                        Check(m.Players["b"].stability==100&&m.Players["a"].grabbing=="","cancelled action resolved later: "+attack);
                    }
                    var swap=Make(); Send(swap,"a","use_item");
                    var p=swap.Players["a"];
                    swap.Receive("a",new Command {sequence=p.sequence+1,action="equip",itemId=swap.Items.First(x=>x.ownerId=="a"&&x.state==ItemState.Inventory).id});
                    Tick(swap,.2f); Check(swap.Players["b"].stability==100,"weapon switch allowed old pending hit");
                });
                Test("public prop is thrown by item use at contact",()=>
                {
                    var m=Make(); m.Players["b"].position=new Vector3(10,1,10);
                    var prop=new PublicProp {id="test-box",visual="box",position=new Vector3(0,1,1.10f)};
                    m.Props.Add(prop); Send(m,"a","grab"); Tick(m,.24f);
                    Check(m.Players["a"].publicProp==prop.id&&prop.holder=="a","box grab failed");
                    Send(m,"a","use_item"); Tick(m,.08f);
                    Check(prop.holder=="a","prop throw happened before item-use contact");
                    Tick(m,.12f);
                    Check(prop.holder==""&&m.Players["a"].publicProp==""&&prop.thrower=="a"&&prop.velocity.z>5,"item use did not throw the held prop");
                });
                Test("item use applies the existing full-charge template strength",()=>
                {
                    var m=Make(); Send(m,"a","use_item"); var victim=m.Players["b"];
                    Check(m.Players["a"].pendingCharge==1,"template did not receive full internal charge");
                    Tick(m,.12f); Check(victim.stability==100,"item use hit during anticipation");
                    Tick(m,.08f);
                    Check(victim.stability<100&&victim.impulse.magnitude>0,"item use lacked the existing template hit");
                });
                Test("ordinary item templates stagger but do not force a full-stability knockdown",()=>
                {
                    foreach(var template in new[]{"Melee","Disc","Ricochet","Deployable","Summon","Utility"})
                    {
                        var m=Make();var attacker=m.Players["a"];var victim=m.Players["b"];
                        m.Item(attacker.equipped).asset.templateId=template;
                        Send(m,"a","use_item");Tick(m,.2f);
                        Check(victim.stability<100,"template missed its use hit: "+template);
                        Check(victim.bodyMode==BodyMode.Stagger,"ordinary template forced knockdown: "+template);
                        Check(m.Items.Count(x=>x.ownerId=="b"&&x.state==ItemState.Ground)==0,"ordinary hit dropped Persona: "+template);
                        if(new[]{"Disc","Ricochet","Deployable","Summon"}.Contains(template))
                            Check(m.Effects.Any(e=>e.kind==template&&!e.knockdown),"effect carries forced knockdown: "+template);
                    }
                });
                Test("physical throw spends the actual Persona and empties target stability",()=>
                {
                    foreach(var visual in new[]{"basketball","bowling-pin","burger","veggie-burger","red-pen","tomato","potato","football-cc0"})
                    {
                        var m=Make();var thrower=m.Players["a"];var victim=m.Players["b"];
                        victim.position=new Vector3(0,1,3);
                        var item=m.Item(thrower.equipped);item.asset.templateId=visual=="football-cc0"?"Ricochet":"Projectile";item.asset.visualId=visual;
                        Send(m,"a","use_item");Tick(m,.12f);
                        Check(item.state==ItemState.Equipped,"throw resolved before contact: "+visual);
                        Tick(m,.08f);
                        Check(item.state==ItemState.Thrown&&thrower.equipped=="","throw did not remove the real item: "+visual);
                        Check(m.View("a").ground.Any(x=>x.id==item.id&&x.state==ItemState.Thrown),"flying model missing from snapshot: "+visual);
                        Check(m.Remaining("a")==3,"throw consumed its owner's life: "+visual);
                        Tick(m,.25f);
                        Check(item.state==ItemState.Ground,"hit did not land the thrown item: "+visual);
                        Check(victim.bodyMode==BodyMode.Falling&&victim.stability==0,"hit did not empty target stability: "+visual);
                        Check(m.Items.Count(x=>x.ownerId=="b"&&x.state==ItemState.Ground)==1,"hit did not drop exactly one target Persona: "+visual);
                        Check(m.Items.Count(x=>x.ownerId=="a"&&x.state==ItemState.Ground)==1,"thrown item was lost or copied: "+visual);
                        Tick(m,2.3f);
                        Check(victim.bodyMode==BodyMode.Standing&&victim.stability>=65,"throw victim did not regain stability on get-up: "+visual+" body="+victim.bodyMode+" stability="+victim.stability);
                        Check(m.Items.Count(x=>x.ownerId=="b"&&x.state==ItemState.Ground)==1,"get-up duplicated victim drop: "+visual);
                    }
                });
                Test("missed throw lands for owner recovery or opponent consumption",()=>
                {
                    foreach(var eater in new[]{"a","b"})
                    {
                        var m=Make();m.Players["b"].position=new Vector3(10,1,10);
                        var owner=m.Players["a"];var item=m.Item(owner.equipped);
                        item.asset.templateId="Projectile";item.asset.visualId="basketball";
                        Send(m,"a","use_item");Tick(m,1);
                        Check(item.state==ItemState.Ground&&m.View("a").ground.Count(x=>x.id==item.id)==1,"miss did not leave one ground model");
                        Check(m.Items.Count(x=>x.ownerId=="b"&&x.state==ItemState.Ground)==0,"miss hit a distant opponent");
                        var collector=m.Players[eater];collector.position=new Vector3(item.position.x,1,item.position.z);
                        Send(m,eater,"interact");
                        Check(collector.carrying==item.id,"ground throw pickup failed state="+item.state+" body="+collector.bodyMode+" time="+m.Time+" stagger="+collector.staggerUntil);
                        Tick(m,eater=="a"?1.6f:2.6f);
                        Check(eater=="a"?item.state==ItemState.Equipped&&owner.equipped==item.id:item.state==ItemState.Consumed&&m.Eaten.Count==1,
                            "ground throw could not be "+(eater=="a"?"recovered":"consumed")+" state="+item.state+" equipped="+owner.equipped+" carrying="+collector.carrying);
                    }
                });
                Test("HeavyWeapon and SpecialAbility retain explicit strong knockdown",()=>
                {
                    foreach(var template in new[]{"HeavyWeapon","SpecialAbility"})
                    {
                        var m=Make();var attacker=m.Players["a"];var victim=m.Players["b"];
                        m.Item(attacker.equipped).asset.templateId=template;
                        Send(m,"a","use_item");Tick(m,.2f);
                        Check(victim.bodyMode==BodyMode.Falling,"strong template did not knock down: "+template);
                        Check(victim.stability>0,"strong fixture unexpectedly depleted stability: "+template);
                        Check(m.Items.Count(x=>x.ownerId=="b"&&x.state==ItemState.Ground)==0,"non-depleting strong hit dropped Persona: "+template);
                    }
                });
                Test("stability exhaustion still drops once with ordinary item use",()=>
                {
                    var m=Make();var attacker=m.Players["a"];var victim=m.Players["b"];
                    victim.stability=40;
                    Send(m,"a","use_item");Tick(m,.2f);
                    Check(victim.bodyMode==BodyMode.Falling&&victim.stability==65,"depleting use did not fall and restore stability");
                    Check(m.Items.Count(x=>x.ownerId=="b"&&x.state==ItemState.Ground)==1,"depleting use did not drop exactly one Persona");
                    for(int i=0;i<30;i++){m.Hit(victim,attacker,999,12,true,true);m.Tick(Step);}
                    Check(m.Items.Count(x=>x.ownerId=="b"&&x.state==ItemState.Ground)==1,"downed follow-ups duplicated the drop");
                });
                Test("stagger and either disconnect clear all grip links",()=>
                {
                    foreach(var target in new[]{"a","b"})
                    {
                        var m=Make(); Connect(m); m.Disconnect(target); Tick(m,Step);
                        Check(m.Players.Values.All(p=>p.grabbing==""),"disconnect left grip link for "+target);
                    }
                    var hit=Make(); Connect(hit); hit.Hit(hit.Players["b"],hit.Players["a"],5,1,false); Tick(hit,Step);
                    Check(hit.Players.Values.All(p=>p.grabbing==""),"stagger left incoming grip link");
                });
                Test("grip exhaustion interrupts pending item use",()=>
                {
                    var m=Make(); Connect(m); var victim=m.Players["b"];
                    victim.stability=.1f; Send(m,"b","use_item");
                    Check(victim.pendingAction=="heavy","victim item-use fixture did not start");
                    Tick(m,Step);
                    Check(victim.staggerUntil>m.Time,"grip did not exhaust low stability");
                    Check(victim.pendingAction=="","grip exhaustion retained pending contact");
                    Check(m.Players["a"].grabbing=="","exhausted victim remained attached");
                    for(int i=0;i<55;i++) { Send(m,"b"); m.Tick(Step); }
                    Check(victim.pendingAction=="","keepalive input revived cancelled attack after stagger");
                });
                Test("out-of-map respawn cancels item use before controls recover",()=>
                {
                    var m=Make(); var fallen=m.Players["a"]; Send(m,"a","use_item");
                    Check(fallen.pendingAction=="heavy","respawn item-use fixture did not start");
                    fallen.position=new Vector3(0,-9,0); Tick(m,Step);
                    Check(fallen.position.y>0&&fallen.staggerUntil>m.Time,"out-of-map fixture did not respawn");
                    Check(fallen.pendingAction=="","respawn retained pending contact");
                    for(int i=0;i<40;i++) { Send(m,"a"); m.Tick(Step); }
                    Check(fallen.pendingAction=="","keepalive input revived attack after respawn");
                });
                Test("double tap W A S D independently starts and releases sprint",()=>
                {
                    foreach(int key in new[]{1,2,4,8})
                    {
                        var input=new LocomotionInput();
                        var axis=key==1?Vector2.up:key==2?Vector2.left:key==4?Vector2.down:Vector2.right;
                        Check(!input.Step(0,.02f,key,key,axis,false,true),"first tap sprinted "+key);
                        input.Step(.05f,.02f,0,0,Vector2.zero,false,true);
                        Check(input.Step(.15f,.02f,key,key,axis,false,true),"double tap failed "+key);
                        Check(input.Step(.4f,.02f,0,key,axis,false,true),"hold lost sprint "+key);
                        Check(!input.Step(.42f,.02f,0,0,Vector2.zero,false,true),"release retained sprint "+key);
                    }
                });
                Test("different keys do not combine into a double tap",()=>
                {
                    foreach(int first in new[]{1,2,4,8}) foreach(int second in new[]{1,2,4,8})
                    {
                        if(first==second)continue;
                        var input=new LocomotionInput();
                        input.Step(0,.02f,first,first,Vector2.up,false,true);
                        input.Step(.04f,.02f,0,0,Vector2.zero,false,true);
                        Check(!input.Step(.1f,.02f,second,second,Vector2.up,false,true),"mixed keys sprinted "+first+" -> "+second);
                    }
                });
                Test("modal lock and opposing keys clear sprint",()=>
                {
                    var input=new LocomotionInput();
                    input.Step(0,.02f,1,1,Vector2.up,false,true); input.Step(.04f,.02f,0,0,Vector2.zero,false,true);
                    Check(input.Step(.1f,.02f,1,1,Vector2.up,false,true),"fixture did not sprint");
                    Check(!input.Step(.12f,.02f,4,5,Vector2.zero,false,true),"opposite keys retained sprint");
                    input.Step(.2f,.02f,0,0,Vector2.zero,false,false);
                    Check(!input.Step(.24f,.02f,1,1,Vector2.up,false,true),"modal retained previous double-tap history");
                });
            }
            finally { GeometryCache.SetValue(null,previous); }
            if(failures.Count>0) throw new Exception("ABOUTME_SOFT_COMBAT_TESTS_FAILED "+failures.Count+" / "+(passed+failures.Count)+"\n"+string.Join("\n",failures));
            Debug.Log("ABOUTME_SOFT_COMBAT_TESTS_PASSED "+passed);
        }
    }
}
