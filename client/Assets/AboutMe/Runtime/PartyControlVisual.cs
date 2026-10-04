using System;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public sealed partial class PartyMechanicVisual
    {
        GameObject[] controlParts;
        void ControlRope(Vector3[] points,Color color,bool loop=false)
        {if(!rope){var go=new GameObject("Public control tether");go.transform.SetParent(transform,false);rope=go.AddComponent<LineRenderer>();rope.sharedMaterial=warningMaterial;rope.startWidth=rope.endWidth=.065f;rope.useWorldSpace=true;}rope.gameObject.SetActive(true);rope.loop=loop;rope.positionCount=points.Length;rope.SetPositions(points);rope.startColor=rope.endColor=color;}
        void PresentControl(EffectState e,PartyMechanic d,float now,Camera camera,Snapshot state)
        {
            var color=d.id=="bubble-lift"?new Color(1,.56f,.75f):e.stage=="attached"?new Color(.9f,.4f,.18f):new Color(.2f,.65f,.9f);color.a=.92f;ring.startColor=ring.endColor=color;
            float y=CampusGeometry.Floor(e.position,.1f)+.06f-e.position.y;ring.positionCount=64;ring.loop=true;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*e.radius,y,Mathf.Sin(a)*e.radius));}
            ring.gameObject.SetActive(e.stage!="flying"&&d.id!="water-jet"&&d.id!="anchor-swing"&&d.id!="body-bank");
            model.SetActive(d.id!="gapped-cage"&&e.stage!="spent");model.transform.localScale=Vector3.one;model.transform.localPosition=Vector3.zero;
            var owner=state.players.FirstOrDefault(p=>p.id==e.owner);var target=state.players.FirstOrDefault(p=>p.id==e.targetId);
            if(rope)rope.gameObject.SetActive(false);
            if(d.id=="lasso-loop"&&target!=null&&owner!=null)ControlRope(new[]{owner.position+Vector3.up*.25f,target.position+Vector3.up*.15f},color);
            if(d.id=="anchor-swing")
            {var anchor=state.props.FirstOrDefault(p=>p.id==e.targetId);if(anchor!=null&&owner!=null)ControlRope(new[]{owner.position+Vector3.up*.2f,anchor.position},color);}
            if(d.id=="break-net"&&e.stage=="attached")
            {var points=new Vector3[17];for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16;points[i]=e.position+new Vector3(Mathf.Cos(a)*e.radius,Mathf.Sin(i*Mathf.PI/2)*.35f+.4f,Mathf.Sin(a)*e.radius);}points[16]=points[0];ControlRope(points,color);}
            if(d.id=="bubble-lift"){model.SetActive(false);ring.gameObject.SetActive(false);PresentFriendlyBubble(e,target,camera);}
            if(d.id=="gapped-cage")
            {
                var boxes=Match.MachineShapes(e).ToArray();if(controlParts==null)controlParts=boxes.Select(x=>ToyFactory.Shape("U cage with open front",PrimitiveType.Cube,transform,Vector3.zero,Vector3.one,"#77BDDB")).ToArray();
                for(int i=0;i<boxes.Length;i++){controlParts[i].transform.position=boxes[i].center;controlParts[i].transform.rotation=boxes[i].rotation;controlParts[i].transform.localScale=boxes[i].size;}
                ring.gameObject.SetActive(false);var yaw=Quaternion.LookRotation(e.velocity);ControlRope(new[]{e.position+yaw*new Vector3(-1.4f,.06f,1.5f),e.position+yaw*new Vector3(0,.06f,2.2f),e.position+yaw*new Vector3(1.4f,.06f,1.5f)},new Color(.1f,.8f,.35f));
            }
            if(d.id=="water-jet")
            {
                if(!burst)
                {var go=new GameObject("Bounded directional water jet");go.transform.SetParent(transform,false);burst=go.AddComponent<ParticleSystem>();burst.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=burst.main;main.loop=true;main.playOnAwake=false;main.maxParticles=48;main.startLifetime=.45f;main.startSpeed=8;main.startSize=.10f;main.startColor=new Color(.4f,.8f,1,.85f);main.simulationSpace=ParticleSystemSimulationSpace.World;var emit=burst.emission;emit.rateOverTime=65;var shape=burst.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=10;shape.radius=.08f;burst.GetComponent<ParticleSystemRenderer>().sharedMaterial=TraitToyEffects.SpriteMaterial(false);burst.Play();}
                burst.transform.localPosition=Vector3.up*.15f+e.velocity*.6f;burst.transform.rotation=Quaternion.LookRotation(e.velocity);
            }
            if(d.id=="skate-arc")model.transform.localPosition=Vector3.down*.8f;
            label.transform.localPosition=Vector3.up*(d.id=="gapped-cage"?2.0f:1.3f);if(camera)label.transform.rotation=camera.transform.rotation;
            string help=d.id=="lasso-loop"?"双方拉扯 · 按住 Q 解套 / 绕墙断绳":d.id=="break-net"?(e.stage=="attached"?"网内仍可移动 · 按住 Q / 两击拆网":"投网飞行"):d.id=="bubble-lift"?"缓升 0.7m · 可漂移 / Q 戳破":d.id=="water-jet"?"持续水柱 · 侧绕 / Q 停止":d.id=="gapped-cage"?"前方绿色出口 · 可跳出 / 两击拆":d.id=="team-sling"?(e.stage=="ready"?"第二人站架按 E 同意 · 放置者留位":e.stage=="launch_warning"?"准备抛举 · 离开 / Q 取消":"支架已用完"):d.id=="anchor-swing"?"绕公共锚点摆荡 · Q 松绳":d.id=="body-bank"?"一次碰撞反弹 · Q 结束":d.id=="momentum-pass"?"滚动物传递动量 · 仅公共箱桶":"惯性滑行 · 弧线转向 / Q 刹车";
            label.text=help+"\n"+Mathf.Max(0,e.deadline-now).ToString("0.0")+"s";phase=e.stage;
        }
    }
}
