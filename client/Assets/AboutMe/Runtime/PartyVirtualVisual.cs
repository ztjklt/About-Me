using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public sealed partial class PartyMechanicVisual
    {
        GameObject[] virtualParts;
        void VirtualParts(int count,string color)
        {if(virtualParts!=null)return;virtualParts=new GameObject[count];for(int i=0;i<count;i++)virtualParts[i]=ToyFactory.Shape("Virtual rule indicator",PrimitiveType.Cube,transform,Vector3.zero,Vector3.one,color);}
        void PresentVirtual(EffectState e,PartyMechanic d,float now,Camera camera,Snapshot state)
        {
            var yaw=Quaternion.LookRotation(e.velocity);float y=CampusGeometry.Floor(e.position,.1f)+.06f-e.position.y;
            var color=new Color(.45f,.3f,1,.9f);ring.startColor=ring.endColor=color;ring.positionCount=64;ring.loop=true;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*e.radius,y,Mathf.Sin(a)*e.radius));}
            model.SetActive(true);model.transform.localPosition=Vector3.zero;model.transform.localScale=Vector3.one;
            if(rope)rope.gameObject.SetActive(false);
            if(d.id=="shot-portal")
            {ring.gameObject.SetActive(false);model.transform.localScale=Vector3.one*.55f;var points=Enumerable.Range(0,33).Select(i=>e.position+yaw*new Vector3(Mathf.Cos(i*Mathf.PI*2/32)*.8f,Mathf.Sin(i*Mathf.PI*2/32)*.8f+.85f,0)).ToArray();ControlRope(points,e.stage=="portal_wait"?Color.yellow:color);VirtualParts(1,"#8C61FF");virtualParts[0].transform.localPosition=e.velocity*.7f+Vector3.up*.85f;virtualParts[0].transform.rotation=yaw;virtualParts[0].transform.localScale=new Vector3(.12f,.12f,.6f);}
            if(d.id=="pixel-stamp")
            {ring.gameObject.SetActive(false);model.transform.localPosition=Vector3.up*.22f;model.transform.localScale=Vector3.one*.5f;VirtualParts(9,"#FFD34B");int beat=Mathf.FloorToInt(now-e.createdAt);for(int i=0;i<9;i++){bool bright=(i/3+i%3+beat)%2==0;virtualParts[i].transform.localPosition=yaw*new Vector3(i/3-1,y+(bright?.10f:0),i%3-1);virtualParts[i].transform.rotation=yaw;virtualParts[i].transform.localScale=new Vector3(.92f,bright?.16f:.025f,.92f);virtualParts[i].GetComponent<Renderer>().material.color=bright?new Color(1,.78f,.15f):new Color(.25f,.2f,.4f);}}
            if(d.id=="vector-arrow"||d.id=="virtual-gravity")
            {VirtualParts(3,"#AD82FF");for(int i=0;i<3;i++){virtualParts[i].transform.localPosition=e.velocity*(i-1)*.7f+Vector3.up*(y+.12f);virtualParts[i].transform.rotation=yaw;virtualParts[i].transform.localScale=new Vector3(.15f,.08f,.45f);}}
            if(d.id=="quantum-echo")
            {ring.gameObject.SetActive(false);if(e.path!=null&&e.path.Length>1)ControlRope(e.path.Select(p=>p+Vector3.down*.7f).ToArray(),e.stage=="record"?Color.yellow:color);model.transform.localPosition=Vector3.up*.3f;model.transform.localScale=Vector3.one*.7f;}
            if(d.id=="light-lattice")
            {ring.gameObject.SetActive(false);model.SetActive(false);VirtualParts(10,"#A894F2");for(int i=0;i<10;i++){bool vertical=i<5;virtualParts[i].transform.localPosition=yaw*new Vector3(vertical?(i-2)*e.radius/2:0,vertical?.75f:(i-5)*.4f,0);virtualParts[i].transform.rotation=yaw;virtualParts[i].transform.localScale=vertical?new Vector3(.035f,2,.035f):new Vector3(e.radius*2,.035f,.035f);}}
            if(d.id=="solid-shadow")
            {ring.gameObject.SetActive(false);model.transform.localPosition=Vector3.up*.5f;model.transform.localScale=Vector3.one*.45f;VirtualParts(1,"#8C6AC4");virtualParts[0].transform.localPosition=new Vector3(0,.27f,0);virtualParts[0].transform.rotation=yaw;virtualParts[0].transform.localScale=new Vector3(1.6f,.16f,1.6f);virtualParts[0].GetComponent<Renderer>().material.color=e.stage=="shadow_fading"&&Mathf.Repeat(now*8,1)<.5f?Color.yellow:color;}
            if(d.id=="weather-vane")
            {model.transform.Rotate(Vector3.forward,e.force*360*Time.deltaTime);var table=state.effects.FirstOrDefault(x=>x.id==e.targetId);if(table!=null)ControlRope(new[]{e.position+Vector3.up*.4f,table.position+Vector3.up*.4f},e.force>.01f?Color.yellow:color);}
            if(d.id=="heartbeat-tempo")
            {bool open=Mathf.Repeat(now-e.createdAt,1.4f)<=.25f;ring.startWidth=ring.endWidth=open?.19f:.06f;ring.startColor=ring.endColor=open?Color.green:color;model.transform.localScale=Vector3.one*(open?1.15f:.8f);}
            if(d.id=="frost-lock")
            {ring.gameObject.SetActive(false);model.transform.localScale=Vector3.one*.5f;VirtualParts(6,"#A4E2FA");for(int i=0;i<6;i++){bool active=e.path!=null&&i<e.path.Length;virtualParts[i].SetActive(active);if(active){virtualParts[i].transform.position=e.path[i]+Vector3.up*.03f;virtualParts[i].transform.localScale=new Vector3(1.7f,.025f,1.7f);}}}
            label.transform.localPosition=Vector3.up*(d.id=="light-lattice"?2.1f:1.5f);if(camera)label.transform.rotation=camera.transform.rotation;
            string help=d.id=="shot-portal"?(e.stage=="portal_wait"?"弹体入口 · 移位 F 放出口":"仅普通弹体传送 · 两击拆除"):d.id=="pixel-stamp"?"凸起亮格弹跳 · 暗格安全":d.id=="vector-arrow"?"弹体转向箭头 · 每颗仅转一次":d.id=="quantum-echo"?(e.stage=="record"?"录制走路 · Q 停止":"路径回响只推公共箱桶"):d.id=="light-lattice"?"箱桶拦网 · 人与 Persona 可穿":d.id=="virtual-gravity"?"方向力只推公共箱桶":d.id=="solid-shadow"?(e.stage=="shadow_fading"?"影板将消失 · 请跳离":"仅人物可站的影板 · 两击拆"):d.id=="weather-vane"?(e.force>.01f?"风正在驱动转台":"等公共风 · 手摇可接管"):d.id=="heartbeat-tempo"?(Mathf.Repeat(now-e.createdAt,1.4f)<=.25f?"绿拍可按公共按钮 E":"等绿拍 · 圈外按钮不受影响"):"湿迹已结冰 · 跳离 / 海绵擦除";
            label.text=help+"\n"+Mathf.Max(0,e.deadline-now).ToString("0.0")+"s";phase=e.stage;
        }
    }
}
