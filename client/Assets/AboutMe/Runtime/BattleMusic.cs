using UnityEngine;

namespace AboutMe
{
    /// <summary>Local, non-spatial music that follows the authoritative match phase.</summary>
    public sealed class BattleMusic : MonoBehaviour
    {
        const string VolumeKey="battleMusicVolume";
        AudioSource source;
        string matchId="";
        float targetVolume;

        public static float SavedVolume=>Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,.30f));
        public static bool PlaysIn(MatchPhase phase)=>phase==MatchPhase.Battle||phase==MatchPhase.FinalBurst;

        void Awake()
        {
            var clip=Resources.Load<AudioClip>("Audio/BattleMusic");
            if(!clip){Debug.LogError("Battle music asset is missing");enabled=false;return;}
            source=gameObject.AddComponent<AudioSource>();
            source.clip=clip;source.loop=true;source.playOnAwake=false;
            source.spatialBlend=0;source.volume=0;
        }

        public void Sync(Snapshot state,bool connected)
        {
            if(!source)return;
            bool active=connected&&state!=null&&PlaysIn(state.phase)&&SavedVolume>.001f;
            if(active&&(!source.isPlaying||matchId!=state.matchId))
            {
                matchId=state.matchId;
                source.Stop();source.volume=0;source.Play();
            }
            targetVolume=active?SavedVolume:0;
        }

        void Update()
        {
            if(!source)return;
            source.volume=Mathf.MoveTowards(source.volume,targetVolume,Time.unscaledDeltaTime*.45f);
            if(targetVolume<=0&&source.volume<=.001f&&source.isPlaying)source.Stop();
        }

        void OnDisable(){if(source)source.Stop();}
    }
}
