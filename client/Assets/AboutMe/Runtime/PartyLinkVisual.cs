using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public sealed partial class PartyMechanicVisual
    {
        GameObject[] linkParts;
        void LinkParts(int count,string color){if(linkParts!=null)return;linkParts=new GameObject[count];for(int i=0;i<count;i++)linkParts[i]=ToyFactory.Shape("Public link rule",PrimitiveType.Cube,transform,Vector3.zero,Vector3.one,color);}
        void PresentLinks(EffectState e,PartyMechanic d,float now,Camera camera,Snapshot state)
        {
            var color=e.stage=="link_wait"||e.stage=="consent"?new Color(1,.73f,.18f):new Color(.15f,.65f,.73f);ring.startColor=ring.endColor=color;ring.loop=true;ring.positionCount=64;float y=CampusGeometry.Floor(e.position,.1f)+.05f-e.position.y;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*Mathf.Min(e.radius,1.3f),y,Mathf.Sin(a)*Mathf.Min(e.radius,1.3f)));}
            if(rope)rope.gameObject.SetActive(false);model.SetActive(true);model.transform.localScale=Vector3.one*.7f;model.transform.localPosition=Vector3.zero;
            if((d.id=="zipline-build"||d.id=="rope-bridge")&&e.path!=null&&e.path.Length==2)
            {ring.gameObject.SetActive(false);ControlRope(e.path.Select(p=>p+Vector3.up*(d.id=="zipline-build"?1.8f:.6f)).ToArray(),color);LinkParts(d.id=="rope-bridge"?6:2,"#E1B865");if(d.id=="rope-bridge"){var shapes=Match.MachineShapes(e).ToArray();for(int i=0;i<shapes.Length;i++){linkParts[i].transform.SetPositionAndRotation(shapes[i].center,shapes[i].rotation);linkParts[i].transform.localScale=shapes[i].size;}}else for(int i=0;i<2;i++){linkParts[i].transform.position=e.path[i]+Vector3.up*.8f;linkParts[i].transform.localScale=new Vector3(.12f,1.5f,.12f);}}
            if(d.id=="wall-cling"){ring.gameObject.SetActive(false);ControlRope(new[]{e.position+Vector3.up*.4f,e.position-e.velocity*.5f+Vector3.up*.4f},color);}
            if(d.id=="safety-tether"&&e.path!=null){ring.gameObject.SetActive(false);ControlRope(new[]{e.position,e.path[0]},color);model.transform.position=e.path[0]+Vector3.down*.75f;}
            if(d.id=="item-scale")
            {ring.gameObject.SetActive(false);LinkParts(3,"#8AD3D1");var shapes=Match.MachineShapes(e).ToArray();for(int i=0;i<2;i++){linkParts[i].transform.SetPositionAndRotation(shapes[i].center,shapes[i].rotation);linkParts[i].transform.localScale=shapes[i].size;}linkParts[2].transform.localPosition=Vector3.up*.4f;linkParts[2].transform.rotation=Quaternion.LookRotation(e.velocity);linkParts[2].transform.localScale=new Vector3(3,.06f,.12f);}
            if(d.id=="public-clamp")
            {var prop=state.props.FirstOrDefault(p=>p.id==e.targetId);if(prop!=null){model.transform.position=prop.position+Vector3.up*.6f;ControlRope(new[]{prop.position+Vector3.left*.6f,prop.position+Vector3.left*.6f+Vector3.up*.7f,prop.position+Vector3.right*.6f+Vector3.up*.7f,prop.position+Vector3.right*.6f},color);}}
            if(d.id=="public-twin-rope"&&e.path!=null){ring.gameObject.SetActive(false);ControlRope(e.path,color);}
            if(d.id=="projectile-fork")
            {LinkParts(2,"#FFC862");for(int i=0;i<2;i++){var rotation=Quaternion.LookRotation(e.velocity)*Quaternion.Euler(0,i==0?-25:25,0);linkParts[i].transform.position=e.position+Vector3.up*.85f+(rotation*Vector3.back)*.5f;linkParts[i].transform.rotation=rotation;linkParts[i].transform.localScale=new Vector3(.08f,.08f,1);}}
            if(d.id=="shot-vault"){model.transform.localScale=Vector3.one*(e.stage=="stored"?1:.7f);if(e.stage=="stored"){ring.startColor=ring.endColor=Color.yellow;ring.startWidth=ring.endWidth=.18f;}}
            if(d.id=="consent-swap")
            {ring.gameObject.SetActive(false);LinkParts(2,"#96D8CC");for(int i=0;i<2;i++){linkParts[i].transform.localPosition=Quaternion.LookRotation(e.velocity)*Vector3.right*(i==0?-1.3f:1.3f)+Vector3.up*y;linkParts[i].transform.localScale=new Vector3(1.1f,.06f,1.1f);linkParts[i].GetComponent<Renderer>().material.color=e.stage=="consent"?Color.yellow:color;}}
            label.transform.localPosition=Vector3.up*(d.id=="wall-cling"?1.2f:1.8f);if(camera)label.transform.rotation=camera.transform.rotation;
            string help=d.id=="zipline-build"?(e.stage=="link_wait"?"首锚 · 移动再 F 放终锚":"共用索道 · 端点 E 坐 / Q 松开"):d.id=="rope-bridge"?(e.stage=="link_wait"?"桥首锚 · 移动再 F 放终锚":"双向绳桥 · 会摇晃 / 两击拆锚"):d.id=="wall-cling"?"扒墙1.5s · 沿墙 / 跳离 / Q":d.id=="safety-tether"?"救回原锚 · 坠落仍掉1件生命":d.id=="item-scale"?"仅真实地面 Persona 压盘 · E 取走":d.id=="public-clamp"?"箱桶固定 · 抓起解锁 / 两击拆":d.id=="public-twin-rope"?"双公共物系绳 · 抓一个可拖另一个":d.id=="projectile-fork"?"普通弹体分两路 · 总伤害冲力不变":d.id=="shot-vault"?(e.stage=="stored"?"暂存弹体 · 近处 E 瞄准释放":"弹体暂存框 · 0.65s 自动放行"):(e.stage=="consent"?"双方同意 · 移动 / Q 取消":"两侧站牌 E 同意换位 · 两击拆");
            label.text=help+"\n"+Mathf.Max(0,e.deadline-now).ToString("0.0")+"s";phase=e.stage;
        }
    }
}
