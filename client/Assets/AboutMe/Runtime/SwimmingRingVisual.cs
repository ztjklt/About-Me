using System.Collections.Generic;
using UnityEngine;

namespace AboutMe
{
    public static class SwimmingRingVisual
    {
        public static Mesh Mesh()
        {
            const int around=36,tube=12;var vertices=new List<Vector3>();var normals=new List<Vector3>();var blue=new List<int>();var white=new List<int>();
            for(int i=0;i<=around;i++)for(int j=0;j<=tube;j++)
            {
                float a=i*Mathf.PI*2/around,b=j*Mathf.PI*2/tube;
                var normal=new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(a)*Mathf.Cos(b),Mathf.Sin(b));
                vertices.Add(new Vector3(Mathf.Cos(a)*.42f,Mathf.Sin(a)*.42f,0)+normal*.115f);normals.Add(normal);
                if(i<around&&j<tube){int n=i*(tube+1)+j;var triangles=i%9<2?white:blue;triangles.AddRange(new[]{n,n+tube+1,n+1,n+1,n+tube+1,n+tube+2});}
            }
            var mesh=new Mesh{name="Original swimming ring · 864 triangles"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.subMeshCount=2;mesh.SetTriangles(blue,0);mesh.SetTriangles(white,1);mesh.RecalculateBounds();return mesh;
        }
        public static GameObject Create(Transform parent,string color)
        {
            var root=new GameObject("swimming-ring");root.transform.SetParent(parent,false);
            var model=new GameObject("Inflatable blue ring",typeof(MeshFilter),typeof(MeshRenderer));model.transform.SetParent(root.transform,false);
            model.GetComponent<MeshFilter>().sharedMesh=Mesh();model.GetComponent<MeshRenderer>().sharedMaterials=new[]{ToyFactory.Material(color),ToyFactory.Material("#FFF9EF")};
            var grip=new GameObject("Grip");grip.transform.SetParent(root.transform,false);
            var collider=root.AddComponent<BoxCollider>();collider.enabled=false;collider.size=new Vector3(1.07f,1.07f,.23f);
            var meta=root.AddComponent<PersonaVisualMeta>();meta.visualId="swimming-ring";meta.templateId="Cushion";meta.colliderRadius=.535f;meta.swallowScale=1;meta.source="About Me original code geometry";meta.license="project-original";
            return root;
        }
    }
}
