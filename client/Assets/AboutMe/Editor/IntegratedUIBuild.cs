using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace AboutMe.Editor
{
    public static class IntegratedUIBuild
    {
        public static void Mac()
        {
            // The copied delivery snapshot already contains generated, validated scenes.
            var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=Array.ConvertAll(EditorBuildSettings.scenes,s=>s.path),
                locationPathName=Environment.GetEnvironmentVariable("ABOUTME_MAC_OUTPUT"),
                target=BuildTarget.StandaloneOSX,
                options=Environment.GetEnvironmentVariable("ABOUTME_RELEASE_BUILD")=="1"?BuildOptions.None:BuildOptions.Development});
            if(build.summary.result!=BuildResult.Succeeded)throw new Exception("Integrated UI Mac build failed");
        }
    }
}
