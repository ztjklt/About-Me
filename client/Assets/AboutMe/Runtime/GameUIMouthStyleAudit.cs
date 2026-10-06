using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace AboutMe
{
    public partial class GameUI
    {
        IEnumerator AuditMouthStyle(string output)
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            // Explicit local UI fixture. No account save, model generation or player entitlement change.
            net.Sample=false;net.Token="";profile=GameNetwork.SampleProfile("mouth-style-preview","嘴型预览");
            profile.unlockedCharacterSkins=AISkinRegistry.All.Select(x=>x.skin).ToArray();profile.dsGirlUnlocked=true;profile.claudeGirlUnlocked=true;
            foreach(string skin in new[]{"claude-girl","normal"})
            {
                profile.characterSkin=skin;profile.appearance=new AppearanceData{expression="joy"};appearanceDirty=false;LoadAppearance();OpenWardrobe();
                yield return null;
                bool normal=skin=="normal";
                if(wardrobeActor.mouth.GetComponentsInChildren<Renderer>(true).Any(r=>(wardrobeCamera.cullingMask&(1<<r.gameObject.layer))==0))
                {Debug.LogError("MOUTH_STYLE_AUDIT_FAILED mouth excluded from wardrobe camera "+skin);Application.Quit(23);yield break;}
                if(wardrobeActor.mouth.gameObject.activeSelf!=normal||wardrobeActor.mouth.Find("Mouth cavity").gameObject.activeSelf)
                {Debug.LogError("MOUTH_STYLE_AUDIT_FAILED idle "+skin);Application.Quit(21);yield break;}
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,skin+"-wardrobe.png"));
                yield return new WaitForSecondsRealtime(.2f);SaveMouthPortrait(Path.Combine(output,skin+"-idle-face.png"));
                var state=new PublicPlayer{position=wardrobeActor.root.transform.position,yaw=0,characterSkin=skin,appearance=profile.appearance,stability=100,connected=true,interaction="eat",progress=1};
                for(int i=0;i<60;i++)wardrobeActor.DrawAt(state,null,1/60f,i/60f);
                if(wardrobeActor.MouthOpen<.95f||!wardrobeActor.mouth.gameObject.activeSelf||!wardrobeActor.mouth.Find("Mouth cavity").gameObject.activeSelf)
                {Debug.LogError("MOUTH_STYLE_AUDIT_FAILED eating "+skin);Application.Quit(22);yield break;}
                yield return new WaitForEndOfFrame();SaveMouthPortrait(Path.Combine(output,skin+"-eating-face.png"));
                state.interaction="";state.progress=0;
                for(int i=0;i<60;i++)wardrobeActor.DrawAt(state,null,1/60f,1+i/60f);
                CloseWardrobe();
            }
            File.WriteAllText(Path.Combine(output,"passed.txt"),"Explicit wardrobe fixture: Claude idle no mouth; normal joy idle closed smile; both retain giant eating mouth.");
            Debug.Log("MOUTH_STYLE_AUDIT_PASSED source=explicit-ui-fixture");yield return new WaitForSecondsRealtime(.5f);Application.Quit();
        }
        void SaveMouthPortrait(string path)
        {
            var position=wardrobeCamera.transform.position;var rotation=wardrobeCamera.transform.rotation;float scale=wardrobeCamera.orthographicSize;
            var target=wardrobeActor.head.position;wardrobeCamera.transform.position=target+wardrobeActor.head.forward*4;wardrobeCamera.transform.LookAt(target,wardrobeActor.head.up);wardrobeCamera.orthographicSize=.85f;wardrobeCamera.Render();
            var previous=RenderTexture.active;RenderTexture.active=wardrobeTexture;
            var texture=new Texture2D(wardrobeTexture.width,wardrobeTexture.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);
            RenderTexture.active=previous;wardrobeCamera.transform.SetPositionAndRotation(position,rotation);wardrobeCamera.orthographicSize=scale;
        }
    }
}
