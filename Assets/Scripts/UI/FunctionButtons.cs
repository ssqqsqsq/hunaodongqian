using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 屏幕左下角的 3 个功能按钮 + 流程逻辑：
//   按钮1 切换气瓶：点击后 10s 倒计时 + 换瓶 QTE；成功 flag1=0 并切换气瓶；
//                   失败 flag1=1，1s 后重试（倒计时继续）；10s 结束则死亡。
//   按钮2 平复心有余悸：仅 flag5=1 时可点；成功后 flag5=0，失败则结束。
//   按钮3 卸下备用气瓶：3s 进度条，再点一次取消；完成则把备用气瓶交到手上。
// 任一 QTE 进行中时所有按钮不可点击。键盘 1/2/3 等同点击。
public class FunctionButtons : MonoBehaviour
{
    [Header("按钮1 切换气瓶")]
    [SerializeField] private float tankSwitchTimeout = 15f;
    [SerializeField] private float tankSwitchRetryDelay = 1f;

    [Header("按钮3 卸下气瓶")]
    [SerializeField] private float detachDuration = 3f;

    [Header("外观")]
    [SerializeField] private Color buttonColor = new Color(0.18f, 0.2f, 0.24f, 0.92f);
    [SerializeField] private Color buttonTextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
    [SerializeField] private Color statusColor = new Color(0.92f, 0.94f, 0.97f, 1f);
    [SerializeField] private Color progressColor = new Color(1f, 0.7f, 0.25f, 1f);

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;
    [Tooltip("F8 设 flag5=1；F9 补满备用瓶（方便测试按钮2/3）")]
    [SerializeField] private bool debugHotkeys = true;

    private DebuffManager dm;
    private PlayerSM player;
    private OxygenComponent oxygen;
    private HandheldController handheld;

    private GameObject canvasGo;
    [SerializeField] private Button btn1, btn2, btn3;
    [SerializeField] private Text statusText;
    [SerializeField] private GameObject statusBarGo;
    [SerializeField] private RectTransform statusFillRt;

