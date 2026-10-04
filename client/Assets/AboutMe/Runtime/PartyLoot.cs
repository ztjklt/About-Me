using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class Match
    {
        static bool PrivateLoot(string id)=>id=="own-item-beacon"||id=="loose-radar";
        static bool SelfLoot(string id)=>PrivateLoot(id)||id=="tongs-place"||id=="rescue-tow"||id=="mischief-swap-props";
        bool LootVisible(EffectState e,string recipient)=>!PrivateLoot(e.mechanicId)||e.owner==recipient;
        bool LootChannel(EffectState e,Player owner)=>owner.connected&&!BodyLocked(owner)&&Time>=owner.staggerUntil&&!IsGrabbed(owner)&&Time-owner.lastInput<=.35f&&Item(e.sourceInstance)?.state==ItemState.Equipped&&owner.equipped==e.sourceInstance;
        void CancelSelfLoot(Player p)
        {foreach(var e in Effects.Where(e=>e.kind=="PartyMechanic"&&e.owner==p.profile.playerId&&(SelfLoot(e.mechanicId)||(e.mechanicId=="trip-line"&&e.stage=="line_wait"))).ToArray())RemovePartyEffect(e);}
        bool LootBlocks(Player p,string action)=>action=="interact"&&Effects.Any(e=>e.mechanicId=="tongs-place"&&e.owner==p.profile.playerId&&e.stage=="clamping");
        void LootMotor(Player p,ref Vector3 desired)
        {
            if(Effects.Any(e=>e.mechanicId=="carry-cradle"&&e.targetId==p.publicProp&&p.publicProp!="")){desired*=.6f;p.fieldStatus="搬盘减速 · E 可直接取资产 / Q 放盘";}
            var self=Effects.FirstOrDefault(e=>e.kind=="PartyMechanic"&&SelfLoot(e.mechanicId)&&e.owner==p.profile.playerId);
            if(self==null)return;
            desired*=self.mechanicId=="tongs-place"||self.mechanicId=="rescue-tow"?.5f:self.mechanicId=="loose-radar"?.65f:1;
            p.fieldStatus=PartyMechanicRegistry.Find(self.mechanicId).label+" · Q 结束";
        }
        bool LootLineClear(Vector3 a,Vector3 b)=>PartyClear(a,b)&&!Effects.Any(e=>e.kind=="Barricade"&&DistanceToSegment(e.position,a,b)<e.radius+.15f);
        bool LootReserved(string id)=>Effects.Any(e=>e.kind=="PartyMechanic"&&PartyMechanicRegistry.Find(e.mechanicId)?.family=="loot"&&((e.targetId==id&&(e.mechanicId=="tongs-place"||e.mechanicId=="rescue-tow"||e.mechanicId=="float-lift"))||(e.mechanicId=="carry-cradle"&&e.hit.Contains(id))));
        PersonaInstance NearestLoose(Player p,float range,bool own=false,bool front=false)=>Items.Where(x=>x.state==ItemState.Ground&&(!own||x.ownerId==p.profile.playerId)&&!LootReserved(x.id)&&Vector3.Distance(x.position,p.position)<=range&&LootLineClear(p.position,x.position)&&(!front||Vector3.Dot(Forward(p),Flat(x.position-p.position).normalized)>.3f)).OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();
        static bool OrdinaryPublic(PublicProp p)=>p.holder==""&&!p.id.StartsWith("device-",StringComparison.Ordinal)&&(p.visual=="box"||p.visual=="trash");
        bool SafeLooseSpot(Vector3 point)=>!CampusGeometry.Outside(point)&&!CampusGeometry.Blocked(point+Vector3.up*.4f)&&!Players.Values.Any(p=>!p.eliminated&&Flat(p.position-point).magnitude<1.2f&&Mathf.Abs(p.position.y-point.y)<1.8f);
        bool RescueSpot(Player target,Player source,out Vector3 point)
        {
            point=target.position;float floor=CampusGeometry.Floor(target.position,0);
            var preferred=Flat(target.position-source.position);if(preferred.sqrMagnitude<.01f)preferred=Vector3.forward;
            foreach(int n in Enumerable.Range(0,16).OrderBy(n=>Mathf.Abs(Mathf.DeltaAngle(n*22.5f,Mathf.Atan2(preferred.x,preferred.z)*Mathf.Rad2Deg))))
            {
                var candidate=target.position+Quaternion.Euler(0,n*22.5f,0)*Vector3.forward*1.3f;float nextFloor=CampusGeometry.Floor(candidate,0);candidate.y=nextFloor+.6f;
                if(Mathf.Abs(nextFloor-floor)>.1f||nextFloor< -5||CampusGeometry.Blocked(candidate,.4f,.6f)||CampusGeometry.Outside(candidate+Vector3.right*2)||CampusGeometry.Outside(candidate-Vector3.right*2)||CampusGeometry.Outside(candidate+Vector3.forward*2)||CampusGeometry.Outside(candidate-Vector3.forward*2)||!LootLineClear(target.position,candidate))continue;
                point=candidate;return true;
            }
            return false;
        }
        bool TryLootMechanic(Player p,PersonaInstance item,PartyMechanic d)
        {
            var waiting=Effects.FirstOrDefault(e=>e.mechanicId=="trip-line"&&e.sourceInstance==item.id&&e.stage=="line_wait");
            if(d.id=="trip-line"&&waiting!=null)
            {
                var end=GroundParty(p.position+Forward(p)*1.2f);float distance=Flat(end-waiting.position).magnitude;
                if(distance<1.2f||distance>d.radius||!LootLineClear(waiting.position,end)||CampusGeometry.Blocked(end+Vector3.up*.6f)){Say("另端需相距1.2–5米且无遮挡，请移动后再按 F。");p.nextAttack=Time+.3f;return true;}
                var other=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,sourceInstance=item.id,mechanicId=d.id,visual=item.asset.visualId,color=item.asset.color,position=end,createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=.45f,health=2,stage="line_end",targetId=waiting.id};
                waiting.targetId=other.id;waiting.path=new[]{waiting.position,end};waiting.stage="line_armed";waiting.deadline=Time+d.lifetime;Effects.Add(other);p.nextAttack=Time+.45f;CombatEvent("party_line_armed",p);return true;
            }
            PersonaInstance target=null;PublicProp[] swap=null;Player down=null;Vector3 safe=Vector3.zero;
            if(d.id=="tongs-place"||d.id=="rescue-tow"||d.id=="float-lift")target=NearestLoose(p,d.radius,d.id=="rescue-tow",d.id=="tongs-place");
            if((d.id=="tongs-place"||d.id=="rescue-tow"||d.id=="float-lift")&&target==null){Say(d.id=="rescue-tow"?"附近没有可拖救的自己的地面生命。":"附近没有无遮挡且未被托住的地面生命。");p.nextAttack=Time+.3f;return true;}
            if(d.id=="mischief-swap-props")
            {swap=Props.Where(x=>OrdinaryPublic(x)&&x.velocity.magnitude<.5f&&Vector3.Distance(x.position,p.position)<d.radius&&LootLineClear(p.position,x.position)).OrderBy(x=>(x.position-p.position).sqrMagnitude).Take(2).ToArray();if(swap.Length!=2||!LootLineClear(swap[0].position,swap[1].position)){Say("附近需要两件无遮挡、未被拿起的公共箱桶。");p.nextAttack=Time+.3f;return true;}}
            if(d.id=="gentle-push-clear")
            {down=Players.Values.Where(x=>x!=p&&!x.eliminated&&BodyDown(x)&&Vector3.Distance(x.position,p.position)<d.radius&&LootLineClear(p.position,x.position)).OrderBy(x=>(x.position-p.position).sqrMagnitude).FirstOrDefault();if(down==null||!RescueSpot(down,p,out safe)){Say("附近没有能安全推离的倒地角色。");p.nextAttack=Time+.3f;return true;}}
            var spot=d.id=="carry-cradle"||d.id=="public-hot-potato"||d.id=="trip-line"?GroundParty(p.position+Forward(p)*1.2f):target?.position??p.position;
            if((d.id=="carry-cradle"||d.id=="public-hot-potato"||d.id=="trip-line")&&CampusGeometry.Blocked(spot+Vector3.up*.6f)){Say("放置位置被挡住，请换个空地。");p.nextAttack=Time+.3f;return true;}
            CancelSelfLoot(p);partyCooldowns[item.id]=Time+d.cooldown;p.nextAttack=Time+.45f;
            var e=new EffectState{id=Guid.NewGuid().ToString("N"),kind="PartyMechanic",owner=p.profile.playerId,sourceInstance=item.id,mechanicId=d.id,visual=item.asset.visualId,color=item.asset.color,position=spot,createdAt=Time,deadline=Time+d.lifetime,remaining=d.lifetime,radius=d.radius,health=2,stage="active",targetId=target?.id??"",velocity=Forward(p)};
            if(d.id=="tongs-place")e.stage="clamping";
            if(d.id=="carry-cradle"||d.id=="public-hot-potato")
            {e.targetId="device-"+e.id;spot.y=CampusGeometry.Floor(spot,.1f)+.6f;e.position=spot;Props.Add(new PublicProp{id=e.targetId,visual=e.visual,position=spot,hitAfter=float.MaxValue});}
            if(d.id=="trip-line"){e.stage="line_wait";e.radius=.45f;}
            if(swap!=null){e.stage="swap_warning";e.hit=swap.Select(x=>x.id).ToList();e.path=swap.Select(x=>x.position).ToArray();e.triggerAt=Time+.6f;}
            if(down!=null){e.targetId=down.profile.playerId;e.path=new[]{safe};var kick=Flat(safe-down.position).normalized*d.force;if(BodyWorld!=null)BodyWorld.Impact(down,kick,down.position,false);else down.impulse+=kick;CombatEvent("party_gentle_rescue",p,down,kick);}
            Effects.Add(e);return true;
        }
        bool OfferHotPotato(Player p,PublicProp prop)
        {
            var e=Effects.FirstOrDefault(e=>e.mechanicId=="public-hot-potato"&&e.targetId==prop.id);if(e==null)return false;
            if(e.stage=="offered"&&Time<e.triggerAt){e.stage="active";e.triggerAt=0;return false;}
            e.stage="offered";e.triggerAt=Time+1.2f;p.nextAttack=Time+.45f;Say(p.profile.nickname+" 邀请接包：对方按 G 自愿接，再按 F 可扔出。");return true;
        }
        bool AcceptHotPotato(Player p)
        {
            foreach(var e in Effects.Where(e=>e.mechanicId=="public-hot-potato"&&e.stage=="offered"&&Time<e.triggerAt).ToArray())
            {
                var prop=Props.Find(x=>x.id==e.targetId);if(prop==null||prop.holder==""||prop.holder==p.profile.playerId||p.carrying!=""||!CanReach(p,prop.position,Rules.grabReach))continue;
                var old=Players[prop.holder];old.publicProp="";SetAction(old,"release");p.publicProp=prop.id;prop.holder=p.profile.playerId;prop.thrower="";prop.velocity=Vector3.zero;e.stage="active";e.triggerAt=0;SetAction(p,"pickup",prop.id);CombatEvent("party_hot_handoff",old,p);return true;
            }
            return false;
        }
        void TickLoot(EffectState e,Player owner,PartyMechanic d,float dt)
        {
            e.remaining=Mathf.Max(0,e.deadline-Time);
            if(e.stage=="ended"||e.stage=="burst"){if(Time>=e.deadline)RemovePartyEffect(e);return;}
            if(SelfLoot(d.id)&&!LootChannel(e,owner)){RemovePartyEffect(e);return;}
            if(Time>=e.deadline)
            {if(d.id=="public-hot-potato"){var prop=Props.Find(x=>x.id==e.targetId);if(prop!=null)e.position=prop.holder!=""&&Players.TryGetValue(prop.holder,out var holder)?holder.position+Forward(holder):prop.position;BurstParty(e,owner,d);}else RemovePartyEffect(e);return;}
            if(d.id=="public-hot-potato")
            {if(e.stage=="burst")return;var prop=Props.Find(x=>x.id==e.targetId);if(prop==null){RemovePartyEffect(e);return;}e.position=prop.holder!=""&&Players.TryGetValue(prop.holder,out var h)?h.position+Forward(h):prop.position;if(e.stage=="offered"&&Time>=e.triggerAt)e.stage="active";return;}
            if(PrivateLoot(d.id))
            {
                e.position=owner.position;
                var hits=Items.Where(x=>x.state==ItemState.Ground&&Vector3.Distance(x.position,owner.position)<=d.radius&&(d.id=="own-item-beacon"?x.ownerId==e.owner:LootLineClear(owner.position,x.position))).OrderBy(x=>(x.position-owner.position).sqrMagnitude).Take(d.id=="own-item-beacon"?1:6).ToArray();e.hit=hits.Select(x=>x.id).ToList();e.path=hits.Select(x=>x.position).ToArray();return;
            }
            if(d.id=="mischief-swap-props")
            {
                var pair=e.hit.Select(id=>Props.Find(x=>x.id==id)).ToArray();
                if(pair.Any(x=>x==null||!OrdinaryPublic(x))||pair.Where((x,i)=>Vector3.Distance(x.position,e.path[i])>.4f||x.velocity.magnitude>1).Any()||!LootLineClear(pair[0].position,pair[1].position)){Say("公共物被拿走、移动或隔断，交换取消。");RemovePartyEffect(e);return;}
                if(Time>=e.triggerAt){if(!SafeLooseSpot(pair[0].position)||!SafeLooseSpot(pair[1].position)){Say("箱位有人或被挡住，交换取消。");RemovePartyEffect(e);return;}var a=pair[0].position;pair[0].position=pair[1].position;pair[1].position=a;pair[0].velocity=pair[1].velocity=Vector3.zero;CombatEvent("party_prop_swap",owner);e.stage="ended";e.deadline=Time+.25f;}return;
            }
            if(d.id=="trip-line")
            {
                if(e.stage=="line_wait"){if(!LootChannel(e,owner))RemovePartyEffect(e);return;}if(e.stage=="line_end")return;
                var other=Effects.FirstOrDefault(x=>x.id==e.targetId);if(other==null||!LootLineClear(e.path[0],e.path[1])){RemovePartyEffect(e);return;}
                var line=Flat(e.path[1]-e.path[0]);var normal=Vector3.Cross(Vector3.up,line.normalized);
                foreach(var target in Players.Values.Where(p=>!p.eliminated&&p.running&&!p.airborne&&p.bodyMode==BodyMode.Standing&&!e.hit.Contains(p.profile.playerId)))
                {
                    float before=Vector3.Dot(target.tickOrigin-e.path[0],normal),after=Vector3.Dot(target.position-e.path[0],normal);
                    if(before*after>0||Mathf.Abs(before-after)<.001f||Mathf.Abs(CampusGeometry.Floor(target.position)-CampusGeometry.Floor(e.position,.1f))>.25f)continue;
                    var point=Vector3.Lerp(target.tickOrigin,target.position,before/(before-after));float t=Vector3.Dot(point-e.path[0],line.normalized);if(t<0||t>line.magnitude)continue;
                    e.hit.Add(target.profile.playerId);Hit(target,owner,d.damage,d.force,false,impulseOverride:Flat(target.velocity).normalized*d.force);CombatEvent("party_trip",owner,target);
                }
                return;
            }
            if(d.id=="carry-cradle"||d.id=="gentle-push-clear")return;
            var item=Item(e.targetId);if(item==null||item.state!=ItemState.Ground){RemovePartyEffect(e);return;}
            if(d.id=="float-lift"){item.position=new Vector3(e.position.x,CampusGeometry.Floor(e.position,.35f)+.95f,e.position.z);item.velocity=Vector3.zero;return;}
            if(!LootLineClear(owner.position,item.position)){RemovePartyEffect(e);return;}
            e.position=owner.position;var wanted=d.id=="tongs-place"?owner.position+Quaternion.Euler(0,90,0)*Forward(owner)*.8f+Forward(owner)*.2f:owner.position;
            wanted.y=CampusGeometry.Floor(item.position,.35f)+.35f;var direction=Flat(wanted-item.position);float stopping=d.id=="rescue-tow"?.85f:0;
            if(direction.magnitude<=stopping+.06f){e.stage="ended";e.deadline=Time+.2f;Say("地面生命已到身边，仍需 E 完成回收或吞噬。");return;}
            var next=item.position+direction.normalized*Mathf.Min(direction.magnitude-stopping,(d.id=="tongs-place"?7:1.8f)*dt);next=CampusGeometry.Move(item.position,next,.15f,.35f);next.y=CampusGeometry.Floor(next,.35f)+.35f;item.position=next;item.velocity=Vector3.zero;
        }
        void TickLootCargo()
        {
            foreach(var e in Effects.Where(x=>x.mechanicId=="carry-cradle").ToArray())
            {
                var prop=Props.Find(x=>x.id==e.targetId);if(prop==null){RemovePartyEffect(e);continue;}
                var old=e.position;
                for(int i=e.hit.Count-1;i>=0;i--){var item=Item(e.hit[i]);if(item==null||item.state!=ItemState.Ground){e.hit.RemoveAt(i);continue;}var offset=Flat(item.position-old);item.position=prop.position+offset+Vector3.up*.16f;item.velocity=Vector3.zero;}
                e.position=prop.position;
                foreach(var item in Items.Where(x=>x.state==ItemState.Ground&&!LootReserved(x.id)&&Flat(x.position-prop.position).magnitude<=.8f&&Mathf.Abs(x.position.y-prop.position.y)<.8f).OrderBy(x=>(x.position-prop.position).sqrMagnitude).Take(Mathf.Max(0,2-e.hit.Count)).ToArray())
                {e.hit.Add(item.id);item.position=new Vector3(item.position.x,prop.position.y+.16f,item.position.z);item.velocity=Vector3.zero;}
                e.path=e.hit.Select(id=>Item(id).position).ToArray();
            }
        }
    }
}
