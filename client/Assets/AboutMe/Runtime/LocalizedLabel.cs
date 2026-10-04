using UnityEngine;
using UnityEngine.UI;

namespace AboutMe
{
    // The original source is retained so switching languages never changes game data.
    public sealed class LocalizedLabel : MonoBehaviour
    {
        Text ui;TextMesh mesh;
        string source,lastRendered;
        public void Configure(Text value){ui=value;source=value.text;lastRendered=value.text;}
        public void Configure(TextMesh value){mesh=value;source=value.text;lastRendered=value.text;}
        void OnEnable(){GameLocalization.Register(this);Canvas.willRenderCanvases+=Refresh;}
        void OnDisable(){GameLocalization.Unregister(this);Canvas.willRenderCanvases-=Refresh;}
        void LateUpdate(){if(mesh)Refresh();}
        public void Refresh()
        {
            if(!enabled||(!ui&&!mesh))return;
            string current=ui?ui.text:mesh.text;
            if(current!=lastRendered)source=current;
            lastRendered=GameLocalization.Text(source);
            if(current==lastRendered)return;
            if(ui)ui.text=lastRendered;else mesh.text=lastRendered;
        }
    }
}
