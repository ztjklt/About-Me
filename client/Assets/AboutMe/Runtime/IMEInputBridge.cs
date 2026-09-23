using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AboutMe
{
    /// <summary>Keeps native IME composition enabled and supplies an HTML/WX text input for WebGL builds.</summary>
    public class IMEInputBridge : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public InputField Field;
        bool selected, closing;

#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void AM_InputOpen(string receiver,string value,string placeholder,int multiline,int maxLength,float x,float y,float width,float height);
        [DllImport("__Internal")] static extern void AM_InputClose(string receiver);
        [DllImport("__Internal")] static extern string AM_InputValue(string receiver,string fallback);
        [DllImport("__Internal")] static extern void AM_InputStore(string receiver,string value);
#endif

        public void Configure(InputField field)
        {
            Field=field;
            gameObject.name="AboutMeInput-"+GetInstanceID();
        }

        public string CurrentText()
        {
            if(Field==null)return "";
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            return AM_InputValue(gameObject.name,Field.text);
#else
            return Field.text;
#endif
        }

        public void SetText(string value)
        {
            if(Field==null)return;
            Field.text=value??"";
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            AM_InputStore(gameObject.name,Field.text);
#endif
        }

        public void OnSelect(BaseEventData eventData)
        {
            if(Field==null)return;
            selected=true;closing=false;
            Input.imeCompositionMode=IMECompositionMode.On;
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            var corners=new Vector3[4];((RectTransform)transform).GetWorldCorners(corners);
            var lower=RectTransformUtility.WorldToScreenPoint(null,corners[0]);
            var upper=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
            var hint=Field.placeholder as Text;
            AM_InputOpen(gameObject.name,Field.text,hint?hint.text:"输入",Field.lineType==InputField.LineType.SingleLine?0:1,Field.characterLimit,
                lower.x/Screen.width,lower.y/Screen.height,(upper.x-lower.x)/Screen.width,(upper.y-lower.y)/Screen.height);
#endif
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected=false;
            Input.imeCompositionMode=IMECompositionMode.Auto;
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            if(!closing)AM_InputClose(gameObject.name);
#endif
        }

        void LateUpdate()
        {
            if(!selected||Field==null)return;
            var textRect=Field.textComponent.rectTransform;
            var position=RectTransformUtility.WorldToScreenPoint(null,textRect.TransformPoint(new Vector3(0,textRect.rect.yMin,0)));
            Input.compositionCursorPos=new Vector2(position.x,Screen.height-position.y);
        }

        public void OnBrowserInput(string value)
        {
            if(Field==null)return;
            Field.text=value??"";Field.caretPosition=Field.text.Length;
        }

        public void OnBrowserBlur(string value)
        {
            OnBrowserInput(value);closing=true;selected=false;
            Input.imeCompositionMode=IMECompositionMode.Auto;
            Field.DeactivateInputField();
            if(EventSystem.current&&EventSystem.current.currentSelectedGameObject==gameObject)EventSystem.current.SetSelectedGameObject(null);
            closing=false;
        }

        void OnDisable()
        {
            if(!selected)return;selected=false;
#if (UNITY_WEBGL || UNITY_WEIXINMINIGAME || UNITY_MINIGAME || PLATFORM_WEIXINMINIGAME) && !UNITY_EDITOR
            AM_InputClose(gameObject.name);
#endif
        }
    }
}
