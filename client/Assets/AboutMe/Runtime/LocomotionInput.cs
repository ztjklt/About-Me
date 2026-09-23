using UnityEngine;
namespace AboutMe
{
    // Input intent only. W/A/S/D own independent double-tap clocks; authority owns speed.
    public sealed class LocomotionInput
    {
        public const float DoubleTapWindow=.28f, TouchHold=.18f;
        readonly float[] lastPress={-10,-10,-10,-10};
        int runKey;float touchTime;bool facingBack;
        public bool Running {get;private set;}
        public void Reset(){for(int i=0;i<4;i++)lastPress[i]=-10;runKey=0;touchTime=0;Running=false;facingBack=false;}
        public float FacingYaw(float cameraYaw,Vector2 localMovement)
        {
            // Keep the last travel-facing direction at rest. In particular, releasing S
            // must not turn the face away before the player can see the completed turn.
            if(localMovement.y<-.01f)facingBack=true;
            else if(localMovement.y>.01f||Mathf.Abs(localMovement.x)>.01f)facingBack=false;
            return cameraYaw+(facingBack?180:0);
        }
        public bool Step(float now,float dt,bool wDown,bool wHeld,Vector2 stick,bool touch,bool enabled)
            =>Step(now,dt,wDown?1:0,wHeld?1:0,stick,touch,enabled);
        public bool Step(float now,float dt,int downMask,int heldMask,Vector2 stick,bool touch,bool enabled)
        {
            if(!enabled){Reset();return false;}
            if(touch)
            {
                runKey=0;for(int i=0;i<4;i++)lastPress[i]=-10;
                if(stick.y<.70f||stick.magnitude<.78f){touchTime=0;Running=false;}
                else if(stick.y>=.85f&&stick.magnitude>=.90f){touchTime+=dt;if(touchTime>=TouchHold)Running=true;}
                else if(!Running)touchTime=0;
                return Running;
            }
            touchTime=0;
            bool conflict=(heldMask&5)==5||(heldMask&10)==10;
            if(conflict){Reset();return false;}
            for(int i=0;i<4;i++)if((downMask&(1<<i))!=0)
            {if(now-lastPress[i]<=DoubleTapWindow)runKey=1<<i;lastPress[i]=now;}
            if((heldMask&runKey)==0)runKey=0;
            Running=runKey!=0&&stick.sqrMagnitude>.01f;return Running;
        }
    }
}
