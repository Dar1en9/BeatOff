using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CommandLineBuild
{
    public static void Windows()
    {
        var previousBackend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone);
        try {
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/BasicScene.unity" },
            locationPathName = "Builds/Windows/VRTest.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Windows build failed: " + report.summary.result);
        } finally { PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, previousBackend); }
    }
}
