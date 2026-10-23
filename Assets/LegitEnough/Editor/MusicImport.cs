using System;
using UnityEditor;
using UnityEngine;

namespace LegitEnough.EditorTools
{
    /// <summary>
    /// Import settings for the background music (Resources/LegitEnough/Music, written by
    /// Tools/Music/compose.py): streamed from disk, Vorbis, not preloaded, so a four-minute loop
    /// costs a small decode buffer instead of tens of megabytes of memory.
    /// </summary>
    sealed class MusicImport : AssetPostprocessor
    {
        const string Folder = "Assets/LegitEnough/Resources/LegitEnough/Music/";

        public override uint GetVersion() => 1;

        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder, StringComparison.OrdinalIgnoreCase)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.ambisonic = false;
            var s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = false;
            importer.defaultSampleSettings = s;
        }
    }
}
