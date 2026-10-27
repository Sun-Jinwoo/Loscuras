using UnityEngine;
using UnityEngine.AI;

// Aspecto y animación procedural del cristal enemigo: racimo facetado, núcleo que late,
// fragmentos orbitando (jefes), inclinación al caminar, estocada al atacar y destello al recibir daño.
public class CrystalVisual : MonoBehaviour
{
    Renderer[] rends;
    Transform body, core;
    Transform[] orbiters;
    Light glow;
    NavMeshAgent agent;
    CrystalEnemy enemy;

    Color baseColor, emissionColor;
    float flash, attackKick, phase;
    float scaleBase;

    // Modelo propio (Blender). Si existe Resources/Models/<nombre>.prefab se usa en vez del cristal procedural.
    Animator anim;
    GameObject model;
    readonly System.Collections.Generic.HashSet<string> animParams = new System.Collections.Generic.HashSet<string>();

    public Color MainColor => baseColor;

    public static CrystalVisual Attach(Transform root, CrystalEnemy.CrystalType type, bool boss, bool finalBoss)
    {
        var old = root.Find("Visual");
        if (old != null) Object.Destroy(old.gameObject);

        var go = new GameObject("Visual");
        go.transform.SetParent(root, false);
        var v = go.AddComponent<CrystalVisual>();
        v.Build(type, boss, finalBoss, root);
        return v;
    }

    void Build(CrystalEnemy.CrystalType type, bool boss, bool finalBoss, Transform root)
    {
        agent = root.GetComponent<NavMeshAgent>();
        enemy = root.GetComponent<CrystalEnemy>();
        phase = Random.value * 10f;

        if (finalBoss) { baseColor = new Color(0.75f, 0.35f, 1f); emissionColor = new Color(0.8f, 0.2f, 1f); }
        else if (type == CrystalEnemy.CrystalType.Saline) { baseColor = new Color(0.78f, 0.93f, 1f); emissionColor = new Color(0.35f, 0.75f, 1f); }
        else { baseColor = new Color(1f, 0.55f, 0.22f); emissionColor = new Color(1f, 0.4f, 0.1f); }

        var prefab = Resources.Load<GameObject>("Models/" + ModelKey(type, boss, finalBoss));
        if (prefab != null)
        {
            BuildFromModel(prefab);
            return;
        }

        int seed = Mathf.Abs(root.name.GetHashCode());
        var bodyGo = new GameObject("Cuerpo");
        bodyGo.transform.SetParent(transform, false);
        body = bodyGo.transform;
        var mf = bodyGo.AddComponent<MeshFilter>();
        var mr = bodyGo.AddComponent<MeshRenderer>();
        mf.sharedMesh = CrystalMeshBuilder.Build(seed, boss ? 11 : 7, 0.6f, boss ? 2.1f : 1.8f);
        mr.sharedMaterial = Fx.LitMaterial(baseColor, 0.92f, 0.15f, emissionColor * 0.35f);

        // Núcleo luminoso
        var coreGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreGo.name = "Nucleo";
        Object.Destroy(coreGo.GetComponent<Collider>());
        coreGo.transform.SetParent(transform, false);
        coreGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        coreGo.transform.localScale = Vector3.one * 0.38f;
        coreGo.GetComponent<Renderer>().sharedMaterial = Fx.LitMaterial(emissionColor, 0.3f, 0f, emissionColor * 3f);
        core = coreGo.transform;

        var lgo = new GameObject("Brillo");
        lgo.transform.SetParent(transform, false);
        lgo.transform.localPosition = new Vector3(0f, 1f, 0f);
        glow = lgo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = emissionColor;
        glow.range = boss ? 7f : 4.5f;
        glow.intensity = boss ? 3.5f : 2f;
        glow.shadows = LightShadows.None;

        // Fragmentos orbitando (solo jefes)
        if (boss)
        {
            int n = finalBoss ? 6 : 4;
            orbiters = new Transform[n];
            var mat = Fx.LitMaterial(baseColor, 0.9f, 0.1f, emissionColor * 1.2f);
            for (int i = 0; i < n; i++)
            {
                var o = new GameObject("Orbita" + i);
                o.transform.SetParent(transform, false);
                o.AddComponent<MeshFilter>().sharedMesh = CrystalMeshBuilder.Build(seed + i + 1, 1, 0.5f, 0.55f);
                o.AddComponent<MeshRenderer>().sharedMaterial = mat;
                orbiters[i] = o.transform;
            }
        }

        rends = GetComponentsInChildren<Renderer>();
    }

