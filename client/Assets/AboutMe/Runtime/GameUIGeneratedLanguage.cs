using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AboutMe
{
    [Serializable] class DisplayLanguageBody {public string text;}
    [Serializable] class DisplayLanguageReply {public string text;}
    public partial class GameUI
    {
        readonly HashSet<string> pendingTranslations=new HashSet<string>();
        void SetGeneratedDisplay(Text label,string value)
        {
            label.text=value;
            if(string.IsNullOrEmpty(value)||value.Length>600||string.IsNullOrEmpty(net.Token)||net.Sample)return;
            string localized=GameLocalization.Text(value,true);
            if(!localized.Any(c=>c>='\u4e00'&&c<='\u9fff'))return;
            if(!pendingTranslations.Add(value))return;
            GameLocalization.AddTranslation(value,"Preparing English commentary…");
            StartCoroutine(TranslateGeneratedDisplay(value));
        }
        IEnumerator TranslateGeneratedDisplay(string value)
        {
            using(var req=GameNetwork.ApiRequest(net.ApiUrl+"/display-language",JsonUtility.ToJson(new DisplayLanguageBody{text=value}),net.Token))
            {
                req.timeout=25;yield return req.SendWebRequest();
                if(req.result!=UnityWebRequest.Result.Success){GameLocalization.AddTranslation(value,"English commentary unavailable. Please try again later.");pendingTranslations.Remove(value);yield break;}
                DisplayLanguageReply reply=null;try{reply=JsonUtility.FromJson<DisplayLanguageReply>(req.downloadHandler.text);}catch(Exception){}
                if(!string.IsNullOrEmpty(reply?.text))GameLocalization.AddTranslation(value,reply.text);
            }
        }
    }
}
