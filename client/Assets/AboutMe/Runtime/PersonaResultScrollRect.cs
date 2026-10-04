using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AboutMe
{
    // Horizontal trackpad deltas stay horizontal; a downward mouse wheel reveals later cards.
    public sealed class PersonaResultScrollRect : ScrollRect
    {
        public override void OnScroll(PointerEventData data)
        {
            var delta=data.scrollDelta;
            try
            {
                data.scrollDelta=new Vector2(Mathf.Abs(delta.y)>Mathf.Abs(delta.x)?delta.y:delta.x,0);
                base.OnScroll(data);
            }
            finally { data.scrollDelta=delta; }
        }
    }
}
