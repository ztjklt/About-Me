using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AboutMe.Editor
{
    public static class StartMenuStaticAudit
    {
        [MenuItem("About Me/Test anchored start-menu mascot")]
        public static void Run()
        {
            var root=new GameObject("start-menu-static-audit",typeof(RectTransform),typeof(CanvasGroup));
            try
            {
                RectTransform Rect(string name,Vector2 position)
                {
                    var child=new GameObject(name,typeof(RectTransform));child.transform.SetParent(root.transform,false);
                    var rect=(RectTransform)child.transform;rect.anchoredPosition=position;rect.sizeDelta=new Vector2(100,100);return rect;
                }
                var title=Rect("title",new Vector2(10,20));
                var hero=Rect("hero",new Vector2(50,-80));
                hero.localRotation=Quaternion.Euler(0,0,5);hero.localScale=new Vector3(1.2f,1.1f,1);
                var doodles=Rect("doodles",new Vector2(30,40));
                var arm=Rect("arm",new Vector2(-20,10));
                var open=Rect("open",Vector2.zero).gameObject.AddComponent<Image>();
                var blink=Rect("blink",Vector2.zero).gameObject.AddComponent<Image>();blink.enabled=false;
                var sheet=Rect("sheet",Vector2.zero);var sheetGroup=sheet.gameObject.AddComponent<CanvasGroup>();
                var hint=Rect("hint",Vector2.zero).gameObject.AddComponent<CanvasGroup>();
                var dimmer=Rect("dimmer",Vector2.zero).gameObject.AddComponent<Image>();
                var buttons=new[]{Rect("button0",Vector2.zero),Rect("button1",Vector2.zero),Rect("button2",Vector2.zero),Rect("button3",Vector2.zero)};
                var stage=root.AddComponent<StartMenuStage2D>();
                stage.Configure(title,hero,open,blink,doodles,new[]{arm},sheet,sheetGroup,hint,dimmer,buttons);
                var heroPosition=hero.anchoredPosition;var heroRotation=hero.localRotation;var heroScale=hero.localScale;
                var titlePosition=title.anchoredPosition;var doodlePosition=doodles.anchoredPosition;var armPosition=arm.anchoredPosition;
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(StartMenuStage2D).GetField("pointer",flags).SetValue(stage,new Vector2(.7f,.4f));
                typeof(StartMenuStage2D).GetField("time",flags).SetValue(stage,1f);
                typeof(StartMenuStage2D).GetField("nextBlink",flags).SetValue(stage,-1f);
                typeof(StartMenuStage2D).GetMethod("Update",flags).Invoke(stage,null);
                if(hero.anchoredPosition!=heroPosition||hero.localRotation!=heroRotation||hero.localScale!=heroScale)
                    throw new Exception("START_MENU_STATIC_FAILED mascot moved");
                if(title.anchoredPosition==titlePosition||doodles.anchoredPosition==doodlePosition||arm.anchoredPosition==armPosition)
                    throw new Exception("START_MENU_STATIC_FAILED surrounding layers stopped");
                if(!blink.enabled||open.enabled)throw new Exception("START_MENU_STATIC_FAILED blink stopped");
                stage.SetExpanded(true,true);stage.SetMotionEnabled(false);
                if(hero.anchoredPosition!=heroPosition||hero.localRotation!=heroRotation||hero.localScale!=heroScale||arm.anchoredPosition!=armPosition)
                    throw new Exception("START_MENU_STATIC_FAILED rest pose changed");
                Debug.Log("START_MENU_STATIC_AUDIT_PASSED mascot fixed; title doodles arm move; blink and reset work");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