    private bool tankFlowActive;
    private float tankDeadline;
    private bool detaching;
    private float detachTime;
    private bool breathHolding;
    private float breathDeadline;
    private bool oxygenHooked;

    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载（R 重开）后重建
        }
        if (FindFirstObjectByType<FunctionButtons>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("~FunctionButtons");
        go.AddComponent<FunctionButtons>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        bool prebuilt = btn1 != null && btn2 != null && btn3 != null;
        if (prebuilt)
        {
            // 场景里已手动放置技能按钮 → 只接线，不再运行时创建
            EnsureEventSystem();
            Wire(btn1, OnButton1);
            Wire(btn2, OnButton2);
            Wire(btn3, OnButton3);
        }
        else
        {
            BuildUi();
        }
    }

    private static void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        if (b.targetGraphic == null) b.targetGraphic = b.GetComponent<Image>();
        b.onClick.RemoveListener(action);
        b.onClick.AddListener(action);
    }

    private void Start()
    {
        ResolveRefs();
    }

    private void OnDestroy()
    {
        if (oxygen != null) oxygen.OnTankEmpty -= HandleTankEmpty;
    }

    private void ResolveRefs()
    {
        if (dm == null) dm = FindFirstObjectByType<DebuffManager>();
        if (player == null) player = FindFirstObjectByType<PlayerSM>();
        if (oxygen == null)
        {
            oxygen = FindFirstObjectByType<OxygenComponent>();
            if (oxygen != null && !oxygenHooked)
            {
                oxygenHooked = true;
                oxygen.OnTankEmpty += HandleTankEmpty;
            }
        }
        if (handheld == null) handheld = HandheldController.Instance;
    }

    private void Update()
    {
        ResolveRefs();

        bool settled = SettlementScreen.Instance != null && SettlementScreen.Instance.IsShown;
        bool busy = QteState.Busy || settled;
        bool flag5 = dm != null && dm.Flags.IsTrue(5);
        bool hasSpare = oxygen != null && oxygen.HasAirTank;

        // 换瓶需要身上有两个气瓶（有备用瓶）才可点
        if (btn1 != null) btn1.interactable = !busy && !tankFlowActive && hasSpare;
        if (btn2 != null) btn2.interactable = !busy && !tankFlowActive && flag5;
        if (btn3 != null) btn3.interactable = !busy && !tankFlowActive && (hasSpare || detaching);

        UpdateDetach();
        UpdateBreathHold();
        UpdateStatus();

        if (!busy) HandleKeys();
    }

    private void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) OnButton1();
        if (Input.GetKeyDown(KeyCode.Alpha2)) OnButton2();
        if (Input.GetKeyDown(KeyCode.Alpha3)) OnButton3();

        if (debugHotkeys)
        {
            if (Input.GetKeyDown(KeyCode.F8) && dm != null)
            {
                dm.SetFlag(5, true);
                if (verboseLog) Debug.Log("[调试] flag5=1（心有余悸）");
            }
            if (Input.GetKeyDown(KeyCode.F9) && oxygen != null)
            {
                oxygen.SetSpareTank(oxygen.MaxOxygen);
                if (verboseLog) Debug.Log("[调试] 备用瓶已补满");
            }
            if (Input.GetKeyDown(KeyCode.F10) && dm != null)
            {
                // 模拟突发事件开关：置真=事件发生，置假=事件解决（解决后应触发心有余悸 flag5）
                bool v = !dm.Flags.IsTrue(1);
                dm.SetFlag(1, v);
                if (verboseLog) Debug.Log($"[调试] flag1={v}（模拟突发事件）");
            }
        }
    }

    // ---- 按钮1：切换气瓶 ----
    private void OnButton1()
    {
        if (tankFlowActive || QteState.Busy) return;
        // 只有身上有两个气瓶（有备用瓶）才能换瓶
        if (oxygen == null || !oxygen.HasAirTank) return;
        StartTankSwitchFlow();
    }

    // 换瓶流程（按钮1 点击 / 当前瓶耗尽时强制触发 共用）
    private void StartTankSwitchFlow()
    {
        if (tankFlowActive) return;
        tankFlowActive = true;
        StartCoroutine(TankSwitchFlow());
    }

    // 当前气瓶耗尽：有备用瓶 → 强制换瓶 QTE；只有一个气瓶 → 15s 憋气倒计时
    private void HandleTankEmpty()
    {
        if (oxygen == null) return;

        if (oxygen.HasAirTank)
        {
            if (verboseLog) Debug.Log("[换瓶] 当前气瓶耗尽 → 强制换瓶 QTE");
            StartTankSwitchFlow();
        }
        else
        {
            StartBreathHold();
        }
    }

    private IEnumerator TankSwitchFlow()
    {
        tankDeadline = Time.time + tankSwitchTimeout;

        while (Time.time < tankDeadline)
        {
            bool? result = null;

            if (TankSwitchQTE.Instance == null || !TankSwitchQTE.Instance.Play(r => result = r))
            {
                if (verboseLog) Debug.LogWarning("[换瓶] 无法启动换瓶 QTE");
                break;
            }

            while (result == null && Time.time < tankDeadline)
                yield return null;

            if (result == null)
            {
                // 10s 到但 QTE 还没结束：强制停
                TankSwitchQTE.Instance?.ForceStop();
                break;
            }

            if (result == true)
            {
                dm?.SetFlag(1, false);
                oxygen?.SwitchTank();
                if (verboseLog) Debug.Log("[换瓶] 成功 → flag1=0，切换当前气瓶");
                tankFlowActive = false;

                // 备用瓶也没气：切换救不活 → 转入憋气倒计时
                if (oxygen != null && oxygen.CurrentOxygen <= 0f)
                    StartBreathHold();

                yield break;
            }

            // result == false
            dm?.SetFlag(1, true);
            if (verboseLog) Debug.Log("[换瓶] 失败 → flag1=1，1 秒后重试");

            float retryAt = Time.time + tankSwitchRetryDelay;
            while (Time.time < retryAt && Time.time < tankDeadline)
                yield return null;
        }

        tankFlowActive = false;

        if (Time.time >= tankDeadline)
        {
            if (verboseLog) Debug.Log("[换瓶] 10s 倒计时结束 → 死亡");
            player?.Kill();
        }
    }

    // ---- 按钮2：平复心有余悸 ----
    private void OnButton2()
    {
        if (QteState.Busy || tankFlowActive) return;
        if (dm == null || !dm.Flags.IsTrue(5)) return;
        if (CalmQTE.Instance == null) return;

        CalmQTE.Instance.Play(r =>
        {
            if (r)
            {
                dm.SetFlag(5, false);
                if (verboseLog) Debug.Log("[平复] 成功 → flag5=0");
            }
            else if (verboseLog)
            {
                Debug.Log("[平复] 失败，结束");
            }
        });
    }

    // ---- 按钮3：卸下备用气瓶 ----
    private void OnButton3()
    {
        if (QteState.Busy || tankFlowActive) return;

        if (detaching)
        {
            detaching = false;
            if (verboseLog) Debug.Log("[卸瓶] 已取消");
            return;
        }

        if (oxygen == null || !oxygen.HasAirTank) return;

        detaching = true;
        detachTime = Time.time;
        if (verboseLog) Debug.Log("[卸瓶] 开始卸下备用气瓶");
    }

    private void UpdateDetach()
    {
        if (!detaching) return;

        float p = (Time.time - detachTime) / Mathf.Max(0.01f, detachDuration);
        if (p >= 1f) CompleteDetach();
    }

    // 憋气：只有一个气瓶且耗尽时倒计时（气体恢复则取消），超时死亡
    private void StartBreathHold()
    {
        if (breathHolding || tankFlowActive) return;
        breathHolding = true;
        breathDeadline = Time.time + tankSwitchTimeout;
        if (verboseLog) Debug.Log($"[憋气] 无备用气瓶，开始 {tankSwitchTimeout:0}s 憋气倒计时");
    }

    private void UpdateBreathHold()
    {
        if (!breathHolding) return;

        // 气体恢复（例如装备手上气瓶并成功换瓶）→ 取消憋气
        if (oxygen != null && oxygen.CurrentOxygen > 0f)
        {
            breathHolding = false;
            return;
        }

        if (Time.time >= breathDeadline)
        {
            breathHolding = false;
            if (verboseLog) Debug.Log("[憋气] 憋气时间到 → 死亡");
            player?.Kill();
        }
    }

    private void CompleteDetach()
    {
        detaching = false;

        if (oxygen == null || !oxygen.HasAirTank) return;

        float gas = oxygen.SpareOxygen;
        float maxGas = oxygen.MaxOxygen;

        // 备用瓶移到手上：气体仍计入总气量，丢出气瓶后才扣除
        oxygen.HasAirTank = false;
        oxygen.SetHeldTank(gas);

        if (handheld == null) handheld = HandheldController.Instance;
        if (handheld != null) handheld.ReceiveTank(gas, maxGas);

        if (verboseLog) Debug.Log($"[卸瓶] 完成，备用气瓶拿在手上（气体 {gas:0}，仍计入总气量）");
    }

    private void UpdateStatus()
    {
        string msg = "";
        bool showBar = false;

        if (tankFlowActive)
        {
            float remain = Mathf.Max(0f, tankDeadline - Time.time);
            msg = $"换瓶中…  剩余 {remain:0.0}s";
        }
        else if (breathHolding)
        {
            float remain = Mathf.Max(0f, breathDeadline - Time.time);
            msg = $"憋气中…  剩余 {remain:0.0}s";
        }
        else if (detaching)
        {
            msg = "正在卸下备用气瓶......（再次点按3取消）";
            showBar = true;
        }

        if (statusText != null)
        {
            statusText.text = msg;
            statusText.gameObject.SetActive(msg.Length > 0);
        }

        if (statusBarGo != null) statusBarGo.SetActive(showBar);
        if (showBar && statusFillRt != null)
        {
            float p = Mathf.Clamp01((Time.time - detachTime) / Mathf.Max(0.01f, detachDuration));
            statusFillRt.sizeDelta = new Vector2(460f * p, 10f);
        }
    }

    // ---- UI ----
    private void BuildUi()
    {
        Canvas canvas = QteUi.CreateOverlay(transform, "FunctionButtonsCanvas", short.MaxValue - 11);
        canvasGo = canvas.gameObject;
        // 按钮点击需要 GraphicRaycaster + EventSystem
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        const float btnSize = 88f;
        const float gap = 16f;
        const float right = 30f; // 距右边距离
        const float baseY = 236f;

        // 右下角从右往左排：卸瓶最右，换瓶最左
        btn3 = MakeButton(canvas.transform, "Btn3", new Vector2(right, baseY), btnSize, "卸瓶", OnButton3);
        btn2 = MakeButton(canvas.transform, "Btn2", new Vector2(right + btnSize + gap, baseY), btnSize, "❤↓", OnButton2);
        btn1 = MakeButton(canvas.transform, "Btn1", new Vector2(right + 2f * (btnSize + gap), baseY), btnSize, "换瓶", OnButton1);

        // 状态文字（按钮上方，靠右）
        statusText = QteUi.NewText(canvas.transform, "Status", 20, TextAnchor.MiddleRight, statusColor);
        QteUi.BottomRight(statusText.rectTransform, right, baseY + btnSize + 14f, 640f, 30f);
        statusText.gameObject.SetActive(false);

        // 状态进度条
        var barBg = QteUi.NewImage(canvas.transform, "StatusBarBg", new Color(0f, 0f, 0f, 0.55f));
        statusBarGo = barBg.gameObject;
        QteUi.BottomRight(barBg.rectTransform, right, baseY + btnSize + 2f, 460f, 10f);

        var fill = QteUi.NewImage(barBg.transform, "StatusFill", progressColor);
        statusFillRt = fill.rectTransform;
        QteUi.Place(statusFillRt, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 10f));

        statusBarGo.SetActive(false);
    }

    private Button MakeButton(Transform parent, string name, Vector2 pos, float size, string label, UnityEngine.Events.UnityAction onClick)
    {
        Image bg = QteUi.NewImage(parent, name, buttonColor);
        bg.raycastTarget = true; // 可点击
        QteUi.BottomRight(bg.rectTransform, pos.x, pos.y, size, size);

        Button btn = bg.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);

        Text t = QteUi.NewText(bg.transform, "Label", 26, TextAnchor.MiddleCenter, buttonTextColor);
        QteUi.Stretch(t.rectTransform);
        t.text = label;

        return btn;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
