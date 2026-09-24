using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class ClaudeGirlFaceReview
    {
        const string Out="../artifacts/claude-girl-modular/face-review";
        [Serializable] class Report {public float eyeDrop,mouthDrop,maxFeedingHandDelta,fullyOpenMouthDelta;public int bodyBones,physicalBones,eyeWhites,pupils,lashes,eyeTriangles,additionalFaceTriangles,totalAddedAppearanceTriangles;public bool blinkMorphValid;}
        [MenuItem("About Me/Claude Girl/Review face details")]
        public static void Review(){ClaudeGirlModularTools.Review();ReviewFaceOnly();}
        public static void ReviewFaceOnly()
        {
            Directory.CreateDirectory(Out);
            foreach(var name in new[]{"front.png","three-quarter.png","action-eat.png","action-eat-side.png","action-grab.png"})File.Copy(ClaudeGirlModularTools.Out+"/review/"+name,Out+"/"+name,true);
            File.Copy(ClaudeGirlModularTools.Out+"/pose-parity.json",Out+"/pose-parity.json",true);
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            var camera=new GameObject("Windowless face review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.60f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ToyFactory.ColorOf("#E9ECF1");
            var light=new GameObject("Face softbox").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(25,-35,0);
            var ds=new ToyActor("DS face","#79CFC5",0,null,new AppearanceData{character="claude"});ds.label.text="";
            var plain=new ToyActor("Original","#79CFC5",0,null,new AppearanceData());plain.root.SetActive(false);
            var report=new Report{eyeDrop=ClaudeGirlFaceDetails.EyeDrop,mouthDrop=ClaudeGirlFaceDetails.MouthDrop,bodyBones=ds.DeformationBoneCount,physicalBones=ds.PhysicalBones.Length};
            Check(Mathf.Abs((plain.mouth.localPosition.y-ds.mouth.localPosition.y)-ClaudeGirlFaceDetails.MouthDrop)<.00001f,"Mouth did not move by the specified amount");
            var eyeParent=new GameObject("Isolated eye topology audit");var parts=new System.Collections.Generic.List<GameObject>();
            foreach(int side in new[]{-1,1})ClaudeGirlFaceDetails.AddEye(eyeParent.transform,side,DollExpression.For("friendly"),parts);
            report.eyeWhites=parts.Count(p=>p.name=="Claude Eye white");report.pupils=parts.Count(p=>p.name=="Claude Eye pupil");report.lashes=parts.Count(p=>p.name=="Claude Eye lashes");
            Check(report.eyeWhites==2&&report.pupils==2&&report.lashes==2,"Eye topology duplicated or missing");
            report.eyeTriangles=parts.Sum(p=>p.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
            // The original two eyes contain three 192-triangle round parts per eye.
            report.additionalFaceTriangles=report.eyeTriangles-2*3*192;
            report.totalAddedAppearanceTriangles=report.additionalFaceTriangles+ds.root.GetComponentInChildren<ClaudeGirlOutfit>().skin.sharedMesh.triangles.Length/3;
            Check(report.totalAddedAppearanceTriangles<=12000,"Combined clothing and extra face geometry exceeds the budget");UnityEngine.Object.DestroyImmediate(eyeParent);
            var v=new Vector3[ds.Skin.sharedMesh.vertexCount];var n=new Vector3[v.Length];var t=new Vector3[v.Length];ds.Skin.sharedMesh.GetBlendShapeFrameVertices(3,0,v,n,t);
            var colors=ds.Skin.sharedMesh.colors;var iris=ToyFactory.ColorOf("#B88950");int irisVertices=0,moving=0;
            for(int i=0;i<colors.Length;i++)if(Mathf.Abs(colors[i].r-iris.r)+Mathf.Abs(colors[i].g-iris.g)+Mathf.Abs(colors[i].b-iris.b)<.001f){irisVertices++;if(v[i].sqrMagnitude>.000001f)moving++;}
            report.blinkMorphValid=irisVertices>0&&moving>irisVertices*.80f;Check(report.blinkMorphValid,"New iris is not included in the blink morph");
            ClaudeGirlModularTools.Capture(camera,Out+"/face-close.png",new Vector3(0,.92f,4),new Vector3(0,.92f,0),1000,1000);
            ds.Skin.SetBlendShapeWeight(3,100);ClaudeGirlModularTools.Capture(camera,Out+"/blink.png",new Vector3(0,.92f,4),new Vector3(0,.92f,0),1000,1000);ds.Skin.SetBlendShapeWeight(3,0);
            foreach(var expression in new[]{"friendly","joy","confident","sleepy","surprised","determined"})
            {
                ds.ApplyAppearance(new AppearanceData{character="claude",expression=expression});
                ClaudeGirlModularTools.Capture(camera,Out+"/expression-"+expression+".png",new Vector3(0,.92f,4),new Vector3(0,.92f,0),700,700);
            }
            ds.ApplyAppearance(new AppearanceData{character="claude"});
            for(int frame=0;frame<120;frame++)
            {
                float time=frame/60f;var player=new PublicPlayer{id="face",nickname="",position=Vector3.zero,appearance=new AppearanceData{character="claude"},characterSkin="claude-girl",connected=true,stability=100,interaction="eat",progress=.9f,simulationTime=time,actionKind="idle"};
                ds.DrawAt(player,camera,1/60f,time);player.appearance=new AppearanceData();player.characterSkin="normal";plain.DrawAt(player,camera,1/60f,time);plain.root.SetActive(false);
                report.maxFeedingHandDelta=Mathf.Max(report.maxFeedingHandDelta,Vector3.Distance(ds.RightHandAttachment.position,plain.RightHandAttachment.position));
            }
            report.fullyOpenMouthDelta=Vector3.Distance(ds.mouth.localPosition,plain.mouth.localPosition);
            Check(report.maxFeedingHandDelta<.0001f&&report.fullyOpenMouthDelta<.0001f,"Face styling changed the feeding envelope");
            File.WriteAllText(Out+"/face-validation.json",JsonUtility.ToJson(report,true));
            UnityEngine.Object.DestroyImmediate(ds.root);UnityEngine.Object.DestroyImmediate(plain.root);
            Debug.Log("CLAUDE_FACE_REVIEW_PASSED "+JsonUtility.ToJson(report));
        }
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
