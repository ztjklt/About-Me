using UnityEngine;
using UnityEngine.Rendering;
namespace AboutMe
{
    public sealed partial class PartyMechanicVisual
    {
        static Mesh friendlyBubbleMesh;
        GameObject friendlyBubble,smileFace;
        void PresentFriendlyBubble(EffectState e,PublicPlayer target,Camera camera)
        {
            if(!friendlyBubble)
            {
                friendlyBubble=new GameObject("Friendly pink bubble");friendlyBubble.transform.SetParent(transform,false);
                var shell=ToyFactory.Shape("Translucent bubble shell",PrimitiveType.Sphere,friendlyBubble.transform,Vector3.zero,Vector3.one,"#FFAEC8");
                if(!friendlyBubbleMesh){friendlyBubbleMesh=Instantiate(shell.GetComponent<MeshFilter>().sharedMesh);friendlyBubbleMesh.name="Cached transparent pink bubble";var colors=new Color[friendlyBubbleMesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=new Color(1,.7f,.85f,.10f);friendlyBubbleMesh.colors=colors;}
                shell.GetComponent<MeshFilter>().sharedMesh=friendlyBubbleMesh;shell.GetComponent<Renderer>().sharedMaterial=warningMaterial;
                for(int axis=0;axis<3;axis++){var ringObj=new GameObject("Pink bubble outline");ringObj.transform.SetParent(friendlyBubble.transform,false);var outline=ringObj.AddComponent<LineRenderer>();outline.useWorldSpace=false;outline.loop=true;outline.positionCount=48;outline.startWidth=outline.endWidth=.012f;outline.sharedMaterial=warningMaterial;outline.startColor=outline.endColor=new Color(1,.64f,.81f,.55f);for(int i=0;i<48;i++){float angle=i*Mathf.PI*2/48;var pt=new Vector3(Mathf.Cos(angle)*.5f,Mathf.Sin(angle)*.5f,0);outline.SetPosition(i,axis==0?pt:axis==1?new Vector3(pt.x,0,pt.y):new Vector3(0,pt.x,pt.y));}}
                smileFace=new GameObject("Friendly smile face");smileFace.transform.SetParent(friendlyBubble.transform,false);
                ToyFactory.Shape("Smile left eye",PrimitiveType.Sphere,smileFace.transform,new Vector3(-.095f,.08f,0),Vector3.one*.026f,"#5B3D65");ToyFactory.Shape("Smile right eye",PrimitiveType.Sphere,smileFace.transform,new Vector3(.095f,.08f,0),Vector3.one*.026f,"#5B3D65");
                var smile=new GameObject("Curved friendly smile");smile.transform.SetParent(smileFace.transform,false);var line=smile.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=17;line.startWidth=line.endWidth=.012f;line.sharedMaterial=warningMaterial;line.startColor=line.endColor=new Color(.36f,.24f,.4f);for(int i=0;i<17;i++){float angle=i*Mathf.PI/16;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*.13f,-.015f-Mathf.Sin(angle)*.07f,0));}
            }
            var center=(target?.position??e.position)+Vector3.up*.25f;friendlyBubble.transform.position=center;friendlyBubble.transform.localScale=Vector3.one*(target!=null?1.7f:.6f);
            if(camera){var facing=(camera.transform.position-center).normalized;smileFace.transform.rotation=Quaternion.LookRotation(facing);smileFace.transform.position=center+facing*(target!=null?.85f:.3f);}
        }
    }
}
