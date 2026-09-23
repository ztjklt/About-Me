using UnityEngine;

namespace AboutMe
{
    /// <summary>Moves only the selection surface; tab positions and hit areas stay fixed.</summary>
    public sealed class UILiquidTabIndicator : MonoBehaviour
    {
        RectTransform indicator;
        RectTransform[] tabs;
        float left,right,leftVelocity,rightVelocity;
        int selected,direction=1;

        public void Configure(RectTransform selectionSurface,RectTransform[] tabRects)
        {
            indicator=selectionSurface;tabs=tabRects;
            selected=0;Snap();
        }

        public void SetSelected(int index,bool immediate=false)
        {
            if(tabs==null||tabs.Length==0)return;
            index=Mathf.Clamp(index,0,tabs.Length-1);
            float nextCenter=TabLeft(index)+tabs[index].rect.width*.5f;
            direction=nextCenter>=(left+right)*.5f?1:-1;
            selected=index;
            if(immediate||PlayerPrefs.GetInt("reduceMotion",0)==1)Snap();
        }

        void LateUpdate()
        {
            if(!indicator||tabs==null||tabs.Length==0)return;
            if(PlayerPrefs.GetInt("reduceMotion",0)==1){Snap();return;}
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            float targetLeft=TabLeft(selected),targetRight=targetLeft+tabs[selected].rect.width;
            // The leading edge arrives first. The trailing edge catches up without moving the labels.
            left=Mathf.SmoothDamp(left,targetLeft,ref leftVelocity,direction>0?.19f:.10f,Mathf.Infinity,dt);
            right=Mathf.SmoothDamp(right,targetRight,ref rightVelocity,direction>0?.10f:.19f,Mathf.Infinity,dt);
            float maxWidth=tabs[selected].rect.width+64f;
            if(right-left>maxWidth){if(direction>0)left=right-maxWidth;else right=left+maxWidth;}
            if(right-left<72f){float center=(left+right)*.5f;left=center-36f;right=center+36f;}
            Draw();
        }

        float TabLeft(int index)=>tabs[index].anchoredPosition.x;

        void Snap()
        {
            if(!indicator||tabs==null||tabs.Length==0)return;
            left=TabLeft(selected);right=left+tabs[selected].rect.width;
            leftVelocity=rightVelocity=0;Draw();
        }

        void Draw()
        {
            indicator.anchoredPosition=new Vector2(left,indicator.anchoredPosition.y);
            indicator.sizeDelta=new Vector2(right-left,indicator.sizeDelta.y);
        }
    }
}
