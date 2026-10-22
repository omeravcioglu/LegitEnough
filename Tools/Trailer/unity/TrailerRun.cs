#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LegitEnoughCI
{
    /// <summary>
    /// Trailer capture entry point. Runs in batch mode on a throwaway copy of the project
    /// (Tools/Trailer/trailer.ps1 makes the copy and patches it), enters play mode and hands
    /// the game over to <see cref="TrailerRig"/>.
    /// Run: Unity -batchmode -projectPath COPY -executeMethod LegitEnoughCI.TrailerRun.Run
    /// </summary>
    public static class TrailerRun
    {
        public static void Run()
        {
            var output = Environment.GetEnvironmentVariable("LE_TRAILER_OUT");
            TrailerRig.Out = string.IsNullOrEmpty(output) ? Path.GetFullPath("TrailerOut") : output;
            var ffmpeg = Environment.GetEnvironmentVariable("LE_FFMPEG");
            if (!string.IsNullOrEmpty(ffmpeg)) TrailerRig.Ffmpeg = ffmpeg;
            TrailerRig.Only = Environment.GetEnvironmentVariable("LE_TRAILER_ONLY");
            Directory.CreateDirectory(TrailerRig.Out);
            TrailerRig.Exit = code => EditorApplication.Exit(code);
            // The trailer films the full game: no demo session, not even the one made at boot.
            LegitEnough.Game.GameRoot.DemoBuild = false;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && UnityEngine.Object.FindFirstObjectByType<TrailerRig>() == null)
                    new GameObject("Trailer Rig").AddComponent<TrailerRig>();
            };
            // Keep the static setup alive across entering play mode (throwaway copy only).
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
