#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Crea un Animator Controller para los monstruos del pack (Idle / Caminar / Correr / Atacar / Golpe / Muerte)
// a partir de los clips que traiga el FBX, y se lo asigna al prefab. Resuelve el caso en que el pack
// trae clips pero NO un controlador (por eso los enemigos se veían rígidos).
//
// Uso: en la ventana Project selecciona el/los PREFAB(S) de los enemigos (o una carpeta con prefabs)
//      y menú  Cronos > Monstruos > Crear animador (selección).
public static class MonsterAnimatorBuilder
{
    const string OutDir = "Assets/Cronos/Animators";

    static readonly string[] Idle = { "idle", "stand", "breath", "wait" };
    static readonly string[] Walk = { "walk" };
    static readonly string[] Run = { "run", "sprint", "chase" };
    static readonly string[] Attack = { "attack", "atk", "bite", "claw", "slash", "strike", "smash", "punch", "swing", "melee" };
    static readonly string[] Hit = { "hit", "hurt", "damage", "react", "flinch" };
    static readonly string[] Death = { "death", "die", "dead", "defeat" };

    struct ClipRef { public string path; public string name; }

    [MenuItem("Cronos/Monstruos/Crear animador (selección)")]
    static void BuildSelection()
    {
        var paths = new HashSet<string>();
        foreach (var guid in Selection.assetGUIDs)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(p))
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { p })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
            else if (p.EndsWith(".prefab")) paths.Add(p);
        }
        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog("Animador de monstruos", "Selecciona en la ventana Project uno o varios PREFABS de enemigos (o una carpeta con prefabs) y vuelve a usar el menú.", "OK");
            return;
        }
        Directory.CreateDirectory(OutDir);
        int ok = 0;
        foreach (var p in paths) if (Process(p)) ok++;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Animador] Listo: {ok} de {paths.Count} prefab(s) con animador nuevo. Lee los mensajes de arriba para ver qué clips se usaron en cada estado.");
    }

    static bool Process(string prefabPath)
    {
        string baseName = Path.GetFileNameWithoutExtension(prefabPath);
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;
        try
        {
            var animators = root.GetComponentsInChildren<Animator>(true);
            if (animators.Length == 0) { Debug.LogWarning($"[Animador] {baseName}: el prefab no tiene ningún Animator."); return false; }

            int idx = 0;
            foreach (var an in animators)
            {
                string cname = animators.Length > 1 ? baseName + "_" + (idx++) : baseName;
                var refs = FindClips(an, baseName);
                if (refs.Count == 0) { Debug.LogWarning($"[Animador] {baseName}: no encontré clips de animación (revisa que el FBX los traiga)."); continue; }

                FixLoops(refs);                       // idle / caminar / correr deben repetirse
                var clips = Reload(refs);

                var cIdle = Pick(clips, Idle, null);
                var cWalk = Pick(clips, Walk, null);
                var cRun = Pick(clips, Run, null);
                var cAtk = Pick(clips, Attack, null);
                var cHit = Pick(clips, Hit, Attack);
                var cDie = Pick(clips, Death, null);
                if (cWalk == null) cWalk = cRun;
                if (cIdle == null) cIdle = cWalk != null ? cWalk : clips.FirstOrDefault();

                Debug.Log($"[Animador] {baseName}: clips en el FBX = [{string.Join(", ", clips.Select(c => c.name))}]\n" +
                          $"   Idle={N(cIdle)} · Caminar={N(cWalk)} · Correr={N(cRun)} · Atacar={N(cAtk)} · Golpe={N(cHit)} · Muerte={N(cDie)}");
                if (cWalk == null) { Debug.LogWarning($"[Animador] {baseName}: sin clip de caminar/correr; no se puede crear el animador."); continue; }

                string path = $"{OutDir}/{cname}.controller";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
                var ctrl = Build(path, cIdle, cWalk, cRun, cAtk, cHit, cDie);

                an.runtimeAnimatorController = ctrl;
                an.applyRootMotion = false;
                changed = true;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return changed;
    }

    static string N(AnimationClip c) => c != null ? c.name : "—";

    // Busca clips: primero en el FBX del avatar/malla; si faltan, en archivos de la misma carpeta con el mismo nombre base.
    static List<ClipRef> FindClips(Animator an, string prefabName)
    {
        var list = new List<ClipRef>();
        string fbx = an.avatar != null ? AssetDatabase.GetAssetPath(an.avatar) : null;
        if (string.IsNullOrEmpty(fbx) || AssetImporter.GetAtPath(fbx) as ModelImporter == null)
        {
            var sk = an.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (sk != null && sk.sharedMesh != null) fbx = AssetDatabase.GetAssetPath(sk.sharedMesh);
        }
        if (!string.IsNullOrEmpty(fbx)) AddFrom(fbx, list);
        if (an.runtimeAnimatorController != null)
            foreach (var c in an.runtimeAnimatorController.animationClips)
            {
                string p = AssetDatabase.GetAssetPath(c);
                if (!string.IsNullOrEmpty(p) && !list.Any(r => r.path == p && r.name == c.name)) list.Add(new ClipRef { path = p, name = c.name });
            }

        bool hasMove = list.Any(r => Matches(r.name, Walk) || Matches(r.name, Run));
        if (!hasMove && !string.IsNullOrEmpty(fbx))
        {
            string dir = Path.GetDirectoryName(fbx).Replace('\\', '/');
            string token = Path.GetFileNameWithoutExtension(fbx).Split('@')[0].ToLowerInvariant();
            foreach (var guid in AssetDatabase.FindAssets("t:Model t:AnimationClip", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(token)) continue;   // solo archivos de ESTE monstruo
                AddFrom(p, list);
            }
        }
        return list;
    }

    static void AddFrom(string assetPath, List<ClipRef> list)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            var c = o as AnimationClip;
            if (c == null || c.name.StartsWith("__preview__")) continue;
            if (!list.Any(r => r.path == assetPath && r.name == c.name)) list.Add(new ClipRef { path = assetPath, name = c.name });
        }
    }

    static List<AnimationClip> Reload(List<ClipRef> refs)
    {
        var res = new List<AnimationClip>();
        foreach (var r in refs)
        {
            var c = AssetDatabase.LoadAllAssetsAtPath(r.path).OfType<AnimationClip>().FirstOrDefault(x => x.name == r.name);
            if (c != null) res.Add(c);
        }
        return res;
    }

    static bool Matches(string name, string[] keys)
    {
        string n = name.ToLowerInvariant();
        return keys.Any(k => n.Contains(k));
    }

    static AnimationClip Pick(List<AnimationClip> clips, string[] keys, string[] avoid)
    {
        foreach (var k in keys)
            foreach (var c in clips)
            {
                string n = c.name.ToLowerInvariant();
                if (!n.Contains(k)) continue;
                if (avoid != null && avoid.Any(a => n.Contains(a))) continue;
                return c;
            }
        return null;
    }

    // Marca como "Loop Time" los clips de idle/caminar/correr (en el importador del FBX)
    static void FixLoops(List<ClipRef> refs)
    {
        foreach (var grp in refs.GroupBy(r => r.path))
        {
            var imp = AssetImporter.GetAtPath(grp.Key) as ModelImporter;
            if (imp == null)
            {
                foreach (var r in grp)   // .anim suelto
                {
                    if (!(Matches(r.name, Idle) || Matches(r.name, Walk) || Matches(r.name, Run))) continue;
                    var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(r.path);
                    if (c == null) continue;
                    var s = AnimationUtility.GetAnimationClipSettings(c);
                    if (!s.loopTime) { s.loopTime = true; AnimationUtility.SetAnimationClipSettings(c, s); EditorUtility.SetDirty(c); }
                }
                continue;
            }
            var arr = imp.clipAnimations;
            if (arr == null || arr.Length == 0) arr = imp.defaultClipAnimations;
            bool dirty = false;
            foreach (var ca in arr)
            {
                if (!(Matches(ca.name, Idle) || Matches(ca.name, Walk) || Matches(ca.name, Run))) continue;
                if (!ca.loopTime) { ca.loopTime = true; dirty = true; }
            }
            if (dirty) { imp.clipAnimations = arr; imp.SaveAndReimport(); }
        }
    }

    static AnimatorController Build(string path, AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip atk, AnimationClip hit, AnimationClip die)
    {
        var c = AnimatorController.CreateAnimatorControllerAtPath(path);
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Stunned", AnimatorControllerParameterType.Bool);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Dead", AnimatorControllerParameterType.Trigger);
        var sm = c.layers[0].stateMachine;

        var sIdle = sm.AddState("Idle"); sIdle.motion = idle; sm.defaultState = sIdle;
        var sWalk = sm.AddState("Walk"); sWalk.motion = walk;

        Link(sIdle, sWalk, "Speed", AnimatorConditionMode.Greater, 0.15f, 0.15f);
        Link(sWalk, sIdle, "Speed", AnimatorConditionMode.Less, 0.1f, 0.15f);

        if (run != null && run != walk)
        {
            var sRun = sm.AddState("Run"); sRun.motion = run;
            Link(sWalk, sRun, "Speed", AnimatorConditionMode.Greater, 3.6f, 0.2f);
            Link(sRun, sWalk, "Speed", AnimatorConditionMode.Less, 3.2f, 0.2f);
            Link(sRun, sIdle, "Speed", AnimatorConditionMode.Less, 0.1f, 0.15f);
        }

        if (atk != null)
        {
            var s = sm.AddState("Attack"); s.motion = atk;
            s.speed = Mathf.Clamp(atk.length / 0.8f, 0.6f, 2.2f);   // que dure ~0.8 s, igual que el golpe del juego
            AnyTo(sm, s, "Attack", 0.08f);
            Back(s, sIdle, 0.92f);
        }
        if (hit != null)
        {
            var s = sm.AddState("Hit"); s.motion = hit;
            s.speed = Mathf.Clamp(hit.length / 0.4f, 0.8f, 2f);
            AnyTo(sm, s, "Hit", 0.05f);
            Back(s, sIdle, 0.9f);
        }
        if (die != null)
        {
            var s = sm.AddState("Death"); s.motion = die;
            AnyTo(sm, s, "Dead", 0.1f);
        }
        return c;
    }

    static void Link(AnimatorState a, AnimatorState b, string param, AnimatorConditionMode mode, float th, float dur)
    {
        var t = a.AddTransition(b);
        t.hasExitTime = false; t.duration = dur;
        t.AddCondition(mode, th, param);
    }

    static void AnyTo(AnimatorStateMachine sm, AnimatorState s, string trigger, float dur)
    {
        var t = sm.AddAnyStateTransition(s);
        t.hasExitTime = false; t.duration = dur; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static void Back(AnimatorState from, AnimatorState to, float exit)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true; t.exitTime = exit; t.duration = 0.15f;
    }
}
#endif
