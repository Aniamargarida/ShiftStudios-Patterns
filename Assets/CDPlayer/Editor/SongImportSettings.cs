using UnityEditor;
using UnityEngine;

/// <summary>
/// Songs added to the FinalScene Audio folder are imported for Quest: streamed from disk while they play
/// (a 4-minute song would otherwise take ~40 MB of memory) and compressed as Vorbis.
/// Only applies the first time a file is imported, so settings changed by hand afterwards are kept.
/// </summary>
public class SongImportSettings : AssetPostprocessor
{
    public const string SongFolder = "Assets/Assets/FinalScene/Audio/";

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(SongFolder) || !assetImporter.importSettingsMissing) return;
        Apply((AudioImporter)assetImporter);
    }

    public static void Apply(AudioImporter importer)
    {
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = true;
    }
}
