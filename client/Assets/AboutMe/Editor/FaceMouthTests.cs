using System;
using System.Linq;
using UnityEngine;
namespace AboutMe.Editor
{
    public static class FaceMouthTests
    {
        public static void Run()
        {
            int passed=0;
            void Check(bool ok,string reason){if(!ok)throw new Exception("FACE_MOUTH_FAILED "+reason);passed++;}
            foreach(string character in new[]{"normal"}.Concat(AISkinRegistry.All.Select(x=>x.character)))
            {
                ToyActor actor=null;
                try
                {
                    foreach(string expression in new[]{"friendly","joy","confident","sleepy","surprised","determined"})
                    {
                        var appearance=new AppearanceData{character=character,expression=expression};
                        if(actor==null)actor=new ToyActor("Face check",appearance.topColor,0,null,appearance);else actor.ApplyAppearance(appearance);
                        bool normal=character=="normal";
                        Check(actor.mouth.gameObject.activeSelf==normal,character+" idle mouth visibility "+expression);
                        Check(!actor.mouth.Find("Mouth cavity").gameObject.activeSelf&&!actor.mouth.Find("Skin lip rim").gameObject.activeSelf,character+" idle aperture closed "+expression);
                        Check(actor.mouth.Find("Idle smile").gameObject.activeSelf==normal,character+" smile policy "+expression);
                        if(normal)Check(actor.mouth.Find("Idle smile").GetComponent<MeshFilter>().sharedMesh.name=="Expression smile 0.22","ordinary expressions retain the same smile");
                    }
                    var state=new PublicPlayer{characterSkin=AISkinRegistry.SkinForCharacter(character),appearance=actor.Appearance.Copy(),position=new Vector3(0,1,0),stability=100,connected=true,interaction="eat",progress=1};
                    for(int i=0;i<60;i++)actor.DrawAt(state,null,1/60f,i/60f);
                    Check(actor.MouthOpen>.95f&&actor.mouth.gameObject.activeSelf&&actor.mouth.Find("Mouth cavity").gameObject.activeSelf,character+" retains giant eating aperture");
                    Check(!actor.mouth.Find("Idle smile").gameObject.activeSelf&&actor.Skin.GetBlendShapeWeight(0)>95,character+" giant jaw deformation retained");
                    state.interaction="";state.progress=0;
                    for(int i=0;i<60;i++)actor.DrawAt(state,null,1/60f,1+i/60f);
                    Check(actor.MouthOpen<.01f&&actor.mouth.gameObject.activeSelf==(character=="normal"),character+" returns to idle after eating");
                }
                finally{if(actor!=null)UnityEngine.Object.DestroyImmediate(actor.root);}
            }
            Debug.Log("FACE_MOUTH_PASSED "+passed);
        }
    }
}
