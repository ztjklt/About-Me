using System;
using System.Linq;
using UnityEngine;

namespace AboutMe
{
    // Opt-in development instrumentation. Reads the real input path; never injects commands or alters the match.
    public sealed class CombatInputAudit : MonoBehaviour
    {
        public static bool Enabled;
        float next;
        void Awake(){Enabled=true;}
        void OnDestroy(){Enabled=false;}
        void Update()
        {
            if(Input.GetMouseButtonDown(0))Debug.Log("COMBAT_POINTER_AUDIT position="+Input.mousePosition+" screen="+Screen.width+"x"+Screen.height+" selected="+(UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name??"none"));
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
            var state=GameNetwork.Game?.State;var me=state?.players?.FirstOrDefault(p=>p.id==state.you);
            if(me==null)return;
            Debug.Log("COMBAT_INPUT_AUDIT "+JsonUtility.ToJson(new Frame{
                time=Time.unscaledTime,position=me.position,velocity=me.velocity,yaw=me.yaw,cameraYaw=Camera.main?Camera.main.transform.eulerAngles.y:0,
                mouseX=Input.GetAxis("Mouse X"),mouseY=Input.GetAxis("Mouse Y"),cursor=Cursor.lockState.ToString(),action=me.actionKind,actionId=me.actionId,
                heldW=Input.GetKey(KeyCode.W),heldA=Input.GetKey(KeyCode.A),heldS=Input.GetKey(KeyCode.S),heldD=Input.GetKey(KeyCode.D),running=me.running,grabbed=me.grabbed,escape=me.escapeProgress}));
        }
        [Serializable] class Frame
        {
            public float time,yaw,cameraYaw,mouseX,mouseY,escape;
            public Vector3 position,velocity;public string cursor,action;public int actionId;
            public bool heldW,heldA,heldS,heldD,running,grabbed;
        }
    }
}
