#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Menú: Cronos > Crear postproceso
// Crea un Volume global con Bloom, viñeta, tonemapping, grano y ajuste de color, y activa el postproceso en la cámara.
public static class CronosPostFx
{
    const string ProfilePath = "Assets/Cronos/CronosPostFx.asset";

    [MenuItem("Cronos/Crear postproceso")]
    public static void CreateMenu() => Ensure();

    public static void Ensure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Cronos")) AssetDatabase.CreateFolder("Assets", "Cronos");

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.7f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.contrast.Override(12f);
            ca.saturation.Override(8f);
            ca.colorFilter.Override(Color.white);

            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            var fg = profile.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Thin1);
            fg.intensity.Override(0.15f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        var vol = Object.FindFirstObjectByType<Volume>();
        if (vol == null) vol = new GameObject("CronosPostFx").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        var cam = Camera.main;
        if (cam != null)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }
    }
}
#endif
