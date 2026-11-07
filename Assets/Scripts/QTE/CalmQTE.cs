using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 平复心情 QTE（按钮2 触发，星露谷钓鱼式半拉圆环）：
//   黄色光标受重力向左滑，按住任意键向右推。
//   心脏在灰色半圆上来回随机移动，要让心脏保持在黄色光标区内。
//   保持 → 橙色进度条上升；离开 → 下降（速度相同，25%/s）。
//   进度满 → QTE 成功；进度归 0 持续 ≥0.5s → QTE 失败。
// 视觉风格与穿越产道一致，放在屏幕左侧，不做镜头缩放。
public class CalmQTE : MonoBehaviour
{
    public static CalmQTE Instance { get; private set; }

    [Header("手感")]
    [Tooltip("光标向左的重力加速度（弧参数/秒²）")]
    [SerializeField] private float gravityAccel = 2.0f;
    [Tooltip("按住任意键向右的加速度（弧参数/秒²）")]
    [SerializeField] private float pushAccel = 5.5f;
    [Tooltip("速度阻尼，越大越跟手、惯性越小")]
    [SerializeField] private float drag = 4f;
    [SerializeField] private float maxSpeed = 0.9f;
    [Tooltip("黄色光标区半宽（弧参数，0~1）")]
    [SerializeField] private float zoneHalfT = 0.13f;

    [Header("心脏")]
    [SerializeField] private float heartSpeed = 0.38f;
    [SerializeField] private float heartRetargetMin = 0.7f;
    [SerializeField] private float heartRetargetMax = 1.5f;

    [Header("进度")]
    [Tooltip("保持时进度条每秒上升量（1/3 ≈ 3 秒涨满）")]
    [SerializeField] private float progressUpRate = 1f / 3f;
    [Tooltip("离开时每秒下降量（原速 0.25 的一半）")]
    [SerializeField] private float progressDownRate = 0.125f;
    [SerializeField] private float startProgress = 0f;
    [Tooltip("进度为空持续多少秒判失败")]
    [SerializeField] private float emptyFailTime = 0.5f;

