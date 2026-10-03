using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    [Serializable] public class OriginalModelPart {public string shape,color;public Vector3 position,scale,rotation;}
    [Serializable] public class OriginalModelRecipe {public string id,label,templateId,color,license,source,stage;public string[] keywords;public OriginalModelPart[] parts;}
    [Serializable] public class OriginalModelRecipes {public int version;public OriginalModelRecipe[] items;}
    public static class OriginalPersonaModels
    {
        static OriginalModelRecipe[] recipes;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        public static OriginalModelRecipe[] All
        {
            get
            {
                if(recipes==null){var text=Resources.Load<TextAsset>("persona-original-models");recipes=text?JsonUtility.FromJson<OriginalModelRecipes>(text.text).items:Array.Empty<OriginalModelRecipe>();}
                return recipes;
            }
        }
        public static GameObject Create(OriginalModelRecipe recipe,Transform parent=null)
        {
            var root=new GameObject(recipe.id);root.transform.SetParent(parent,false);
            var model=new GameObject("Original toy mesh",typeof(MeshFilter),typeof(MeshRenderer));model.transform.SetParent(root.transform,false);
            model.GetComponent<MeshFilter>().sharedMesh=BuildMesh(recipe);
            model.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("CampusDoll/Palette");
            var grip=new GameObject("Grip");grip.transform.SetParent(root.transform,false);
            var swallow=new GameObject("SwallowCenter");swallow.transform.SetParent(root.transform,false);
            var bounds=model.GetComponent<MeshFilter>().sharedMesh.bounds;
            var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;collider.enabled=false;
            var meta=root.AddComponent<PersonaVisualMeta>();meta.visualId=recipe.id;meta.templateId=recipe.templateId;meta.source=recipe.source;meta.license=recipe.license;meta.colliderRadius=bounds.extents.magnitude;meta.swallowScale=1;
            return root;
        }
        public static Mesh BuildMesh(OriginalModelRecipe recipe)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color32>();
            foreach(var part in recipe.parts)
            {
                if(!meshes.TryGetValue(part.shape,out var source))meshes[part.shape]=source=Primitive(part.shape);
                int offset=vertices.Count;var matrix=Matrix4x4.TRS(part.position,Quaternion.Euler(part.rotation),part.scale);
                var color=(Color32)ToyFactory.ColorOf(part.color);
                foreach(var vertex in source.vertices){vertices.Add(matrix.MultiplyPoint3x4(vertex));colors.Add(color);}
                triangles.AddRange(source.triangles.Select(i=>i+offset));
            }
            // A single palette material and single draw call per model; no transient editor materials.
            var mesh=new Mesh{name=recipe.id+" original mesh"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();
            float largest=Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y,mesh.bounds.size.z);
            float scale=.85f/Mathf.Max(.85f,largest);var center=mesh.bounds.center;
            mesh.SetVertices(vertices.Select(v=>(v-center)*scale).ToList());mesh.RecalculateBounds();return mesh;
        }
        static Mesh Primitive(string shape)
        {
            var v=new List<Vector3>();var t=new List<int>();
            void Tri(int a,int b,int c){t.Add(a);t.Add(b);t.Add(c);}
            const int n=12;
            if(shape=="box"||shape=="wedge")
            {
                var points=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
                if(shape=="wedge"){points[2].x=.35f;points[3].x=-.35f;points[6].x=.35f;points[7].x=-.35f;}
                int[] faces={0,3,2,1,4,5,6,7,0,4,7,3,1,2,6,5,3,7,6,2,0,1,5,4};
                for(int f=0;f<6;f++){int start=v.Count;for(int j=0;j<4;j++)v.Add(points[faces[f*4+j]]);Tri(start,start+1,start+2);Tri(start,start+2,start+3);}
            }
            else if(shape=="torus"||shape=="arc")
            {
                const int tube=6;bool arc=shape=="arc";
                for(int i=0;i<=n;i++)for(int j=0;j<=tube;j++)
                {
                    float a=(arc?Mathf.PI:0)+i*Mathf.PI*(arc?1:2)/n,b=j*Mathf.PI*2/tube;
                    v.Add(new Vector3(Mathf.Cos(a)*(.42f+.08f*Mathf.Cos(b)),Mathf.Sin(a)*(.42f+.08f*Mathf.Cos(b)),Mathf.Sin(b)*.5f));
                    if(i<n&&j<tube){int k=i*(tube+1)+j;Tri(k,k+tube+1,k+1);Tri(k+1,k+tube+1,k+tube+2);}
                }
            }
            else if(shape=="sphere"||shape=="hemisphere")
            {
                int rings=shape=="hemisphere"?3:6;float sweep=shape=="hemisphere"?Mathf.PI*.5f:Mathf.PI;
                for(int i=0;i<=rings;i++)for(int j=0;j<=n;j++)
                {
                    float a=i*sweep/rings,b=j*Mathf.PI*2/n;v.Add(new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b))*.5f);
                    if(i<rings&&j<n){int k=i*(n+1)+j;Tri(k,k+1,k+n+1);Tri(k+1,k+n+2,k+n+1);}
                }
                if(shape=="hemisphere"){int center=v.Count;v.Add(Vector3.zero);for(int j=0;j<n;j++)Tri(center,rings*(n+1)+j,rings*(n+1)+j+1);}
            }
            else if(shape=="cylinder"||shape=="cone"||shape=="bucket")
            {
                bool cone=shape=="cone";
                for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;float lower=shape=="bucket"?.35f:.5f;v.Add(new Vector3(Mathf.Cos(a)*lower,-.5f,Mathf.Sin(a)*lower));v.Add(new Vector3(Mathf.Cos(a)*(cone?0:.5f),.5f,Mathf.Sin(a)*(cone?0:.5f)));}
                int bottom=v.Count;v.Add(Vector3.down*.5f);int top=v.Count;v.Add(Vector3.up*.5f);
                for(int i=0;i<n;i++){int a=i*2,b=(i*2+2)%(n*2);Tri(a,a+1,b);if(!cone)Tri(a+1,b+1,b);Tri(bottom,a,b);if(!cone)Tri(top,b+1,a+1);}
            }
            else throw new ArgumentException("Unknown original shape: "+shape);
            var mesh=new Mesh{name="Original primitive "+shape};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
