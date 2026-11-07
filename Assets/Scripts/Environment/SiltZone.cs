using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 泥沙激起程度（由玩家当前状态机决定）
public enum SiltActivity
{
    Idle = 0, // 静态：激起小
    Swim = 1, // 游动：激起中
    Dash = 2, // 冲刺：激起强
}

// 泥沙判定区：挂在墙面等位置的 BoxCollider(isTrigger) 上。
//
// 玩法流程（与策划「泥沙」细则对应）：
//   · 玩家接触判定区 → 按状态机决定激起程度：静态小 / 游动中 / 冲刺强；
//   · 激起 → 爆发大量粒子、判定区随泥沙密度扩大、屏幕蒙上浑浊色遮挡视线；
//   · 玩家处于区内且密度较高 → flag3 = 1
//       （触发 Duff.json 里 id 19「突发事件3：搅动泥沙」Debuff，心率 +35）；
//   · 玩家离开 / 密度降下来 → flag3 = 0（该 Debuff 随之解除）。
//
// 多个泥沙区同时高密度时，只要有一个满足就保持 flag3（见 LateUpdate 汇总）。
[RequireComponent(typeof(BoxCollider))]
public class SiltZone : TriggerZoneBase
{
    [Header("激起强度（区内时密度朝这个目标靠近）")]
    [Tooltip("静态（待机）：小")]
    [SerializeField, Range(0f, 1f)] private float idleTargetDensity = 0.3f;
    [Tooltip("游动：中")]
    [SerializeField, Range(0f, 1f)] private float swimTargetDensity = 0.8f;
    [Tooltip("冲刺：强")]
    [SerializeField, Range(0f, 1f)] private float dashTargetDensity = 1f;

    [Header("密度")]
    [Tooltip("密度上升速度（每秒）")]
    [SerializeField] private float densityRiseSpeed = 1.2f;
    [Tooltip("离开后泥沙自然沉降速度（每秒）")]
    [SerializeField] private float densityDecaySpeed = 0.45f;
    [Tooltip("密度高于这个值视为「密度较高」→ flag3")]
    [SerializeField, Range(0f, 1f)] private float highDensityThreshold = 0.5f;

    [Header("判定区扩张")]
    [Tooltip("密度最大时 BoxCollider 长宽的放大倍率")]
    [SerializeField] private float maxExpansion = 1.7f;
    [SerializeField] private bool expandZoneOnStir = true;

    [Header("粒子")]
    [Tooltip("可选粒子系统预设；留空则运行时自动生成默认泥沙粒子")]
    [SerializeField] private ParticleSystem particlePrefab;
    [Tooltip("密度 1 时的持续发射速率（个/秒）")]
    [SerializeField] private float maxEmissionRate = 180f;
    [Tooltip("激起瞬间的粒子数（冲刺强度时）")]
    [SerializeField] private int stirBurstCount = 130;
    [Tooltip("单颗粒子大小范围（世界单位，越小越像细颗粒）")]
    [SerializeField] private Vector2 particleSizeRange = new Vector2(0.03f, 0.09f);
    [Tooltip("粒子可扩散范围 = 当前判定区尺寸 × 该倍率（1.2 = 最多超出判定区 20%）")]
    [SerializeField] private float particleSpreadMultiplier = 1.2f;
    [SerializeField] private float particleLifetime = 3f;
    [SerializeField] private float particleSpeed = 0.6f;
    [Tooltip("粒子相对判定区的 Z 偏移：相机在负 Z，往相机方向挪一点遮挡更明显")]
    [SerializeField] private float particleZOffset = -2f;
    [SerializeField] private Color siltColor = new Color(0.42f, 0.31f, 0.2f, 1f);

    [Header("粒子对象池")]
    [Tooltip("预生成的爆发粒子发射器数量（不够时自动扩容）")]
    [SerializeField] private int particlePoolSize = 6;
    [Tooltip("每个粒子系统的最多粒子数；对象池按发射器数量成倍占用内存，不要设太大")]
    [SerializeField] private int maxParticlesPerSystem = 4000;

    [Header("遮挡视线")]
    [Tooltip("全屏蒙上浑浊色，透明度随密度变化")]
    [SerializeField] private bool useFullScreenTint = true;
    [SerializeField] private Color visionTint = new Color(0.18f, 0.12f, 0.07f, 1f);
    [SerializeField, Range(0f, 1f)] private float maxVisionAlpha = 0.45f;

