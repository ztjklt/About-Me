using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace AboutMe
{
    public partial class GameUI
    {
        readonly Dictionary<string,Button> expressionChoices=new Dictionary<string,Button>();
        void BuildExpressionChoices(RectTransform parent)
        {
            var choices=AppearanceCatalog.Data.slots.First(s=>s.id=="expression").options;
            for(int i=0;i<choices.Length;i++)
            {
                string id=choices[i].id;
                var button=ArtButton(parent,choices[i].label.Substring(0,2),30+i*99,529,84,36,()=>{if(busy)return;appearanceDraft.expression=id;appearanceDirty=true;RefreshAppearancePreview();net.Status="表情已选："+choices.First(o=>o.id==id).label+"。可在衣橱预览并保存。";},UITheme.Field,UITheme.Ink);
                button.gameObject.name="Expression · "+id;expressionChoices[id]=button;
            }
            RefreshExpressionChoices();
        }
        void RefreshExpressionChoices()
        {
            foreach(var pair in expressionChoices)
            {var style=pair.Value.GetComponent<StickerButton>();if(style)style.SetState(pair.Key==appearanceDraft.expression?UIVisualState.Selected:UIVisualState.Normal);}
        }
    }
}
