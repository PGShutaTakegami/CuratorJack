using UnityEditor;
using UnityEngine;

/// <summary>
/// CJForge（Tools/CJForge）が書き出した素材に、フォルダーごとの読み込み設定を付ける。
/// BGM はストリーミング再生、効果音は読み込み時に展開、画像は透過ありの Sprite にする。
/// 初めて読み込むときだけ設定し、その後 Inspector で変えた設定は作り直しても保つ。
/// </summary>
public class GeneratedAssetImporter : AssetPostprocessor
{
    const string BgmFolder = "Assets/Temp/Audio/BGM/";
    const string SeFolder = "Assets/Temp/Audio/SE/";
    const string ImageFolder = "Assets/Temp/Sprites/Generated/";

    void OnPreprocessAudio()
    {
        bool isBgm = assetPath.StartsWith(BgmFolder);
        if ((!isBgm && !assetPath.StartsWith(SeFolder)) || !assetImporter.importSettingsMissing)
            return;

        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = isBgm ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = isBgm ? 0.7f : 0.85f;
        settings.preloadAudioData = !isBgm;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = isBgm;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ImageFolder) || !assetImporter.importSettingsMissing)
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
    }
}
