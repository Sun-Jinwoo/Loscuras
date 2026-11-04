#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Ajusta solo las texturas de Assets/Resources/Menu/ al importarlas (tamaño exacto, sin compresión, máscara en lineal).
public class MenuTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Menu/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.maxTextureSize = 4096;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        bool mask = assetPath.ToLower().Contains("mascara");
        ti.sRGBTexture = !mask;
        ti.alphaIsTransparency = !mask;
        ti.isReadable = false;
    }
}
#endif
