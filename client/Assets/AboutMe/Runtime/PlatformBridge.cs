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
