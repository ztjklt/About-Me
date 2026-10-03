using UnityEngine;
namespace AboutMe
{
    public sealed partial class PartyMechanicVisual
    {
        LineRenderer[] wetRings;ParticleSystem fieldParticles;
        LineRenderer FieldLine(Transform parent,string name)
        {var obj=new GameObject(name);obj.transform.SetParent(parent,false);var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=warningMaterial;line.startWidth=line.endWidth=.08f;return line;}
        void PresentField(EffectState e,PartyMechanic d,float now,Camera camera)
        {
            bool cone=d.id=="flame-fan"||d.id=="clean-rain"||d.id=="fan-volume";
            var color=d.id=="flame-fan"?new Color(1,.35f,.12f):d.id=="sand-sink"?new Color(.82f,.60f,.22f):d.id=="sticky-step"?new Color(.68f,.38f,.9f):d.id=="tractor-loose"?new Color(.25f,.55f,.85f):new Color(.14f,.7f,.85f);
            color.a=.85f;ring.startColor=ring.endColor=color;
            float y=CampusGeometry.Floor(e.position,.1f)+.05f-e.position.y;
            ring.loop=!cone;ring.positionCount=cone?5:64;
            if(cone)
            {
                var direction=e.velocity;var left=Quaternion.Euler(0,-35,0)*direction*d.radius;var right=Quaternion.Euler(0,35,0)*direction*d.radius;
                ring.SetPosition(0,new Vector3(0,y,0));ring.SetPosition(1,new Vector3(left.x,y,left.z));ring.SetPosition(2,new Vector3(direction.x*d.radius,y,direction.z*d.radius));ring.SetPosition(3,new Vector3(right.x,y,right.z));ring.SetPosition(4,new Vector3(0,y,0));
            }
            else for(int i=0;i<64;i++){float angle=i*Mathf.PI*2/64;float r=d.id=="item-push-wave"?Mathf.Max(.05f,e.radius):d.radius;ring.SetPosition(i,new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r));}
            ring.gameObject.SetActive(d.id!="wet-trail");
            model.SetActive(d.id!="item-push-wave"&&d.id!="wet-trail");model.transform.localScale=Vector3.one;model.transform.localRotation=Quaternion.LookRotation(e.velocity.sqrMagnitude>.01f?e.velocity:Vector3.forward);
            if(d.id=="loose-whirlpool")model.transform.localRotation*=Quaternion.Euler(0,now*90,0);
            if(d.id=="wet-trail")
            {
                if(wetRings==null){wetRings=new LineRenderer[6];for(int i=0;i<6;i++){wetRings[i]=FieldLine(transform,"Public wet footprint");wetRings[i].useWorldSpace=true;wetRings[i].loop=true;wetRings[i].positionCount=32;}}
                for(int i=0;i<6;i++)
                {
                    bool shown=e.path!=null&&i<e.path.Length;wetRings[i].gameObject.SetActive(shown);if(!shown)continue;wetRings[i].startColor=wetRings[i].endColor=color;
                    for(int j=0;j<32;j++){float a=j*Mathf.PI*2/32;wetRings[i].SetPosition(j,e.path[i]+new Vector3(Mathf.Cos(a)*1.2f,-.09f,Mathf.Sin(a)*1.2f));}
                }
            }
            if(cone)
            {
                if(!fieldParticles)
                {
                    var obj=new GameObject("Bounded field particles");obj.transform.SetParent(transform,false);fieldParticles=obj.AddComponent<ParticleSystem>();fieldParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=fieldParticles.main;main.loop=true;main.maxParticles=48;main.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.6f);main.startSpeed=d.id=="flame-fan"?7:5;main.startSize=new ParticleSystem.MinMaxCurve(d.id=="flame-fan"?.28f:.07f,d.id=="flame-fan"?.65f:.12f);main.startColor=Color.white;main.simulationSpace=ParticleSystemSimulationSpace.World;
                    var emission=fieldParticles.emission;emission.rateOverTime=d.id=="fan-volume"?12:d.id=="flame-fan"?60:30;
                    var shape=fieldParticles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=25;shape.radius=.08f;fieldParticles.transform.localPosition=Vector3.up*(d.id=="fan-volume"?.6f:1.1f);fieldParticles.transform.rotation=Quaternion.LookRotation(e.velocity);
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(d.id=="flame-fan"?new Color(1,.9f,.25f):color,0),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});var fade=fieldParticles.colorOverLifetime;fade.enabled=true;fade.color=gradient;
                    fieldParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial=TraitToyEffects.SpriteMaterial(false);fieldParticles.Play();if(d.id=="flame-fan")fieldParticles.Emit(16);
                }
                fieldParticles.transform.localPosition=Vector3.up*(d.id=="fan-volume"?.6f:1.1f);fieldParticles.transform.rotation=Quaternion.LookRotation(e.velocity);
            }
            label.gameObject.SetActive(true);label.transform.localPosition=Vector3.up*(SelfFieldLabel(d.id)?1.5f:.95f)+(camera?-camera.transform.right*2.4f:Vector3.zero);if(camera)label.transform.rotation=camera.transform.rotation;
            string hint=d.id=="wet-trail"?"湿迹 · 跳起脱离":d.id=="sand-sink"?"浅沙 · 跳离恢复":d.id=="sticky-step"?"触胶短停 · 跳 / 喷雨":d.id=="fan-volume"?"风筒 · 侧绕 / 可拆":d.id=="loose-whirlpool"?"只旋转地面道具":d.id=="tractor-loose"?"磁吸地面物 · 抓起可脱离":d.id=="polarity-push"?(e.stage=="repel_warning"?"排斥预告 · 抓走可取消":"公共物已排斥"):d.id=="item-push-wave"?"推物低波 · 人物安全":d.id=="clean-rain"?"喷雨 · 清胶和贴纸":"喷火 · 侧绕 / 墙体挡火";
            label.text=hint+"\n"+Mathf.Max(0,e.deadline-now).ToString("0.0")+"s";phase=e.stage;
        }
        static bool SelfFieldLabel(string id)=>id=="wet-trail"||id=="flame-fan"||id=="clean-rain";
    }
}