    [Header("视觉")]
    [SerializeField] private float ringSize = 300f;
    [SerializeField] private Color grayRingColor = new Color(0.5f, 0.55f, 0.6f, 0.85f);
    [SerializeField] private Color cursorColor = new Color(1f, 0.82f, 0.12f, 0.95f);
    [SerializeField] private Color heartColor = new Color(1f, 0.32f, 0.36f, 1f);
    [SerializeField] private Color progressColor = new Color(1f, 0.6f, 0.2f, 1f);
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.25f);

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;

    public bool IsActive { get; private set; }
    public event Action<bool> OnFinished;

    private GameObject canvasGo;
    private RectTransform ringRoot;
    private RectTransform cursorZoneRt;
    private RectTransform heartRt;
    private RectTransform progressFillRt;
    private Text statusText;
    private Text heartText;

    private float cursorT = 0.5f;
    private float cursorV;
    private float heartT = 0.5f;
    private float heartTarget = 0.5f;
    private float heartRetargetTimer;
    private float progress;
    private float emptyTime;

    private Action<bool> currentCallback;
    private Coroutine session;

    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载（R 重开）后重建
        }
        if (FindFirstObjectByType<CalmQTE>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("~CalmQTE");
        go.AddComponent<CalmQTE>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        Instance = this;
        BuildUi();
        canvasGo.SetActive(false);
    }

    private void OnDestroy()
    {
        if (session != null) StopCoroutine(session);
        if (IsActive) { IsActive = false; QteState.End(); }
        if (Instance == this) Instance = null;
    }

    public bool Play(Action<bool> onFinished)
    {
        if (IsActive) return false;

        currentCallback = onFinished;
        IsActive = true;
        QteState.Begin();
        session = StartCoroutine(Run());
        return true;
    }

    public void ForceStop()
    {
        if (!IsActive) return;
        if (session != null) StopCoroutine(session);
        Finish(false);
    }

    private IEnumerator Run()
    {
        canvasGo.SetActive(true);
        statusText.text = "按住任意键，让 ♥ 保持在黄色区域内";

        cursorT = 0.5f;
        cursorV = 0f;
        heartT = 0.5f;
        heartTarget = UnityEngine.Random.Range(0.1f, 0.9f);
        heartRetargetTimer = 0f;
        progress = Mathf.Clamp01(startProgress);
        emptyTime = 0f;

        while (IsActive)
        {
            float dt = Time.unscaledDeltaTime;

            // 光标：重力向左，按住任意键向右推；加阻尼降低惯性、更跟手
            cursorV -= gravityAccel * dt;
            if (Input.anyKey) cursorV += pushAccel * dt;
            cursorV *= Mathf.Exp(-drag * dt);
            cursorV = Mathf.Clamp(cursorV, -maxSpeed, maxSpeed);
            cursorT += cursorV * dt;
            if (cursorT <= 0f) { cursorT = 0f; cursorV = Mathf.Max(0f, cursorV); }
            if (cursorT >= 1f) { cursorT = 1f; cursorV = Mathf.Min(0f, cursorV); }

            // 心脏：随机来回移动
            heartRetargetTimer -= dt;
            if (heartRetargetTimer <= 0f || Mathf.Abs(heartT - heartTarget) < 0.02f)
            {
                heartTarget = UnityEngine.Random.Range(0.08f, 0.92f);
                heartRetargetTimer = UnityEngine.Random.Range(heartRetargetMin, heartRetargetMax);
            }
            heartT = Mathf.MoveTowards(heartT, heartTarget, heartSpeed * dt);

            // 判定：心脏是否在黄色光标区内
            bool keep = Mathf.Abs(cursorT - heartT) <= zoneHalfT;
            if (keep)
            {
                progress += progressUpRate * dt;
                emptyTime = 0f;
            }
            else
            {
                progress -= progressDownRate * dt;
            }
            progress = Mathf.Clamp01(progress);

            if (progress <= 0.0001f)
                emptyTime += dt;
            else
                emptyTime = 0f;

            UpdateVisuals();

            if (progress >= 1f)
            {
                statusText.text = "QTE 成功！";
                Finish(true);
                yield break;
            }
            if (emptyTime >= emptyFailTime)
            {
                statusText.text = "QTE 失败";
                Finish(false);
                yield break;
            }

            yield return null;
        }
    }

    private void UpdateVisuals()
    {
        // 黄色光标区
        cursorZoneRt.localRotation = Quaternion.Euler(0f, 0f, 90f - cursorT * 180f);

        // 心脏位置
        float ang = (180f - heartT * 180f) * Mathf.Deg2Rad;
        float radius = ringSize * 0.5f * 0.86f;
        heartRt.anchoredPosition = new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);

        // 橙色进度条
        float barHeight = 240f;
        progressFillRt.sizeDelta = new Vector2(28f, barHeight * progress);
    }

    private void Finish(bool success)
    {
        if (!IsActive) return;

        IsActive = false;
        canvasGo.SetActive(false);
        QteState.End();

        if (verboseLog) Debug.Log($"[平复QTE] {(success ? "成功" : "失败")}");

        Action<bool> cb = currentCallback;
        currentCallback = null;
        cb?.Invoke(success);
    }

    // ---- UI ----
    private void BuildUi()
    {
        Canvas canvas = QteUi.CreateOverlay(transform, "CalmQteCanvas", short.MaxValue - 7);
        canvasGo = canvas.gameObject;

        var dim = QteUi.NewImage(canvas.transform, "Dim", dimColor);
        QteUi.Stretch(dim.rectTransform);

        // 半环容器（左侧）
        ringRoot = QteUi.NewRect(canvas.transform, "RingRoot");
        QteUi.LeftMiddle(ringRoot, 380f, 40f, ringSize, ringSize);

        // 灰色半圆环（上半圆）
        Image baseArc = QteUi.NewImage(ringRoot, "BaseArc", grayRingColor);
        baseArc.sprite = QteUi.SpriteFrom(QteUi.MakeArcTexture(256, 0.72f, 1f, 90f));
        QteUi.Stretch(baseArc.rectTransform);

        // 黄色光标区（圆弧，随光标旋转）
        Image cursorZone = QteUi.NewImage(ringRoot, "CursorZone", cursorColor);
        cursorZone.sprite = QteUi.SpriteFrom(QteUi.MakeArcTexture(256, 0.72f, 1f, zoneHalfT * 180f));
        cursorZoneRt = cursorZone.rectTransform;
        QteUi.Stretch(cursorZoneRt);

        // 心脏
        heartText = QteUi.NewText(ringRoot, "Heart", 40, TextAnchor.MiddleCenter, heartColor);
        heartText.text = "♥";
        heartRt = heartText.rectTransform;
        QteUi.Place(heartRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));

        // 橙色进度条（圆环右侧）
        var barBg = QteUi.NewImage(canvas.transform, "ProgressBg", new Color(0f, 0f, 0f, 0.55f));
        QteUi.LeftMiddle(barBg.rectTransform, 380f + ringSize * 0.5f + 60f, 40f, 28f, 240f);

        var fill = QteUi.NewImage(barBg.transform, "ProgressFill", progressColor);
        progressFillRt = fill.rectTransform;
        QteUi.Place(progressFillRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(28f, 0f));

        // 状态文字
        statusText = QteUi.NewText(canvas.transform, "Status", 30, TextAnchor.MiddleCenter, Color.white);
        QteUi.LeftMiddle(statusText.rectTransform, 380f, -180f, 700f, 50f);
    }
}
