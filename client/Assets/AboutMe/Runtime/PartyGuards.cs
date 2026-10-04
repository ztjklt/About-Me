using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class Match
    {
        static bool SelfGuard(string id)=>id=="front-shield"||id=="timed-reflect"||id=="ground-anchor"||id=="impact-bank"||id=="ribbon-cover"||id=="wind-deflector";
        bool GuardAlive(EffectState e)
        {
            var source=Item(e.sourceInstance);
            if(!Effects.Contains(e)||e.health<=0||Time>=e.deadline||source==null||source.state==ItemState.Consumed||!Players.TryGetValue(e.owner,out var p)||p.eliminated)return false;
            if(!SelfGuard(e.mechanicId))return true;
            return source.state==ItemState.Equipped&&p.equipped==source.id&&p.connected&&!BodyLocked(p)&&Time-p.lastInput<=.35f&&(e.mechanicId=="ground-anchor"||!IsGrabbed(p));
        }
        EffectState GuardFor(Player p,string id=null)=>Effects.FirstOrDefault(e=>e.kind=="PartyMechanic"&&SelfGuard(e.mechanicId)&&e.owner==p.profile.playerId&&(id==null||e.mechanicId==id)&&GuardAlive(e));
        public bool GuardAnchored(Player p)=>GuardFor(p,"ground-anchor")!=null;
        void CancelSelfGuards(Player p)
        {foreach(var e in Effects.Where(x=>x.kind=="PartyMechanic"&&SelfGuard(x.mechanicId)&&x.owner==p.profile.playerId).ToArray())RemovePartyEffect(e);}
        bool GuardBlocks(Player p,string action)=>GuardFor(p)!=null&&(action=="interact"||(GuardAnchored(p)&&action=="jump"));
        void GuardYaw(Player p,float dt){p.yaw=GuardFor(p,"front-shield")!=null?Mathf.MoveTowardsAngle(p.yaw,p.input.yaw,100*dt):p.input.yaw;}
        void GuardMotor(Player p,ref Vector3 desired,float dt)
        {
            var e=GuardFor(p);if(e==null)return;
            if(e.mechanicId=="ground-anchor"){desired=Vector3.zero;p.impulse=new Vector3(0,p.impulse.y,0);}
            else desired*=e.mechanicId=="front-shield"?.5f:.7f;

            p.fieldStatus=e.mechanicId=="impact-bank"?(e.stage=="banked"?"已蓄冲 · F 返还 / Q 放弃":"蓄冲等待 · 削韧不免除"):PartyMechanicRegistry.Find(e.mechanicId).label+" · Q 结束";
        }
        void GuardIncoming(Player p,ref Vector3 kick,bool falls)
        {
            if(GuardAnchored(p)){kick.x=kick.z=0;return;}
            var bank=GuardFor(p,"impact-bank");
            if(bank!=null&&bank.stage=="guard"&&!falls&&Flat(kick).magnitude>.1f)
            {bank.force=Mathf.Min(6,Flat(kick).magnitude);bank.stage="banked";kick.x=kick.z=0;CombatEvent("party_bank",p,reason:bank.force.ToString("F1"));}
        }
        bool TryGuardMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            var bank=GuardFor(p,"impact-bank");
            if(d.id=="impact-bank"&&bank!=null&&bank.stage=="banked")
            {
                var force=bank.force;RemovePartyEffect(bank);p.nextAttack=Time+.45f;
                foreach(var target in Players.Values.Where(x=>x!=p&&!x.eliminated&&!BodyLocked(x)&&Time>=x.bodyProtectedUntil))
                {var delta=Flat(target.position-p.position);if(delta.magnitude>d.radius||Vector3.Dot(Forward(p),delta.normalized)<.82f||!PartyClear(p.position,target.position))continue;var kick=delta.normalized*force;if(BodyWorld!=null)BodyWorld.Impact(target,kick,target.position,false);else target.impulse+=kick;}
                Effects.Add(new EffectState{id=Guid.NewGuid().ToString("N"),kind="Pulse",owner=p.profile.playerId,color=item.asset.color,position=p.position+Forward(p),remaining=.25f,damage=d.radius});CombatEvent("party_bank_return",p,reason:force.ToString("F1"));return true;
            }
            if(d.id=="ground-anchor"&&!OnPartyGround(p)){Say("落地后才能蹲锚，请先站稳。");p.nextAttack=Time+.25f;return true;}
            var spot=SelfGuard(d.id)?p.position:GroundParty(p.position+Forward(p)*1.8f);
            if(!SelfGuard(d.id)&&CampusGeometry.Blocked(spot+Vector3.up*.85f)){Say("屏障位置被挡住，请换个空地。");p.nextAttack=Time+.25f;return true;}
            CancelSelfGuards(p);partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.4f;
            Effects.Add(new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",mechanicId=d.id,sourceInstance=item.id,owner=p.profile.playerId,visual=item.asset.visualId,color=item.asset.color,wordCategory=item.asset.wordCategory,wordText=item.asset.wordText,abilityId=item.asset.abilityId,position=spot,velocity=Forward(p),createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,health=d.id=="front-shield"||d.id=="rhythm-gate"||d.id=="sponge-dry"?2:1,stage="guard"});return true;
        }
        void TickGuard(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            e.remaining=Mathf.Max(0,e.deadline-Time);
            if(e.stage=="parried"){if(Time>=e.deadline)RemovePartyEffect(e);return;}
            if(!GuardAlive(e))
            {if(d.id=="timed-reflect"&&Time>=e.deadline&&e.health>0){owner.nextAttack=Mathf.Max(owner.nextAttack,Time+.4f);Say(owner.profile.nickname+" 的反射窗口结束了。");}RemovePartyEffect(e);return;}
            if(SelfGuard(d.id)){e.position=owner.position;e.velocity=Forward(owner);}
            if(d.id=="ground-anchor"&&!OnPartyGround(owner)){RemovePartyEffect(e);return;}
            if(d.id=="sponge-dry"&&e.stage!="saturated")
            {
                DryFrozenPoints(e,owner);
                foreach(var wet in Effects.Where(x=>x.kind=="PartyMechanic"&&x.mechanicId=="wet-trail"&&x.path!=null).ToArray())
                {
                    var points=wet.path.ToList();
                    foreach(var point in wet.path)
                    {if(e.chainIndex>=2)break;if(Flat(point-e.position).magnitude>e.radius||Mathf.Abs(point.y-e.position.y)>.3f||!PartyClear(e.position,point))continue;points.Remove(point);e.chainIndex++;}
                    wet.path=points.ToArray();if(e.chainIndex>=2){e.stage="saturated";e.deadline=Time+.35f;CombatEvent("party_dry",owner,reason:"2");break;}
                }
            }
        }
        static bool OrdinaryShot(EffectState e)=>e.kind=="Projectile"||e.kind=="Disc"||e.kind=="ThrownDisc"||e.kind=="Ricochet";
        static bool GuardPlane(Vector3 center,Vector3 normal,float width,Vector3 from,Vector3 to,bool front,out Vector3 point)
        {
            point=from;float a=Vector3.Dot(from-center,normal),b=Vector3.Dot(to-center,normal);
            if(a*b>0||Mathf.Abs(a-b)<.0001f||(front&&(a<-.03f||Vector3.Dot(to-from,normal)>=0)))return false;
            float t=a/(a-b);if(t<0||t>1)return false;point=Vector3.Lerp(from,to,t);var side=Vector3.Cross(Vector3.up,normal);
            return Mathf.Abs(Vector3.Dot(point-center,side))<=width+.15f&&Mathf.Abs(point.y-center.y)<=1.05f;
        }
        static bool GuardSphere(Vector3 center,float radius,Vector3 from,Vector3 to)
        {
            var direction=to-from;var offset=from-center;float a=direction.sqrMagnitude;if(a<.000001f)return false;
            float b=2*Vector3.Dot(offset,direction),c=offset.sqrMagnitude-radius*radius,disc=b*b-4*a*c;if(disc<0)return false;
            float root=Mathf.Sqrt(disc),first=(-b-root)/(2*a),second=(-b+root)/(2*a);return(first>=0&&first<=1)||(second>=0&&second<=1);
        }
        // Return 1: safely stop and land the same asset. Return 2: redirect and advance next tick.
        // Real Persona ownerId never changes; reflected thrownBy records only the new impulse source.
        int GuardShot(string id,ref Vector3 position,ref Vector3 velocity,ref string thrower,ref float pushScale,HashSet<string> touched,Vector3 next)
        {
            var start=position;
            foreach(var e in Effects.Where(x=>x.kind=="PartyMechanic"&&PartyMechanicRegistry.Find(x.mechanicId)?.family=="guard"&&GuardAlive(x)).OrderBy(x=>Vector3.Distance(x.position,start)).ToArray())
            {
                if(touched.Contains(e.id)||touched.Count>=24)continue;
                var d=PartyMechanicRegistry.Find(e.mechanicId);var owner=Players[e.owner];var normal=SelfGuard(d.id)?Forward(owner):e.velocity;
                var center=SelfGuard(d.id)?owner.position:e.position+Vector3.up*.85f;Vector3 point;
                if(d.id=="calm-dome")
                {if(GuardSphere(center,d.radius,position,next)){pushScale=Mathf.Min(pushScale,.35f);touched.Add(e.id);CombatEvent("party_soften",owner,reason:id);}continue;}
                if(d.id=="wind-deflector")
                {
                    var delta=Flat(next-owner.position);if(delta.magnitude>d.radius||Mathf.Abs(next.y-owner.position.y)>1.5f||Vector3.Dot(normal,delta.normalized)<.55f||!PartyClear(owner.position,next))continue;
                    touched.Add(e.id);velocity=Quaternion.Euler(0,60,0)*velocity;CombatEvent("party_deflect",owner,reason:id);return 2;
                }
                bool self=d.id=="front-shield"||d.id=="timed-reflect"||d.id=="ribbon-cover";
                if(!self&&d.id!="recover-screen"&&d.id!="rhythm-gate")continue;
                if(self&&e.owner==thrower)continue;
                if(d.id=="ribbon-cover")normal=-normal;
                if(self)center+=normal*1.6f;
                if(d.id=="rhythm-gate"&&Mathf.Repeat(Time-e.createdAt,1)<.35f)continue;
                if(!GuardPlane(center,normal,d.radius,position,next,self,out point)||!CampusGeometry.ClearSegment(position,point))continue;
                touched.Add(e.id);position=point;
                if(d.id=="timed-reflect")
                {velocity=-velocity;thrower=e.owner;position+=normal*.07f;e.stage="parried";e.health=0;e.deadline=Time+.2f;CombatEvent("party_reflect",owner,reason:id);return 2;}
                CombatEvent("party_block",owner,reason:id);if(--e.health<=0)RemovePartyEffect(e);return 1;
            }
            return 0;
        }
    }
}
