using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class Match
    {
        readonly System.Collections.Generic.Dictionary<string,EffectState> vaultedShots=new System.Collections.Generic.Dictionary<string,EffectState>();
        static bool LinkPair(string id)=>id=="zipline-build"||id=="rope-bridge";
        static bool SelfLink(string id)=>id=="wall-cling"||id=="safety-tether";
        EffectState LinkSelf(Player p)=>Effects.FirstOrDefault(e=>SelfLink(e.mechanicId)&&e.owner==p.profile.playerId);
        EffectState LinkRide(Player p)=>Effects.FirstOrDefault(e=>e.id==p.linkRide&&e.mechanicId=="zipline-build"&&e.stage=="linked"&&VirtualAlive(e));
        void CancelSelfLinks(Player p)
        {
            p.linkRide="";
            foreach(var e in Effects.Where(e=>SelfLink(e.mechanicId)&&e.owner==p.profile.playerId||LinkPair(e.mechanicId)&&e.owner==p.profile.playerId&&e.stage=="link_wait").ToArray())RemovePartyEffect(e);
            foreach(var e in Effects.Where(e=>e.mechanicId=="consent-swap"&&(e.targetId==p.profile.playerId||e.hit.Contains(p.profile.playerId)))){e.hit.Clear();e.targetId="";e.stage="ready";}
        }
        bool LinkBlocks(Player p,string action)
        {return (LinkRide(p)!=null||LinkSelf(p)?.mechanicId=="wall-cling"||Effects.Any(e=>e.mechanicId=="consent-swap"&&e.stage=="consent"&&(e.hit.Contains(p.profile.playerId)||e.targetId==p.profile.playerId)))&&(action=="use_item"||action=="interact");}
        string LinkStatus(Player p)=>LinkRide(p)!=null?"共用索道 · 方向键减速 / Q 松开":LinkSelf(p)?.mechanicId=="wall-cling"?"短时扒墙 · 沿墙移动 / 跳离 / Q 松手":null;
        bool LinkPlacement(Vector3 point,float radius=.6f)
        {return !CampusGeometry.Outside(point)&&CampusGeometry.Floor(point,.1f)>-20&&!CampusGeometry.Blocked(point+Vector3.up,.4f)&&!CampusGeometry.Blocked(point+Vector3.up*2,.4f)&&!MachineGeometry().Any(b=>b.Contains(point+Vector3.up*.6f,radius));}
        bool LinkRoute(Vector3 a,Vector3 b,string ignore=null)
        {return !CampusGeometry.Outside(b)&&CampusGeometry.ClearSegment(a+Vector3.up*.8f,b+Vector3.up*.8f)&&MachineClear(a+Vector3.up*.8f,b+Vector3.up*.8f,ignore);}
        bool TryLinkMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            var direction=Forward(p);var spot=GroundParty(p.position+direction*1.8f);var first=Effects.FirstOrDefault(e=>e.mechanicId==d.id&&e.sourceInstance==item.id&&e.stage=="link_wait");PublicProp[] selected=null;Vector3 wallNormal=Vector3.zero;
            if(LinkPair(d.id)&&first!=null)
            {float distance=Flat(spot-first.position).magnitude;if(distance<2||distance>6||Mathf.Abs(spot.y-first.position.y)>1.2f||!LinkRoute(first.position,spot)||!LinkPlacement(spot)){Say("第二锚需2–6米内、端点可站立、高差至多1.2米，连线不可穿墙。");p.nextAttack=Time+.3f;return true;}
                first.path=new[]{first.position,spot};first.stage="linked";first.deadline=Time+d.lifetime;first.remaining=d.lifetime;first.velocity=Flat(spot-first.position).normalized;if(d.id=="rope-bridge")first.machine=new MachineState();Say("公共"+d.label+"已连：双方都可使用，锚点两击拆除。");p.nextAttack=Time+.45f;return true;}
            if(d.id=="wall-cling")
            {
                if(BodyLocked(p)||IsGrabbed(p)||p.carrying!=""||p.publicProp!=""||p.grabbing!=""){Say("扒墙需要双手空闲且未被抓。");p.nextAttack=Time+.3f;return true;}
                var probe=p.position+direction*1.1f;
                var wall=CampusGeometry.Solids.Where(s=>s.size.y>=1.5f&&s.size.y<12&&Mathf.Abs(probe.x-s.center.x)<s.size.x*.5f+.1f&&Mathf.Abs(probe.z-s.center.z)<s.size.z*.5f+.1f&&p.position.y>s.center.y-s.size.y*.5f&&p.position.y<s.center.y+s.size.y*.5f-.45f).OrderBy(s=>(s.center-probe).sqrMagnitude).FirstOrDefault();
                if(wall==null){Say("前方1.1米内需要可攀的校园实体墙，不能爬临时机关。");p.nextAttack=Time+.3f;return true;}
                var center=wall.center;var half=wall.size*.5f;var offsets=new[]{new Vector3(center.x-half.x-.55f,p.position.y,p.position.z),new Vector3(center.x+half.x+.55f,p.position.y,p.position.z),new Vector3(p.position.x,p.position.y,center.z-half.z-.55f),new Vector3(p.position.x,p.position.y,center.z+half.z+.55f)};spot=offsets.OrderBy(x=>(x-p.position).sqrMagnitude).First();wallNormal=Flat(spot-center);if(Mathf.Abs(spot.x-center.x)>half.x)wallNormal=Vector3.right*Mathf.Sign(spot.x-center.x);else wallNormal=Vector3.forward*Mathf.Sign(spot.z-center.z);
                if(Flat(spot-p.position).magnitude>.9f||CampusGeometry.Blocked(spot,.4f)||!LinkRoute(p.position,spot)){Say("墙面边缘或站位不合法，换一面墙。");p.nextAttack=Time+.3f;return true;}
            }
            if(d.id=="safety-tether")
            {if(!OnPartyGround(p)||p.carrying!=""||p.publicProp!=""||p.grabbing!=""||IsGrabbed(p)){Say("救援绳需在可站立地面绑自己，双手空闲。");p.nextAttack=Time+.3f;return true;}spot=GroundParty(p.position);}
            if(d.id=="public-clamp"||d.id=="public-twin-rope")
            {selected=Props.Where(OrdinaryPublic).Where(x=>Vector3.Distance(p.position,x.position)<4&&LootLineClear(p.position,x.position)&&!Effects.Any(e=>e.mechanicId=="public-clamp"&&e.targetId==x.id||e.mechanicId=="public-twin-rope"&&e.hit.Contains(x.id))).OrderBy(x=>(x.position-p.position).sqrMagnitude).Take(d.id=="public-clamp"?1:2).ToArray();if(selected.Length!=(d.id=="public-clamp"?1:2)||(selected.Length==2&&(Vector3.Distance(selected[0].position,selected[1].position)>5||!LootLineClear(selected[0].position,selected[1].position)))){Say(d.id=="public-clamp"?"需要近处可见的未持有公共箱桶。":"需要两件近处可见、未持有且未被系绳的公共箱桶。");p.nextAttack=Time+.3f;return true;}spot=selected[0].position;}
            if(d.id=="item-scale"||d.id=="consent-swap")spot=GroundParty(p.position+direction*3.7f);
            if(!SelfLink(d.id)&&selected==null&&!LinkPlacement(spot)){Say("装置锚点需合法空地，换位置再放。");p.nextAttack=Time+.3f;return true;}
            if(d.id=="item-scale"||d.id=="consent-swap")
            {for(int x=-1;x<=1;x++)if(!LinkPlacement(spot+Vector3.Cross(Vector3.up,direction)*x*1.3f)||Mathf.Abs(CampusGeometry.Floor(spot+Vector3.Cross(Vector3.up,direction)*x*1.3f,.1f)-spot.y+.15f)>.1f||Players.Values.Any(q=>!q.eliminated&&Flat(q.position-spot).magnitude<2.4f)){Say("双盘或双站点需要平整空地，不能放在人脚下。");p.nextAttack=Time+.3f;return true;}}
            if(SelfLink(d.id)){CancelSelfMovement(p);CancelSelfLoot(p);CancelSelfGuards(p);}
            partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.45f;
            var effect=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,mechanicId=d.id,sourceInstance=item.id,visual=item.asset.visualId,color=item.asset.color,position=spot,velocity=direction,createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,health=2,stage="ready"};
            if(LinkPair(d.id)){effect.stage="link_wait";effect.deadline=Time+6;Say("第一锚已放，移动到另一个端点再 F；Q 取消等待。");}
            if(d.id=="wall-cling"){var shift=spot-p.position;p.position=spot;p.motorHeight=spot.y;BodyWorld?.Translate(p,shift);effect.position=spot;effect.velocity=wallNormal;effect.height=MotorY(p);effect.path=new[]{spot};effect.stage="clinging";effect.position=p.position;SetAction(p,"reel");}
            if(d.id=="safety-tether"){effect.path=new[]{p.position};effect.stage="tethered";}
            if(d.id=="item-scale")effect.machine=new MachineState();
            if(d.id=="public-clamp"){effect.targetId=selected[0].id;effect.path=new[]{selected[0].position};effect.stage="clamped";}
            if(d.id=="public-twin-rope"){effect.hit=selected.Select(x=>x.id).ToList();effect.radius=Vector3.Distance(selected[0].position,selected[1].position);effect.path=selected.Select(x=>x.position).ToArray();effect.stage="linked";}
            Effects.Add(effect);return true;
        }
        bool TryLinkInteraction(Player p,string target)
        {
            if(!string.IsNullOrEmpty(target)||!ControlTarget(p)||IsGrabbed(p)||p.grabbing!=""||p.carrying!=""||p.publicProp!=""||Items.Any(x=>x.state==ItemState.Ground&&Vector3.Distance(x.position,p.position)<Rules.interactionRange))return false;
            if(p.linkRide!=""){p.linkRide="";Say("已主动松开索道。");return true;}
            var e=Effects.Where(x=>VirtualAlive(x)).FirstOrDefault(x=>x.mechanicId=="zipline-build"&&x.stage=="linked"&&x.path.Any(at=>Vector3.Distance(p.position,at+Vector3.up*.85f)<1.25f)&&LinkRoute(p.position,x.position,x.id)||x.mechanicId=="consent-swap"&&x.stage=="ready"&&Vector3.Distance(p.position,x.position+Vector3.up*.85f)<2.3f&&LinkRoute(p.position,x.position,x.id)||x.mechanicId=="shot-vault"&&x.stage=="stored"&&Vector3.Distance(p.position,x.position+Vector3.up*.85f)<1.8f&&LinkRoute(p.position,x.position,x.id));
            if(e==null)return false;
            if(e.mechanicId=="zipline-build")
            {bool atStart=Vector3.Distance(p.position,e.path[0]+Vector3.up*.85f)<Vector3.Distance(p.position,e.path[1]+Vector3.up*.85f);CancelSelfMovement(p);CancelAction(p);p.linkRide=e.id;p.linkProgress=atStart?0:1;p.linkDirection=atStart?1:-1;Say("索道双方可坐：方向键减速，Q 松开；不能同时吞噬。");return true;}
            if(e.mechanicId=="shot-vault"){ReleaseVault(e,p);return true;}
            var yaw=Quaternion.LookRotation(e.velocity);var left=e.position+yaw*Vector3.left*1.3f;var right=e.position+yaw*Vector3.right*1.3f;int slot=Flat(p.position-left).magnitude<.75f?0:Flat(p.position-right).magnitude<.75f?1:-1;
            if(slot<0){Say("站在左或右换位牌上再按 E。");return true;}
            if(e.hit.Count==0){CancelSelfMovement(p);e.hit.Add(p.profile.playerId);e.chainIndex=slot;e.path=new[]{p.position};e.triggerAt=Time+2;Say("已同意换位，等另一侧第二人 E；移动或 Q 取消。");return true;}
            if(e.hit.Contains(p.profile.playerId)||slot==e.chainIndex){Say("另一侧需第二个不同玩家主动 E 同意。");return true;}
            CancelSelfMovement(p);e.targetId=p.profile.playerId;e.path=new[]{e.path[0],p.position};e.stage="consent";e.triggerAt=Time+.45f;Say("双方同意，0.45秒后交换站位；移动、Q或受击取消。");return true;
        }
        void LinkMotor(Player p,ref Vector3 desired,float dt)
        {
            var ride=LinkRide(p);if(ride!=null)
            {if(!ControlTarget(p)||IsGrabbed(p)||p.publicProp!=""||p.carrying!=""||p.grabbing!=""||Time-p.lastInput>.35f){p.linkRide="";return;}float length=Vector3.Distance(ride.path[0],ride.path[1]);float input=Vector3.Dot(new Vector3(p.input.x,0,p.input.z),ride.velocity)*p.linkDirection;float speed=input<-.4f?1.5f:5;float step=speed*dt/Mathf.Max(2,length);p.linkProgress=Mathf.Clamp01(p.linkProgress+p.linkDirection*step);var next=Vector3.Lerp(ride.path[0],ride.path[1],p.linkProgress)+Vector3.up*.95f;
                if(!LinkRoute(p.position,next,ride.id)){p.linkRide="";desired=Vector3.zero;return;}desired=Flat(next-p.position)/dt;desired=Vector3.ClampMagnitude(desired,6);return;}
            var wall=LinkSelf(p);if(wall?.mechanicId!="wall-cling")return;
            if(!MovementOwnerValid(wall,p)||p.carrying!=""||p.publicProp!=""||p.grabbing!=""){RemovePartyEffect(wall);return;}
            var tangent=Vector3.Cross(Vector3.up,wall.velocity);desired=tangent*Vector3.Dot(desired,tangent)*.45f;var nextPoint=p.position+desired*dt;
            if(Flat(nextPoint-wall.path[0]).magnitude>1||!CampusGeometry.Blocked(nextPoint-wall.velocity*.65f,.05f)||!LinkRoute(p.position,nextPoint))desired=Vector3.zero;
        }
        void LinkVertical(Player p,ref Vector3 next)
        {
            var ride=LinkRide(p);if(ride!=null){next.y=Vector3.Lerp(ride.path[0],ride.path[1],p.linkProgress).y+.95f;p.verticalSpeed=0;p.airborne=true;if(p.linkProgress>=1&&p.linkDirection>0||p.linkProgress<=0&&p.linkDirection<0){p.linkRide="";CombatEvent("party_zipline_exit",p);}return;}
            var wall=LinkSelf(p);if(wall?.mechanicId=="wall-cling"&&MovementOwnerValid(wall,p)){next.y=wall.height;p.verticalSpeed=0;p.airborne=true;}
        }
        bool RescueLink(Player p,ref Vector3 next)
        {
            var e=LinkSelf(p);if(e?.mechanicId!="safety-tether"||e.path==null||!MovementOwnerValid(e,p))return false;
            float floor=SurfaceFloor(next);bool hazard=CampusGeometry.Outside(next)||floor<-20||p.fallOriginHeight-floor>2&&p.fallOriginHeight-(next.y-1)>.6f;if(!hazard)return false;
            var anchor=e.path[0];if(Vector3.Distance(p.position,anchor)>6||CampusGeometry.Blocked(anchor)||!LinkRoute(anchor,p.position,e.id)){RemovePartyEffect(e);return false;}
            // The fall still drops exactly one original life. The rope changes only the person's return point.
            Drop(p,Rules.fallDrop);next=anchor;p.position=anchor;p.motorHeight=anchor.y;p.verticalSpeed=0;p.impulse=Vector3.zero;p.airborne=false;p.fallOriginHeight=SurfaceFloor(anchor);p.staggerUntil=Time+.3f;BodyWorld?.Reset(p);RemovePartyEffect(e);CombatEvent("party_tether_rescue",p);Say("救援绳拉回原锚；坠落仍掉一件真实 Persona，请回收。");return true;
        }
        bool LinkJump(Player p)
        {var e=LinkSelf(p);if(e?.mechanicId!="wall-cling")return false;var normal=e.velocity;RemovePartyEffect(e);LaunchParty(p,4.2f,normal*2);return true;}
        bool LinkPublicHeld(PublicProp prop)
        {return Effects.Any(e=>e.mechanicId=="public-clamp"&&e.targetId==prop.id&&VirtualAlive(e)&&prop.holder=="");}
        bool LinkShot(EffectState shot,float dt)
        {
            var from=shot.position;var next=from+shot.velocity*dt;
            foreach(var e in Effects.Where(x=>(x.mechanicId=="projectile-fork"||x.mechanicId=="shot-vault")&&VirtualAlive(x)).ToArray())
            {
                if(e.mechanicId=="projectile-fork"&&shot.guardTouches.Contains("link-forked")||shot.guardTouches.Contains(e.id)||!GuardPlane(e.position+Vector3.up*.85f,e.velocity,e.radius,from,next,true,out var hit)||!LootLineClear(from,hit))continue;
                if(e.mechanicId=="shot-vault")
                {if(e.stage!="ready")continue;e.stage="stored";e.path=new[]{shot.velocity};e.force=shot.force;e.damage=shot.damage;e.targetId=shot.owner;e.height=shot.remaining;e.chainIndex=Array.IndexOf(new[]{"Projectile","Disc","ThrownDisc","Ricochet"},shot.kind);e.triggerAt=Time+.65f;e.nextHit=Time;vaultedShots[e.id]=shot;Effects.Remove(shot);CombatEvent("party_shot_store",Players[e.owner],reason:shot.id);return true;}
                if(Effects.Count>=32){shot.guardTouches.Add(e.id);continue;}
                shot.guardTouches.Add("link-forked");shot.guardTouches.Add(e.id);shot.damage*=.5f;shot.force*=.5f;shot.velocity=Quaternion.Euler(0,-25,0)*shot.velocity;shot.position=hit;
                var copy=new EffectState{id=Guid.NewGuid().ToString("N"),kind=shot.kind,owner=shot.owner,visual=shot.visual,color=shot.color,position=hit,velocity=Quaternion.Euler(0,50,0)*shot.velocity,remaining=shot.remaining,damage=shot.damage,force=shot.force,heavy=shot.heavy,knockdown=shot.knockdown,radius=shot.radius,bounces=shot.bounces,pushScale=shot.pushScale,hit=new System.Collections.Generic.List<string>(shot.hit),emblemPath=shot.emblemPath,emblemCrop=shot.emblemCrop};foreach(var mark in shot.guardTouches)copy.guardTouches.Add(mark);Effects.Add(copy);CombatEvent("party_shot_fork",Players[e.owner],reason:shot.id);return true;
            }
            return false;
        }
        void ReleaseVault(EffectState e,Player op=null)
        {if(e.stage!="stored")return;var direction=op!=null?Forward(op):e.velocity;var velocity=e.path[0];var aim=direction*Flat(velocity).magnitude+Vector3.up*velocity.y;var start=e.position+Vector3.up*.85f+direction*.55f;e.stage="spent";
            if(vaultedShots.TryGetValue(e.id,out var stored)){vaultedShots.Remove(e.id);stored.remaining=Mathf.Max(0,stored.remaining-(Time-e.nextHit));stored.position=start;stored.velocity=aim;stored.guardTouches.Add(e.id);if(stored.remaining>0&&!CampusGeometry.Blocked(start,.2f,.3f)&&MachineClear(e.position+Vector3.up*.85f,start,e.id))Effects.Add(stored);}CombatEvent("party_shot_release",op??Players[e.owner],reason:e.targetId);e.deadline=Time+.2f;}
        void TickLinks(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            e.remaining=Mathf.Max(0,e.deadline-Time);if(Time>=e.deadline){RemovePartyEffect(e);return;}if(e.machine!=null){if(e.machine.lastTick==Time)return;e.machine.lastTick=Time;}
            if(SelfLink(d.id)){if(!MovementOwnerValid(e,owner)){RemovePartyEffect(e);return;}e.position=owner.position;if(d.id=="safety-tether"&&(Vector3.Distance(owner.position,e.path[0])>6||!LinkRoute(owner.position,e.path[0],e.id)))RemovePartyEffect(e);return;}
            if(d.id=="rope-bridge"&&e.stage=="linked"){e.machine.previousAngle=e.machine.angle;e.machine.angle=Mathf.Sin((Time-e.createdAt)*3)*3;return;}
            if(d.id=="public-clamp")
            {var prop=Props.Find(x=>x.id==e.targetId);if(prop==null||prop.holder!=""){RemovePartyEffect(e);return;}prop.position=e.path[0];prop.velocity=Vector3.zero;prop.thrower="";return;}
            if(d.id=="public-twin-rope")
            {var a=Props.Find(x=>x.id==e.hit[0]);var b=Props.Find(x=>x.id==e.hit[1]);if(a==null||b==null||Vector3.Distance(a.position,b.position)>6||!LootLineClear(a.position,b.position)){RemovePartyEffect(e);return;}e.position=(a.position+b.position)*.5f;e.path=new[]{a.position,b.position};var delta=Flat(b.position-a.position);float excess=delta.magnitude-e.radius;if(excess>.05f){var force=delta.normalized*Mathf.Min(6,excess*10)*dt;if(a.holder=="")a.velocity=Vector3.ClampMagnitude(a.velocity+force,8);if(b.holder=="")b.velocity=Vector3.ClampMagnitude(b.velocity-force,8);}return;}
            if(d.id=="shot-vault"&&e.stage=="stored"&&Time>=e.triggerAt){ReleaseVault(e);return;}
            if(d.id=="consent-swap"&&e.stage!="spent"&&e.hit.Count>0)
            {
                var a=Players[e.hit[0]];var b=e.targetId!=""&&Players.TryGetValue(e.targetId,out var other)?other:null;
                bool Ready(Player q,int i)=>ControlTarget(q)&&!IsGrabbed(q)&&q.carrying==""&&q.publicProp==""&&q.grabbing==""&&Time-q.lastInput<=.35f&&Flat(q.position-e.path[i]).magnitude<.25f&&q.input.x*q.input.x+q.input.z*q.input.z<.01f&&Mathf.Abs(q.position.y-e.path[i].y)<.2f;
                if(!Ready(a,0)||b!=null&&!Ready(b,1)||e.stage=="ready"&&Time>=e.triggerAt){e.hit.Clear();e.targetId="";e.stage="ready";Say("换位同意已取消，站回两侧可重试。");return;}
                if(e.stage=="consent"&&Time>=e.triggerAt)
                {var left=e.path[0];var right=e.path[1];if(CampusGeometry.Blocked(left)||CampusGeometry.Blocked(right)||BlockedByBarricade(left)||BlockedByBarricade(right)||MachineGeometry().Any(shape=>shape.Contains(left,.4f)||shape.Contains(right,.4f))||!LinkRoute(left,right,e.id)||Players.Values.Any(p=>p!=a&&p!=b&&!p.eliminated&&(Flat(p.position-left).magnitude<1.15f||Flat(p.position-right).magnitude<1.15f))){e.hit.Clear();e.targetId="";e.stage="ready";Say("换位落点被挡，未交换。");return;}a.position=right;a.motorHeight=right.y;b.position=left;b.motorHeight=left.y;a.velocity=b.velocity=Vector3.zero;a.impulse=b.impulse=Vector3.zero;BodyWorld?.Reset(a);BodyWorld?.Reset(b);e.stage="spent";e.deadline=Time+.3f;CombatEvent("party_consent_swap",a,b);}
            }
            if(d.id=="item-scale")TickItemScale(e,dt);
        }
        void TickItemScale(EffectState e,float dt)
        {
            var m=e.machine;m.previousHeight=m.height;var before=MachineShapes(e,true).ToArray();int left=0,right=0;
            foreach(var item in Items.Where(x=>x.state==ItemState.Ground))for(int i=0;i<2;i++)if(before[i].Surface(item.position,.35f,out var top)&&Mathf.Abs(item.position.y-.35f-top)<.22f){if(i==0)left++;else right++;}
            float target=Mathf.Clamp((left-right)*.45f,-.45f,.45f);m.height=Mathf.MoveTowards(m.height,target,.65f*dt);e.force=left-right;var after=MachineShapes(e).ToArray();
            for(int i=0;i<2;i++){float change=after[i].center.y-before[i].center.y;foreach(var item in Items.Where(x=>x.state==ItemState.Ground))if(before[i].Surface(item.position,.35f,out var top)&&Mathf.Abs(item.position.y-.35f-top)<.22f){item.position+=Vector3.up*change;item.velocity.y=0;}foreach(var p in Players.Values)if(!p.eliminated&&before[i].Surface(p.position,1,out var top)&&Mathf.Abs(MotorY(p)-1-top)<.22f){p.position+=Vector3.up*change;p.motorHeight+=change;BodyWorld?.Translate(p,Vector3.up*change);}}
        }
    }
}
