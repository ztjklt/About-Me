using UnityEngine;
namespace AboutMe
{
    // Server world-space bones, interpolated together. No client physics or animation writes after this pass.
    public sealed class DollPhysicsPresentation
    {
        readonly Vector3[] previous=new Vector3[15],next=new Vector3[15];
        readonly Quaternion[] previousRot=new Quaternion[15],nextRot=new Quaternion[15];
        readonly Vector3[] displayed=new Vector3[15];readonly Quaternion[] displayedRot=new Quaternion[15];
        int tick=-1;float arrived,duration=.05f;bool initialized;
        public void Apply(ToyActor actor,PublicPlayer player,float now,bool immediate)
        {
            var pose=player.bodyPose;if(pose==null||pose.positions==null||pose.positions.Length!=15)return;
            var bones=actor.PhysicalBones;
            if(initialized&&now<actor.FreezeUntil){for(int i=0;i<15;i++)bones[i].SetPositionAndRotation(displayed[i],displayedRot[i]);return;}
            if(pose.tick!=tick)
            {
                float blend=immediate?1:Mathf.Clamp01((now-arrived)/duration);
                bool snap=!initialized||(pose.positions[0]+player.position-next[0]).sqrMagnitude>64;
                for(int i=0;i<15;i++)
                {
                    previous[i]=snap?pose.positions[i]+player.position:Vector3.Lerp(previous[i],next[i],blend);
                    previousRot[i]=snap?pose.rotations[i]:Quaternion.Slerp(previousRot[i],nextRot[i],blend);
                    next[i]=pose.positions[i]+player.position;nextRot[i]=pose.rotations[i];
                }
                duration=initialized?Mathf.Clamp((pose.tick-tick)*.02f,.02f,.12f):.05f;tick=pose.tick;arrived=now;initialized=true;
            }
            float t=immediate?1:Mathf.Clamp01((now-arrived)/duration);
            actor.root.transform.localScale=Vector3.one;
            for(int i=0;i<15;i++){displayed[i]=Vector3.Lerp(previous[i],next[i],t);displayedRot[i]=Quaternion.Slerp(previousRot[i],nextRot[i],t);bones[i].SetPositionAndRotation(displayed[i],displayedRot[i]);}
        }
    }
}
