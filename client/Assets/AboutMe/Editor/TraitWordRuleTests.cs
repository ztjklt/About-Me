using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace AboutMe.Editor
{
    // Executes production Match commands and ticks with deterministic profiles, not model-generated or human gameplay evidence.
    public static class TraitWordRuleTests
    {
        [Serializable] public class Case { public string name, status, actual; }
        [Serializable] public class Results { public string environment, utc, scope, runtimeSnapshot; public int passed, failed; public Case[] cases; }
        static readonly FieldInfo Cache=typeof(CampusGeometry).GetField("solids",BindingFlags.NonPublic|BindingFlags.Static);
        static CampusSolid[] Flat=>new[]{new CampusSolid{name="word_test_floor",center=new Vector3(0,-.5f,0),size=new Vector3(60,1,60)}};
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static PersonaAsset Word(string ability="flame-fan",string text="热情",string category="personality")=>new PersonaAsset{assetId="fixture-word-"+text,name=text,templateId="SpecialAbility",visualId=TraitAbilityCatalog.VisualId,wordCategory=category,wordText=text,abilityId=ability,color="#86ADCB",source="PRIVATE_SOURCE_SENTINEL"};
        static Profile Profile(string id,int count=4)=>new Profile{playerId=id,nickname=id,personas=Enumerable.Range(0,count).Select(n=>new PersonaAsset{assetId=id+"-"+n,name="书",visualId="book",templateId="Melee",color="#86ADCB",source="PRIVATE_SOURCE_SENTINEL"}).ToArray()};
        static Match Make(string ability="flame-fan",int count=4)
        {
            var m=new Match("WORD-RULE-QA",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999});
            for(int i=0;i<3;i++){var p=Profile("p"+i,count);if(i==0)p.personas[0]=Word(ability);Check(m.Add(p),"fixture profile rejected");}
            m.Phase=MatchPhase.Reveal;m.Tick(.02f);m.Time=10;m.Props.Clear();
            for(int i=0;i<3;i++){var p=m.Players["p"+i];Place(m,p,new Vector3(i*10,1,0));p.input=new Command();p.lastInput=m.Time;p.yaw=0;}
            return m;
        }
        static void Place(Match m,Player p,Vector3 at)=>PartyMachineryTests.Place(m,p,at);
        static void Input(Match m,string id,string action="",string item="",float x=0,float z=0){var p=m.Players[id];m.Receive(id,new Command{sequence=p.sequence+1,action=action,itemId=item,x=x,z=z,yaw=p.yaw});}
        static void Tick(Match m,float seconds,string mover=null,float x=0,float z=0)=>PartyFieldTests.Tick(m,seconds,mover,x,z);
        static EffectState Deploy(Match m,string id="p0")
        {var item=m.Item(m.Players[id].equipped);var d=PartyMechanicRegistry.ForAsset(item.asset);Input(m,id,"use_item");Tick(m,d.windup+.02f);return m.Effects.LastOrDefault(e=>e.kind=="PartyMechanic"&&e.sourceInstance==item.id)??throw new Exception("no actual ability effect: "+d.id);}
        static PersonaInstance DropNear(Match m,string owner="p0",string borrower="p1")
        {var item=m.Item(m.Players[owner].equipped);m.Drop(m.Players[owner],1);item.velocity=Vector3.zero;Place(m,m.Players[borrower],item.position+Vector3.up*.6f);return item;}
        static float Cooldown(Match m,string id)=>m.View(id).players.First(p=>p.id==id).mechanicCooldown;
        [MenuItem("About Me/Tests/R5 trait word authority")]
        public static void Run()
        {
            var old=Cache.GetValue(null);var cases=new List<Case>();
            void Test(string name,Action run){Cache.SetValue(null,Flat);try{run();cases.Add(new Case{name=name,status="passed",actual="production Match assertions passed"});Debug.Log("TRAIT_WORD_PASS "+name);}catch(Exception e){cases.Add(new Case{name=name,status="failed",actual=e.ToString()});Debug.LogError("TRAIT_WORD_FAIL "+name+": "+e);}}
            try
            {
                Test("directory: every trait, six moods, sixteen MBTI reuse six existing rules",()=>{
                    Check(PartyMechanicRegistry.All.Length==96&&PartyMechanicRegistry.All.Select(d=>d.id).Distinct().Count()==96,"mechanic count changed");
                    Check(TraitAbilityCatalog.Data.mbti.Length==16&&TraitAbilityCatalog.Data.mbti.Select(w=>w.text).Distinct().Count()==16,"MBTI directory incomplete");
                    Check(TraitAbilityCatalog.Data.words.Count(w=>w.category=="mood")==6,"mood directory incomplete");
                    var expected=new Dictionary<string,string>{{"ENFP","flame-fan"},{"ESFP","flame-fan"},{"INFP","bubble-lift"},{"ENFJ","bubble-lift"},{"ISFP","bubble-lift"},{"ESFJ","bubble-lift"},{"ENTJ","front-shield"},{"ISFJ","front-shield"},{"ESTJ","front-shield"},{"INTJ","calm-dome"},{"ISTP","calm-dome"},{"ISTJ","calm-dome"},{"INFJ","tractor-loose"},{"INTP","tractor-loose"},{"ENTP","spring-pad"},{"ESTP","spring-pad"}};
                    foreach(var w in TraitAbilityCatalog.Words){var a=Word(w.abilityId,w.text,w.category);Check(ReferenceEquals(PartyMechanicRegistry.ForAsset(a),PartyMechanicRegistry.Find(w.abilityId)),"new rule or wrong mapping "+w.text);Check(PartyMechanicRegistry.ForAsset(a.PublicCopy()).id==w.abilityId,"public mapping lost "+w.text);if(w.category=="mbti")Check(w.abilityId==expected[w.text],"MBTI mapping wrong "+w.text);foreach(var alias in w.aliases??Array.Empty<string>())Check(PartyMechanicRegistry.ForAsset(Word(w.abilityId,alias,w.category))!=null,"alias unsupported "+alias);}
                });
                Test("explicit ability change and custom words use only allowlisted abilities",()=>{foreach(var a in TraitAbilityCatalog.Data.abilities){Check(PartyMechanicRegistry.ForAsset(Word(a.id,"ENFP","mbti")).id==a.id,"cannot change MBTI ability");Check(PartyMechanicRegistry.ForAsset(Word(a.id,"耐心","custom")).id==a.id,"custom ability invalid");}Check(PartyMechanicRegistry.ForAsset(Word("arbitrary-code"))==null,"unknown rule accepted");Check(PartyMechanicRegistry.ForAsset(Word("flame-fan","陌生","personality"))==null,"unknown known-category word accepted");});
                Test("twelve asset budget and defense against thirteen",()=>{using(var m=Make(count:12)){Check(m.Items.Count==36&&m.Remaining("p0")==12,"12-asset profile changed");using(var lobby=new Match("BUDGET",new MatchRules())){Check(!lobby.Add(Profile("p3",13))&&lobby.Items.Count==0,"lobby accepted 13 assets");Check(lobby.Add(Profile("p3",12))&&lobby.Items.Count==12,"lobby rejected 12 assets");}var item=DropNear(m);Input(m,"p1","borrow_word",item.id);Check(m.Items.Count==36&&m.Remaining("p0")==12&&m.Remaining("p1")==12,"borrowing duplicated life");}});
                Test("all six word abilities dispatch through actual Match use",()=>{foreach(var a in TraitAbilityCatalog.Data.abilities)using(var m=Make(a.id)){var item=m.Item(m.Players["p0"].equipped);var e=Deploy(m);Check(e.mechanicId==a.id&&e.sourceInstance==item.id&&e.wordText==item.asset.wordText,"ability dispatched wrong rule "+a.id);Check(m.Remaining("p0")==4&&m.Items.Count==12,"ability created extra asset "+a.id);}});
                Test("simultaneous borrowing has one holder and owner cannot equip stolen word",()=>{using(var m=Make(count:1)){var item=DropNear(m);Place(m,m.Players["p2"],m.Players["p1"].position);Input(m,"p1","borrow_word",item.id);Input(m,"p2","borrow_word",item.id);Input(m,"p0","equip",item.id);Check(item.carrierId=="p1"&&m.Players["p1"].equipped==item.id&&m.Players["p2"].equipped!=item.id&&m.Players["p0"].equipped!=item.id,"multiple equipped holders");m.Drop(m.Players["p0"],1);Check(item.state==ItemState.Equipped&&item.carrierId=="p1","owner drop duplicated stolen word");}});
                Test("release remains available during shield and stagger",()=>{using(var m=Make("front-shield")){var item=DropNear(m);Input(m,"p1","borrow_word",item.id);Tick(m,.32f);var e=Deploy(m,"p1");m.Players["p1"].staggerUntil=m.Time+2;Input(m,"p1","release_word");Check(item.state==ItemState.Ground&&m.Players["p1"].equipped==""&&!m.Effects.Contains(e),"guard or stagger trapped borrowed word");}});
                Test("formal word flame damages front target twice and excludes side",()=>{using(var m=Make()){Place(m,m.Players["p1"],new Vector3(0,1,2));Place(m,m.Players["p2"],new Vector3(2,1,0));var e=Deploy(m);Tick(m,.8f);Check(m.Players["p1"].stability==76&&m.Players["p2"].stability==100,"flame damage/cone incorrect");Check(m.CombatEvents.Count(x=>x.kind=="party_flame")==2,"flame pulses not actual");Check(e.wordText=="热情"&&e.abilityId=="flame-fan","effect lost word identity");}});
                Test("flame and borrowing respect solid wall",()=>{using(var m=Make()){Place(m,m.Players["p1"],new Vector3(0,1,2));Cache.SetValue(null,Flat.Concat(new[]{new CampusSolid{name="wall",center=new Vector3(0,1,1),size=new Vector3(3,2,.2f)}}).ToArray());Deploy(m);Tick(m,.8f);Check(m.Players["p1"].stability==100,"word flame crossed wall");m.Drop(m.Players["p0"],1);var item=m.Items.First(i=>i.asset.visualId==TraitAbilityCatalog.VisualId);item.position=new Vector3(0,.4f,0);item.velocity=Vector3.zero;Input(m,"p1","borrow_word",item.id);Check(item.state==ItemState.Ground,"borrow crossed wall");}});
                Test("bubble word actually lifts and allows lateral movement",()=>{using(var m=Make("bubble-lift")){Place(m,m.Players["p1"],new Vector3(0,1,2.4f));var e=Deploy(m);Tick(m,.25f);Check(e.stage=="attached"&&e.targetId=="p1","bubble never attached");var b=m.Players["p1"];float y=b.motorHeight,x=b.position.x;Tick(m,.35f,"p1",1,0);Check(b.motorHeight>y+.2f&&b.motorHeight<=e.height+.72f&&b.position.x>x+.4f&&b.stability==100,"bubble lift/drift wrong");}});
                Test("bubble pops on actual target item attack",()=>{using(var m=Make("bubble-lift")){Place(m,m.Players["p1"],new Vector3(0,1,2.4f));var e=Deploy(m);Tick(m,.25f);Check(e.stage=="attached","bubble not attached");Input(m,"p1","use_item");Tick(m,.35f);Check(!m.Effects.Contains(e),"bubble survived target attack");Check(m.Remaining("p0")==4&&m.Remaining("p1")==4,"bubble pop consumed life");}});
                Test("last word borrowing preserves owner and borrower lives without swallowing",()=>{using(var m=Make(count:1)){var item=DropNear(m);Input(m,"p1","borrow_word",item.id);Tick(m,.4f);Check(item.state==ItemState.Equipped&&item.carrierId=="p1"&&item.ownerId=="p0"&&m.Players["p1"].equipped==item.id,"word was not equipped by borrower");Check(m.Remaining("p0")==1&&m.Remaining("p1")==1&&!m.Players["p0"].eliminated&&m.Eaten.Count==0,"borrow swallowed or eliminated owner");Check(m.View("p1").inventory.Any(i=>i.id==item.id)&&!m.View("p0").inventory.Any(i=>i.id==item.id),"borrowed inventory recipient wrong");}});
                Test("per-instance cooldown survives drop, borrow and original recovery",()=>{using(var m=Make()){var e=Deploy(m);var item=DropNear(m);Check(!m.Effects.Contains(e),"drop did not cancel flame immediately");Input(m,"p1","borrow_word",item.id);Tick(m,.32f);float borrowed=Cooldown(m,"p1");Check(borrowed>1,"transfer cleared cooldown");Input(m,"p1","use_item");Tick(m,.3f);Check(!m.Effects.Any(x=>x.sourceInstance==item.id),"borrower bypassed cooldown");Input(m,"p1","release_word");item.velocity=Vector3.zero;Place(m,m.Players["p0"],item.position+Vector3.up*.6f);Input(m,"p0","interact",item.id);Check(m.Players["p0"].interaction=="recover","owner cannot recover");Tick(m,m.Rules.recoverDuration+.04f);Input(m,"p0","equip",item.id);Check(item.state==ItemState.Equipped&&Cooldown(m,"p0")>0,"recovery reset cooldown");Check(m.Remaining("p0")==4&&m.Remaining("p1")==4,"transfer/recover changed life budget");}});
                Test("borrowed word can be knocked loose then recovered by owner",()=>{using(var m=Make()){var item=DropNear(m);Input(m,"p1","borrow_word",item.id);m.Drop(m.Players["p1"],1);Check(item.state==ItemState.Ground&&item.carrierId==""&&m.Players["p1"].equipped=="","knock did not drop borrowed word first");item.velocity=Vector3.zero;Place(m,m.Players["p0"],item.position+Vector3.up*.6f);Input(m,"p0","interact",item.id);Tick(m,m.Rules.recoverDuration+.04f);Check(item.state==ItemState.Equipped||item.state==ItemState.Inventory,"owner recovery failed");Check(item.ownerId=="p0"&&m.Remaining("p0")==4&&m.Remaining("p1")==4,"owner changed");}});
                Test("explicit E after borrow swallows once and charges original life only",()=>{using(var m=Make(count:1)){var item=DropNear(m);Input(m,"p1","borrow_word",item.id);Tick(m,.32f);Input(m,"p1","interact");Check(item.state==ItemState.Carried&&m.Players["p1"].interaction=="eat","E did not explicitly start eat");Check(m.Remaining("p0")==1&&!m.Players["p0"].eliminated,"eat start charged life early");Tick(m,m.Rules.eatDuration+.04f);Check(item.state==ItemState.Consumed&&m.Remaining("p0")==0&&m.Players["p0"].eliminated&&m.Remaining("p1")==1&&m.Eaten.Count==1,"eat/life accounting wrong");Input(m,"p1","interact",item.id);Tick(m,.2f);Check(m.Eaten.Count==1,"duplicate consume");}});
                Test("release, inventory equip and disconnect terminate holder flame",()=>{foreach(var loss in new[]{"release","equip","disconnect"})using(var m=Make()){var item=DropNear(m);Input(m,"p1","borrow_word",item.id);Tick(m,.32f);var e=Deploy(m,"p1");if(loss=="release")Input(m,"p1","release_word");else if(loss=="equip")Input(m,"p1","equip",m.Items.First(i=>i.ownerId=="p1"&&i.state==ItemState.Inventory).id);else m.Disconnect("p1");Check(!m.Effects.Contains(e)&&item.state==ItemState.Ground&&item.carrierId=="","holder loss retained field: "+loss);Check(m.Remaining("p0")==4&&m.Remaining("p1")==4,"holder loss charged life");}});
                Test("ordinary physical props keep E eat and cannot be borrowed",()=>{using(var m=Make()){var item=m.Item(m.Players["p2"].equipped);m.Drop(m.Players["p2"],1);item.velocity=Vector3.zero;Place(m,m.Players["p1"],item.position+Vector3.up*.6f);Input(m,"p1","borrow_word",item.id);Check(item.state==ItemState.Ground,"ordinary prop was borrowable");Input(m,"p1","interact",item.id);Check(item.state==ItemState.Carried&&m.Players["p1"].interaction=="eat","ordinary E semantics changed");Tick(m,m.Rules.eatDuration+.04f);Check(item.state==ItemState.Consumed&&m.Remaining("p2")==3&&m.Remaining("p1")==4,"ordinary swallow changed");}});
                Test("public snapshot has protocol four and no private original source",()=>{using(var m=Make()){var item=DropNear(m);Input(m,"p1","borrow_word",item.id);foreach(var id in m.Players.Keys){var view=m.View(id);string json=JsonUtility.ToJson(view);Check(view.protocolVersion==4,"protocol not four");Check(!json.Contains("PRIVATE_SOURCE_SENTINEL")&&!json.Contains("\"source\":")&&!json.Contains("introduction"),"private introduction leaked");Check(view.players.First(p=>p.id=="p1").equippedWordText=="热情","public word identity missing");}}});
            }
            finally
            {
                Cache.SetValue(null,old);
                var root=Directory.GetParent(Application.dataPath).Parent.FullName;
                var files=Directory.GetFiles(Path.Combine(Application.dataPath,"AboutMe/Runtime"),"*.cs").OrderBy(s=>s,StringComparer.Ordinal);
                var bytes=Encoding.UTF8.GetBytes(string.Join("\n",files.Select(f=>Path.GetFileName(f)+":"+BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(f))).Replace("-","").ToLowerInvariant())));
                var result=new Results{environment=Application.unityVersion+" / macOS Editor batchmode",utc=DateTime.UtcNow.ToString("o"),scope="Production Match commands and 50 Hz ticks; deterministic explicit profiles, flat geometric campus fixture. Not real AI, network transport, rendered mesh, human play, six-player performance or mobile acceptance.",runtimeSnapshot=BitConverter.ToString(SHA256.Create().ComputeHash(bytes)).Replace("-","").ToLowerInvariant(),passed=cases.Count(c=>c.status=="passed"),failed=cases.Count(c=>c.status=="failed"),cases=cases.ToArray()};
                var output=Path.Combine(root,"artifacts/trait-persona-r5/rules-test-results.json");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(result,true));
            }
            if(cases.Any(c=>c.status=="failed"))throw new Exception(string.Join("\n",cases.Where(c=>c.status=="failed").Select(c=>c.name+": "+c.actual)));
            Debug.Log("TRAIT_WORD_RULES_PASSED "+cases.Count);
        }
    }
}
