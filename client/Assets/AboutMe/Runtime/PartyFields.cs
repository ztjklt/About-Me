using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class Match
    {
        static bool SelfField(string id)=>id=="wet-trail"||id=="clean-rain"||id=="flame-fan";
        static bool PartyDevice(EffectState e)=>e.kind=="PartyMechanic"&&(e.mechanicId=="fuse-burst"||e.mechanicId=="relay-burst"||e.mechanicId=="remote-burst"||e.mechanicId=="proximity-burst"||e.mechanicId=="vacuum-burst"||e.mechanicId=="spring-pad"||e.mechanicId=="belt-floor"||e.mechanicId=="paired-teleport"||e.mechanicId=="fan-volume"||e.mechanicId=="tractor-loose"||e.mechanicId=="loose-whirlpool"||e.mechanicId=="sand-sink"||e.mechanicId=="sticky-step"||e.mechanicId=="recover-screen"||e.mechanicId=="calm-dome"||e.mechanicId=="rhythm-gate"||e.mechanicId=="sponge-dry"||e.mechanicId=="carry-cradle"||e.mechanicId=="public-hot-potato"||e.mechanicId=="trip-line");
        bool PartyHasBudget(PartyMechanic d)
        {
            int extra=d.id=="cluster-burst"?3:1;
            if(Effects.Count(e=>e.kind=="PartyMechanic")+extra>24){Say("场上效果较多，请等一个效果结束再使用。");return false;}
            var probe=new EffectState{kind="PartyMechanic",mechanicId=d.id};
            if(PartyDevice(probe)&&Effects.Count(PartyDevice)>=8){Say("场上公共装置已满，先拆除一个或等它结束。");return false;}
            return true;
        }
        void CancelSelfFields(Player p)
        {foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.owner==p.profile.playerId&&SelfField(e.mechanicId)).ToArray())RemovePartyEffect(e);}
        bool FieldCone(EffectState e,Vector3 point)
        {
            var delta=point-e.position;var flat=Flat(delta);return flat.magnitude<=PartyMechanicRegistry.Find(e.mechanicId).radius&&Mathf.Abs(delta.y)<2&&
                (flat.sqrMagnitude<.04f||Vector3.Dot(e.velocity,flat.normalized)>.82f)&&PartyClear(e.position,point);
        }
        bool FieldGround(EffectState e,Vector3 point,float foot)
        {
            float floor=CampusGeometry.Floor(point,foot);return Mathf.Abs(point.y-foot-floor)<.3f&&Mathf.Abs(floor-CampusGeometry.Floor(e.position,.1f))<.3f&&Flat(point-e.position).magnitude<e.radius&&PartyClear(e.position,point);
        }
        bool WetAt(EffectState e,Vector3 point,float foot)
        {
            if(e.path==null)return false;
            foreach(var spot in e.path){float floor=CampusGeometry.Floor(spot,.1f);if(Mathf.Abs(point.y-foot-floor)<.3f&&Flat(point-spot).magnitude<1.2f&&Mathf.Abs(CampusGeometry.Floor(point,foot)-floor)<.3f&&PartyClear(spot,point))return true;}
            return false;
        }
        bool TryFieldMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            var spot=SelfField(d.id)?p.position:GroundParty(p.position+Forward(p)*1.8f);
            if(!SelfField(d.id)&&d.id!="polarity-push"&&d.id!="item-push-wave"&&CampusGeometry.Blocked(spot+Vector3.up*.85f))
            {Say("放置位置被挡住，请换个空地。");p.nextAttack=Time+.35f;return true;}
            PublicProp target=null;
            if(d.id=="polarity-push")
            {
                target=Props.Where(x=>x.holder==""&&(x.visual=="box"||x.visual=="trash")&&Vector3.Distance(x.position,p.position)<d.radius&&Vector3.Dot(Forward(p),Flat(x.position-p.position).normalized)>.82f&&PartyClear(p.position,x.position)).OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();
                if(target==null){Say("前方没有可排斥的公共箱子或垃圾桶。");p.nextAttack=Time+.35f;return true;}
            }
            partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.45f;
            var e=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,sourceInstance=item.id,mechanicId=d.id,visual=item.asset.visualId,color=item.asset.color,
                position=spot,velocity=Forward(p),createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,damage=d.damage,force=d.force,health=2,stage="field"};
            if(d.id=="wet-trail"){e.path=new[]{GroundParty(p.position)};e.nextHit=Time+.3f;}
            if(d.id=="polarity-push"){e.position=target.position;e.targetId=target.id;e.stage="repel_warning";e.triggerAt=Time+.5f;}
            if(d.id=="item-push-wave"){e.position=GroundParty(p.position);e.radius=0;e.stage="loose_wave";}
            Effects.Add(e);return true;
        }
        void FieldMotor(Player p,ref Vector3 desired,float dt)
        {
            p.fieldStatus="";if(BodyLocked(p)||p.eliminated)return;
            if(Effects.Any(e=>e.kind=="PartyMechanic"&&e.owner==p.profile.playerId&&e.mechanicId=="flame-fan"))desired*=.5f;
            var point=p.position;point.y=MotorY(p);bool wet=false;
            foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&PartyMechanicRegistry.Find(e.mechanicId)?.family=="field"))
            {
                bool grounded=OnPartyGround(p);
                if(e.mechanicId=="wet-trail"&&WetAt(e,point,1)&&grounded)wet=true;
                if(e.mechanicId=="fan-volume"&&FieldCone(e,point))desired+=e.velocity*2.2f;
                if(e.mechanicId=="sand-sink")
                {
                    if(grounded&&FieldGround(e,point,1))
                    {if(!e.touchTimes.TryGetValue(p.profile.playerId,out var started)){started=Time;e.touchTimes[p.profile.playerId]=started;}desired*=Mathf.Lerp(1,.35f,Mathf.Clamp01((Time-started)/1.2f));p.fieldStatus="浅沙减速 · 跳离恢复";}
                    else e.touchTimes.Remove(p.profile.playerId);
                }
                if(e.mechanicId=="sticky-step")
                {
                    string id=p.profile.playerId;
                    if(grounded&&FieldGround(e,point,1))
                    {
                        e.touchTimes.TryGetValue(id+"hold",out var until);e.touchTimes.TryGetValue(id+"next",out var next);e.touchTimes.TryGetValue(id+"distance",out var distance);
                        distance+=Mathf.Min(8,Flat(p.velocity).magnitude)*dt;
                        if(distance>=.65f&&Time>=next){e.touchTimes[id+"hold"]=until=Time+.09f;e.touchTimes[id+"next"]=Time+.5f;distance=0;}
                        e.touchTimes[id+"distance"]=distance;
                        if(Time<until){desired=Vector3.zero;if(BodyWorld!=null)BodyWorld.Impact(p,-Flat(p.velocity)*Mathf.Min(.2f,dt*8),p.position,false);}
                        p.fieldStatus="步行触胶 · 跳起 / 喷雨解除";
                    }
                    else {e.touchTimes.Remove(id+"distance");e.touchTimes.Remove(id+"hold");}
                }
            }
            if(wet)
            {
                if(!p.wasWet)p.wetMomentum=Flat(p.velocity);
                p.wetMomentum=Vector3.MoveTowards(p.wetMomentum,desired,(desired.sqrMagnitude<.01f?1.8f:6)*dt);desired=Vector3.ClampMagnitude(p.wetMomentum,Rules.runSpeed+2.2f);p.fieldStatus="湿滑地面 · 刹车变慢，跳起脱离";
            }
            else p.wetMomentum=Vector3.zero;p.wasWet=wet;
        }
        bool FieldLoose(ref Vector3 velocity,Vector3 point,float foot,float dt)
        {
            bool driven=false;
            foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.stage=="field"))
            {
                if(e.mechanicId=="fan-volume"&&FieldCone(e,point)){velocity+=e.velocity*(9*dt);driven=true;}
                if((e.mechanicId=="loose-whirlpool"||e.mechanicId=="tractor-loose")&&FieldGround(e,point,foot))
                {
                    var toward=Flat(e.position-point);Vector3 want;
                    if(e.mechanicId=="tractor-loose")want=toward.magnitude>.8f?toward.normalized*3:Vector3.zero;
                    else want=Vector3.Cross(Vector3.up,toward.normalized)*3+toward.normalized*Mathf.Clamp(toward.magnitude-1,-1,1);
                    var flat=Vector3.MoveTowards(Flat(velocity),want,12*dt);velocity=new Vector3(flat.x,velocity.y,flat.z);driven|=want.sqrMagnitude>.01f;
                }
            }
            return driven;
        }
        void TickField(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            e.remaining=Mathf.Max(0,e.deadline-Time);
            if(SelfField(d.id))
            {
                if(!MovementOwnerValid(e,owner)||owner.grabbing!=""||owner.publicProp!=""){RemovePartyEffect(e);return;}
                e.position=owner.position;e.velocity=Forward(owner);
            }
            if(d.id=="wet-trail"&&Time>=e.nextHit&&OnPartyGround(owner))
            {
                var point=GroundParty(owner.position);
                if(e.path==null||Flat(point-e.path.Last()).magnitude>.5f){var points=(e.path??Array.Empty<Vector3>()).ToList();points.Add(point);while(points.Count>6)points.RemoveAt(0);e.path=points.ToArray();}e.nextHit=Time+.3f;
            }
            if(d.id=="fan-volume"&&BodyWorld!=null)
                foreach(var player in Players.Values.Where(x=>!x.eliminated&&x.bodyMode!=BodyMode.GettingUp&&Time>=x.bodyProtectedUntil&&FieldCone(e,x.position)))BodyWorld.Impact(player,e.velocity*(5*dt),player.position,false);
            if(d.id=="polarity-push"&&e.stage=="repel_warning")
            {
                var target=Props.Find(x=>x.id==e.targetId&&x.holder=="");
                if(target==null||!PartyClear(owner.position,target.position)||Vector3.Distance(owner.position,target.position)>d.radius){RemovePartyEffect(e);Say("排斥目标已被拿走或挡住。");return;}
                e.position=target.position;
                if(Time>=e.triggerAt){var direction=Flat(target.position-owner.position).normalized;target.velocity=Vector3.ClampMagnitude(target.velocity+direction*7,8);target.thrower=owner.profile.playerId;target.hitAfter=Time+.15f;e.stage="repelled";CombatEvent("party_repel",owner,reason:target.id);}
            }
            if(d.id=="item-push-wave")
            {
                float before=e.radius;e.radius=d.radius*Mathf.Clamp01((Time-e.createdAt)/d.lifetime);
                foreach(var item in Items.Where(x=>x.state==ItemState.Ground&&!e.hit.Contains(x.id)).ToArray())
                {var delta=Flat(item.position-e.position);float floor=CampusGeometry.Floor(e.position,.1f);if(delta.magnitude>e.radius+.25f||delta.magnitude<before-.25f||Mathf.Abs(item.position.y-.35f-floor)>.3f||!PartyClear(e.position,item.position))continue;item.velocity=Vector3.ClampMagnitude(item.velocity+delta.normalized*5,8);e.hit.Add(item.id);}
                foreach(var prop in Props.Where(x=>x.holder==""&&!e.hit.Contains(x.id)).ToArray())
                {var delta=Flat(prop.position-e.position);float floor=CampusGeometry.Floor(e.position,.1f);if(delta.magnitude>e.radius+.4f||delta.magnitude<before-.4f||Mathf.Abs(prop.position.y-.6f-floor)>.3f||!PartyClear(e.position,prop.position))continue;prop.velocity=Vector3.ClampMagnitude(prop.velocity+delta.normalized*5,8);prop.thrower=owner.profile.playerId;prop.hitAfter=Time+.15f;e.hit.Add(prop.id);}
            }
            if(d.id=="clean-rain")
                foreach(var other in Effects.Where(x=>x!=e&&((x.mechanicId=="sticky-step"&&FieldCone(e,x.position))||(x.mechanicId=="sticky-burst"&&x.stage=="attached"&&FieldCone(e,x.position)))).ToArray())
                {if(other.stage=="attached"&&Players.TryGetValue(other.targetId,out var victim))ResetEscape(victim);RemovePartyEffect(other);CombatEvent("party_clean",owner,reason:other.mechanicId);}
            if(d.id=="flame-fan")
            {
                int beat=(Time-e.createdAt>=.6f?2:Time-e.createdAt>=.2f?1:0);
                if(beat>e.chainIndex)
                {
                    e.chainIndex=beat;
                    foreach(var target in Players.Values.Where(x=>x!=owner&&!x.eliminated&&!BodyLocked(x)&&FieldCone(e,x.position)).ToArray())
                    {bool push=!e.hit.Contains(target.profile.playerId);Hit(target,owner,d.damage,0,false,false,false,owner.position,push?e.velocity*d.force:Vector3.zero);if(push)e.hit.Add(target.profile.playerId);}
                    CombatEvent("party_flame",owner,reason:beat.ToString());
                }
            }
            if(Time>=e.deadline)RemovePartyEffect(e);
        }
    }
}
