using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LegitEnough.EditorTools
{
    /// <summary>
    /// The player build: Windows x64 (default) or macOS (Universal: Apple silicon + Intel), the one
    /// scene (GameBootstrap builds everything else at runtime). It only ever builds the DEMO: it refuses when GameRoot.DemoBuild is false in
    /// source (play the full game in the Editor with Legit Enough > Play Full Game in Editor).
    /// The exe gets the store's app icon: the hand-drawn sizes in Steam/store/client_icon.ico are
    /// imported for the build (point filtered, uncompressed) and set as the Standalone icons, then
    /// the project's own icon settings are put back.
    /// Batch use (Tools/Build/build-demo.ps1 runs it on a throwaway copy of the project):
    ///   Unity -batchmode -projectPath COPY -executeMethod LegitEnough.EditorTools.BuildDemo.Run
    /// The output path comes from LE_BUILD_OUT (the .exe path, or the .app path for macOS); without it,
    /// Builds/LegitEnoughDemo (Windows) or Builds/LegitEnoughDemoMac (macOS) under the project.
    /// LE_BUILD_TARGET=mac builds the macOS player (Tools/Build/build-demo-mac.ps1); anything else, Windows.
    /// The icon file comes from LE_BUILD_ICON; without it, Steam/store/client_icon.ico under the project.
    /// Also on the menu: Legit Enough / Build Demo (Windows x64) and Build Demo (macOS).
    /// </summary>
    public static class BuildDemo
    {
        // Listed explicitly: the build never depends on what EditorBuildSettings happens to hold.
        static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };
        const string ExeName = "Legit Enough.exe";
        const string AppName = "Legit Enough.app";
        /// <summary>The macOS bundle id while building (the project's own value is put back afterwards).</summary>
        const string MacBundleId = "com.mchunterstudio.legitenough";
        /// <summary>Where the icon sizes are imported for the build (removed again afterwards).</summary>
        const string IconDir = "Assets/LegitEnoughBuildIcons";

        [MenuItem("Legit Enough/Build Demo (Windows x64)")]
        static void BuildFromMenu() => BuildFromMenu(false);

        [MenuItem("Legit Enough/Build Demo (macOS)")]
        static void BuildMacFromMenu() => BuildFromMenu(true);

        static void BuildFromMenu(bool mac)
        {
            string text;
            try
            {
                text = Summary(Build(DefaultPath(mac), mac));
            }
            catch (Exception e)
            {
                text = "Not built: " + e.Message;
                Debug.LogError("LEGIT ENOUGH BUILD: " + e);
            }
            EditorUtility.DisplayDialog("Build Demo", text, "OK");
        }

        /// <summary>Batch entry point: builds, logs a summary line and exits (0 = built).</summary>
        public static void Run()
        {
            int code = 1;
            try
            {
                bool mac = string.Equals(Environment.GetEnvironmentVariable("LE_BUILD_TARGET"), "mac", StringComparison.OrdinalIgnoreCase);
                string path = Environment.GetEnvironmentVariable("LE_BUILD_OUT");
                if (string.IsNullOrEmpty(path)) path = DefaultPath(mac);
                var report = Build(path, mac);
                Debug.Log("LEGIT ENOUGH BUILD: " + Summary(report));
                if (report.summary.result == BuildResult.Succeeded) code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("LEGIT ENOUGH BUILD: refused or failed: " + e.Message + "\n" + e);
            }
            EditorApplication.Exit(code);
        }

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? ".";

        static string DefaultPath(bool mac) => mac
            ? Path.Combine(ProjectRoot, "Builds", "LegitEnoughDemoMac", AppName)
            : Path.Combine(ProjectRoot, "Builds", "LegitEnoughDemo", ExeName);

        static BuildReport Build(string exePath, bool mac)
        {
            // The shipping build is the demo. A DemoBuild left false in source would ship the full
            // game under the demo's name, so stop here instead.
            if (!LegitEnough.Game.GameRoot.DemoBuild)
                throw new BuildFailedException(
                    "GameRoot.DemoBuild is false in source (Assets/LegitEnough/Scripts/Game/Boot/GameRoot.cs). " +
                    "Set it back to true: the player build is always the demo. To play the full game, use " +
                    "Legit Enough > Play Full Game in Editor instead.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? ".");
            foreach (var scene in Scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Build scene missing: " + scene);
            var target = mac ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneWindows64;
            if (mac && !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException("macOS Build Support (Mono) isn't installed for this Unity version (Unity Hub > Installs > Add modules).");
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target);
            if (mac) SetMacUniversal();

            var keepIcons = StandaloneIcons.Get();
            var keepDefault = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            var keepBundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
            try
            {
                SetAppIcons(ImportIcons());
                if (mac) PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, MacBundleId);
                var options = new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = exePath,
                    target = target,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                };
                Debug.Log($"LEGIT ENOUGH BUILD: {PlayerSettings.productName} {PlayerSettings.bundleVersion} ({PlayerSettings.companyName}), demo, {target} -> {exePath}");
                return BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                // The icons and bundle id belong to this build only: the project's settings and Assets stay as they were.
                if (keepIcons != null) StandaloneIcons.Set(keepIcons);
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, keepDefault, IconKind.Any);
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, keepBundleId);
                if (AssetDatabase.IsValidFolder(IconDir)) AssetDatabase.DeleteAsset(IconDir);
            }
        }

        /// <summary>
        /// One app for both kinds of Mac (Apple silicon and Intel). The setting lives in
        /// UnityEditor.OSXStandalone.UserBuildSettings, which only exists when the macOS module is
        /// installed, so it's reached by reflection and this file still compiles without it.
        /// </summary>
        static void SetMacUniversal()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("UnityEditor.OSXStandalone.UserBuildSettings");
                var prop = type?.GetProperty("architecture", BindingFlags.Static | BindingFlags.Public);
                if (prop == null) continue;
                prop.SetValue(null, Enum.Parse(prop.PropertyType, "x64ARM64"));
                Debug.Log("LEGIT ENOUGH BUILD: macOS architecture " + prop.GetValue(null));
                return;
            }
            Debug.LogWarning("LEGIT ENOUGH BUILD: couldn't set the macOS architecture; Unity's default is used.");
        }

        // ------------------------------------------------------------------ app icon

        /// <summary>
        /// Pulls every size out of the store's .ico (each entry is a PNG) into IconDir and imports
        /// them as pixel art. Returns the textures by pixel size.
        /// </summary>
        static SortedDictionary<int, Texture2D> ImportIcons()
        {
            string ico = Environment.GetEnvironmentVariable("LE_BUILD_ICON");
            if (string.IsNullOrEmpty(ico)) ico = Path.Combine(ProjectRoot, "Steam", "store", "client_icon.ico");
            if (!File.Exists(ico)) throw new FileNotFoundException("App icon missing (set LE_BUILD_ICON): " + ico);

            byte[] b = File.ReadAllBytes(ico);
            int count = b.Length >= 6 ? BitConverter.ToUInt16(b, 4) : 0;
            if (b.Length < 6 || BitConverter.ToUInt16(b, 2) != 1 || count == 0) throw new InvalidDataException("Not an .ico file: " + ico);
            if (!AssetDatabase.IsValidFolder(IconDir)) AssetDatabase.CreateFolder("Assets", Path.GetFileName(IconDir));

            var paths = new SortedDictionary<int, string>();
            for (int i = 0; i < count; i++)
            {
                int entry = 6 + 16 * i;
                int size = b[entry] == 0 ? 256 : b[entry];
                int length = BitConverter.ToInt32(b, entry + 8);
                int offset = BitConverter.ToInt32(b, entry + 12);
                // Only PNG-encoded entries (the store art is all PNG); the signature starts 0x89 'P' 'N' 'G'.
                if (offset + length > b.Length || length < 8 || b[offset] != 0x89 || b[offset + 1] != 0x50) continue;
                var png = new byte[length];
                Array.Copy(b, offset, png, 0, length);
                string path = $"{IconDir}/AppIcon_{size}.png";
                File.WriteAllBytes(path, png);
                paths[size] = path;
            }
            if (paths.Count == 0) throw new InvalidDataException("No PNG icon sizes in " + ico);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var textures = new SortedDictionary<int, Texture2D>();
            foreach (var kv in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(kv.Value);
                importer.textureType = TextureImporterType.Default;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.isReadable = true;
                importer.SaveAndReimport();
                textures[kv.Key] = AssetDatabase.LoadAssetAtPath<Texture2D>(kv.Value);
            }
            Debug.Log("LEGIT ENOUGH BUILD: app icon sizes " + string.Join(", ", textures.Keys) + " from " + ico);
            return textures;
        }

        /// <summary>
        /// Every slot Unity asks for gets the hand-drawn size that matches it, or the next larger
        /// one (never an upscaled small one), so the "LE" mark stays crisp in the taskbar and Alt-Tab.
        /// </summary>
        static void SetAppIcons(SortedDictionary<int, Texture2D> bySize)
        {
            Texture2D Pick(int want)
            {
                Texture2D best = null;
                foreach (var kv in bySize)
                {
                    best = kv.Value; // ascending: ends on the largest when none is big enough
                    if (kv.Key >= want) return kv.Value;
                }
                return best;
            }

            // The Default Icon (Player Settings > Icon): the largest size, for anything that reads that one.
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { Pick(1024) }, IconKind.Any);

            // The per-size Standalone override (what the exe's icon resource is made from).
            int[] sizes = StandaloneIcons.Sizes();
            if (sizes.Length == 0)
            {
                Debug.LogWarning("LEGIT ENOUGH BUILD: no Standalone icon slots found; the exe uses the default icon, scaled by Unity.");
                return;
            }
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) icons[i] = Pick(sizes[i]);
            StandaloneIcons.Set(icons);
            var set = StandaloneIcons.Get() ?? new Texture2D[0];
            var used = new List<string>();
            for (int i = 0; i < sizes.Length; i++)
                used.Add(sizes[i] + "<-" + (i < set.Length && set[i] != null ? set[i].width.ToString() : "none"));
            Debug.Log("LEGIT ENOUGH BUILD: Standalone icon slots: " + string.Join(", ", used));
        }

        /// <summary>
        /// The Standalone icon slots. In 6000.0 the public PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, ...)
        /// reports no slots in batch mode (so SetIcons stores nothing), while the platform-name
        /// methods it wraps list 1024 down to 16. Those are internal, so they're reached by reflection
        /// (public API first, in case a later Unity fixes it).
        /// </summary>
        static class StandaloneIcons
        {
            const string Platform = "Standalone";
            const BindingFlags Any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            static MethodInfo Method(string name, params Type[] args) => typeof(PlayerSettings).GetMethod(name, Any, null, args, null);

            static bool UsePublic => PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Length > 0;

            public static int[] Sizes()
            {
                if (UsePublic) return PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
                var m = Method("GetIconWidthsForPlatform", typeof(string), typeof(IconKind));
                return m?.Invoke(null, new object[] { Platform, IconKind.Any }) as int[] ?? new int[0];
            }

            public static Texture2D[] Get()
            {
                if (UsePublic) return PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);
                var m = Method("GetIconsForPlatform", typeof(string), typeof(IconKind));
                return m?.Invoke(null, new object[] { Platform, IconKind.Any }) as Texture2D[];
            }

            public static void Set(Texture2D[] icons)
            {
                if (UsePublic)
                {
                    PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
                    return;
                }
                var m = Method("SetIconsForPlatform", typeof(string), typeof(Texture2D[]), typeof(IconKind));
                if (m == null) throw new MissingMethodException("PlayerSettings.SetIconsForPlatform (Standalone icons)");
                m.Invoke(null, new object[] { Platform, icons, IconKind.Any });
            }
        }

        static string Summary(BuildReport report)
        {
            var s = report.summary;
            return $"{s.result}, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024f * 1024f):0.0} MB, " +
                   $"{s.totalTime.TotalSeconds:0} s -> {s.outputPath}";
        }
    }
}
