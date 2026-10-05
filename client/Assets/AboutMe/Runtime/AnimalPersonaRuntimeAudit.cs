using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe
{
    // Opt-in constructed match and isolated UI audit; no player profile is saved.
    public sealed class AnimalPersonaRuntimeAudit:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();string output;GameNetwork net;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Begin(){if(Environment.GetCommandLineArgs().Contains("--animal-persona-audit"))new GameObject("Animal audit").AddComponent<AnimalPersonaRuntimeAudit>();}
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"--animal-audit-output");output=args[n+1];Directory.CreateDirectory(output);
            var task=Run();while(true){object next;try{if(!task.MoveNext())break;next=task.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failed.txt"),e.ToString());Debug.LogException(e);Application.Quit(1);yield break;}yield return next;}
            File.WriteAllText(Path.Combine(output,"passed.txt"),string.Join("\n",checks));Debug.Log("ANIMAL_RUNTIME_AUDIT_PASSED "+checks.Count);Application.Quit(0);
        }
        IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return new WaitForSecondsRealtime(.25f);}
        IEnumerator WalkGallery()
        {
            var cameraObject=new GameObject("Runtime animal walk camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.94f,.91f,.85f);camera.orthographic=true;camera.orthographicSize=.94f;
            camera.transform.position=new Vector3(1000,1.25f,5);camera.transform.LookAt(new Vector3(1000,.4f,0));
            var lightObject=new GameObject("Runtime animal walk light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(35,160,0);
            var all=new GameObject[4];for(int i=0;i<4;i++){var go=PersonaVisualCatalog.Create(AnimalPersonaCatalog.Visuals[i],null);all[i]=go;go.transform.position=new Vector3(1000+(i-1.5f)*1.1f,.35f,0);go.transform.rotation=Quaternion.Euler(0,-25,0);foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;}
            var target=new RenderTexture(1600,650,24);camera.targetTexture=target;
            string frames=Path.Combine(output,"walk-frames");Directory.CreateDirectory(frames);
            try
            {
                for(int frame=0;frame<120;frame++)
                {
                    foreach(var go in all)go.GetComponent<AnimalPersonaMotion>().Present(true,frame/30f);
                    yield return new WaitForEndOfFrame();camera.Render();var prior=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(1600,650,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,650),0,0);image.Apply();File.WriteAllBytes(Path.Combine(frames,"frame-"+frame.ToString("000")+".png"),image.EncodeToPNG());RenderTexture.active=prior;Destroy(image);
                }
            }
            finally{foreach(var go in all)Destroy(go);Destroy(cameraObject);Destroy(lightObject);Destroy(target);}
            Check(Directory.GetFiles(frames,"frame-*.png").Length==120,"four actual rendered skeletal walks across runtime frames");
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);net=GameNetwork.Game;
            var question=GameUI.IntroductionQuestions.Last();Check(question.labels.SequenceEqual(new[]{"猫","狗","兔子","仓鼠"})&&question.visuals.SequenceEqual(AnimalPersonaCatalog.Visuals),"four animal choices in introduction");
            foreach(var id in AnimalPersonaCatalog.Visuals)
            {
                var go=ToyFactory.Item(id,"#FFFFFF",null);Check(go.GetComponent<AnimalPersonaMotion>()!=null,"imported animal prefab "+id);
                Check(go.GetComponentsInChildren<MeshFilter>().Sum(x=>x.sharedMesh.triangles.Length/3)+go.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(x=>x.sharedMesh.triangles.Length/3)>1000,"real imported mesh "+id);
                Check(go.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m.mainTexture!=null)),"actual textures "+id);
                var motion=go.GetComponent<AnimalPersonaMotion>();var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();Check(motion.walk&&skin&&skin.bones.Length>=7,"actual skeletal clip "+id);
                var a=new Mesh();var b=new Mesh();motion.Present(true,0);skin.BakeMesh(a,true);motion.Present(true,motion.walk.length/motion.walkSpeed*.25f);skin.BakeMesh(b,true);
                Check(a.vertices.Zip(b.vertices,(x,y)=>(x-y).magnitude).Max()>.005f,"actual runtime skin deformation "+id);motion.Present(false,0);Destroy(a);Destroy(b);Destroy(go);
            }
            yield return WalkGallery();
            bool prior=GameLocalization.English;
            var ui=GameUI.UI;var f=BindingFlags.Instance|BindingFlags.NonPublic;
            var questionnaire=typeof(GameUI).GetMethod("OpenIntroductionQuestionnaire",f);questionnaire.Invoke(ui,null);yield return null;
            var scroll=((RectTransform)typeof(GameUI).GetField("introductionQuestionnaire",f).GetValue(ui)).GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;yield return null;yield return Shot("animal-choices-zh");
            GameLocalization.SetEnglish(true);yield return null;yield return Shot("animal-choices-en");GameLocalization.SetEnglish(prior);
            // Dispose the questionnaire through the same user action.
            var rect=(RectTransform)typeof(GameUI).GetField("introductionQuestionnaire",f).GetValue(ui);Destroy(rect.gameObject);
            using(var match=new Match("ANIMALRUNTIME",new MatchRules{revealDuration=.01f,handInterval=9999,finalTime=9999}))
            {
                for(int i=0;i<4;i++)match.Add(new Profile{playerId="a"+i,nickname="Animal QA "+i,appearance=AppearanceData.Demo(i),personas=AnimalPersonaCatalog.Visuals.Select((id,j)=>new PersonaAsset{assetId="qa"+i+j,name=new[]{"猫","狗","兔子","仓鼠"}[j],visualId=id,templateId="Summon",color="#FFFFFF"}).ToArray()});
                match.Phase=MatchPhase.Reveal;match.Tick(.02f);match.Time=10;match.Props.Clear();
                var positions=new[]{new Vector3(-6,1,-22),new Vector3(-3,1,-18),new Vector3(4,1,-22),new Vector3(7,1,-18)};
                for(int i=0;i<4;i++){var p=match.Players["a"+i];p.position=positions[i];p.motorHeight=1;p.yaw=0;p.lastInput=match.Time;match.Receive(p.profile.playerId,new Command{sequence=p.sequence+1,action="equip",itemId=match.Items.First(x=>x.ownerId==p.profile.playerId&&x.asset.visualId==AnimalPersonaCatalog.Visuals[i]).id,yaw=0});p.switchUntil=0;}
                match.EnableBodyPhysics();
                for(int frame=0;frame<10;frame++){foreach(var p in match.Players.Values)match.Receive(p.profile.playerId,new Command{sequence=p.sequence+1,yaw=p.yaw});match.Tick(.02f);net.State=match.View("a0");yield return null;}
                yield return Shot("animals-held");
                var item=match.Item(match.Players["a0"].equipped);Check(item.asset.visualId=="cat","cat held before use");var instance=item.id;
                match.Receive("a0",new Command{sequence=match.Players["a0"].sequence+1,action="use_item",yaw=0});
                bool chased=false,landed=false,hit=false;
                for(int frame=0;frame<230;frame++)
                {
                    foreach(var p in match.Players.Values)match.Receive(p.profile.playerId,new Command{sequence=p.sequence+1,yaw=p.yaw});match.Tick(.02f);net.State=match.View("a0");
                    if(!chased&&item.state==ItemState.Thrown&&!string.IsNullOrEmpty(item.chaseTarget)){chased=true;yield return null;yield return Shot("animal-chase");}
                    if(match.Players.Values.Any(p=>p.profile.playerId!="a0"&&p.knockdownId>0))hit=true;
                    if(chased&&!landed&&item.state==ItemState.Ground){landed=true;yield return null;yield return Shot("animal-landed");}
                    yield return null;
                }
                Check(chased&&hit&&landed,"actual body world chase hit and land");Check(item.id==instance&&item.ownerId=="a0"&&match.Remaining("a0")==4,"same original life on ground");
                Check(net.State.protocolVersion==5&&net.State.players.All(p=>p.bodyPose?.positions?.Length==15),"protocol5 with four true body frames");
                net.State=null;
            }
        }
    }
}