    // Modelo que ya viene dentro del prefab del enemigo (puesto por ti en Unity)
    public static CrystalVisual WrapExisting(Transform root, Transform modelRoot, CrystalEnemy.CrystalType type, bool finalBoss)
    {
        var host = modelRoot != null ? modelRoot.gameObject : root.gameObject;
        var v = host.GetComponent<CrystalVisual>();
        if (v == null) v = host.AddComponent<CrystalVisual>();
        v.agent = root.GetComponent<NavMeshAgent>();
        v.enemy = root.GetComponent<CrystalEnemy>();
        if (finalBoss) { v.baseColor = new Color(0.75f, 0.35f, 1f); v.emissionColor = new Color(0.8f, 0.2f, 1f); }
        else if (type == CrystalEnemy.CrystalType.Saline) { v.baseColor = new Color(0.78f, 0.93f, 1f); v.emissionColor = new Color(0.35f, 0.75f, 1f); }
        else { v.baseColor = new Color(1f, 0.55f, 0.22f); v.emissionColor = new Color(1f, 0.4f, 0.1f); }
        v.model = host;
        v.anim = host.GetComponentInChildren<Animator>();
        if (v.anim != null)
            foreach (var p in v.anim.parameters) v.animParams.Add(p.name);
        v.rends = host.GetComponentsInChildren<Renderer>();
        v.Sanitize();
        return v;
    }

    // Nombres de prefab que busca en Resources/Models/
    static string ModelKey(CrystalEnemy.CrystalType type, bool boss, bool finalBoss)
    {
        if (finalBoss) return "Boss_Final";
        string t = type == CrystalEnemy.CrystalType.Saline ? "Saline" : "Warm";
        return boss ? "Boss_" + t : t;
    }

