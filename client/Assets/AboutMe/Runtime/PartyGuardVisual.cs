using UnityEngine;
namespace AboutMe
{
    public sealed partial class PartyMechanicVisual
    {
        LineRenderer[] domeRings;
        void PresentGuard(EffectState e,PartyMechanic d,float now,Camera camera)
        {
            bool self=d.id=="front-shield"||d.id=="timed-reflect"||d.id=="ground-anchor"||d.id=="impact-bank"||d.id=="ribbon-cover"||d.id=="wind-deflector";
            bool plane=d.id=="front-shield"||d.id=="timed-reflect"||d.id=="ribbon-cover"||d.id=="recover-screen"||d.id=="rhythm-gate";
            bool opened=d.id=="rhythm-gate"&&Mathf.Repeat(now-e.createdAt,1)<.35f;
            var normal=d.id=="ribbon-cover"?-e.velocity:e.velocity;var side=Vector3.Cross(Vector3.up,normal);
            var color=opened?new Color(.15f,.8f,.5f):d.id=="rhythm-gate"?new Color(1,.53f,.15f):d.id=="timed-reflect"?new Color(.72f,.45f,1):d.id=="ribbon-cover"?new Color(.95f,.4f,.65f):new Color(.1f,.7f,.85f);ring.startColor=ring.endColor=color;
            float floor=CampusGeometry.Floor(e.position,.1f)+.06f-e.position.y;
            ring.loop=false;
            if(plane)
            {
                var center=self?normal*1.6f:Vector3.up*.85f;float width=d.radius;
                var points=new[]{center-side*width-Vector3.up,center-side*width+Vector3.up,center+side*width+Vector3.up,center+side*width-Vector3.up,center-side*width-Vector3.up};
                ring.positionCount=opened?3:5;for(int i=0;i<ring.positionCount;i++)ring.SetPosition(i,points[i]);
            }
            else if(d.id=="wind-deflector")
            {
                ring.positionCount=4;var left=Quaternion.Euler(0,-56,0)*normal*d.radius;var right=Quaternion.Euler(0,56,0)*normal*d.radius;
                ring.SetPosition(0,new Vector3(0,floor,0));ring.SetPosition(1,new Vector3(left.x,floor,left.z));ring.SetPosition(2,new Vector3(right.x,floor,right.z));ring.SetPosition(3,new Vector3(0,floor,0));
            }
            else
            {ring.loop=true;ring.positionCount=64;float groundRadius=d.id=="calm-dome"?Mathf.Sqrt(Mathf.Max(0,d.radius*d.radius-(floor-.85f)*(floor-.85f))):d.radius;for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*groundRadius,floor,Mathf.Sin(a)*groundRadius));}}
            if(d.id=="calm-dome")
            {
                if(domeRings==null){domeRings=new LineRenderer[2];for(int j=0;j<2;j++){domeRings[j]=FieldLine(transform,"Public dome arc");domeRings[j].useWorldSpace=false;domeRings[j].positionCount=33;}}
                for(int j=0;j<2;j++){domeRings[j].startColor=domeRings[j].endColor=color;for(int i=0;i<33;i++){float low=Mathf.Asin(Mathf.Clamp((floor-.85f)/d.radius,-1,1));float a=low+i*(Mathf.PI-2*low)/32;domeRings[j].SetPosition(i,j==0?new Vector3(Mathf.Cos(a)*d.radius,Mathf.Sin(a)*d.radius+.85f,0):new Vector3(0,Mathf.Sin(a)*d.radius+.85f,Mathf.Cos(a)*d.radius));}}
            }
            model.SetActive(true);model.transform.localScale=Vector3.one*(plane?2:1);model.transform.localPosition=plane?(self?normal*1.6f:Vector3.up*.85f):self?Vector3.up*(-.7f):Vector3.up*.25f;model.transform.localRotation=Quaternion.LookRotation(normal);
            if(d.id=="front-shield"||d.id=="recover-screen")model.transform.localRotation*=Quaternion.Euler(90,0,0);
            if(d.id=="impact-bank"&&e.stage=="banked")model.transform.Rotate(Vector3.up,now*120);
            if(d.id=="rhythm-gate")model.transform.localRotation*=Quaternion.Euler(0,0,Mathf.Sin(now*Mathf.PI*2)*25);
            label.gameObject.SetActive(true);label.transform.localPosition=Vector3.up*(self?1.5f:1.45f)+(camera?-camera.transform.right*2.4f:Vector3.zero);if(camera)label.transform.rotation=camera.transform.rotation;
            string hint=d.id=="front-shield"?"正面盾 "+e.health+"/2 · 侧绕 / 抓取":d.id=="timed-reflect"?(e.stage=="parried"?"已反射 · 原主不变":"反射窗口 · 近战有效"):d.id=="ground-anchor"?"蹲锚 · 近战 / 竖直有效":d.id=="impact-bank"?(e.stage=="banked"?"已蓄冲 "+e.force.ToString("0.0")+" · F 返还":"蓄冲 · 削韧仍结算"):d.id=="ribbon-cover"?"背盖 · 正面可击":d.id=="recover-screen"?"挡风屏 · 人物可穿":d.id=="calm-dome"?"消力罩 · 削韧不减":d.id=="wind-deflector"?"偏弹风 · 不推人物":d.id=="rhythm-gate"?(opened?"开门 · 弹体可过":"闭门 · 人物可过"):"吸湿 "+e.chainIndex+"/2 · 可拆";
            label.text=hint+"\n"+Mathf.Max(0,e.deadline-now).ToString("0.0")+"s";phase=e.stage;
        }
    }
}
