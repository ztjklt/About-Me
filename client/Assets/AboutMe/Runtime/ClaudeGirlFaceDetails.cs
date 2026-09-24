using System;
using System.Collections.Generic;
using UnityEngine;
namespace AboutMe
{
    /// <summary>Face-only styling on the existing head; no new bones or eye transforms.</summary>
    public static class ClaudeGirlFaceDetails
    {
        public const float EyeDrop=0f,MouthDrop=.035f,BrowDrop=0f;
        static Mesh smallGlint;
        static Mesh SmallGlint()
        {
            if(smallGlint)return smallGlint;
            const int rings=4,segments=8;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int r=0;r<=rings;r++)for(int s=0;s<=segments;s++)
            {float latitude=r*Mathf.PI/rings,angle=s*2*Mathf.PI/segments;vertices.Add(new Vector3(Mathf.Sin(latitude)*Mathf.Cos(angle),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(angle))*.5f);}
            for(int r=0;r<rings;r++)for(int s=0;s<segments;s++)
            {int i=r*(segments+1)+s;triangles.AddRange(new[]{i,i+1,i+segments+1,i+1,i+segments+2,i+segments+1});}
            smallGlint=new Mesh{name="Claude shared small eye glint",hideFlags=HideFlags.DontSave};smallGlint.SetVertices(vertices);smallGlint.SetTriangles(triangles,0);smallGlint.RecalculateNormals();smallGlint.RecalculateBounds();return smallGlint;
        }
        public static void AddEye(Transform head,int side,DollExpression face,List<GameObject> cosmetics)
        {
            GameObject Part(string name,Vector3 pos,Vector3 size,string color)
            {var go=DollFactory.Part("Claude Eye "+name,head,pos,size,color);cosmetics.Add(go);return go;}
            const string white="#FFF9EF",ink="#342C29";
            float whiteY=.055f-EyeDrop,irisY=.044f-EyeDrop,x=side*.17f;
            Part("white",new Vector3(side*.185f,whiteY,.417f),new Vector3(.19f*face.eyeWidth,.205f*face.eyeHeight,.07f),white);
            Part("iris rim",new Vector3(x,irisY,.453f),new Vector3(.111f*face.pupil,.153f*face.eyeHeight,.029f),"#705039");
            Part("iris",new Vector3(x,irisY,.465f),new Vector3(.096f*face.pupil,.137f*face.eyeHeight,.023f),"#B88950");
            Part("pupil",new Vector3(x,irisY+.005f,.481f),new Vector3(.044f*face.pupil,.084f*face.eyeHeight,.017f),ink);
            Part("highlight",new Vector3(x-.020f,irisY+.035f*face.eyeHeight,.495f),Vector3.one*.024f,white);
            Part("small highlight",new Vector3(x+.018f,irisY-.037f*face.eyeHeight,.487f),Vector3.one*.012f,"#FFE1A7").GetComponent<MeshFilter>().sharedMesh=SmallGlint();
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
            var mesh=new Mesh{name="Claude upper lid and three lashes",hideFlags=HideFlags.DontSave};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var lid=Part("lashes",Vector3.zero,Vector3.one,ink);lid.GetComponent<MeshFilter>().sharedMesh=mesh;lid.AddComponent<DollMeshOwner>().mesh=mesh;
        }
    }
}
