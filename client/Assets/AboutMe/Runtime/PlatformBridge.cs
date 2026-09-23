using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AboutMe
{
    public class PlatformBridge : MonoBehaviour
    {
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int AM_Init(string receiver);
        [DllImport("__Internal")] static extern void AM_Share(string room);
        [DllImport("__Internal")] static extern void AM_RecordStart(string receiver);
        [DllImport("__Internal")] static extern void AM_RecordStop();
        [DllImport("__Internal")] static extern void AM_Screenshot();
        [DllImport("__Internal")] static extern void AM_Login(string receiver);
        [DllImport("__Internal")] static extern void AM_PickIntroductionImage(string receiver);
#endif
        public bool IsWeChat;
        AudioClip recording;string microphone;
        public void Initialize()
        {
            gameObject.name="PlatformBridge";
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            IsWeChat=AM_Init(gameObject.name)==1;
            if(IsWeChat&&GameNetwork.Game.Token=="")AM_Login(gameObject.name);
#endif
        }
        public void OnLaunchRoom(string room){if(!string.IsNullOrWhiteSpace(room))GameUI.UI.SetRoom(room);}
        public void OnWeChatLogin(string code){GameUI.UI.WechatLogin(code);}
        public void Share(string room)
        {
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            AM_Share(room);
#else
            GUIUtility.systemCopyBuffer=room;GameNetwork.Game.Status="房间码已复制："+room;
#endif
        }
        public void StartRecording()
        {
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            AM_RecordStart(gameObject.name);
#else
            StartCoroutine(RecordDesktop());
#endif
        }
#if (!UNITY_WEBGL && !UNITY_WEIXINMINIGAME && !UNITY_MINIGAME && !PLATFORM_WEIXINMINIGAME) || UNITY_EDITOR
        IEnumerator RecordDesktop()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone)||Microphone.devices.Length==0){OnPlatformError("没有麦克风权限或可用设备");yield break;}
            microphone=Microphone.devices[0];recording=Microphone.Start(microphone,false,60,16000);
        }
#endif
        public void StopRecording()
        {
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            AM_RecordStop();
#else
            if(recording==null)return;
            int count=Microphone.GetPosition(microphone);Microphone.End(microphone);
            if(count<=0)count=recording.samples;
            float[] samples=new float[count*recording.channels];recording.GetData(samples,0);
            using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)recording.channels);writer.Write(recording.frequency);writer.Write(recording.frequency*recording.channels*2);writer.Write((short)(recording.channels*2));writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
                foreach(var sample in samples)writer.Write((short)(Mathf.Clamp(sample,-1,1)*32767));
                GameUI.UI.Speech(memory.ToArray(),"wav");
            }
            Destroy(recording);recording=null;
#endif
        }
        public void OnRecorded(string encoded)
        {
            try{int split=encoded.IndexOf(':');GameUI.UI.Speech(Convert.FromBase64String(encoded.Substring(split+1)),encoded.Substring(0,split));}
            catch(Exception){OnPlatformError("录音数据不可用");}
        }
        public void PickIntroductionImage()
        {
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            AM_PickIntroductionImage(gameObject.name);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            StartCoroutine(PickIntroductionImageMac());
#else
            OnPlatformError("当前平台暂不支持图片选择，请使用 WebGL 或 macOS 客户端。");
#endif
        }
#if (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX) && (!UNITY_WEBGL || UNITY_EDITOR)
        IEnumerator PickIntroductionImageMac()
        {
            var start=new System.Diagnostics.ProcessStartInfo("/usr/bin/osascript", "-e \"POSIX path of (choose file with prompt \\\"选择自我介绍图片\\\" of type {\\\"public.image\\\"})\"")
            {UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            System.Diagnostics.Process process;
            try{process=System.Diagnostics.Process.Start(start);}catch(Exception){OnPlatformError("无法打开图片选择器");yield break;}
            while(!process.HasExited)yield return null;
            string path=process.StandardOutput.ReadToEnd().Trim();
            if(process.ExitCode!=0||string.IsNullOrWhiteSpace(path))yield break;
            try
            {
                var data=File.ReadAllBytes(path);var mime=ImageMime(path);if(string.IsNullOrEmpty(mime)){OnPlatformError("仅支持 JPEG、PNG、GIF 或 WebP 图片");yield break;}
                GameUI.UI.IntroductionImage(data,mime);
            }
            catch(Exception){OnPlatformError("无法读取所选图片");}
        }
        static string ImageMime(string path)
        {
            switch(Path.GetExtension(path).ToLowerInvariant()){case ".jpg":case ".jpeg":return "image/jpeg";case ".png":return "image/png";case ".gif":return "image/gif";case ".webp":return "image/webp";default:return "";}
        }
#endif
        public void OnIntroductionImage(string encoded)
        {
            try
            {
                int split=encoded.IndexOf(':');if(split<=0)throw new FormatException();
                GameUI.UI.IntroductionImage(Convert.FromBase64String(encoded.Substring(split+1)),encoded.Substring(0,split));
            }
            catch(Exception){OnPlatformError("图片数据不可用，请重新选择");}
        }
        public void OnPlatformError(string message){GameNetwork.Game.Status=message;}
        public void Screenshot()
        {
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            AM_Screenshot();
#else
            string path=Path.Combine(Application.persistentDataPath,"AboutMe-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png");
            ScreenCapture.CaptureScreenshot(path);GameNetwork.Game.Status="结算截图保存到："+path;
#endif
        }
    }
}
