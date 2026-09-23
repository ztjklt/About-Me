using System;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    // Exported from the same metre-scale structure as the render mesh. Unity X/Y/Z.
    [Serializable] public class CampusSolid { public string name; public Vector3 center, size; }
    [Serializable] public class CampusCollisionData { public CampusSolid[] solids; }
    public static class CampusGeometry
    {
        static CampusSolid[] solids;
        public static CampusSolid[] Solids
        {
            get
            {
                if(solids==null)
                {
                    var source=Resources.Load<TextAsset>("CampusV04/Collision");
                    if(!source)throw new InvalidOperationException("Campus V0.4 collision data is missing. Run CampusImport.Build.");
                    var clubThresholds=(from side in new[]{-1,1} from z in new[]{-14f,10f}
                        select new CampusSolid
                        {
                            name="club_wing_threshold",
                            center=new Vector3(side*30.375f,4.25f,z),
                            size=new Vector3(1f,.5f,8f)
                        }).ToArray();
                    solids=JsonUtility.FromJson<CampusCollisionData>(source.text).solids
                        .Concat(clubThresholds).Concat(CampusFacadeSeals.Solids).ToArray();
                }
                return solids;
            }
        }
        public static float Floor(Vector3 p,float footOffset=1)
        {
            float top=-100,ceiling=p.y-footOffset+.45f;
            foreach(var s in Solids)
            {
                var lo=s.center-s.size*.5f;var hi=s.center+s.size*.5f;
                if(p.x>=lo.x&&p.x<=hi.x&&p.z>=lo.z&&p.z<=hi.z&&hi.y<=ceiling&&hi.y>top)top=hi.y;
            }
            return top;
        }
        public static bool Blocked(Vector3 p,float radius=.4f,float footOffset=1)
        {
            float foot=p.y-footOffset;
            foreach(var s in Solids)
            {
                var lo=s.center-s.size*.5f;var hi=s.center+s.size*.5f;
                if(hi.y<=foot+.3f||lo.y>=foot+1.85f)continue;
                float inset=s.name.StartsWith("stair_")?0:radius;
                if(p.x>lo.x-inset&&p.x<hi.x+inset&&p.z>lo.z-inset&&p.z<hi.z+inset)return true;
            }
            return false;
        }
        public static Vector3 CameraPosition(Vector3 target,Vector3 desired,float padding=.20f)
        {
            var ray=desired-target;float nearest=1;
            foreach(var solid in Solids)
            {
                var low=solid.center-solid.size*.5f-Vector3.one*padding;var high=solid.center+solid.size*.5f+Vector3.one*padding;
                float near=0,far=1;
                for(int axis=0;axis<3;axis++)
                {
                    if(Mathf.Abs(ray[axis])<.00001f){if(target[axis]<low[axis]||target[axis]>high[axis]){near=2;break;}continue;}
                    float a=(low[axis]-target[axis])/ray[axis],b=(high[axis]-target[axis])/ray[axis];
                    near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));if(near>far)break;
                }
                if(near<=far&&near>0&&near<=1)nearest=Mathf.Min(nearest,Mathf.Max(0,near-.025f));
            }
            return target+ray*nearest;
        }
        public static bool ClearSegment(Vector3 from,Vector3 to)
        {
            var delta=to-from;
            foreach(var solid in Solids)
            {
                var low=solid.center-solid.size*.5f;var high=solid.center+solid.size*.5f;
                float near=0,far=1;
                for(int axis=0;axis<3;axis++)
                {
                    if(Mathf.Abs(delta[axis])<.00001f){if(from[axis]<low[axis]||from[axis]>high[axis]){near=2;break;}continue;}
                    float a=(low[axis]-from[axis])/delta[axis],b=(high[axis]-from[axis])/delta[axis];
                    near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));
                    if(near>far)break;
                }
                if(near<=far&&far>=0&&near<=1)return false;
            }
            return true;
        }
        public static bool Outside(Vector3 p)=>Mathf.Abs(p.x)>40||Mathf.Abs(p.z)>32||p.y< -6;
        public static Vector3 SafeDrop(Vector3 p)
        {
            // Only out-of-map items return to the open central fight area.
            if(Outside(p)||Floor(new Vector3(p.x,1,p.z))< -50)
                return new Vector3(Mathf.Clamp(p.x,-10,10),1.4f,Mathf.Clamp(p.z,-10,10));
            p.y=Mathf.Max(p.y,1.3f);return p;
        }
        public static Vector3 Move(Vector3 from,Vector3 to,float radius=.4f,float footOffset=1)
        {
            var step=from;step.x=to.x;if(!Blocked(step,radius,footOffset))from.x=to.x;
            step=from;step.z=to.z;if(!Blocked(step,radius,footOffset))from.z=to.z;
            from.y=to.y;return from;
        }
    }
}
