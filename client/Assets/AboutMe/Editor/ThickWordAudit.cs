using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Stopwatch=System.Diagnostics.Stopwatch;

namespace AboutMe.Editor
{
    // Isolated engine fixture, never creates or connects to a network match.
    public static class ThickWordAudit
    {
        static readonly string[] Cases={"热情","冷静","好奇","温柔","理性","活力","有序","自由","幽默","勇敢","内向","外向","专注","乐观","谨慎","独立","友好微笑","开心大笑","自信挑眉","慵懒半眯","惊讶睁眼","认真坚定","ISTJ","ISFJ","INFJ","INTJ","ISTP","ISFP","INFP","INTP","ESTP","ESFP","ENFP","ENTP","ESTJ","ESFJ","ENFJ","ENTJ"};
        [Serializable] public class Report
        {
            public string platform,gpu,unity,scope;public int supportedCodepoints,words,renderers,triangles,vertices,cachedGlyphs,cachedWords,cachedPages,meshBuilds,meshHits;
            public long meshBytes,pageBytes;public double coldMilliseconds,warm72Milliseconds,render72AverageMs,render72P95Ms;
            public int worstWords,worstVertices,worstTriangles;public long worstMeshBytes;public double worstBuildMilliseconds;
            public bool frontBackSides,missingGlyphRejected,unicodeSurrogatePassed,boxCollisionPassed,realHandAttachment,sharedMeshPassed,cacheEvictionPassed;
        }
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("ABOUTME_THICK_WORD_AUDIT_OUTPUT");if(string.IsNullOrEmpty(output))output=Path.GetFullPath("../artifacts/thick-word-r5/engine");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.58f,.6f,.64f);
            var light=new GameObject("Word key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(35,150,0);
            var camera=new GameObject("Word audit camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#FFF4DC");camera.orthographic=true;camera.orthographicSize=3.6f;camera.transform.position=new Vector3(0,.15f,10);camera.transform.LookAt(new Vector3(0,.15f,0));
            var group=new GameObject("38 real font words");var list=new List<GameObject>();var report=new Report{platform=Application.platform.ToString(),gpu=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,scope="macOS editor actual Camera.Render isolated six ToyActor / 72 solid-font models fixture; CPU synchronous render timing, not game frame pacing, phone, live ability or multiplayer."};
            var timer=Stopwatch.StartNew();
            for(int i=0;i<Cases.Length;i++)
            {
                string text=Cases[i];Assert(ThickWordVisual.TryValidate(text,out var reason),reason);
                var go=ThickWordVisual.Create(text,"audit",group.transform,i<22?"#5CBFAA":"#E6A5C3");Assert(go!=null,"model "+text);list.Add(go);
                go.transform.localPosition=new Vector3((3.5f-i%8)*1.35f,2.05f-(i/8)*.94f,0);go.transform.localRotation=Quaternion.Euler(0,-30,0);
                var mesh=go.GetComponent<MeshFilter>().sharedMesh;Assert(Mathf.Abs(mesh.bounds.size.z-ThickWordVisual.Depth)<.0001f,"visible thickness");
                Assert(go.GetComponentsInChildren<MeshRenderer>().Length==1&&go.GetComponentsInChildren<TextMesh>().Length==0,"single solid renderer");
                Assert(mesh.normals.Any(n=>n.z>.9f)&&mesh.normals.Any(n=>n.z<-.9f)&&mesh.normals.Any(n=>Mathf.Abs(n.z)<.1f),"front/back/sides");
                Assert(go.transform.Find("Grip")&&go.GetComponent<BoxCollider>(),"grip collision metadata");
                report.vertices+=mesh.vertexCount;report.triangles+=mesh.triangles.Length/3;
            }
            timer.Stop();report.coldMilliseconds=timer.Elapsed.TotalMilliseconds;report.frontBackSides=true;Capture(camera,Path.Combine(output,"all-38-thick-words.png"));
            foreach(var go in list)go.transform.localRotation=Quaternion.Euler(0,0,0);Capture(camera,Path.Combine(output,"all-38-front.png"));
            foreach(var go in list)go.transform.localRotation=Quaternion.Euler(0,180,0);Capture(camera,Path.Combine(output,"all-38-back.png"));
            group.SetActive(false);
            Assert(!ThickWordVisual.TryValidate("🫠",out var missing)&&missing.Contains("U+1FAE0"),"missing glyph reason");Assert(!ThickWordVisual.HasGlyphs("1234567890123")&&!ThickWordVisual.HasGlyphs(" ")&&ThickWordVisual.HasGlyphs("very kind"),"limits and space advance");report.missingGlyphRejected=true;
            Assert(ThickWordVisual.HasGlyphs(char.ConvertFromUtf32(0x2000B)),"supplementary codepoint");report.unicodeSurrogatePassed=true;
            var detail=new GameObject("Counter details");
            foreach(string text in new[]{"B","温","ENFP"}){var go=ThickWordVisual.Create(text,"audit",detail.transform,"#FFD36D");go.transform.position=new Vector3((1-Array.IndexOf(new[]{"B","温","ENFP"},text))*2.7f,0,0);go.transform.rotation=Quaternion.Euler(0,-35,0);}
            camera.orthographicSize=2.3f;Capture(camera,Path.Combine(output,"counters-and-side-walls.png"));detail.SetActive(false);
            // Use real rig anchors; gameplay and networking are owned by the integrating agent.
            var fixture=new GameObject("Six actors and 72 physical words");var active=new List<GameObject>();
            for(int p=0;p<6;p++)
            {
                var actor=new ToyActor("word actor "+p,"#79CFC5",p,fixture.transform,AppearanceData.Demo(p));actor.label.text="";actor.root.transform.position=new Vector3((p%3-1)*3.2f,1.1f,(p/3)*3.3f);
                for(int j=0;j<12;j++)
                {
                    var word=ThickWordVisual.Create(Cases[(p*12+j)%Cases.Length],"audit",j==0?actor.RightHandAttachment:fixture.transform,j%2==0?"#5CBFAA":"#E6A5C3");active.Add(word);
                    if(j==0){word.transform.localPosition=new Vector3(0,0,.12f);word.transform.localScale=Vector3.one*.6f;Assert(word.transform.parent==actor.RightHandAttachment,"actual hand anchor");}
                    else word.transform.localPosition=new Vector3((p%3-1)*3.2f+(j%4-1.5f)*.64f,.24f+(j/4)*.47f,(p/3)*3.3f-1.1f);
                    word.transform.localRotation=Quaternion.Euler(-15,30+j*8,0);word.transform.localScale=Vector3.one*(j==0?.6f:.5f);
                }
            }
            report.realHandAttachment=true;report.words=active.Count;report.renderers=active.Sum(x=>x.GetComponentsInChildren<MeshRenderer>().Length);
            report.vertices=active.Sum(x=>x.GetComponent<MeshFilter>().sharedMesh.vertexCount);report.triangles=active.Sum(x=>x.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
            var single=active[0];Physics.SyncTransforms();var collider=single.GetComponent<BoxCollider>();var ray=new Ray(collider.bounds.center+Vector3.forward*5,Vector3.back);Assert(collider.Raycast(ray,out _,10),"solid bounds ray");report.boxCollisionPassed=true;
            Assert(active[0].GetComponent<MeshFilter>().sharedMesh==list[0].GetComponent<MeshFilter>().sharedMesh,"shared cached mesh");report.sharedMeshPassed=true;
            int builds=ThickWordVisual.MeshBuilds;timer.Restart();
            var transient=new List<GameObject>();for(int i=0;i<72;i++)transient.Add(ThickWordVisual.Create(Cases[i%Cases.Length],"audit",null,"#5CBFAA"));timer.Stop();report.warm72Milliseconds=timer.Elapsed.TotalMilliseconds;
            Assert(ThickWordVisual.MeshBuilds==builds,"warm instances reuse meshes");foreach(var go in transient)UnityEngine.Object.DestroyImmediate(go);
            camera.orthographicSize=5.0f;camera.transform.position=new Vector3(5,9,13);camera.transform.LookAt(new Vector3(0,.7f,1.5f));Capture(camera,Path.Combine(output,"six-actors-72-words.png"));
            var rt=new RenderTexture(1280,720,24);rt.Create();camera.targetTexture=rt;camera.aspect=1280f/720;var timings=new List<double>();
            for(int frame=0;frame<100;frame++){foreach(var word in active)word.transform.Rotate(Vector3.up,2,Space.Self);timer.Restart();camera.Render();timer.Stop();if(frame>=20)timings.Add(timer.Elapsed.TotalMilliseconds);}
            camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);timings.Sort();report.render72AverageMs=timings.Average();report.render72P95Ms=timings[(int)(timings.Count*.95)];
            report.supportedCodepoints=ThickWordVisual.SupportedCodepoints;report.cachedGlyphs=ThickWordVisual.CachedGlyphs;report.cachedWords=ThickWordVisual.CachedWords;report.cachedPages=ThickWordVisual.CachedPages;report.meshBuilds=ThickWordVisual.MeshBuilds;report.meshHits=ThickWordVisual.MeshHits;report.meshBytes=ThickWordVisual.MeshBytes;report.pageBytes=ThickWordVisual.PageBytes;
            // >96 unique custom strings and >256 glyphs while active objects pin their meshes.
            for(int i=0;i<310;i++){var go=ThickWordVisual.Create("字"+(char)(0x4e00+i)+i,"audit",null,"#5CBFAA");Assert(go,"custom pressure model");UnityEngine.Object.DestroyImmediate(go);}
            Assert(ThickWordVisual.GlyphLoads>256&&ThickWordVisual.CachedWords<=96&&ThickWordVisual.CachedGlyphs<=256&&ThickWordVisual.CachedPages<=8,"bounded caches under >256 distinct glyphs");Assert(single.GetComponent<MeshFilter>().sharedMesh!=null,"pinned active mesh");report.cacheEvictionPassed=true;
            UnityEngine.Object.DestroyImmediate(group);UnityEngine.Object.DestroyImmediate(detail);UnityEngine.Object.DestroyImmediate(fixture);Assert(ThickWordVisual.ClearUnusedCaches(),"cache leases released");
            // Adversarial upper bound: 72 UNIQUE twelve-character strings composed
            // almost entirely of the font's most complex rare CJK glyph (494 points).
            // Keep this memory case separate from the normal 38-word six-actor fixture.
            var worst=new GameObject("Worst word budget");string rare=char.ConvertFromUtf32(200812);string prefix=string.Concat(Enumerable.Repeat(rare,11));timer.Restart();
            for(int i=0;i<72;i++)
            {
                var go=ThickWordVisual.Create(prefix+(char)(0x4e00+i),"audit",worst.transform,"#5CBFAA");Assert(go,"worst-case physical word");
                var mesh=go.GetComponent<MeshFilter>().sharedMesh;report.worstVertices+=mesh.vertexCount;report.worstTriangles+=mesh.triangles.Length/3;
            }
            timer.Stop();report.worstBuildMilliseconds=timer.Elapsed.TotalMilliseconds;report.worstWords=72;report.worstMeshBytes=ThickWordVisual.MeshBytes;
            UnityEngine.Object.DestroyImmediate(worst);Assert(ThickWordVisual.ClearUnusedCaches(),"worst cache leases released");
            File.WriteAllText(Path.Combine(output,"engine-validation.json"),JsonUtility.ToJson(report,true));Debug.Log("THICK_WORD_ENGINE_AUDIT_PASSED "+JsonUtility.ToJson(report));
        }
        static void Assert(bool pass,string message){if(!pass)throw new Exception("THICK_WORD_AUDIT_FAILED: "+message);}
        static void Capture(Camera camera,string path)
        {
            var previous=RenderTexture.active;var target=new RenderTexture(1800,1100,24);target.antiAliasing=4;target.Create();camera.targetTexture=target;camera.aspect=1800f/1100;camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1800,1100,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1800,1100),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
