using System;
using System.Collections.Generic;
using System.Linq;
namespace AboutMe
{
    // An authority-owned attempt belongs to the consumer, regardless of who controls it.
    public sealed class EatingRoastAttempt
    {
        public string id,eaterId,itemId;
        public bool cancelled,completed,spoken;
    }
    public partial class Match
    {
        public readonly List<EatingRoastAttempt> EatingRoasts=new List<EatingRoastAttempt>();
        void BeginEatingRoast(Player eater,PersonaInstance item)
        {
            if(item.ownerId==eater.profile.playerId)return;
            EatingRoasts.Add(new EatingRoastAttempt{id=Guid.NewGuid().ToString("N"),eaterId=eater.profile.playerId,itemId=item.id});
        }
        EatingRoastAttempt CurrentEatingRoast(Player eater)=>EatingRoasts.LastOrDefault(x=>x.eaterId==eater.profile.playerId&&!x.completed&&!x.cancelled);
        void CancelEatingRoast(Player eater)
        {
            var attempt=CurrentEatingRoast(eater);if(attempt!=null)attempt.cancelled=true;
        }
        void CompleteEatingRoast(Player eater,PersonaInstance item)
        {
            var attempt=CurrentEatingRoast(eater);if(attempt!=null&&attempt.itemId==item.id)attempt.completed=true;
        }
        public bool SayEatingRoast(EatingRoastAttempt attempt,string line)
        {
            if(attempt==null||attempt.cancelled||attempt.spoken||!EatingRoasts.Contains(attempt)||string.IsNullOrWhiteSpace(line)||!Players.TryGetValue(attempt.eaterId,out var eater)||!eater.connected)return false;
            if(!attempt.completed&&(eater.interaction!="eat"||eater.carrying!=attempt.itemId))return false;
            if(Events.Any(x=>x.roastId==attempt.id))return false;
            attempt.spoken=true;
            Events.Add(new MatchEvent{id=++eventId,kind="eating-roast",speakerId=attempt.eaterId,itemId=attempt.itemId,roastId=attempt.id,line=line,spokenAt=Time,text=eater.profile.nickname+"：「"+line+"」"});
            if(Events.Count>12)Events.RemoveAt(0);
            return true;
        }
    }
}
