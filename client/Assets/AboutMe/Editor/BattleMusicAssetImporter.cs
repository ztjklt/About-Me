using UnityEditor;
using UnityEngine;

namespace AboutMe.Editor
{
    public sealed class BattleMusicAssetImporter : AssetPostprocessor
    {
        const string Path="Assets/AboutMe/Resources/Audio/BattleMusic.mp3";

        void OnPreprocessAudio()
        {
            if(assetPath!=Path)return;
            var importer=(AudioImporter)assetImporter;
            var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=.72f;
            settings.preloadAudioData=false;
            importer.defaultSampleSettings=settings;
            importer.loadInBackground=true;
            importer.forceToMono=false;
        }
    }
}
