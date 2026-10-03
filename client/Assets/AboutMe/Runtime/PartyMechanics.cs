using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    [Serializable] public class PartyMechanic
    {
        public string id,visualId,label,hint,family;
        public float cooldown,windup,lifetime,radius,damage,force;
    }
    [Serializable] public class PartyMechanics {public int version;public PartyMechanic[] items;}
    public static class PartyMechanicRegistry
    {
        static PartyMechanic[] all;
        public static PartyMechanic[] All
        {
            get {if(all==null){var text=Resources.Load<TextAsset>("persona-mechanics");all=text?JsonUtility.FromJson<PartyMechanics>(text.text).items:Array.Empty<PartyMechanic>();}return all;}
        }
        public static PartyMechanic ForVisual(string visual)=>All.FirstOrDefault(x=>x.visualId==visual);
        public static PartyMechanic Find(string id)=>All.FirstOrDefault(x=>x.id==id);
    }
    public partial class Match
    {
        readonly Dictionary<string,float> partyCooldowns=new Dictionary<string,float>();
        bool HasStickyCharge(Player p)=>Effects.Any(e=>e.kind=="PartyMechanic"&&e.mechanicId=="sticky-burst"&&e.stage=="attached"&&e.targetId==p.profile.playerId);
        void RemoveStickyCharges(Player p)
        {
            foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.mechanicId=="sticky-burst"&&e.stage=="attached"&&e.targetId==p.profile.playerId).ToArray())RemovePartyEffect(e);
            ResetEscape(p);Say(p.profile.nickname+" 撕掉了黏附爆风！");
        }
        bool PartyCanUse(Player p,PersonaInstance item)
        {
            var definition=PartyMechanicRegistry.ForVisual(item?.asset?.visualId);if(definition==null)return true;
            var active=Effects.Where(e=>e.kind=="PartyMechanic"&&e.sourceInstance==item.id&&e.stage!="burst").ToArray();
            if(definition.id=="impact-bank"&&active.Any(e=>e.stage=="banked"&&GuardAlive(e)))return true;
            if(definition.id=="paired-teleport"&&active.Length==1&&active[0].stage=="portal_wait")return true;
            if(definition.id=="remote-burst"&&active.Length>0)return true;
            if(definition.id=="relay-burst"&&active.Length>0&&active.Length<3&&active.All(e=>e.stage=="waiting"))return true;
            return active.Length==0&&(!partyCooldowns.TryGetValue(item.id,out var until)||Time>=until);
        }
        float PartyCooldown(Player p)
        {
            var item=Item(p.equipped);if(PartyMechanicRegistry.ForVisual(item?.asset?.visualId)==null)return 0;
            if(PartyCanUse(p,item))return Mathf.Max(0,p.nextAttack-Time);
            return Mathf.Max(0,(partyCooldowns.TryGetValue(item.id,out var until)?Mathf.Max(until,p.nextAttack):p.nextAttack)-Time);
        }
        bool TryPartyMechanic(Player p,PersonaInstance item)
        {
            var d=PartyMechanicRegistry.ForVisual(item?.asset?.visualId);if(d==null)return false;
            if(!PartyCanUse(p,item)){p.nextAttack=Time+.2f;return true;}
            if(!(d.id=="impact-bank"&&GuardFor(p,"impact-bank")?.stage=="banked")&&!(d.id=="remote-burst"&&Effects.Any(effect=>effect.kind=="PartyMechanic"&&effect.sourceInstance==item.id&&effect.stage!="burst"))&&!PartyHasBudget(d)){p.nextAttack=Time+.25f;return true;}
            if(d.family=="guard")return TryGuardMechanic(p,item,d);
            if(d.family=="field")return TryFieldMechanic(p,item,d);
            if(d.family=="movement")return TryMovementMechanic(p,item,d);
            var previous=Effects.Where(e=>e.kind=="PartyMechanic"&&e.sourceInstance==item.id&&e.stage!="burst").ToArray();
            if(d.id=="remote-burst"&&previous.Length>0)
            {BurstParty(previous[0],p,d);p.nextAttack=Time+.45f;partyCooldowns[item.id]=Time+d.cooldown;return true;}
            partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+Rules.heavyInterval;
            var forward=Forward(p);
            EffectState New(Vector3 position,string stage)=>new EffectState {
                id=Guid.NewGuid().ToString("N"),owner=p.profile.playerId,kind="PartyMechanic",mechanicId=d.id,sourceInstance=item.id,
                visual=item.asset.visualId,color=item.asset.color,position=position,velocity=forward*9+Vector3.up*3,
                stage=stage,createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,damage=d.damage,force=d.force,health=2,heavy=true};
            if(d.id=="cone-burst")
            {
                var e=New(p.position,"burst");e.velocity=forward;e.deadline=Time+.5f;e.remaining=.5f;Effects.Add(e);
                foreach(var target in Players.Values.Where(x=>x!=p&&!x.eliminated).ToArray())
                {
                    var delta=target.position-p.position;delta.y=0;
                    if(delta.magnitude<=d.radius&&Vector3.Dot(forward,delta.normalized)>=Mathf.Cos(30*Mathf.Deg2Rad)&&PartyClear(p.position,target.position))
                        Hit(target,p,d.damage,d.force,true,false,false,p.position,delta.normalized*d.force+Vector3.up*2);
                }
                var recoil=-forward*2.5f;if(BodyWorld!=null)BodyWorld.Impact(p,recoil,p.position,false);else p.impulse+=recoil;
                return true;
            }
            if(d.id=="ring-burst")
            {var e=New(GroundParty(p.position),"ring");e.velocity=Vector3.zero;e.radius=0;Effects.Add(e);return true;}
            if(d.id=="cluster-burst")
            {
                for(int i=0;i<3;i++){var side=Quaternion.Euler(0,90,0)*forward;var e=New(GroundParty(p.position+forward*(2.4f+i*.8f)+side*(i-1)*1.8f),"armed");e.velocity=Vector3.zero;e.deadline=Time+1.1f+i*.35f;Effects.Add(e);}
                return true;
            }
            if(d.id=="fuse-burst"||d.id=="relay-burst"||d.id=="remote-burst")
            {
                // Public devices have no extra Persona life. They can be picked up and thrown by either player.
                var e=New(p.position+forward*1.2f+Vector3.up*.1f,d.id=="relay-burst"?"waiting":"armed");e.targetId="device-"+e.id;e.velocity=Vector3.zero;
                Props.Add(new PublicProp{id=e.targetId,visual=e.visual,position=e.position,velocity=d.id=="fuse-burst"?forward*6+Vector3.up*2:Vector3.zero,hitAfter=float.MaxValue});
                Effects.Add(e);
                if(d.id=="remote-burst")p.nextAttack=Time+.45f;
                if(d.id=="relay-burst")
                {
                    foreach(var node in previous)node.deadline=Time+3;
                    e.chainIndex=previous.Length+1;
                    if(previous.Length<2)p.nextAttack=Time+.45f;
                    else {var first=previous.OrderBy(x=>x.chainIndex).First();first.stage="armed";first.deadline=Time+.6f;}
                }
                return true;
            }
            if(d.id=="proximity-burst")
            {var e=New(GroundParty(p.position+forward*1.8f),"watching");e.velocity=Vector3.zero;Effects.Add(e);return true;}
            Effects.Add(New(p.position+forward*.8f+Vector3.up*.15f,"flying"));return true;
        }
        static Vector3 GroundParty(Vector3 position)
        {position=CampusGeometry.SafeDrop(position);position.y=CampusGeometry.Floor(position,.1f)+.15f;return position;}
        static bool PartyClear(Vector3 from,Vector3 to)=>CampusGeometry.ClearSegment(from+Vector3.up*.35f,to+Vector3.up*.1f);
        void RemovePartyEffect(EffectState e)
        {
            if(e.targetId.StartsWith("device-",StringComparison.Ordinal))
            {
                var prop=Props.Find(x=>x.id==e.targetId);
                if(prop!=null&&Players.TryGetValue(prop.holder,out var holder)&&holder.publicProp==prop.id){holder.publicProp="";SetAction(holder,"release");}
                Props.RemoveAll(x=>x.id==e.targetId);
            }
            foreach(var p in Players.Values)if(p.portalEntry==e.id)p.portalEntry="";
            if(e.mechanicId=="paired-teleport")
                foreach(var other in Effects.Where(x=>x!=e&&x.sourceInstance==e.sourceInstance&&x.mechanicId==e.mechanicId).ToArray())
                {Effects.Remove(other);foreach(var p in Players.Values)if(p.portalEntry==other.id)p.portalEntry="";}
            Effects.Remove(e);
        }
        void BurstParty(EffectState e,Player owner,PartyMechanic d)
        {
            if(e.stage=="burst")return;
            if(d.id=="relay-burst")
            {
                // Only the next untriggered device in this owner's chain, within four metres. No recursive explosion loop.
                var next=Effects.Where(x=>x.kind=="PartyMechanic"&&x.mechanicId==d.id&&x.sourceInstance==e.sourceInstance&&x.stage=="waiting"&&x.chainIndex>e.chainIndex)
                    .OrderBy(x=>x.chainIndex).FirstOrDefault();
                if(next!=null){var prop=Props.Find(x=>x.id==next.targetId);if(prop!=null)next.position=prop.position;
                    if(Vector3.Distance(e.position,next.position)<=4&&PartyClear(e.position,next.position)){next.stage="armed";next.deadline=Time+.35f;}}
            }
            var id=e.targetId;
            if(id.StartsWith("device-",StringComparison.Ordinal))
            {
                var prop=Props.Find(x=>x.id==id);if(prop!=null&&Players.TryGetValue(prop.holder,out var holder)&&holder.publicProp==id){holder.publicProp="";SetAction(holder,"release");}
                Props.RemoveAll(x=>x.id==id);
            }
            e.targetId="";e.stage="burst";e.deadline=Time+.5f;e.remaining=.5f;e.velocity=Vector3.zero;
            foreach(var target in Players.Values.Where(x=>!x.eliminated).ToArray())
            {
                var delta=target.position-e.position;
                if(delta.magnitude>d.radius||!PartyClear(e.position,target.position))continue;
                delta.y=0;if(delta.sqrMagnitude<.001f)delta=Forward(owner);delta.Normalize();
                Hit(target,owner,d.damage,d.force,true,false,false,e.position,delta*d.force+Vector3.up*2.5f);
                e.hit.Add(target.profile.playerId);
            }
            CombatEvent("party_burst",owner,reason:d.id);
        }
        void TickPartyEffect(EffectState e,float dt)
        {
            var d=PartyMechanicRegistry.Find(e.mechanicId);
            if(d==null||!Players.TryGetValue(e.owner,out var owner)){RemovePartyEffect(e);return;}
            var source=Item(e.sourceInstance);
            if(source==null||source.state==ItemState.Consumed||owner.eliminated){RemovePartyEffect(e);return;}
            if(d.family=="guard"){TickGuard(e,owner,d,dt);return;}
            if(d.family=="field"){TickField(e,owner,d,dt);return;}
            if(d.family=="movement"){TickMovement(e,owner,d,dt);return;}
            e.remaining=Mathf.Max(0,e.deadline-Time);
            if(e.stage=="burst"){if(Time>=e.deadline)RemovePartyEffect(e);return;}
            var prop=Props.Find(x=>x.id==e.targetId);
            if(e.targetId.StartsWith("device-",StringComparison.Ordinal)&&prop==null){RemovePartyEffect(e);return;}
            if(prop!=null)e.position=prop.holder!=""&&Players.TryGetValue(prop.holder,out var holder)?holder.position+Forward(holder):prop.position;
            if(e.stage=="flying")
            {
                var from=e.position;var wanted=from+e.velocity*dt;e.velocity+=Vector3.down*Rules.gravity*dt;
                var next=CampusGeometry.Move(from,wanted,.15f,.2f);
                var target=Players.Values.Where(x=>x!=owner&&!x.eliminated&&x.bodyMode!=BodyMode.GettingUp&&Time>=x.bodyProtectedUntil)
                    .Where(x=>DistanceToSegment(x.position,from,next)<.8f&&PartyClear(from,x.position)).OrderBy(x=>DistanceToSegment(x.position,from,next)).FirstOrDefault();
                bool landed=(next-wanted).sqrMagnitude>.0001f||next.y<=CampusGeometry.Floor(next,.15f)+.2f||CampusGeometry.Outside(next);
                e.position=next;
                if(d.id=="sticky-burst"&&target!=null)
                {e.targetId=target.profile.playerId;e.stage="attached";e.deadline=Time+1.6f;e.velocity=Vector3.zero;ResetEscape(target);return;}
                if(d.id=="impact-burst"&&(target!=null||landed)){BurstParty(e,owner,d);return;}
                if(landed){e.position=GroundParty(next);e.velocity=Vector3.zero;e.stage="armed";if(d.id=="vacuum-burst")e.deadline=Time+.5f;}
            }
            if(e.stage=="attached")
            {if(!Players.TryGetValue(e.targetId,out var victim)||victim.eliminated){RemovePartyEffect(e);return;}e.position=victim.position+Vector3.up*.1f;}
            if(e.stage=="watching"||e.stage=="sensing")
            {
                bool inside=Players.Values.Any(p=>p!=owner&&!p.eliminated&&!p.airborne&&Mathf.Abs(CampusGeometry.Floor(p.position)-CampusGeometry.Floor(e.position,.1f))<.3f&&
                    Vector3.Distance(p.position,e.position)<d.radius&&PartyClear(e.position,p.position));
                if(inside&&e.stage=="watching"){e.stage="sensing";e.triggerAt=Time+.6f;}
                else if(!inside){e.stage="watching";e.triggerAt=0;}
                if(e.stage=="sensing"&&Time>=e.triggerAt){BurstParty(e,owner,d);return;}
                if(Time>=e.deadline)RemovePartyEffect(e);return;
            }
            if(e.stage=="ring")
            {
                float before=e.radius;e.radius=d.radius*Mathf.Clamp01((Time-e.createdAt)/d.lifetime);
                foreach(var p in Players.Values.Where(p=>p!=owner&&!p.eliminated&&!e.hit.Contains(p.profile.playerId)).ToArray())
                {
                    float r=new Vector2(p.position.x-e.position.x,p.position.z-e.position.z).magnitude;
                    float foot=(BodyWorld==null?p.position.y:p.motorHeight)-1;
                    if(r>e.radius+.3f||r<before-.3f||foot>CampusGeometry.Floor(e.position,.1f)+.45f||Mathf.Abs(CampusGeometry.Floor(p.position)-CampusGeometry.Floor(e.position,.1f))>.3f||!PartyClear(e.position,p.position))continue;
                    var direction=p.position-e.position;direction.y=0;direction.Normalize();
                    Hit(p,owner,d.damage,d.force,true,false,false,e.position,direction*d.force+Vector3.up*1.2f);e.hit.Add(p.profile.playerId);
                }
                if(Time>=e.deadline)RemovePartyEffect(e);return;
            }
            if(d.id=="vacuum-burst"&&e.stage=="pulling")
            {
                foreach(var p in Players.Values.Where(x=>!x.eliminated&&x.bodyMode!=BodyMode.GettingUp&&Time>=x.bodyProtectedUntil))
                {
                    var delta=e.position-p.position;delta.y=0;
                    if(delta.magnitude>d.radius||!PartyClear(e.position,p.position))continue;
                    var pull=delta.normalized*(10*dt);if(BodyWorld!=null)BodyWorld.Impact(p,pull,p.position,false);else p.impulse+=pull;
                }
            }
            if(Time+.0001f>=e.deadline)
            {
                if(d.id=="remote-burst"){RemovePartyEffect(e);return;}
                if(d.id=="vacuum-burst"&&e.stage=="armed"){e.stage="pulling";e.deadline=Time+.5f;return;}
                if(d.id=="relay-burst"&&e.stage=="waiting"&&e.chainIndex>1){RemovePartyEffect(e);return;}
                BurstParty(e,owner,d);
            }
        }
        void DamagePartyDevices(Player attacker,Vector3 point,float reach)
        {
            foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.stage!="burst"&&PartyDevice(e)&&Vector3.Distance(e.position,point)<=reach&&PartyClear(attacker.position,e.position)).ToArray())
            {if(--e.health<=0){RemovePartyEffect(e);Say(attacker.profile.nickname+" 拆掉了公共装置！");}}
        }
    }
}