    [Header("触发 Flag")]
    [Tooltip("密度较高且玩家在区内时置真的 flag 序号（默认 3 = 「搅动泥沙」）")]
    [SerializeField] private int siltFlagNumber = 3;

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;
    [SerializeField] private float density;          // 当前泥沙密度 0~1
    [SerializeField] private bool high;              // 区内且密度较高
    [SerializeField] private SiltActivity lastActivity = SiltActivity.Idle;

    // ---- 对外只读状态 ----
    public float Density => density;
    public bool IsHighDensity => high;
    public SiltActivity CurrentActivity => lastActivity;
    public event Action<SiltActivity> OnStirred; // 激起一次（刚进入 / 状态升级）

    // 场景里所有启用的泥沙区，用于汇总 flag3
    private static readonly HashSet<SiltZone> ActiveZones = new HashSet<SiltZone>();

    private DebuffManager dm;
    private Vector3 baseSize;
    private bool wasInside;
    private bool wasHigh;

    private ParticleSystem ps;          // 常驻系统：按密度持续发射弥散颗粒
    private ParticlePool burstPool;     // 爆发粒子对象池
    private Image tint;
    private Material particleMat;
    private Texture2D particleTex;
    private ParticleSystem.Particle[] particleBuffer; // 越界夹回用

    // 一次爆发对应的发射器与归还时间
    private struct PooledBurst
    {
        public ParticleSystem system;
        public float releaseTime;
    }

    private readonly List<PooledBurst> activeBursts = new List<PooledBurst>();

    protected override void Awake()
    {
        base.Awake();
        baseSize = Box.size;
        BuildParticles();
        BuildVision();
    }

    private void OnEnable()
    {
        ActiveZones.Add(this);
    }

    private void OnDisable()
    {
        ActiveZones.Remove(this);
        if (dm != null) dm.SetFlag(siltFlagNumber, AnyZoneHigh(siltFlagNumber));
    }

    private void OnDestroy()
    {
        // 对象池与运行时生成的材质/贴图，随场景卸载一并释放
        burstPool?.Dispose();
        burstPool = null;

        if (particleMat != null) Destroy(particleMat);
        if (particleTex != null) Destroy(particleTex);
    }

    private void Start()
    {
        dm = FindFirstObjectByType<DebuffManager>();
        if (dm == null && verboseLog)
            Debug.LogWarning($"[泥沙] {name} 场景里没有 DebuffManager，「搅动泥沙」Buff 不会生效");
    }

    // 所有区的 Update 跑完后再统一写 flag，避免多个区互相覆盖
    private void LateUpdate()
    {
        // 粒子自由扩散，但限制在判定区 1.2 倍范围内
        ConstrainParticles();

        if (dm == null) return;
        dm.SetFlag(siltFlagNumber, AnyZoneHigh(siltFlagNumber));
    }

    // 把所有在用的粒子系统（常驻 + 正在爆发的池对象）限制在范围内
    private void ConstrainParticles()
    {
        ConstrainSystem(ps);

        for (int i = 0; i < activeBursts.Count; i++)
            ConstrainSystem(activeBursts[i].system);
    }

    // 把单个粒子系统的粒子限制在「当前判定区尺寸 × particleSpreadMultiplier」内；
    // 越界时夹回边界，并把该轴速度反向减半，形成轻微反弹、避免堆在边缘。
    private void ConstrainSystem(ParticleSystem system)
    {
        if (system == null || system.particleCount == 0) return;

        // 只处理世界空间的粒子；自定义预设若是 Local 空间则交给预设自己控制
        if (system.main.simulationSpace != ParticleSystemSimulationSpace.World) return;

        // 按实际粒子数分配缓冲（对象池里多个系统共用同一块，顺序处理即可）
        int live = system.particleCount;
        if (particleBuffer == null || particleBuffer.Length < live)
            particleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(Mathf.Max(live, 64))];

        int count = system.GetParticles(particleBuffer);
        if (count <= 0) return;

        // box.size 是局部尺寸，transform 逆变换后的局部坐标与之同单位
        Vector3 half = Box.size * (0.5f * particleSpreadMultiplier);
        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            Vector3 local = transform.InverseTransformPoint(particleBuffer[i].position);
            bool cx = local.x < -half.x || local.x > half.x;
            bool cy = local.y < -half.y || local.y > half.y;
            if (!cx && !cy) continue;

            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            particleBuffer[i].position = transform.TransformPoint(local);

