using UnityEngine;
namespace AboutMe
{
    // All faces share the same head, jaw and feeding envelope.
    public struct DollExpression
    {
        public float eyeHeight,eyeWidth,pupil,browAngle,browLift,asymmetry,mouthWidth,mouthHeight,mouthTilt,smile;
        public static DollExpression For(string id)
        {
            var f=new DollExpression{eyeHeight=1,eyeWidth=1,pupil=1,mouthWidth=.25f,mouthHeight=.075f,smile=.22f};
            switch(id)
            {
                case "joy":f.eyeHeight=.42f;f.browLift=.045f;f.browAngle=12;f.mouthWidth=.36f;f.mouthHeight=.19f;f.smile=.32f;break;
                case "confident":f.eyeHeight=.80f;f.asymmetry=.055f;f.browAngle=12;f.mouthTilt=-12;f.mouthWidth=.28f;break;
                case "sleepy":f.eyeHeight=.36f;f.pupil=.85f;f.browLift=-.045f;f.browAngle=-5;f.mouthWidth=.17f;f.mouthHeight=.04f;f.smile=0;break;
                case "surprised":f.eyeHeight=1.2f;f.eyeWidth=1.12f;f.pupil=.82f;f.browLift=.09f;f.mouthWidth=.17f;f.mouthHeight=.21f;f.smile=0;break;
                case "determined":f.eyeHeight=.72f;f.browAngle=-23;f.browLift=-.025f;f.mouthWidth=.24f;f.mouthHeight=.045f;f.smile=-.13f;break;
            }
            return f;
        }
    }
}
