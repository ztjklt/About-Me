using System;
using System.Linq;
using System.Collections;
using Mirror.SimpleWeb;
using UnityEngine;

namespace AboutMe
{
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;UnityEngine.Time.fixedDeltaTime=.02f;
            var go=new GameObject("AboutMe Network");go.AddComponent<SimpleWebTransport>();var network=go.AddComponent<GameNetwork>();
            var args=Environment.GetCommandLineArgs();
            if(args.Contains("--character-motion-probe")){go.AddComponent<CharacterMotionProbe>();return;}
            if(args.Contains("--bot")){go.AddComponent<NetworkProbe>();return;}
            if(args.Contains("--aboutme-server"))return;
            var world=new GameObject("Toy World").AddComponent<ToyWorld>();world.Build();
            if(args.Contains("--body-physics-probe")){go.AddComponent<BodyPhysicsRuntimeProbe>();return;}
            world.gameObject.AddComponent<Diagnostics>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(args.Contains("--input-audit"))world.gameObject.AddComponent<CombatInputAudit>();
#endif
            var ui=new GameObject("Game UI").AddComponent<GameUI>();ui.Build(network,world);
            if(DeskBackdropAudit.Requested){go.AddComponent<DeskBackdropAudit>();return;}
            if(args.Contains("--schedule-audit"))go.AddComponent<ScheduleAudit>();
            int captureSize=Array.IndexOf(args,"--capture-size");
            if(captureSize>=0&&captureSize+1<args.Length)
            {
                var parts=args[captureSize+1].ToLowerInvariant().Split('x');
                if(parts.Length==2&&int.TryParse(parts[0],out var width)&&int.TryParse(parts[1],out var height)&&width>=640&&height>=360)Screen.SetResolution(width,height,false);
            }
            int capture=Array.IndexOf(args,"--capture");if(capture>=0&&capture+1<args.Length)StartCoroutine(Capture(args[capture+1]));
        }
        IEnumerator Capture(string path){yield return new WaitForSeconds(4);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path);yield return new WaitForSeconds(2);Application.Quit();}
    }
}
