using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe.Editor
{
    public static class ButtonFlowTests
    {
        public static void Run()
        {
            var root=new GameObject("Button recovery test");root.SetActive(false);
            try
            {
                var net=root.AddComponent<GameNetwork>();var ui=root.AddComponent<GameUI>();
                var settings=new GameObject("Settings",typeof(RectTransform));settings.transform.SetParent(root.transform);
                var label=new GameObject("Recording label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(root.transform);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                void Set(string field,object value)=>typeof(GameUI).GetField(field,flags).SetValue(ui,value);
                Set("net",net);Set("settings",settings.GetComponent<RectTransform>());
                var profile=GameNetwork.SampleProfile("cached-test","Cached");Set("profile",profile);net.Sample=false;net.Token="";
                typeof(GameUI).GetMethod("Enter",flags).Invoke(ui,new object[]{""});
                if(!net.Status.Contains("登录已过期")||typeof(GameUI).GetField("profile",flags).GetValue(ui)!=profile)throw new Exception("Expired cache attempted online entry or lost offline profile");
                Set("recordLabel",label);Set("recording",true);label.text="停止录音";ui.ResetRecordingButton();
                if((bool)typeof(GameUI).GetField("recording",flags).GetValue(ui)||label.text!="录音介绍")throw new Exception("Recording failure did not restore its button");
                var toast=new GameObject("Toast",typeof(RectTransform));toast.transform.SetParent(root.transform);
                var cover=new GameObject("Wardrobe cover",typeof(RectTransform));cover.transform.SetParent(root.transform);
                Set("statusPill",toast.GetComponent<RectTransform>());
                typeof(GameUI).GetMethod("PositionStatus",flags).Invoke(ui,new object[]{null});
                if(toast.transform.GetSiblingIndex()!=root.transform.childCount-1)throw new Exception("Status feedback was hidden behind a modal");
                Debug.Log("ABOUTME_BUTTON_RECOVERY_TESTS_PASSED expired-cache guard / offline retained / recording reset / visible modal feedback");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        public static void BuildDeliverables(){Run();UniversityCrestTests.BuildDeliverables();}
    }
}