    void BuildFromModel(GameObject prefab)
    {
        model = Instantiate(prefab, transform);
        model.name = "Modelo";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);   // el collider lo pone la raíz
        anim = model.GetComponentInChildren<Animator>();
        if (anim != null)
            foreach (var p in anim.parameters) animParams.Add(p.name);
        rends = model.GetComponentsInChildren<Renderer>();
        Sanitize();
    }

    // Limpia lo que traen los modelos de packs y que descuadra al enemigo:
    // root motion (el modelo se aleja de la raíz y por eso "no pega"), Rigidbody/CharacterController propios (caen o flotan).
    void Sanitize()
    {
        if (model == null) return;
        foreach (var a in model.GetComponentsInChildren<Animator>(true)) { a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (var cc in model.GetComponentsInChildren<CharacterController>(true)) Destroy(cc);
        foreach (var sk in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) sk.updateWhenOffscreen = true;
        modelBase = model.transform.localPosition;
        originalRot = model.transform.localRotation;
        baseRot = Quaternion.Euler(0f, CurrentYaw(), 0f) * originalRot;
        appliedVersion = yawVersion;
        baseScaleM = model.transform.localScale;
        groundFrames = 0;
        ResolveAnim();
    }

    // ---- Animación tolerante a nombres: sirve con cualquier Animator de pack ----
    // ---- Orientación del modelo (cada modelo de pack mira hacia un lado distinto) ----
    // Se calibra jugando: apunta a un enemigo y pulsa RePág (+90), AvPág (-90) o Inicio (180).
    // Se guarda por tipo (Enemigo_Salino, Enemigo_Calido, Jefe_Salino, Jefe_Calido, Jefe_Final).
    static int yawVersion;
    int appliedVersion;
    Quaternion originalRot;
    static string YawKey(string k) => "cronos_yaw_" + k;

    float CurrentYaw()
    {
        string k = enemy != null ? enemy.ModelKey : "x";
        if (PlayerPrefs.HasKey(YawKey(k))) return PlayerPrefs.GetFloat(YawKey(k));
        return AutoYaw();
    }

    // Humanoides: deduce hacia dónde mira por la posición de las piernas
    float AutoYaw()
    {
        if (anim == null || !anim.isHuman) return 0f;
        var l = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        var r = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        if (l == null || r == null) return 0f;
        Vector3 right = r.position - l.position; right.y = 0f;
        if (right.sqrMagnitude < 1e-4f) return 0f;
        Vector3 fwd = Vector3.Cross(right, Vector3.up);
        return Vector3.SignedAngle(fwd, transform.root.forward, Vector3.up);
    }

    public string YawName => enemy != null ? enemy.ModelKey : "?";

    public static float AdjustYaw(CrystalVisual v, float deg, bool absolute180 = false)
    {
        string k = v.YawName;
        float cur = v.CurrentYaw();
        float next = Mathf.Repeat(cur + deg + 180f, 360f) - 180f;
        PlayerPrefs.SetFloat(YawKey(k), next);
        PlayerPrefs.Save();
        yawVersion++;
        return next;
    }

    public static CrystalVisual Aimed(Camera cam)
    {
        if (cam == null) return null;
        CrystalVisual best = null; float bestDot = 0.8f;
        foreach (var v in FindObjectsByType<CrystalVisual>(FindObjectsSortMode.None))
        {
            if (v.model == null) continue;
            Vector3 d = v.transform.root.position + Vector3.up - cam.transform.position;
            if (d.magnitude > 40f) continue;
            float dot = Vector3.Dot(cam.transform.forward, d.normalized);
            if (dot > bestDot) { bestDot = dot; best = v; }
        }
        return best;
    }

    string pSpeed, pMoveBool, pStun, pAttack, pHit;
    int sIdle, sMove, sAttack, sHit;       // hashes de estados (0 = no existe)
    bool hasLocomotion;                    // true si hay animación real de movimiento
    bool moving; float attackUntil, hitUntil;
    Quaternion baseRot; Vector3 baseScaleM, bobOffset;

    static bool Has(string n, params string[] keys)
    {
        n = n.ToLowerInvariant();
        foreach (var k in keys) if (n.Contains(k)) return true;
        return false;
    }

    int FindState(string[] keys, string[] avoid)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return 0;
        var names = new System.Collections.Generic.List<string>();
        foreach (var c in anim.runtimeAnimatorController.animationClips) names.Add(c.name);
        // 1) clips cuyo nombre contiene la palabra (el estado suele llamarse igual que el clip)
        foreach (var n in names)
        {
            if (!Has(n, keys) || Has(n, avoid)) continue;
            foreach (var cand in new[] { n, n.Substring(n.LastIndexOf('|') + 1), n.Substring(n.LastIndexOf('@') + 1) })
            {
                int h = Animator.StringToHash(cand);
                if (anim.HasState(0, h)) return h;
            }
        }
        // 2) nombres típicos de estado
        foreach (var k in keys)
            foreach (var cand in new[] { k, char.ToUpper(k[0]) + k.Substring(1) })
            {
                int h = Animator.StringToHash(cand);
                if (anim.HasState(0, h)) return h;
            }
        return 0;
    }

    void ResolveAnim()
    {
        pSpeed = pMoveBool = pStun = pAttack = pHit = null;
        sIdle = sMove = sAttack = sHit = 0; hasLocomotion = false;
        if (anim == null || anim.runtimeAnimatorController == null) return;

        foreach (var p in anim.parameters)
        {
            string n = p.name;
            if (p.type == AnimatorControllerParameterType.Float && pSpeed == null && Has(n, "speed", "veloc", "move", "walk", "run", "blend", "forward")) pSpeed = n;
            else if (p.type == AnimatorControllerParameterType.Bool)
            {
                if (pMoveBool == null && Has(n, "walk", "run", "move", "chase")) pMoveBool = n;
                else if (pStun == null && Has(n, "stun", "dizzy", "freeze")) pStun = n;
            }
            else if (p.type == AnimatorControllerParameterType.Trigger)
            {
                if (pAttack == null && Has(n, "attack", "atk", "hit1", "slash", "swing", "melee")) pAttack = n;
                else if (pHit == null && Has(n, "hit", "damage", "hurt", "react")) pHit = n;
            }
        }
        sIdle = FindState(new[] { "idle", "stand", "breath", "wait" }, new string[0]);
        sMove = FindState(new[] { "walk", "run", "move", "chase", "locomotion" }, new[] { "idle" });
        sAttack = FindState(new[] { "attack", "slash", "swing", "melee", "strike", "punch", "smash" }, new string[0]);
        sHit = FindState(new[] { "hit", "hurt", "damage", "react" }, new[] { "attack" });

        hasLocomotion = pSpeed != null || pMoveBool != null || sMove != 0;
        if (anim != null) anim.speed = 1f;
    }

    void AnimateModel(float speed)
    {
        if (appliedVersion != yawVersion) { baseRot = Quaternion.Euler(0f, CurrentYaw(), 0f) * originalRot; appliedVersion = yawVersion; }
        bool stunned = enemy != null && enemy.IsStunned;
        if (speed > 0.25f) moving = true; else if (speed < 0.08f) moving = false;

        if (anim != null)
        {
            if (pSpeed != null) anim.SetFloat(pSpeed, speed);
            if (pMoveBool != null) anim.SetBool(pMoveBool, moving);
            if (pStun != null) anim.SetBool(pStun, stunned);
            SetParam("Speed", speed); SetParam("Stunned", stunned);

            // Sin parámetros útiles: saltamos directo a los estados por nombre
            if (pSpeed == null && pMoveBool == null && sMove != 0 && Time.time > attackUntil && Time.time > hitUntil)
            {
                int want = moving && !stunned ? sMove : (sIdle != 0 ? sIdle : sMove);
                if (want != curState) { anim.CrossFadeInFixedTime(want, 0.2f); curState = want; }
            }
            anim.speed = stunned ? 0f : (moving && hasLocomotion ? Mathf.Clamp(speed / 2.5f, 0.8f, 1.6f) : 1f);
        }

        // Si no hay animación real de movimiento: balanceo procedural para que no se deslice tieso
        var mt = model.transform;
        mt.position -= bobOffset;
        bobOffset = Vector3.zero;
        mt.localRotation = baseRot;
        mt.localScale = baseScaleM;
        if (!hasLocomotion)
        {
            float t = Time.time + phase;
            float k = Mathf.Clamp01(speed / 2f);
            float f = 7f + speed * 1.5f;
            float step = Mathf.Abs(Mathf.Sin(t * f));
            bobOffset = new Vector3(0f, (step * 0.12f * k + Mathf.Sin(t * 1.6f) * 0.03f), 0f);
            mt.position += bobOffset;
            mt.localRotation = baseRot * Quaternion.Euler(k * 7f, 0f, Mathf.Sin(t * f * 0.5f) * 5f * k);
            float breathe = 1f + Mathf.Sin(t * 2f) * 0.02f;
            mt.localScale = baseScaleM * breathe;
            if (attackKick > 0f)
            {
                attackKick = Mathf.MoveTowards(attackKick, 0f, Time.deltaTime * 4f);
                float kick = Mathf.Sin(attackKick * Mathf.PI);
                mt.localRotation = baseRot * Quaternion.Euler(kick * 25f, 0f, 0f);
            }
        }
    }
    int curState;


    Vector3 modelBase;
    int groundFrames;

    // Apoya la base visible del modelo en el suelo UNA sola vez (solo vertical, sin recentrar: recentrar causaba "teletransportes")
    bool grounded;
    void Ground()
    {
        if (model == null || grounded) return;
        if (model.transform == transform.root) { grounded = true; return; }   // nunca mover la raíz del enemigo
        if (++groundFrames < 8) return;                                        // espera a que el Animator evalúe la pose
        grounded = true;

        float minY = float.MaxValue;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue;
            var sk = r as SkinnedMeshRenderer;
            if (sk != null && sk.sharedMesh != null)
            {
                var tmp = new Mesh();
                sk.BakeMesh(tmp, true);
                var m = Matrix4x4.TRS(sk.transform.position, sk.transform.rotation, Vector3.one);
                foreach (var v in tmp.vertices) minY = Mathf.Min(minY, m.MultiplyPoint3x4(v).y);
                Destroy(tmp);
            }
            else minY = Mathf.Min(minY, r.bounds.min.y);
        }
        if (minY == float.MaxValue) return;

        // Suelo real bajo el enemigo (ignora al propio enemigo y al jugador)
        float floorY = transform.root.position.y;
        var hits = Physics.RaycastAll(transform.root.position + Vector3.up * 1.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform.root)) continue;
            if (h.collider.CompareTag("Player")) continue;
            if (h.distance < best) { best = h.distance; floorY = h.point.y; }
        }

        float dy = floorY - minY;
        if (Mathf.Abs(dy) > 0.01f) model.transform.position += new Vector3(0f, Mathf.Clamp(dy, -4f, 4f), 0f);
    }

    void LateUpdate() { if (model != null) Ground(); }

    void SetParam(string n, float v) { if (anim != null && animParams.Contains(n)) anim.SetFloat(n, v); }
    void SetParam(string n, bool v) { if (anim != null && animParams.Contains(n)) anim.SetBool(n, v); }
    void Trigger(string n) { if (anim != null && animParams.Contains(n)) anim.SetTrigger(n); }

    void Start() => scaleBase = 1f;

    public void Flash()
    {
        flash = 1f;
        if (anim == null) return;
        if (pHit != null) anim.SetTrigger(pHit);
        else if (sHit != 0 && Time.time > attackUntil) { anim.CrossFadeInFixedTime(sHit, 0.05f); hitUntil = Time.time + 0.4f; curState = sHit; }
        else Trigger("Hit");
    }
    public void AttackKick()
    {
        attackKick = 1f;
        if (anim == null) return;
        if (pAttack != null) anim.SetTrigger(pAttack);
        else if (sAttack != 0) { anim.CrossFadeInFixedTime(sAttack, 0.08f); attackUntil = Time.time + 0.9f; curState = sAttack; }
        else Trigger("Attack");
    }

    void Update()
    {
        if (model != null)
        {
            float sp = agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            AnimateModel(sp);
            return;
        }
        if (body == null) return;
        float t = Time.time + phase;
        float dt = Time.deltaTime;

        float speed = agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
        bool stunned = enemy != null && enemy.IsStunned;
        bool slowed = enemy != null && enemy.IsSlowed;

        // Respiración + flotación
        float breathe = 1f + Mathf.Sin(t * 2f) * 0.03f;
        float bob = Mathf.Sin(t * 1.6f) * 0.05f;
        // Inclinación al caminar
        float lean = Mathf.Clamp(speed * 4f, 0f, 14f);
        float wobble = Mathf.Sin(t * (4f + speed * 2.5f)) * Mathf.Clamp(speed, 0f, 4f) * 1.4f;
        if (stunned) wobble = Mathf.Sin(t * 40f) * 3f;

        attackKick = Mathf.MoveTowards(attackKick, 0f, dt * 4f);
        float kick = Mathf.Sin(attackKick * Mathf.PI);

        body.localPosition = new Vector3(0f, bob, kick * 0.35f);
        body.localRotation = Quaternion.Euler(lean + kick * 18f, 0f, wobble);
        body.localScale = new Vector3(1f + kick * 0.1f, breathe * (1f - kick * 0.08f), 1f + kick * 0.1f) * scaleBase;

        // Núcleo late (más rápido cuanto más cerca de morir)
        float hpK = enemy != null ? enemy.HealthRatio : 1f;
        float beat = 1f + Mathf.Sin(t * (3f + (1f - hpK) * 6f)) * 0.18f;
        core.localScale = Vector3.one * 0.38f * beat;
        core.localPosition = new Vector3(0f, 0.95f + bob, 0f);
        glow.intensity = (orbiters != null ? 3.5f : 2f) * (0.8f + 0.4f * beat) + flash * 3f;

        if (orbiters != null)
        {
            for (int i = 0; i < orbiters.Length; i++)
            {
                float a = t * 1.1f + i * Mathf.PI * 2f / orbiters.Length;
                float rad = 1.5f + Mathf.Sin(t + i) * 0.1f;
                orbiters[i].localPosition = new Vector3(Mathf.Cos(a) * rad, 1.1f + Mathf.Sin(t * 1.7f + i * 2f) * 0.35f, Mathf.Sin(a) * rad);
                orbiters[i].localRotation = Quaternion.Euler(t * 60f + i * 40f, t * 90f, 20f);
            }
        }

        // Tinte de estado y destello de golpe
        flash = Mathf.MoveTowards(flash, 0f, dt * 5f);
        Color tint = emissionColor;
        float strength = 0.35f;
        if (stunned) { tint = new Color(0.7f, 0.9f, 1f); strength = 1.2f; }
        else if (slowed) { tint = new Color(0.8f, 0.3f, 1f); strength = 0.8f; }
        if (flash > 0f) { tint = Color.Lerp(tint, Color.white, flash); strength += flash * 3f; }

        var mpb = new MaterialPropertyBlock();
        foreach (var r in rends)
        {
            if (r == null || r.gameObject == core.gameObject) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", tint * strength);
            r.SetPropertyBlock(mpb);
        }
    }
}
