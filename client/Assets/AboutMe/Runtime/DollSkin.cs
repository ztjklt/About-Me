using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AboutMe
{
    // Only generated, disposable meshes belong here. Imported and exported asset meshes are shared.
    public sealed class DollMeshOwner : MonoBehaviour
    {
        public Mesh mesh;
        void OnDestroy(){if(mesh&&mesh.hideFlags==HideFlags.DontSave){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
    public static class DollSkin
    {
        static Material paletteMaterial;
        public static GameObject Sleeve(Transform upper,Transform torso,Transform lower,bool shortSleeve,string color,bool skinArm=false)
        {
            const int segments=12;int rings=shortSleeve?3:5;
            var points=new List<Vector3>();var weights=new List<BoneWeight>();var triangles=new List<int>();
            float inward=upper.localPosition.x<0?1:-1;
            float[] y=skinArm?new[]{-.10f,-.17f,-.23f,-.285f,-.52f}:shortSleeve?new[]{.06f,-.055f,-.2f}:new[]{.06f,-.07f,-.19f,-.285f,-.445f};
            for(int r=0;r<rings;r++)for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments,radius=skinArm?.12f:r==0?.15f:r==1?.175f:.15f;
                points.Add(new Vector3(Mathf.Cos(angle)*radius+(r==0&&!skinArm?inward*.14f:0),y[r],Mathf.Sin(angle)*(radius+.012f)));
                // Indices are local sleeve bones: torso, upper arm, forearm.
                float chest=skinArm?0:r==0?1:r==1?.12f:0,forearm=shortSleeve?0:r==3?.5f:r==4?1:0;
                weights.Add(new BoneWeight{boneIndex0=0,weight0=chest,boneIndex1=1,weight1=1-chest-forearm,boneIndex2=2,weight2=forearm});
                if(r<rings-1){int a=r*segments+i,b=r*segments+(i+1)%segments,c=a+segments,d=b+segments;triangles.AddRange(new[]{a,b,c,b,d,c});}
            }
            var mesh=new Mesh{name="Weighted shoulder and sleeve",hideFlags=HideFlags.DontSave};mesh.SetVertices(points);mesh.SetTriangles(triangles,0);mesh.boneWeights=weights.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Weighted sleeve",typeof(MeshFilter),typeof(MeshRenderer),typeof(DollMeshOwner));go.transform.SetParent(upper,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=DollFactory.Material(color);go.GetComponent<DollMeshOwner>().mesh=mesh;return go;
        }
        public static SkinnedMeshRenderer Build(Transform root,Transform[] bones,List<GameObject> cosmetics,params Transform[] special)
        {
            Transform mouth=special[0],leftBrow=special[1],rightBrow=special[2],leftArm=special[3],rightArm=special[4],leftForearm=special[5],rightForearm=special[6],torso=special[7];
            var surface=root.GetComponent<PersonalitySkinSurface>();var patternUV=new List<Vector3>();
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var weights=new List<BoneWeight>();var triangles=new List<int>();var gulp=new List<Vector3>();var belly=new List<Vector3>();var armStretch=new List<Vector3>();var blink=new List<Vector3>();var keep=new List<GameObject>();
            foreach(var part in cosmetics)
            {
                if(part.transform==mouth||part.transform==leftBrow||part.transform==rightBrow){keep.Add(part);continue;}
                foreach(var filter in part.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();if(!mesh||!renderer)continue;
                    int bone=Array.IndexOf(bones,part.transform.parent);if(bone<0)throw new InvalidOperationException("Unbound cosmetic "+part.name);
                    var matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;var points=mesh.vertices;var sourceNormals=mesh.normals;var sourceWeights=part.name=="Weighted sleeve"?mesh.boneWeights:null;
                    var tint=renderer.sharedMaterial.color;var painted=mesh.name=="Doubao painted original body"?mesh.colors:null;int offset=vertices.Count;
                    for(int i=0;i<points.Length;i++)
                    {
                        var worldVertex=matrix.MultiplyPoint3x4(points[i]);
                        vertices.Add(worldVertex);
                        bool patterned=surface&&surface.Active&&(part.name=="Shirt silhouette"||part.name=="Shirt hem"||part.name=="Hood"||part.name=="Kangaroo pocket");
                        var clothPoint=torso.InverseTransformPoint(root.TransformPoint(worldVertex));
                        patternUV.Add(patterned?new Vector3((clothPoint.x+.44f)/.88f,(clothPoint.y+.44f)/.88f,1):Vector3.zero);
                        var delta=Vector3.zero;string name=part.name;
                        bool dsEye=name.StartsWith("DS Eye ",StringComparison.Ordinal);
                        bool claudeEye=name.StartsWith("Claude Eye ",StringComparison.Ordinal);
                        bool gptEye=name.StartsWith("GPT Eye ",StringComparison.Ordinal);
                        bool geminiEye=name.StartsWith("Gemini Eye ",StringComparison.Ordinal);
                        bool grokEye=name.StartsWith("Grok Eye ",StringComparison.Ordinal);
                        bool kimiEye=name.StartsWith("Kimi Eye ",StringComparison.Ordinal);
                        bool qwenEye=name.StartsWith("Qwen Eye ",StringComparison.Ordinal);
                        bool minimaxEye=name.StartsWith("MiniMax Eye ",StringComparison.Ordinal);
                        bool doubaoEye=name.StartsWith("Doubao Eye ",StringComparison.Ordinal);
                        bool glmEye=name.StartsWith("GLM Eye ",StringComparison.Ordinal);
                        bool styledEye=dsEye||claudeEye||gptEye||geminiEye||grokEye||kimiEye||qwenEye||minimaxEye||doubaoEye||glmEye;
                        float eyeDrop=gptEye?GPTGirlFaceDetails.EyeDrop:geminiEye?GeminiGirlFaceDetails.EyeDrop:grokEye?GrokGirlFaceDetails.EyeDrop:kimiEye?KimiGirlFaceDetails.EyeDrop:qwenEye?QwenGirlFaceDetails.EyeDrop:minimaxEye?MiniMaxGirlFaceDetails.EyeDrop:doubaoEye?DoubaoGirlFaceDetails.EyeDrop:glmEye?GLMGirlFaceDetails.EyeDrop:claudeEye?ClaudeGirlFaceDetails.EyeDrop:dsEye?DSGirlFaceDetails.EyeDrop:0;
                        bool isEye=styledEye||name=="Eye white"||name=="Pupil"||name=="Eye glint";
                        if(name.StartsWith("Head(")||name=="Round head and chin")
                        {
                            var inHead=bones[bone].InverseTransformPoint(root.TransformPoint(worldVertex));float lower=Mathf.Clamp01((-inHead.y-.03f)/.42f);delta=new Vector3(inHead.x*.10f*lower,.10f*lower+Mathf.Clamp01(inHead.y/.48f)*.12f,.18f*lower);
                        }
                        else if(name=="Chin")delta=new Vector3(0,.10f,.18f);
                        else if(name=="Nose")delta=new Vector3(0,.28f,.025f);
                        else if(name.StartsWith("Cap "))delta=Vector3.up*.20f;
                        else if(name.StartsWith("Headphones("))delta=Vector3.up*.16f;
                        else if(name.Contains("fringe")||name=="Hair crown")delta=Vector3.up*.16f;
                        else if(isEye)
                        {
                            var local=bones[bone].InverseTransformPoint(root.TransformPoint(worldVertex));
                            delta=new Vector3(Mathf.Sign(styledEye?local.x:part.transform.localPosition.x)*.085f,.20f+eyeDrop,.015f);
                        }
                        gulp.Add(root.InverseTransformVector(bones[bone].TransformVector(delta)));
                        var eyeDelta=Vector3.zero;
                        if(isEye)
                        {var local=bones[bone].InverseTransformPoint(root.TransformPoint(worldVertex));eyeDelta.y=(.05f-eyeDrop-local.y)*.96f;}
                        blink.Add(root.InverseTransformVector(bones[bone].TransformVector(eyeDelta)));
                        var swelling=Vector3.zero;
                        if(bones[bone]==torso&&name!="Neck"&&name!="Collar"&&name!="Hood")
                        {
                            var local=torso.InverseTransformPoint(root.TransformPoint(worldVertex));float influence=Mathf.Clamp01(1-Mathf.Abs(local.y+.18f)/.4f)*Mathf.Clamp01((local.z+.1f)/.4f);swelling=new Vector3(local.x*.10f*influence,0,.11f*influence);
                        }
                        belly.Add(root.InverseTransformVector(torso.TransformVector(swelling)));
                        var stretch=Vector3.zero;
                        if(bones[bone]==leftForearm||bones[bone]==rightForearm)
                        {
                            var local=bones[bone].InverseTransformPoint(root.TransformPoint(worldVertex));
                            float shift=name=="Wrist"?.30f:name=="Mitten palm"||name=="Thumb"?.345f:name=="Cuff"?.24f:Mathf.Max(0,-local.y)*1.5f;
                            stretch=Vector3.down*shift;
                        }
                        else if(name=="Weighted sleeve")
                        {
                            var local=bones[bone].InverseTransformPoint(root.TransformPoint(worldVertex));stretch=Vector3.down*Mathf.Max(0,-local.y-.285f)*1.5f;
                        }
                        armStretch.Add(root.InverseTransformVector(bones[bone].TransformVector(stretch)));
                        normals.Add(normalMatrix.MultiplyVector(sourceNormals[i]).normalized);colors.Add(painted!=null&&painted.Length==points.Length?painted[i]:tint);
                        if(sourceWeights==null)
                        {
                            var w=new BoneWeight{boneIndex0=bone,weight0=1};
                            // The trunk bends over pelvis/spine/chest, not a single rigid shirt block.
                            if(bones[bone]==torso)
                            {
                                var local=torso.InverseTransformPoint(root.TransformPoint(worldVertex));
                                int adjacent=Array.FindIndex(bones,b=>b.name==(local.y<-.12f?"Hips":local.y>.12f?"Chest":"Spine"));
                                float share=local.y<-.12f?Mathf.Clamp01((-local.y-.12f)/.4f):local.y>.12f?Mathf.Clamp01((local.y-.12f)/.35f):.25f;
                                if(adjacent>=0){w.boneIndex1=adjacent;w.weight1=share*.8f;w.weight0=1-w.weight1;}
                            }
                            weights.Add(w);
                        }
                        else
                        {
                            var w=sourceWeights[i];w.boneIndex0=Array.IndexOf(bones,torso);w.boneIndex1=bone;w.boneIndex2=Array.IndexOf(bones,part.transform.parent==leftArm?leftForearm:rightForearm);weights.Add(w);
                        }
                    }
                    foreach(int index in mesh.triangles)triangles.Add(offset+index);
                }
                part.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(part);else UnityEngine.Object.DestroyImmediate(part);
            }
            var combined=new Mesh{name="Campus doll palette skin",hideFlags=HideFlags.DontSave,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            combined.SetVertices(vertices);combined.SetNormals(normals);combined.SetColors(colors);combined.SetUVs(0,patternUV);combined.SetTriangles(triangles,0);combined.boneWeights=weights.ToArray();
            var bind=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)bind[i]=bones[i].worldToLocalMatrix*root.localToWorldMatrix;combined.bindposes=bind;combined.RecalculateBounds();
            combined.AddBlendShapeFrame("Giant mouth and jaw",100,gulp.ToArray(),new Vector3[vertices.Count],new Vector3[vertices.Count]);
            combined.AddBlendShapeFrame("Swallow belly",100,belly.ToArray(),new Vector3[vertices.Count],new Vector3[vertices.Count]);
            combined.AddBlendShapeFrame("Stretch feeding arms",100,armStretch.ToArray(),new Vector3[vertices.Count],new Vector3[vertices.Count]);
            combined.AddBlendShapeFrame("Blink",100,blink.ToArray(),new Vector3[vertices.Count],new Vector3[vertices.Count]);
            if(!paletteMaterial)paletteMaterial=Resources.Load<Material>("CampusDoll/Palette");
            if(!paletteMaterial)paletteMaterial=new Material(Shader.Find("AboutMe/DollPalette")){name="Campus doll shared vertex palette"};
            var obj=new GameObject("Skinned campus outfit",typeof(SkinnedMeshRenderer),typeof(DollMeshOwner));obj.transform.SetParent(root,false);obj.GetComponent<DollMeshOwner>().mesh=combined;
            var skin=obj.GetComponent<SkinnedMeshRenderer>();skin.sharedMesh=combined;skin.sharedMaterial=surface&&surface.Active?surface.Material:paletteMaterial;skin.bones=bones;skin.rootBone=torso;skin.quality=SkinQuality.Bone4;skin.localBounds=new Bounds(new Vector3(0,.3f,0),new Vector3(3.3f,3.8f,3.3f));skin.updateWhenOffscreen=false;
            cosmetics.Clear();cosmetics.AddRange(keep);cosmetics.Add(obj);return skin;
        }
    }
    // Server stamps, rather than render frames, identify snapshots. A small buffer absorbs 20 Hz arrival jitter.
    public sealed class DollMotionBuffer
    {
        struct SamplePoint {public float time,yaw;public Vector3 position;}
        readonly SamplePoint[] points=new SamplePoint[8];int count;float latestStamp=-1,latestArrival;
        public void Push(float serverTime,float arrival,Vector3 position,float yaw)
        {
            if(serverTime==latestStamp)return;
            if(count>0&&(serverTime<latestStamp||Vector3.Distance(points[count-1].position,position)>8))count=0;
            if(count==points.Length){Array.Copy(points,1,points,0,count-1);count--;}
            points[count++]=new SamplePoint{time=serverTime,position=position,yaw=yaw};latestStamp=serverTime;latestArrival=arrival;
        }
        public void Sample(float now,out Vector3 position,out float yaw)
        {
            if(count==0){position=Vector3.zero;yaw=0;return;}
            float time=latestStamp+Mathf.Clamp(now-latestArrival,0,.12f)-.075f;
            var a=points[0];for(int i=1;i<count;i++)
            {
                var b=points[i];if(time<=b.time){float t=Mathf.InverseLerp(a.time,b.time,time);position=Vector3.Lerp(a.position,b.position,t);yaw=Mathf.LerpAngle(a.yaw,b.yaw,t);return;}a=b;
            }
            // At most 45 ms extrapolation; packet loss cannot send the visual through the campus indefinitely.
            position=a.position;yaw=a.yaw;
            if(count>1){var previous=points[count-2];float step=a.time-previous.time;if(step>.001f)position+=(a.position-previous.position)/step*Mathf.Clamp(time-a.time,0,.045f);}
        }
    }
}
