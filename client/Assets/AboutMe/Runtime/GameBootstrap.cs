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
            if(args.Contains("--bot")){go.AddComponent<NetworkProbe>();return;}
            if(args.Contains("--aboutme-server"))return;
            var world=new GameObject("Toy World").AddComponent<ToyWorld>();world.Build();
            world.gameObject.AddComponent<Diagnostics>();
            var ui=new GameObject("Game UI").AddComponent<GameUI>();ui.Build(network,world);
            int capture=Array.IndexOf(args,"--capture");if(capture>=0&&capture+1<args.Length)StartCoroutine(Capture(args[capture+1]));
        }
        IEnumerator Capture(string path){yield return new WaitForSeconds(4);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path);yield return new WaitForSeconds(2);Application.Quit();}
    }
}
