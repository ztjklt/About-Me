using System;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    public partial class Match : IDisposable
    {
        public ActiveDollWorld BodyWorld {get;private set;}
        public void EnableBodyPhysics(){if(BodyWorld==null)BodyWorld=new ActiveDollWorld();}
        public void Dispose(){BodyWorld?.Dispose();BodyWorld=null;}
        public bool BodyDown(Player p)=>p.bodyMode==BodyMode.Falling||p.bodyMode==BodyMode.Grounded;
        public bool BodyLocked(Player p)=>BodyDown(p)||p.bodyMode==BodyMode.GettingUp;
        public Vector3 BodyGrabPoint(Player p)
        {
            if(p.grabbing==""||!Players.TryGetValue(p.grabbing,out var other))return Vector3.zero;
            if(BodyWorld!=null){var b=BodyWorld.Get(other).Bodies[Mathf.Clamp(p.bodyGrabBone,0,14)];return b.position+b.rotation*p.bodyGrabAnchor;}
            return GrabAnchor(p);
        }
        void DropBodyOnce(Player p)
        {if(p.droppedThisFall)return;p.droppedThisFall=true;Drop(p,1);}
        void BeginBodyFall(Player p,Vector3 kick,Vector3 contact,bool high=false)
        {
            if(p.bodyMode==BodyMode.GettingUp||Time<p.bodyProtectedUntil)return;
            if(BodyDown(p))return;
            Interrupt(p);ReleaseAllGrabs(p);CancelAction(p);p.bodyMode=BodyMode.Falling;p.bodyStateAt=Time;p.knockdownId++;p.droppedThisFall=false;p.highFall=high;
            p.staggerUntil=Time+1.2f;p.running=false;p.runBlend=0;p.impactId++;p.impactImpulse=kick;p.impactPoint=contact;
            SetAction(p,"fall");
            if(BodyWorld!=null)BodyWorld.Impact(p,kick,contact,true);
            else{p.impulse=kick;p.verticalSpeed=kick.y;}
        }
        public void BodyLanded(Player p,float speed)
        {
            if(p.bodyMode!=BodyMode.Falling)return;
            p.bodyMode=BodyMode.Grounded;p.bodyStateAt=Time;p.staggerUntil=Time+1.6f;
            if(p.highFall){DropBodyOnce(p);p.stability=0;p.lastHit=Time;}
            CombatEvents.Add(new CombatEvent{id=++combatEventId,tick=serverTick,time=Time,kind="landing",sourceId=p.profile.playerId,targetId=p.profile.playerId,point=new Vector3(p.position.x,SurfaceFloor(p.position+Vector3.up)+.025f,p.position.z),impulse=Vector3.down*speed,severity=2});
        }
        public bool TryBodyGetUp(Player p,Vector3 chestForward)
        {
            if(p.bodyMode!=BodyMode.Grounded)return false;
            Vector3 basePoint=p.position;bool found=false;
            for(int ring=0;ring<=3&&!found;ring++)for(int direction=0;direction<(ring==0?1:8);direction++)
            {
                float angle=direction*Mathf.PI*.25f;var candidate=basePoint+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*.4f);
                float floor=SurfaceFloor(candidate+Vector3.up*1.5f,1);if(floor< -20||floor>basePoint.y+.35f)continue;
                candidate.y=floor+1;
                if(MoveOnMachines(basePoint,candidate,.85f)!=candidate||CampusGeometry.Blocked(candidate,.85f)||!CampusGeometry.ClearSegment(basePoint+Vector3.up*.15f,candidate+Vector3.up*.15f))continue;
                // The raised head needs room, not just the final motor capsule.
                bool roof=CampusGeometry.Solids.Any(s=>{var d=candidate+Vector3.up*.85f-s.center;var h=s.size*.5f;return Mathf.Abs(d.x)<h.x+.73f&&Mathf.Abs(d.z)<h.z+.73f&&Mathf.Abs(d.y)<h.y+.73f;});
                if(roof)continue;p.getUpPosition=candidate;found=true;break;
            }
            if(!found)return false;
            ReleaseAllGrabs(p);p.getUpFaceUp=chestForward.y>=0;p.bodyMode=BodyMode.GettingUp;p.bodyStateAt=Time;p.staggerUntil=Time+.6f;p.bodyProtectedUntil=Time+1.1f;SetAction(p,"getup");return true;
        }
        public void CompleteBodyGetUp(Player p)
        {
            p.motorHeight=p.getUpPosition.y;p.bodyMode=BodyMode.Standing;p.bodyStateAt=Time;p.bodyProtectedUntil=Time+.5f;p.staggerUntil=Time;p.highFall=false;p.droppedThisFall=false;p.impulse=Vector3.zero;p.verticalSpeed=0;
            if(p.restoreStabilityOnGetUp){p.stability=Rules.maxStability*.65f;p.restoreStabilityOnGetUp=false;}
            SetAction(p,"idle");
        }
        void BodyTickState(Player p)
        {
            if(p.bodyMode==BodyMode.Stagger&&Time>=p.staggerUntil){p.bodyMode=BodyMode.Standing;p.bodyStateAt=Time;}
            if(BodyLocked(p))p.staggerUntil=Mathf.Max(p.staggerUntil,Time+.05f);
            if(BodyWorld==null)
            {
                if(p.bodyMode==BodyMode.Falling&&p.position.y<=SurfaceFloor(p.position)+1.03f&&p.verticalSpeed<=0)BodyLanded(p,0);
                if(p.bodyMode==BodyMode.Grounded&&Time-p.bodyStateAt>=1)TryBodyGetUp(p,Vector3.up);
                if(p.bodyMode==BodyMode.GettingUp&&Time-p.bodyStateAt>=.6f)CompleteBodyGetUp(p);
            }
        }
        void BodyRespawn(Player p,int index)
        {
            Interrupt(p);ReleaseAllGrabs(p);CancelAction(p);DropBodyOnce(p);p.stability=0;p.lastHit=Time;
            p.position=Spawn(index);p.motorHeight=p.position.y;p.impulse=Vector3.zero;p.verticalSpeed=0;p.airborne=false;p.fallOriginHeight=0;p.bodyMode=BodyMode.Standing;p.bodyProtectedUntil=Time+.5f;p.staggerUntil=Time+.3f;p.highFall=false;p.restoreStabilityOnGetUp=false;
            BodyWorld?.Reset(p);p.droppedThisFall=false;SetAction(p,"idle");
        }
    }
}
