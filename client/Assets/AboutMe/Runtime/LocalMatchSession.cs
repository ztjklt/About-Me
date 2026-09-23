using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    public interface IMatchSession
    {
        Snapshot Current { get; }
        void Submit(Command command);
        void Close();
    }

    public sealed class LocalMatchSession : IMatchSession
    {
        public readonly Match Match;
        public readonly string PlayerId;
        readonly NpcController npc;
        float nextThink;
        bool closed;
        public Snapshot Current => closed?null:Match.View(PlayerId);
        public LocalMatchSession(Profile player, MatchRules rules=null)
        {
            // Never mutate the cached persistent profile during a match.
            var copy=JsonUtility.FromJson<Profile>(JsonUtility.ToJson(player));PlayerId=copy.playerId;
            Match=new Match("SOLO",rules??new MatchRules());if(Application.isPlaying)Match.EnableBodyPhysics();Match.Add(copy);
            var opponent=GameNetwork.SampleProfile("npc-"+Guid.NewGuid().ToString("N"),"陪练 NPC");
            opponent.appearance=AppearanceData.Demo(1);
            var catalog=opponent.personas;
            opponent.personas=Enumerable.Range(0,copy.personas.Length).Select(i=>{
                var asset=catalog[i%catalog.Length].Copy();asset.assetId="npc-asset-"+i;return asset;
            }).ToArray();
            Match.Add(opponent);npc=new NpcController(opponent.playerId);
            Match.Receive(PlayerId,new Command{sequence=0,action="start"});
        }
        public void Tick(float dt)
        {
            if(closed||Match.Phase==MatchPhase.Finished)return;
            if(Match.Active&&Match.Time>=nextThink){nextThink=Match.Time+.1f;Match.Receive(npc.Id,npc.Decide(Match.View(npc.Id)));}
            Match.Tick(dt);
        }
        public void Submit(Command command){if(!closed)Match.Receive(PlayerId,command);}
        public void Close(){closed=true;Match.Dispose();}
    }

    // NPC sees the same public snapshot and own inventory as a human client.
    public sealed class NpcController
    {
        public readonly string Id;
        int sequence, attacks;
        float nextAttack, nextPath, stuckAt, reactionUntil, grabReactionAt=-1, nextGrab;
        string trackedEnemy="";
        Vector3 previous, destination;
        readonly Queue<Vector3> path=new Queue<Vector3>();
        public NpcController(string id){Id=id;}
        public Command Decide(Snapshot state)
        {
            var c=new Command{sequence=++sequence};
            var me=state.players.FirstOrDefault(p=>p.id==Id);
            if(me==null||me.eliminated||state.phase!=MatchPhase.Battle)return c;
            float now=state.simulationTime;
            var enemy=state.players.Where(p=>p.id!=Id&&!p.eliminated).OrderBy(p=>Vector3.Distance(p.position,me.position)).FirstOrDefault();
            if(enemy==null)return c;
            if(trackedEnemy!=enemy.id){trackedEnemy=enemy.id;reactionUntil=now+.35f;}
            if(me.grabbed){if(grabReactionAt<0)grabReactionAt=now+.35f;c.escapeHeld=now>=grabReactionAt;c.yaw=me.yaw;return c;}
            grabReactionAt=-1;
            c.yaw=me.yaw;
            bool danger=state.handAnimation>0&&now-state.handStartedAt<.65f&&Mathf.Abs(me.position.y-state.handPosition.y)<2&&FlatDistance(me.position,state.handPosition)<4;
            if(danger)
            {
                if(me.interaction!="")c.action="cancel";
                var away=me.position-state.handPosition;away.y=0;if(away.sqrMagnitude<.1f)away=Vector3.forward;
                Move(c,me.position,me.position+away.normalized*5,now);return c;
            }
            if(me.stagger>0)return c;
            if(me.interaction!="")return c;
            var drops=state.ground.Where(x=>x.state==ItemState.Ground).ToArray();
            var item=drops.OrderBy(x=>x.ownerId==Id&&me.remaining==1?0:x.ownerId!=Id?1:2)
                .ThenBy(x=>Vector3.Distance(x.position,me.position)).FirstOrDefault();
            if(item!=null)
            {
                if(Vector3.Distance(item.position,me.position)<2.15f){c.action="interact";c.itemId=item.id;return c;}
                Move(c,me.position,item.position,now);return c;
            }
            if(me.equippedVisual==""&&state.inventory.Length>0){c.action="equip";c.itemId=state.inventory[0].id;return c;}
            if(now<reactionUntil)return c;
            float distance=Vector3.Distance(enemy.position,me.position);
            var aim=enemy.position-me.position;c.yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
            if(distance>1.08f)Move(c,me.position,enemy.position,now);
            if(distance<2.4f&&now>=nextAttack)
            {
                attacks++;
                if(me.grabbing!=""){c.action="cancel";nextAttack=now+.4f;nextGrab=now+2.5f;}
                else if(attacks%5==0&&distance<1.25f&&now>=nextGrab){c.action="grab";nextAttack=now+1.2f;nextGrab=now+3.5f;}
                else if(me.equippedVisual!=""||me.publicProp!="") {c.action="use_item";nextAttack=now+1.1f;}
            }
            return c;
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        void Move(Command c,Vector3 from,Vector3 target,float now)
        {
            if(FlatDistance(from,previous)>.25f){previous=from;stuckAt=now;}
            bool stuck=now-stuckAt>1.2f;
            if(now>=nextPath||Vector3.Distance(destination,target)>2||stuck)
            {
                destination=target;nextPath=now+1;path.Clear();foreach(var point in CampusNavigation.Route(from,target))path.Enqueue(point);
                if(stuck){c.action="jump";stuckAt=now;}
            }
            while(path.Count>0&&FlatDistance(from,path.Peek())<.55f&&Mathf.Abs(from.y-path.Peek().y)<.75f)path.Dequeue();
            var goal=path.Count>0?path.Peek():target;var direction=goal-from;direction.y=0;
            if(direction.sqrMagnitude>.04f){direction.Normalize();c.x=direction.x;c.z=direction.z;c.yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;}
        }
    }

    // A two-level walk graph derived from the same static solids used by Match.
    public static class CampusNavigation
    {
        static List<Vector3> nodes;
        static List<int>[] edges;
        static readonly Dictionary<Vector2Int,List<int>> cells=new Dictionary<Vector2Int,List<int>>();
        static void Build()
        {
            if(nodes!=null)return;nodes=new List<Vector3>();
            for(int x=-39;x<=39;x++)for(int z=-31;z<=31;z++)
            {
                var heights=CampusGeometry.Solids.Where(s=>x>=s.center.x-s.size.x*.5f&&x<=s.center.x+s.size.x*.5f&&z>=s.center.z-s.size.z*.5f&&z<=s.center.z+s.size.z*.5f)
                    .Select(s=>s.center.y+s.size.y*.5f).Where(h=>h>=-.1f&&h<=4.6f).Distinct();
                foreach(float h in heights)
                {
                    var p=new Vector3(x,h+1,z);if(CampusGeometry.Blocked(p))continue;
                    var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var list)){list=new List<int>();cells[key]=list;}list.Add(nodes.Count);nodes.Add(p);
                }
            }
            edges=new List<int>[nodes.Count];
            for(int i=0;i<nodes.Count;i++)
            {
                edges[i]=new List<int>();var p=nodes[i];
                foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                if(cells.TryGetValue(new Vector2Int((int)p.x+d.x,(int)p.z+d.y),out var next))foreach(int j in next)
                {
                    if(Mathf.Abs(nodes[j].y-p.y)>.75f)continue;
                    var mid=(nodes[j]+p)*.5f;mid.y=Mathf.Max(p.y,nodes[j].y);
                    if(!CampusGeometry.Blocked(mid))edges[i].Add(j);
                }
            }
        }
        public static Vector3[] Route(Vector3 from,Vector3 to)
        {
            Build();if(nodes.Count==0)return Array.Empty<Vector3>();
            int Nearest(Vector3 p){int best=0;float score=float.MaxValue;for(int i=0;i<nodes.Count;i++){var d=nodes[i]-p;float v=d.x*d.x+d.z*d.z+d.y*d.y*16;if(v<score){score=v;best=i;}}return best;}
            int start=Nearest(from),goal=Nearest(to);var queue=new Queue<int>();var previous=new int[nodes.Count];for(int i=0;i<previous.Length;i++)previous[i]=-1;
            previous[start]=start;queue.Enqueue(start);
            while(queue.Count>0&&previous[goal]<0){int n=queue.Dequeue();foreach(int j in edges[n])if(previous[j]<0){previous[j]=n;queue.Enqueue(j);}}
            if(previous[goal]<0)return Array.Empty<Vector3>();
            var route=new List<Vector3>();for(int at=goal;at!=start;at=previous[at])route.Add(nodes[at]);route.Reverse();return route.ToArray();
        }
    }
}
