using UnityEditor;
using UnityEngine;

namespace AboutMe.Editor
{
    public sealed class StartMenuAssetImporter : AssetPostprocessor
    {
        const string Folder="Assets/AboutMe/Resources/StartMenu2D/";

        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Folder,System.StringComparison.Ordinal))return;
            Configure((TextureImporter)assetImporter);
        }

        static void Configure(TextureImporter importer)
        {
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;
            importer.sRGBTexture=true;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;
            importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Compressed;
            importer.compressionQuality=72;
        }

        [MenuItem("About Me/Prepare start menu 2.5D assets")]
        public static void ApplyAll()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Folder.TrimEnd('/')}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)continue;
                Configure(importer);importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();Debug.Log("ABOUTME_START_MENU_ASSETS_READY");
        }
    }
}
