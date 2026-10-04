using System;
using System.Collections.Generic;
using UnityEngine;
namespace AboutMe
{
    /// <summary>Face-only styling on the existing head; no new bones or eye transforms.</summary>
    public static class DoubaoGirlFaceDetails
    {
        public const float EyeDrop=0f,MouthDrop=.035f,BrowDrop=.012f;
        public const string BrowColor="#59402C";
        public const float BrowDepth=.535f;
        public static readonly Vector3 BrowSize=new Vector3(.20f,.042f,.035f);
        static Mesh smallGlint;
        static Mesh SmallGlint()
        {
            if(smallGlint)return smallGlint;
            const int rings=4,segments=8;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int r=0;r<=rings;r++)for(int s=0;s<=segments;s++)
            {float latitude=r*Mathf.PI/rings,angle=s*2*Mathf.PI/segments;vertices.Add(new Vector3(Mathf.Sin(latitude)*Mathf.Cos(angle),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(angle))*.5f);}
            for(int r=0;r<rings;r++)for(int s=0;s<segments;s++)
            {int i=r*(segments+1)+s;triangles.AddRange(new[]{i,i+1,i+segments+1,i+1,i+segments+2,i+segments+1});}
            smallGlint=new Mesh{name="Doubao shared small eye glint",hideFlags=HideFlags.DontSave};smallGlint.SetVertices(vertices);smallGlint.SetTriangles(triangles,0);smallGlint.RecalculateNormals();smallGlint.RecalculateBounds();return smallGlint;
        }
        // Refine only the existing render surface; all points remain on its original triangles.
        // Paint shares the exact body weights and swelling, so there is no separate floating belly shell.
        static Mesh paintedBody;
        public static int AdditionalBodyTriangles {get;private set;}
        public static void PaintBody(GameObject body)
        {
            var filter=body.GetComponent<MeshFilter>();
            if(!paintedBody)
            {
                var source=filter.sharedMesh;var points=new List<Vector3>(source.vertices);var normals=new List<Vector3>(source.normals);var triangles=new List<int>(source.triangles);
                for(int pass=0;pass<4;pass++)
                {
                    var edges=new Dictionary<ulong,int>();var refined=new List<int>();
                    int Mid(int a,int b)
                    {
                        uint lo=(uint)Math.Min(a,b),hi=(uint)Math.Max(a,b);ulong key=((ulong)lo<<32)|hi;
                        if(edges.TryGetValue(key,out var i))return i;
                        i=points.Count;points.Add((points[a]+points[b])*.5f);normals.Add((normals[a]+normals[b]).normalized);edges.Add(key,i);return i;
                    }
                    for(int j=0;j<triangles.Count;j+=3)
                    {
                        int a=triangles[j],b=triangles[j+1],c=triangles[j+2];
                        if(pass>=2)
                        {
                            float Radius(Vector3 p)=>Mathf.Sqrt(Mathf.Pow(p.x/.43f,2)+Mathf.Pow((p.y+.04f)/.43f,2));
                            float ra=Radius(points[a]),rb=Radius(points[b]),rc=Radius(points[c]);
                            bool front=points[a].z>.30f&&points[b].z>.30f&&points[c].z>.30f;
                            float inner=pass==2?.72f:.87f,outer=pass==2?1.16f:1.08f;
                            if(!front||Mathf.Min(ra,Mathf.Min(rb,rc))>outer||Mathf.Max(ra,Mathf.Max(rb,rc))<inner)continue;
                        }
                        Mid(a,b);Mid(b,c);Mid(c,a);
                    }
                    // Conform both sides of every split edge. Otherwise skinning opens T-junctions.
                    bool Edge(int a,int b,out int mid)=>edges.TryGetValue(((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b),out mid);
                    for(int j=0;j<triangles.Count;j+=3)
                    {
                        int a=triangles[j],b=triangles[j+1],c=triangles[j+2];
                        bool e0=Edge(a,b,out int ab),e1=Edge(b,c,out int bc),e2=Edge(c,a,out int ca);
                        if(e0&&e1&&e2)refined.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
                        else if(e0&&e1)refined.AddRange(new[]{ab,b,bc,a,ab,c,ab,bc,c});
                        else if(e1&&e2)refined.AddRange(new[]{bc,c,ca,a,b,ca,b,bc,ca});
                        else if(e2&&e0)refined.AddRange(new[]{ca,a,ab,ab,b,c,ab,c,ca});
                        else if(e0)refined.AddRange(new[]{a,ab,c,ab,b,c});
                        else if(e1)refined.AddRange(new[]{a,b,bc,a,bc,c});
                        else if(e2)refined.AddRange(new[]{a,b,ca,b,c,ca});
                        else refined.AddRange(new[]{a,b,c});
                    }
                    triangles=refined;
                }
                var colors=new List<Color>();var yellow=ToyFactory.ColorOf("#FFD44B");var cream=ToyFactory.ColorOf("#FFF0C4");
                foreach(var point in points)
                {
                    float radius=Mathf.Sqrt(Mathf.Pow(point.x/.43f,2)+Mathf.Pow((point.y+.04f)/.43f,2));
                    float area=point.z>.30f?Mathf.Clamp01((1-radius)/.065f):0;
                    colors.Add(Color.Lerp(yellow,cream,area));
                }
                paintedBody=new Mesh{name="Doubao painted original body",hideFlags=HideFlags.DontSave};paintedBody.SetVertices(points);paintedBody.SetNormals(normals);paintedBody.SetTriangles(triangles,0);paintedBody.SetColors(colors);paintedBody.RecalculateBounds();
                AdditionalBodyTriangles=triangles.Count/3-source.triangles.Length/3;
                if(AdditionalBodyTriangles>8260)throw new InvalidOperationException("Body paint refinement exceeds its budget");
            }
            filter.sharedMesh=paintedBody;
        }
        public static void AddEye(Transform head,int side,DollExpression face,List<GameObject> cosmetics)
        {
            GameObject Part(string name,Vector3 pos,Vector3 size,string color)
            {var go=DollFactory.Part("Doubao Eye "+name,head,pos,size,color);cosmetics.Add(go);return go;}
            const string white="#FFF9EF",ink="#352D35";
            float whiteY=.055f-EyeDrop,irisY=.044f-EyeDrop,x=side*.17f;
            Part("white",new Vector3(side*.185f,whiteY,.417f),new Vector3(.19f*face.eyeWidth,.205f*face.eyeHeight,.07f),white);
            Part("iris rim",new Vector3(x,irisY,.453f),new Vector3(.111f*face.pupil,.153f*face.eyeHeight,.029f),"#553626");
            Part("iris",new Vector3(x,irisY,.465f),new Vector3(.096f*face.pupil,.137f*face.eyeHeight,.023f),"#925B36");
            Part("pupil",new Vector3(x,irisY+.005f,.481f),new Vector3(.044f*face.pupil,.084f*face.eyeHeight,.017f),ink);
            Part("highlight",new Vector3(x-.020f,irisY+.035f*face.eyeHeight,.495f),Vector3.one*.024f,white);
            Part("small highlight",new Vector3(x+.018f,irisY-.037f*face.eyeHeight,.487f),Vector3.one*.012f,"#F5E6FF").GetComponent<MeshFilter>().sharedMesh=SmallGlint();
            // A single small mesh per upper lid. Both the lid and its lashes use the same blink morph.
            var verts=new List<Vector3>();var tris=new List<int>();
            const int segments=12;
            float radiusX=.096f*face.eyeWidth,radiusY=.100f*face.eyeHeight;
            for(int i=0;i<=segments;i++)
            {
                float t=(.05f+.90f*i/segments)*Mathf.PI;
                float px=side*.185f+Mathf.Cos(t)*radiusX,py=whiteY+Mathf.Sin(t)*radiusY;
                verts.Add(new Vector3(px,py-.006f,.452f));verts.Add(new Vector3(px,py+.008f,.452f));
                if(i<segments){int a=i*2;tris.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});}
            }
            for(int i=0;i<3;i++)
            {
                float t=(side>0?.12f+i*.105f:.88f-i*.105f)*Mathf.PI;
                float px=side*.185f+Mathf.Cos(t)*radiusX,py=whiteY+Mathf.Sin(t)*radiusY;
                int a=verts.Count;verts.Add(new Vector3(px-side*.011f,py-.004f,.454f));verts.Add(new Vector3(px+side*.007f,py+.005f,.454f));
                verts.Add(new Vector3(px+side*(.030f-i*.005f),py+(.022f+i*.007f)*face.eyeHeight,.454f));
                if(side>0)tris.AddRange(new[]{a,a+1,a+2});else tris.AddRange(new[]{a,a+2,a+1});
            }
            // Give the thin upper lid a back face for oblique close-ups.
            int front=tris.Count;for(int i=0;i<front;i+=3)tris.AddRange(new[]{tris[i],tris[i+2],tris[i+1]});
            var mesh=new Mesh{name="Doubao upper lid and three lashes",hideFlags=HideFlags.DontSave};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var lid=Part("lashes",Vector3.zero,Vector3.one,ink);lid.GetComponent<MeshFilter>().sharedMesh=mesh;lid.AddComponent<DollMeshOwner>().mesh=mesh;
        }
    }
}
