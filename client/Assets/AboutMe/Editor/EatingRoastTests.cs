using System;
using System.Linq;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class EatingRoastTests
    {
        public static void Run()
        {
            int passed=0;
            void Check(bool value,string label){if(!value)throw new Exception("EATER_ROAST_FAILED "+label);passed++;Debug.Log("EATER_ROAST_PASS "+label);}
            Match Create(bool ai=false)
            {
                var m=new Match("ROAST",new MatchRules{eatDuration=.1f,handInterval=999,finalTime=999});
                var a=GameNetwork.SampleProfile("human","真人");a.playerId="human";
                var b=GameNetwork.SampleProfile("npc","人机");if(ai){b.characterSkin="ds-girl";b.dsGirlUnlocked=true;}
                m.Add(a);m.Add(b);m.Phase=MatchPhase.Battle;return m;
            }
            PersonaInstance Ground(Match m,string owner,string eater)
            {
                var p=m.Players[eater];var item=m.Items.First(x=>x.ownerId==owner);item.state=ItemState.Ground;item.carrierId="";item.position=p.position;item.velocity=Vector3.zero;return item;
            }
            var m=Create();var npc=m.Players.Keys.Single(x=>x!="human");var ball=Ground(m,npc,"human");m.Interact(m.Players["human"],ball.id);
            Check(m.Eaten.Count==0&&m.EatingRoasts.Count==1,"human triggers before completion");
            var attempt=m.EatingRoasts.Single();Check(attempt.eaterId=="human"&&attempt.itemId==ball.id,"consumer and actual item retained");
            const string line="唱跳缺个篮球";
            Check(m.SayEatingRoast(attempt,line),"provider reply accepted during eating");
            var view=m.View(npc);var spoken=view.events.Last();Check(spoken.speakerId=="human"&&spoken.itemId==ball.id&&spoken.line==line&&spoken.text.StartsWith("真人："),"observers receive attributed dialogue");
            m.Events.Clear();Check(!m.SayEatingRoast(attempt,line),"duplicate reply remains suppressed after event history eviction");m.Interrupt(m.Players["human"]);
            Check(attempt.cancelled&&!m.SayEatingRoast(attempt,line),"interrupted attempt cannot speak later");
            m.Interact(m.Players["human"],ball.id);var retry=m.EatingRoasts.Last();Check(retry.id!=attempt.id,"new attempt has independent identity");m.Dispose();
            var own=Create();Ground(own,"human","human");own.Interact(own.Players["human"]);Check(own.EatingRoasts.Count==0,"recovering own Persona is silent");own.Dispose();
            foreach(bool ai in new[]{false,true})
            {
                var bot=Create(ai);var botId=bot.Players.Keys.Single(x=>x!="human");var item=Ground(bot,"human",botId);bot.Interact(bot.Players[botId],item.id);
                Check(bot.EatingRoasts.Single().eaterId==botId,(ai?"AI skin":"NPC")+" triggers independently");
                for(int i=0;i<8;i++)bot.Tick(.02f);
                var started=bot.EatingRoasts.Single();Check(started.completed&&bot.SayEatingRoast(started,line),"late response still belongs to completed consumer");bot.Dispose();
            }
            var word=Create();var wordBot=word.Players.Keys.Single(x=>x!="human");var borrowed=word.Items.First(x=>x.ownerId==wordBot);
            borrowed.asset.templateId="SpecialAbility";borrowed.asset.visualId="trait-word";borrowed.asset.name="理性";borrowed.asset.wordText="理性";borrowed.asset.abilityId="front-shield";
            borrowed.state=ItemState.Equipped;borrowed.carrierId="human";word.Players["human"].equipped=borrowed.id;word.Interact(word.Players["human"]);
            Check(word.EatingRoasts.Single().itemId==borrowed.id,"borrowed word uses same eater event");word.Dispose();
            var prepared=Create();var preparedBot=prepared.Players.Keys.Single(x=>x!="human");
            var preparedItem=Ground(prepared,preparedBot,"human");prepared.PreparedRoasts[Match.RoastItemKey(preparedItem.asset)]=line;
            prepared.Interact(prepared.Players["human"],preparedItem.id);
            Check(prepared.Events.Last().kind=="eating-roast"&&prepared.EatingRoasts.Single().spoken&&prepared.Eaten.Count==0,"prepared line speaks synchronously before any eat tick");
            prepared.Interrupt(prepared.Players["human"]);Check(prepared.EatingRoasts.Single().cancelled,"cached attempt still cancels normally");prepared.Dispose();
            var loading=Create();loading.Phase=MatchPhase.Reveal;loading.Rules.revealDuration=.1f;loading.Time=0;loading.RoastPreparationPending=true;
            for(int i=0;i<8;i++)loading.Tick(.02f);
            Check(loading.Phase==MatchPhase.Reveal,"battle waits for opening preparation");
            loading.RoastPreparationPending=false;loading.Tick(.02f);Check(loading.Active,"battle begins once preparation finishes or fails");loading.Dispose();
            var occupied=new System.Collections.Generic.List<Rect>();
            for(int i=0;i<7;i++)
            {
                var point=EaterRoastPresentation.PlaceBubble(new Vector2(0,-160),new Vector2(1600,900),occupied);
                var rect=new Rect(point-new Vector2(82,30),new Vector2(164,60));
                Check(!occupied.Any(other=>other.Overlaps(rect)),"concurrent bubble "+i+" does not overlap");occupied.Add(rect);
            }
            Check(occupied.All(x=>Mathf.Abs(x.center.x)<.001f),"crowded bubbles stay directly above each speaker");
            Debug.Log("EATER_ROAST_PASSED "+passed);
        }
    }
}
