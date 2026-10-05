using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class Match
    {
        static bool SelfControl(string id)=>id=="lasso-loop"||id=="water-jet"||id=="skate-arc"||id=="body-bank"||id=="anchor-swing";
        bool HasControlRestraint(Player p)=>Effects.Any(e=>e.kind=="PartyMechanic"&&e.targetId==p.profile.playerId&&e.stage=="attached"&&(e.mechanicId=="lasso-loop"||e.mechanicId=="break-net"||e.mechanicId=="bubble-lift"));
        void RemoveControlRestraints(Player p)
        {foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.targetId==p.profile.playerId&&e.stage=="attached"&&(e.mechanicId=="lasso-loop"||e.mechanicId=="break-net"||e.mechanicId=="bubble-lift")).ToArray()){RemovePartyEffect(e);CombatEvent("party_control_escape",p,reason:e.mechanicId);}ResetEscape(p);}
        void ReleaseSwingMomentum(Player p)
        {var e=SelfControlFor(p);if(e==null||e.mechanicId!="anchor-swing")return;var velocity=Vector3.ClampMagnitude(e.velocity,7);if(BodyWorld!=null)BodyWorld.Impact(p,Vector3.ClampMagnitude(velocity-Flat(p.velocity),3),p.position,false);else p.impulse=velocity;}
        void CancelControlConsent(Player p)
        {foreach(var e in Effects.Where(e=>e.mechanicId=="team-sling"&&e.stage=="launch_warning"&&(e.owner==p.profile.playerId||e.targetId==p.profile.playerId))){e.stage="ready";e.targetId="";Say("抛举同意已取消。");}}
        void BreakBubbleOnAttack(Player p)
        {foreach(var e in Effects.Where(e=>e.mechanicId=="bubble-lift"&&e.stage=="attached"&&e.targetId==p.profile.playerId).ToArray())RemovePartyEffect(e);}
        void CancelSelfControl(Player p)
        {foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.owner==p.profile.playerId&&SelfControl(e.mechanicId)).ToArray())RemovePartyEffect(e);}
        EffectState SelfControlFor(Player p)=>Effects.FirstOrDefault(e=>e.kind=="PartyMechanic"&&e.owner==p.profile.playerId&&SelfControl(e.mechanicId)&&e.stage!="ended");
        bool ControlBlocks(Player p,string action)=>(SelfControlFor(p)!=null&&(action=="use_item"||action=="interact"))||Effects.Any(e=>e.mechanicId=="team-sling"&&e.stage=="launch_warning"&&e.owner==p.profile.playerId&&action!="");
        bool ControlTarget(Player p)=>p.connected&&!p.eliminated&&!BodyLocked(p)&&Time>=p.staggerUntil&&Time>=p.bodyProtectedUntil;
        bool ControlOwner(EffectState e,Player p)=>MovementOwnerValid(e,p)&&p.grabbing==""&&p.publicProp==""&&p.carrying=="";
        void ControlForce(Player p,Vector3 delta)
        {if(BodyWorld!=null)BodyWorld.Impact(p,delta,p.position,false);else p.impulse+=delta;}
        bool TryControlMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            Player target=null;PublicProp anchor=null;var direction=Forward(p);var spot=GroundParty(p.position+direction*3.7f);
            if(d.id=="lasso-loop")
            {target=Players.Values.Where(x=>x!=p&&ControlTarget(x)&&!HasControlRestraint(x)&&Vector3.Distance(p.position,x.position)<d.radius&&Vector3.Dot(direction,Flat(x.position-p.position).normalized)>.7f&&LootLineClear(p.position,x.position)).OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();if(target==null){Say("套索需前方近处、无遮挡的角色；不能重复套住。");p.nextAttack=Time+.3f;return true;}}
            if(d.id=="anchor-swing")
            {anchor=Props.Where(x=>OrdinaryPublic(x)&&Flat(x.velocity).magnitude<.2f&&Vector3.Distance(x.position,p.position)<d.radius&&Vector3.Distance(x.position,p.position)>1.5f&&LootLineClear(p.position,x.position)).OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();if(anchor==null){Say("附近需要一个静止、可见且未被拿起的公共箱桶作摆荡锚点。");p.nextAttack=Time+.3f;return true;}}
            if(d.id=="body-bank"&&Flat(p.velocity).magnitude<2){Say("先移动产生速度，再启用一次反弹。");p.nextAttack=Time+.3f;return true;}
            if(d.id=="skate-arc"&&!OnPartyGround(p)){Say("惯性滑行需要先落地。");p.nextAttack=Time+.3f;return true;}
            if(d.id=="gapped-cage"||d.id=="team-sling")
            {
                for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)
                {var sample=spot+Quaternion.LookRotation(direction)*new Vector3(x*.8f,0,z*.8f);if(CampusGeometry.Outside(sample)||Mathf.Abs(CampusGeometry.Floor(sample,.1f)-CampusGeometry.Floor(spot,.1f))>.12f||CampusGeometry.Blocked(sample+Vector3.up,.6f)||CampusGeometry.Blocked(sample+Vector3.up*2,.6f)){Say("支架或软笼需要平整、无遮挡的空地。");p.nextAttack=Time+.3f;return true;}}
                if(Players.Values.Any(x=>!x.eliminated&&Flat(x.position-spot).magnitude<2.4f)||Effects.Where(MachineEffect).Any(x=>Flat(x.position-spot).magnitude<4)){Say("范围有人或机关，请换空地再放置。");p.nextAttack=Time+.3f;return true;}
            }
            CancelSelfMovement(p);CancelSelfGuards(p);CancelSelfLoot(p);partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.45f;
            var e=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,sourceInstance=item.id,mechanicId=d.id,visual=item.asset.visualId,color=item.asset.color,wordCategory=item.asset.wordCategory,wordText=item.asset.wordText,abilityId=item.asset.abilityId,createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,health=2,position=SelfControl(d.id)?p.position:spot,velocity=direction,stage="active"};
            if(d.id=="water-jet")e.height=p.yaw;
            if(target!=null){e.targetId=target.profile.playerId;e.stage="attached";e.height=p.yaw;ResetEscape(target);Interrupt(p);SetAction(p,"reel",e.targetId);}
            if(anchor!=null){e.targetId=anchor.id;e.path=new[]{anchor.position};e.radius=Flat(p.position-anchor.position).magnitude;e.velocity=Flat(p.velocity);e.stage="swing";}
            if(d.id=="skate-arc"||d.id=="body-bank")e.velocity=Flat(p.velocity).sqrMagnitude>1?Vector3.ClampMagnitude(Flat(p.velocity),7):direction*3;
            if(d.id=="gapped-cage"){e.stage="cage";e.machine=new MachineState();}
            if(d.id=="team-sling"){e.stage="ready";e.path=new[]{p.position};}
            if(d.id=="break-net"||d.id=="bubble-lift"||d.id=="momentum-pass")
            {e.position=p.position+direction*.85f;e.velocity=direction*(d.id=="momentum-pass"?8:9);e.stage="flying";e.triggerAt=Time+.08f;}
            Effects.Add(e);if(d.id=="break-net"||d.id=="momentum-pass")StartSingleUsePartyThrow(p,item,e);return true;
        }
        bool TryControlInteraction(Player p,string targetId)
        {
            if(!string.IsNullOrEmpty(targetId)||p.carrying!=""||p.grabbing!=""||p.publicProp!=""||IsGrabbed(p)||!ControlTarget(p)||!OnPartyGround(p))return false;
            if(Items.Any(x=>x.state==ItemState.Ground&&Vector3.Distance(x.position,p.position)<Rules.interactionRange))return false;
            var e=Effects.FirstOrDefault(x=>x.mechanicId=="team-sling"&&x.stage=="ready"&&x.owner!=p.profile.playerId&&Flat(p.position-x.position).magnitude<.8f&&LootLineClear(x.position,p.position));
            if(e==null)return false;
            if(!Players.TryGetValue(e.owner,out var op)||!ControlTarget(op)||IsGrabbed(op)||op.carrying!=""||op.publicProp!=""||Flat(op.position-e.path[0]).magnitude>.6f||Vector3.Distance(op.position,e.position)>4.5f||Time-op.lastInput>.35f)
            {Say("需要放支架的人留在原处协作；也可先回收附近的地面生命。");return true;}
            CancelSelfMovement(p);CancelAction(p);Interrupt(p);e.stage="launch_warning";e.targetId=p.profile.playerId;e.triggerAt=Time+.45f;Say("已同意抛举，0.45秒后起跳；离开支架或 Q 可取消。");return true;
        }
        void ControlMotor(Player p,ref Vector3 desired,float dt)
        {
            foreach(var jet in Effects.Where(e=>e.mechanicId=="water-jet"&&e.stage=="active"&&e.owner!=p.profile.playerId))
            {var delta=Flat(p.position-jet.position);if(ControlTarget(p)&&Vector3.Distance(p.position,jet.position)<4&&Vector3.Dot(delta.normalized,jet.velocity)>.88f&&LootLineClear(jet.position,p.position)){desired+=jet.velocity*2.2f;p.fieldStatus="持续水柱推动 · 侧绕 / 墙挡";}}
            foreach(var net in Effects.Where(x=>x.mechanicId=="break-net"&&x.stage=="attached"&&x.targetId==p.profile.playerId))
            {var next=Flat(p.position+desired*dt-net.position);if(next.magnitude>net.radius){var boundary=net.position+next.normalized*net.radius;desired=Flat(boundary-p.position)/dt;}p.fieldStatus="投网边界 · 按住 Q / 打网脱离";}
            var e=SelfControlFor(p);if(e==null||!ControlOwner(e,p))return;
            if(e.mechanicId=="water-jet"){desired*=.45f;p.yaw=Mathf.MoveTowardsAngle(e.height,p.input.yaw,65*dt);e.height=p.yaw;desired-=Forward(p)*1.1f;p.fieldStatus="持续水柱 · 反向后坐 / Q 停止";}
            if(e.mechanicId=="skate-arc")
            {var input=new Vector3(p.input.x,0,p.input.z);float speed=e.velocity.magnitude;float angle=Mathf.Atan2(e.velocity.x,e.velocity.z)*Mathf.Rad2Deg;if(input.sqrMagnitude>.01f){angle=Mathf.MoveTowardsAngle(angle,Mathf.Atan2(input.x,input.z)*Mathf.Rad2Deg,55*dt);speed=Mathf.Min(7,speed+2*dt);}else speed=Mathf.Max(0,speed-.6f*dt);e.velocity=Quaternion.Euler(0,angle,0)*Vector3.forward*speed;desired=e.velocity;p.fieldStatus="弧线滑行 · Q 刹车 / G 停止";}
            if(e.mechanicId=="body-bank")desired=e.velocity;
            if(e.mechanicId=="anchor-swing")
            {var anchor=Props.Find(x=>x.id==e.targetId);if(anchor==null)return;var radial=Flat(p.position-anchor.position);var tangent=Vector3.Cross(Vector3.up,radial.normalized);float speed=Vector3.Dot(e.velocity,tangent);var input=new Vector3(p.input.x,0,p.input.z);speed=Mathf.Clamp(speed+Vector3.Dot(input,tangent)*9*dt,-7,7);var next=radial+tangent*speed*dt;var orbit=next.normalized*e.radius;desired=(orbit-radial)/dt;e.velocity=desired;p.fieldStatus="围锚摆荡 · 按切线移动 / Q 松绳";}
            if(e.mechanicId=="skate-arc"||e.mechanicId=="body-bank"||e.mechanicId=="anchor-swing")
            {
                var from=p.position;from.y=MotorY(p);var wanted=from+desired*dt;var moved=MoveOnMachines(from,CampusGeometry.Move(from,wanted));bool wall=Flat(moved-wanted).sqrMagnitude>.00001f||BlockedByBarricade(wanted);
                var other=Players.Values.FirstOrDefault(x=>x!=p&&ControlTarget(x)&&DistanceToSegment(x.position,from,wanted)<1.05f&&Mathf.Abs(x.position.y-from.y)<1.2f&&LootLineClear(from,x.position));
                if(wall||other!=null)
                {
                    if(e.mechanicId=="body-bank")
                    {var normal=other!=null?Flat(from-other.position).normalized:WallNormal(from,wanted);var reflected=Vector3.Reflect(e.velocity,normal)*.8f;RemovePartyEffect(e);desired=reflected;ControlForce(p,reflected);p.staggerUntil=Time+.20f;if(other!=null)ControlForce(other,-normal*1.5f);CombatEvent("party_body_reflect",p,other,reflected);}
                    else {RemovePartyEffect(e);desired=Vector3.zero;if(other!=null){Hit(other,p,0,1.5f,false,impulseOverride:Flat(e.velocity).normalized*1.5f);Hit(p,other,0,1.5f,false,impulseOverride:-Flat(e.velocity).normalized*1.5f);}Say("碰撞断开滑行或绳索。");}
                }
            }
        }
        Vector3 WallNormal(Vector3 from,Vector3 wanted)
        {
            var onlyX=new Vector3(wanted.x,from.y,from.z);var onlyZ=new Vector3(from.x,from.y,wanted.z);
            bool xBlocked=Flat(MoveOnMachines(from,CampusGeometry.Move(from,onlyX))-onlyX).sqrMagnitude>.00001f;
            bool zBlocked=Flat(MoveOnMachines(from,CampusGeometry.Move(from,onlyZ))-onlyZ).sqrMagnitude>.00001f;
            return xBlocked&&!zBlocked?Vector3.left*Mathf.Sign(wanted.x-from.x):zBlocked&&!xBlocked?Vector3.back*Mathf.Sign(wanted.z-from.z):-Flat(wanted-from).normalized;
        }
        bool ControlLoose(ref Vector3 velocity,Vector3 position,float dt)
        {bool driven=false;foreach(var e in Effects.Where(e=>e.mechanicId=="water-jet"&&e.stage=="active")){var flat=Flat(position-e.position);if(Vector3.Distance(position,e.position)<4&&Vector3.Dot(flat.normalized,e.velocity)>.88f&&LootLineClear(e.position,position)){velocity+=e.velocity*7*dt;driven=true;}}return driven;}
        string ControlStatus(Player p)
        {var restraint=Effects.FirstOrDefault(e=>e.targetId==p.profile.playerId&&e.stage=="attached"&&(e.mechanicId=="break-net"||e.mechanicId=="bubble-lift"||e.mechanicId=="lasso-loop"));if(restraint!=null)return PartyMechanicRegistry.Find(restraint.mechanicId).label+" · 按住 Q 挣脱";var e2=SelfControlFor(p);return e2==null?null:PartyMechanicRegistry.Find(e2.mechanicId).label+" · Q 结束";}
        void ControlVertical(Player p,float dt)
        {
            var bubble=Effects.FirstOrDefault(e=>e.mechanicId=="bubble-lift"&&e.targetId==p.profile.playerId&&e.stage=="attached");if(bubble==null)return;
            float target=bubble.height+.7f;p.verticalSpeed=Mathf.Clamp((target-MotorY(p))/dt,0,1.2f);p.fieldStatus="泡泡上浮 · 仍可漂移 / 按住 Q 戳破";
        }
        void TickControl(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            e.remaining=Mathf.Max(0,e.deadline-Time);
            if(e.machine!=null){if(e.machine.lastTick==Time)return;e.machine.lastTick=Time;}
            if(Time>=e.deadline){RemovePartyEffect(e);return;}
            if(SelfControl(d.id)&&!ControlOwner(e,owner)){RemovePartyEffect(e);return;}
            if(d.id=="gapped-cage")return;
            if(d.id=="team-sling")
            {
                if(e.stage=="ready"||e.stage=="spent")return;
                var rider=Players.TryGetValue(e.targetId,out var r)?r:null;
                if(rider==null||!ControlTarget(rider)||!ControlTarget(owner)||IsGrabbed(rider)||IsGrabbed(owner)||rider.input.escapeHeld||owner.input.escapeHeld||!OnPartyGround(rider)||Flat(rider.position-e.position).magnitude>.9f||Flat(owner.position-e.path[0]).magnitude>.6f||Time-owner.lastInput>.35f||Time-rider.lastInput>.35f||rider.carrying!=""||owner.carrying!=""||!LootLineClear(owner.position,e.position))
                {e.stage="ready";e.targetId="";Say("抛举被取消：需两人留位、空手并保持连接。");return;}
                if(Time>=e.triggerAt){LaunchParty(rider,6.4f,e.velocity*3);e.stage="spent";e.targetId="";e.deadline=Time+.4f;CombatEvent("party_team_launch",owner,rider);}return;
            }
            if(SelfControl(d.id))e.position=owner.position;
            if(d.id=="lasso-loop")
            {
                var target=Players.TryGetValue(e.targetId,out var t)?t:null;
                if(target==null||!ControlTarget(target)||!LootLineClear(owner.position,target.position)||Vector3.Distance(owner.position,target.position)>5||Mathf.Abs(Mathf.DeltaAngle(e.height,owner.yaw))>55){RemovePartyEffect(e);return;}e.height=owner.yaw;
                var delta=Flat(target.position-owner.position);if(delta.magnitude>2.2f){var force=delta.normalized*Mathf.Min(6,(delta.magnitude-2.2f)*7)*dt;ControlForce(owner,force);ControlForce(target,-force);}return;
            }
            if(d.id=="anchor-swing")
            {var anchor=Props.Find(x=>x.id==e.targetId);if(anchor==null||anchor.holder!=""||Flat(anchor.position-e.path[0]).magnitude>.35f||!LootLineClear(owner.position,anchor.position)||Flat(owner.position-anchor.position).magnitude>e.radius+.6f){RemovePartyEffect(e);Say("锚点被搬走或绳索路线断开。");}return;}
            if(d.id=="body-bank"||d.id=="skate-arc")return;
            if(d.id=="water-jet")
            {
                e.velocity=Forward(owner);
                return;
            }
            if(e.stage=="flying")
            {
                var from=e.position;var wanted=from+e.velocity*dt;var next=CampusGeometry.Move(from,wanted,.18f,.4f);bool blocked=Flat(next-wanted).sqrMagnitude>.00001f||!MachineClear(from,next)||!LootLineClear(from,next);
                if(d.id=="momentum-pass")
                {
                    var prop=Props.Where(OrdinaryPublic).Where(x=>DistanceToSegment(x.position,from,next)<.7f&&LootLineClear(from,x.position)).OrderBy(x=>DistanceToSegment(x.position,from,next)).FirstOrDefault();
                    if(prop!=null&&Time>=e.triggerAt){var transferred=Flat(e.velocity)*.7f;var prior=Flat(prop.velocity);float energyLimit=Mathf.Sqrt(prior.sqrMagnitude+Flat(e.velocity).sqrMagnitude*.96f);prop.velocity=Vector3.ClampMagnitude(prior+transferred,Mathf.Min(8,energyLimit));e.velocity*=.2f;e.position=next;e.stage="spent";LandSingleUsePartyThrow(e);e.deadline=Time+.3f;CombatEvent("party_momentum_transfer",owner,impulse:transferred);return;}
                    e.velocity=Flat(e.velocity)*Mathf.Max(0,1-.65f*dt);next.y=SurfaceFloor(next,.4f)+.4f;
                }
                else
                {
                    var target=Players.Values.Where(x=>x!=owner&&ControlTarget(x)&&!HasControlRestraint(x)&&DistanceToSegment(x.position,from,next)<.75f&&LootLineClear(from,x.position)).OrderBy(x=>DistanceToSegment(x.position,from,next)).FirstOrDefault();
                    if(target!=null&&!blocked&&Time>=e.triggerAt)
                    {LandSingleUsePartyThrow(e);e.stage="attached";e.targetId=target.profile.playerId;e.height=MotorY(target);e.position=d.id=="break-net"?target.position:target.position;e.radius=d.id=="break-net"?1.1f:.75f;e.velocity=Vector3.zero;e.deadline=Time+(d.id=="break-net"?3:2.2f);ResetEscape(target);CombatEvent("party_control_attach",owner,target,reason:d.id);return;}
                }
                e.position=next;if(blocked||CampusGeometry.Outside(next)){RemovePartyEffect(e);return;}return;
            }
            if(e.stage=="attached")
            {
                var target=Players.TryGetValue(e.targetId,out var t)?t:null;if(target==null||!ControlTarget(target)){RemovePartyEffect(e);return;}
                if(d.id=="bubble-lift")
                {if(IsGrabbed(target)||target.grabbing!=""||CampusGeometry.Blocked(target.position+Vector3.up*.8f,.55f)){RemovePartyEffect(e);return;}e.position=target.position;}
                if(d.id=="break-net"&&Mathf.Abs(MotorY(target)-e.height)>2.1f){RemovePartyEffect(e);return;}
            }
            if(e.stage=="spent"&&Time>=e.deadline)RemovePartyEffect(e);
        }
    }
}
