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
                    var m=Make(); Connect(m); Send(m,"a","light");
                    Check(m.Players["a"].grabbing=="","grabber can attack while retaining victim");
                });
                Test("grabbed player can counterattack and interrupt grabber",()=>
                {
                    var m=Make(); Connect(m); var before=m.Players["a"].stability;
                    Send(m,"b","light"); Tick(m,.14f);
                    Check(m.Players["a"].stability<before,"victim's counterattack was suppressed");
                    Check(m.Players["a"].grabbing=="","hit grabber kept grip");
                });
                Test("light strike waits for contact and deals damage only once",()=>
                {
                    var m=Make(); var victim=m.Players["b"]; var before=victim.stability;
                    Send(m,"a","light"); Check(victim.stability==before,"light hit immediately");
                    Tick(m,.06f); Check(victim.stability==before,"light hit during anticipation");
                    Tick(m,.08f); var hit=victim.stability;
                    Check(hit<before,"light missed valid contact window");
                    Tick(m,.5f); Check(Mathf.Abs(victim.stability-hit)<.001f,"one strike dealt repeated damage");
                });
                Test("cancel and equipment changes discard pending contact",()=>
                {
                    foreach(var attack in new[]{"light","grab"})
                    {
                        var m=Make(); Send(m,"a",attack); Tick(m,.04f); Send(m,"a","cancel"); Tick(m,.3f);
                        Check(m.Players["b"].stability==100&&m.Players["a"].grabbing=="","cancelled action resolved later: "+attack);
                    }
                    var swap=Make(); Send(swap,"a","light");
                    var p=swap.Players["a"];
                    swap.Receive("a",new Command {sequence=p.sequence+1,action="equip",itemId=swap.Items.First(x=>x.ownerId=="a"&&x.state==ItemState.Inventory).id});
                    Tick(swap,.2f); Check(swap.Players["b"].stability==100,"weapon switch allowed old pending hit");
                });
                Test("public prop remains held through charge and is thrown at contact",()=>
                {
                    var m=Make(); m.Players["b"].position=new Vector3(10,1,10);
                    var prop=new PublicProp {id="test-box",visual="box",position=new Vector3(0,1,1.10f)};
                    m.Props.Add(prop); Send(m,"a","grab"); Tick(m,.24f);
                    Check(m.Players["a"].publicProp==prop.id&&prop.holder=="a","box grab failed");
                    Send(m,"a","charge");
                    Check(m.Players["a"].publicProp==prop.id&&prop.holder=="a","charge prematurely released public prop");
                    for(int i=0;i<20;i++) { Send(m,"a"); m.Tick(Step); }
                    Send(m,"a","heavy"); Tick(m,.08f);
                    Check(prop.holder=="a","prop throw happened before heavy contact");
                    Tick(m,.12f);
                    Check(prop.holder==""&&m.Players["a"].publicProp==""&&prop.thrower=="a"&&prop.velocity.z>5,"heavy did not throw the held prop");
                });
                Test("charged heavy strike is delayed and stronger than light",()=>
                {
                    var light=Make(); Send(light,"a","light"); Tick(light,.14f);
                    float lightLoss=100-light.Players["b"].stability;
                    var heavy=Make(); Send(heavy,"a","charge");
                    for(int i=0;i<30;i++) { Send(heavy,"a"); heavy.Tick(Step); }
                    Send(heavy,"a","heavy"); var victim=heavy.Players["b"];
                    Tick(heavy,.12f); Check(victim.stability==100,"heavy hit during anticipation");
                    Tick(heavy,.08f);
                    Check(100-victim.stability>lightLoss,"charged heavy is not stronger than light");
                    Check(victim.impulse.magnitude>light.Players["b"].impulse.magnitude,"heavy has no stronger recoil");
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
                Test("grip exhaustion interrupts a victim's held heavy charge",()=>
                {
                    var m=Make(); Connect(m); var victim=m.Players["b"];
                    victim.stability=.1f; Send(m,"b","charge");
                    Check(victim.chargingSince>=0,"victim charge fixture did not start");
                    Tick(m,Step);
                    Check(victim.staggerUntil>m.Time,"grip did not exhaust low stability");
                    Check(victim.chargingSince<0&&victim.pendingAction=="","grip exhaustion retained heavy charge or pending contact");
                    Check(m.Players["a"].grabbing=="","exhausted victim remained attached");
                    for(int i=0;i<55;i++) { Send(m,"b"); m.Tick(Step); }
                    Check(victim.chargingSince<0,"keepalive input revived cancelled charge after stagger");
                });
                Test("out-of-map respawn cancels charge before controls recover",()=>
                {
                    var m=Make(); var fallen=m.Players["a"]; Send(m,"a","charge");
                    Check(fallen.chargingSince>=0,"respawn charge fixture did not start");
                    fallen.position=new Vector3(0,-9,0); Tick(m,Step);
                    Check(fallen.position.y>0&&fallen.staggerUntil>m.Time,"out-of-map fixture did not respawn");
                    Check(fallen.chargingSince<0&&fallen.pendingAction=="","respawn retained heavy charge or pending contact");
                    for(int i=0;i<40;i++) { Send(m,"a"); m.Tick(Step); }
                    Check(fallen.chargingSince<0,"keepalive input revived charge after respawn");
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
