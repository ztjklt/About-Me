using System.Collections.Generic;
using UnityEngine;

namespace AboutMe
{
    public static class DollMouth
    {
        static Mesh rimMesh;
        static readonly Dictionary<string,Mesh> curves=new Dictionary<string,Mesh>();
        public static void SetOpening(Transform root,float opening,bool hideIdle=false)
        {
            bool eating=opening>=.08f;
            root.gameObject.SetActive(!hideIdle||eating);
            var idle=root.Find("Idle smile");if(idle)idle.gameObject.SetActive(!eating&&!hideIdle);
            var rim=root.Find("Skin lip rim");if(rim)rim.gameObject.SetActive(eating);
            for(int i=0;i<root.childCount;i++){var c=root.GetChild(i);if(c.name=="Mouth cavity"||c.name=="Tongue"||c.name=="Upper teeth")c.gameObject.SetActive(eating);}
        }
        public static GameObject Build(Transform head,string skin,float smile=0)
        {
            var root=new GameObject("Expressive mouth");root.transform.SetParent(head,false);root.transform.localPosition=new Vector3(0,-.235f,.46f);root.transform.localScale=new Vector3(.19f,.065f,.10f);
            if(!rimMesh)
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();const int segments=32;
                for(int ring=0;ring<4;ring++)for(int i=0;i<segments;i++)
                {
                    float angle=i*Mathf.PI*2/segments,radius=ring==0||ring==3?.5f:.415f;
                    vertices.Add(new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,ring<2?.2f:-.1f));
                    int a=ring*segments+i,b=ring*segments+(i+1)%segments,c=((ring+1)%4)*segments+i,d=((ring+1)%4)*segments+(i+1)%segments;
                    triangles.AddRange(new[]{a,b,c,b,d,c});
                }
                rimMesh=new Mesh{name="Open mouth lip ring"};rimMesh.SetVertices(vertices);rimMesh.SetTriangles(triangles,0);rimMesh.RecalculateNormals();rimMesh.RecalculateBounds();
            }
            var rim=new GameObject("Skin lip rim",typeof(MeshFilter),typeof(MeshRenderer));rim.transform.SetParent(root.transform,false);rim.GetComponent<MeshFilter>().sharedMesh=rimMesh;rim.GetComponent<MeshRenderer>().sharedMaterial=DollFactory.Material(skin);
            DollFactory.Part("Mouth cavity",root.transform,new Vector3(0,0,-.08f),new Vector3(.91f,.91f,.35f),"#332438");
            DollFactory.Part("Tongue",root.transform,new Vector3(0,-.26f,.115f),new Vector3(.48f,.2f,.07f),"#F3AFC2");
            DollFactory.Part("Upper teeth",root.transform,new Vector3(0,.325f,.14f),new Vector3(.47f,.12f,.075f),"#FFF9EF");
            var idle=new GameObject("Idle smile",typeof(MeshFilter),typeof(MeshRenderer));idle.transform.SetParent(root.transform,false);
            string key=smile.ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
            if(!curves.TryGetValue(key,out var curve)||!curve)
            {
                // Closed colored smile/frown: curved capsule ribbon in normalized mouth space.
                var v=new List<Vector3>();var t=new List<int>();const int n=24,sides=8;
                for(int i=0;i<=n;i++)for(int k=0;k<sides;k++)
                {
                    float x=(i/(float)n-.5f)*.95f,y=smile*3*(4*x*x-.5f),angle=k*Mathf.PI*2/sides;
                    v.Add(new Vector3(x,y+Mathf.Cos(angle)*.16f,.4f+Mathf.Sin(angle)*.12f));
                    if(i<n){int a=i*sides+k,b=i*sides+(k+1)%sides,c=a+sides,d=b+sides;t.AddRange(new[]{a,b,c,b,d,c});}
                }
                curve=new Mesh{name="Expression smile "+key};curve.SetVertices(v);curve.SetTriangles(t,0);curve.RecalculateNormals();curve.RecalculateBounds();curves[key]=curve;
            }
            idle.GetComponent<MeshFilter>().sharedMesh=curve;idle.GetComponent<MeshRenderer>().sharedMaterial=DollFactory.Material("#332438");
            // The mouth aperture is reserved for eating, independently of the eye expression.
            return root;
        }
    }
}