            Vector3 vl = transform.InverseTransformDirection(particleBuffer[i].velocity);
            if (cx) vl.x = -vl.x * 0.5f;
            if (cy) vl.y = -vl.y * 0.5f;
            particleBuffer[i].velocity = transform.TransformDirection(vl);

            changed = true;
        }

        if (changed) system.SetParticles(particleBuffer, count);
    }

    private static bool AnyZoneHigh(int flagNumber)
    {
        foreach (SiltZone z in ActiveZones)
        {
            if (z != null && z.siltFlagNumber == flagNumber && z.high) return true;
        }
        return false;
    }

    private void Update()
    {
        UpdateBurstReleases();

        bool active = HasLivePlayerInside();

        // 状态机决定激起程度
        SiltActivity act = active ? ActivityFromState() : SiltActivity.Idle;

        // 激起瞬间：刚进入 / 状态升级到更强时爆发一次
        if (active && (!wasInside || (int)act > (int)lastActivity))
            Burst(act);

        wasInside = active;
        lastActivity = act;

        // 密度朝目标靠近：区内按状态目标上升，区外沉降到 0
        float target = active ? TargetFor(act) : 0f;
        float speed = target >= density ? densityRiseSpeed : densityDecaySpeed;
        density = Mathf.MoveTowards(density, target, speed * Time.deltaTime);

        // 判定区随密度扩张（扩大判定范围，同时粒子范围跟随）
        if (expandZoneOnStir)
        {
            float expand = Mathf.Lerp(1f, maxExpansion, density);
            Box.size = new Vector3(baseSize.x * expand, baseSize.y * expand, baseSize.z);
        }

        // 粒子持续发射量随密度；形状跟随判定区大小
        if (ps != null)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = active ? maxEmissionRate * density : 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.scale = new Vector3(Box.size.x, Box.size.y, 0.4f);
        }

        // 遮挡视线：浑浊蒙层透明度随密度
        if (tint != null)
        {
            tint.color = new Color(
                visionTint.r, visionTint.g, visionTint.b,
                maxVisionAlpha * density);
        }

        // 区内 + 密度较高 → flag3（真正的 flag 写入在 LateUpdate 汇总）
        high = active && density >= highDensityThreshold;

        if (verboseLog && high != wasHigh)
        {
            wasHigh = high;
            Debug.Log($"[泥沙] {name} 密度{(high ? "较高（flag3=1）" : "降低（flag3=0）")}，当前 {density:0.00}");
        }
    }

    // 到期的爆发发射器归还对象池
    private void UpdateBurstReleases()
    {
        if (activeBursts.Count == 0) return;

        float now = Time.time;
        for (int i = activeBursts.Count - 1; i >= 0; i--)
        {
            if (now < activeBursts[i].releaseTime) continue;

            burstPool?.Release(activeBursts[i].system);
            activeBursts.RemoveAt(i);
        }
    }

    // ---- 强度映射 ----

    private SiltActivity ActivityFromState()
    {
        PlayerSM p = Player;
        if (p == null) return SiltActivity.Idle;

        BaseState s = p.CurrentState;
        if (s == p.dashState) return SiltActivity.Dash;
        if (s == p.swimState) return SiltActivity.Swim;
        return SiltActivity.Idle;
    }

    private float TargetFor(SiltActivity act)
    {
        switch (act)
        {
            case SiltActivity.Swim: return swimTargetDensity;
            case SiltActivity.Dash: return dashTargetDensity;
            default: return idleTargetDensity;
        }
    }

    private void Burst(SiltActivity act)
    {
        int count = Mathf.RoundToInt(stirBurstCount * TargetFor(act));
        count = Mathf.Clamp(count, 1, 200000);

        if (burstPool != null)
        {
            // 从对象池取一个发射器，放在判定区中心，爆发后按寿命归还
            ParticleSystem emitter = burstPool.Get();
            emitter.transform.localPosition = new Vector3(0f, 0f, particleZOffset);

            if (particlePrefab == null)
            {
                // 默认粒子：形状跟随当前判定区大小，只靠 Emit 爆发
                ParticleSystem.ShapeModule shape = emitter.shape;
                shape.scale = new Vector3(Box.size.x, Box.size.y, 0.4f);

                ParticleSystem.EmissionModule emission = emitter.emission;
                emission.rateOverTime = 0f;
            }

            emitter.Emit(count);

            activeBursts.Add(new PooledBurst
            {
                system = emitter,
                releaseTime = Time.time + particleLifetime + 0.5f,
            });

            if (verboseLog)
                Debug.Log($"[泥沙] {name} 激起泥沙（{act}）：粒子 {count}（对象池）");
        }
        else if (ps != null)
        {
            // 对象池不可用时退回常驻系统
            ps.Emit(count);
        }

        OnStirred?.Invoke(act);
    }

    // ---- 粒子 / 蒙层 ----

    private void BuildParticles()
    {
        if (particlePrefab != null)
        {
            // 常驻系统：持续弥散
            ps = Instantiate(particlePrefab, transform);
            ps.transform.localPosition = new Vector3(0f, 0f, particleZOffset);

            // 爆发对象池的模板（隐藏，仅用于克隆）
            ParticleSystem template = Instantiate(particlePrefab, transform);
            template.transform.localPosition = new Vector3(0f, 0f, particleZOffset);
            template.gameObject.SetActive(false);

            burstPool = new ParticlePool(template, particlePoolSize);
            return;
        }

        BuildSharedParticleMaterial();

        ps = CreateDefaultParticleSystem("SiltAmbientParticles");

        ParticleSystem burstTemplate = CreateDefaultParticleSystem("SiltBurstTemplate");
        burstTemplate.gameObject.SetActive(false);

        burstPool = new ParticlePool(burstTemplate, particlePoolSize);
    }

    // 运行时生成一份共享的粒子材质/贴图（常驻系统与对象池共用）
    private void BuildSharedParticleMaterial()
    {
        particleMat = MakeParticleMaterial();
        particleTex = MakeSoftParticleTexture(64);
        if (particleMat.HasProperty("_BaseMap")) particleMat.SetTexture("_BaseMap", particleTex);
        if (particleMat.HasProperty("_MainTex")) particleMat.SetTexture("_MainTex", particleTex);
    }

    // 创建一个配置好的默认泥沙粒子系统（子物体，Z 偏移到相机一侧）
    private ParticleSystem CreateDefaultParticleSystem(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, particleZOffset);

        ParticleSystem system = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = system.main;
        main.startLifetime = particleLifetime;
        main.startSpeed = particleSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(particleSizeRange.x, particleSizeRange.y);
        main.startColor = siltColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f; // 零重力：泥沙悬浮水中自由扩散，不会往下沉
        main.maxParticles = maxParticlesPerSystem;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Box.size.x, Box.size.y, 0.4f);
        shape.randomDirectionAmount = 1f; // 初速度全方向随机 → 向四面八方自由扩散

        // 从很小长到正常大小，像扩散的泥沙云
        ParticleSystem.SizeOverLifetimeModule sizeLt = system.sizeOverLifetime;
        sizeLt.enabled = true;
        sizeLt.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f));

        // 淡入淡出
        ParticleSystem.ColorOverLifetimeModule colorLt = system.colorOverLifetime;
        colorLt.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f),
            });
        colorLt.color = new ParticleSystem.MinMaxGradient(grad);

        // 轻微噪声随机游走：颗粒像水中泥沙一样缓慢无规则弥散，而不是直线飘走
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.4f;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 100; // 盖在角色之上
        renderer.sharedMaterial = particleMat;

        return system;
    }

    private void BuildVision()
    {
        if (!useFullScreenTint) return;

        var canvasGo = new GameObject("SiltVisionCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 13; // HUD/任务/结算之下，屏幕效果(-15)之上

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var imgGo = new GameObject("Tint", typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);

        tint = imgGo.GetComponent<Image>();
        tint.color = new Color(visionTint.r, visionTint.g, visionTint.b, 0f);
        tint.raycastTarget = false;

        RectTransform rt = tint.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Material MakeParticleMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        return mat;
    }

    // 柔边圆点贴图，比硬边方块更像泥沙
    private static Texture2D MakeSoftParticleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // 调试：手动激起一次（冲刺强度）
    [ContextMenu("测试激起（冲刺强度）")]
    public void DebugStir()
    {
        density = Mathf.Max(density, dashTargetDensity);
        Burst(SiltActivity.Dash);
    }
}
