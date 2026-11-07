using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 手持物系统：
//   · 靠近物品显示「按 F 拾取」，F 拾到手上（手上已有则先丢下旧的）
//   · 手上物品：左下角卡片显示 名字 / 图标 / 信息 / 操作
//   · 轻按 Q 放下，长按 Q 蓄力至多 2s 后投掷
//   · 气瓶：按 E 装备（2s 进度条，再按 E 取消）；收集品：按 E 查看详情
// 运行时自动创建。
public class HandheldController : MonoBehaviour
{
    public static HandheldController Instance { get; private set; }

    [Header("拾取")]
    [Tooltip("扫描附近物品的最大半径（物品自身 pickupRadius 仍会再判定一次）")]
    [SerializeField] private float pickupScanRadius = 4f;

    [Header("投掷")]
    [SerializeField] private float tapThreshold = 0.25f;    // 小于它算点按放下
    [SerializeField] private float throwMaxCharge = 2f;     // 最大蓄力时长
    [SerializeField] private float throwMinDistance = 2f;
    [SerializeField] private float throwMaxDistance = 8f;

    [Header("气瓶")]
    [SerializeField] private float equipDuration = 2f;      // 装备进度条时长

    [Header("外观")]
    [SerializeField] private Color cardColor = new Color(0.06f, 0.07f, 0.09f, 0.82f);
    [SerializeField] private Color nameColor = new Color(0.96f, 0.97f, 0.99f, 1f);
    [SerializeField] private Color infoColor = new Color(0.78f, 0.82f, 0.86f, 1f);
    [SerializeField] private Color hintColor = new Color(0.62f, 0.67f, 0.72f, 1f);
    [SerializeField] private Color chargeColor = new Color(1f, 0.82f, 0.3f, 1f);
    [SerializeField] private Color equipColor = new Color(0.4f, 0.85f, 1f, 1f);

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;

    public bool HasItem => held != null;
    public ItemData Held => held;

    private ItemData held;
    private HoldableItem source;
    private readonly HashSet<string> registeredCollectibles = new HashSet<string>();

    private PlayerSM player;
    private OxygenComponent oxygen;
    private LevelStats stats;

    private HoldableItem nearest;

    // 输入状态
    private bool qCharging;
    private float qPressTime;
    private float qCharge;
    private bool equipping;
    private float equipTime;
    private bool showDetails;

    // UI
    private GameObject canvasGo;
    private GameObject cardGo;
    private Image cardIcon;
    private Text cardName;
    private Text cardInfo;
    private Text cardHint;
    private Text handLabel;
    private GameObject chargeBarGo;
    private RectTransform chargeFillRt;
    private GameObject equipBarGo;
    private RectTransform equipFillRt;
    private Text pickupPrompt;

    private Sprite circleSprite;

    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载（R 重开）后重建
        }
        if (FindFirstObjectByType<HandheldController>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("~HandheldController");
        go.AddComponent<HandheldController>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        Instance = this;
        BuildUi();
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerSM>();
        if (player == null) Debug.LogWarning("HandheldController：找不到 PlayerSM");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerSM>();
            if (player == null) return;
        }

        // QTE 进行中 / 死亡 / 结算中：不处理手持操作
        bool blocked = QteState.Busy || player.IsDead
            || (SettlementScreen.Instance != null && SettlementScreen.Instance.IsShown);

        ScanNearest();
        UpdatePickupPrompt(blocked);

        if (!blocked)
        {
            HandleF();
            HandleE();
            HandleQ();
        }
        else if (qCharging)
        {
            qCharging = false;
            qCharge = 0f;
        }

        UpdateEquip(blocked);
        RefreshUI();
    }

    // ---- 拾取 ----
    private void ScanNearest()
    {
        nearest = null;
        float best = float.MaxValue;

        HoldableItem[] all = FindObjectsByType<HoldableItem>(FindObjectsSortMode.None);
        Vector2 p = player.transform.position;

        foreach (HoldableItem item in all)
        {
            if (item == null || item.IsPickedUp) continue;

            float dist = Vector2.Distance(p, item.transform.position);
            if (dist > item.pickupRadius) continue;
            if (dist > pickupScanRadius) continue;

            if (dist < best)
            {
                best = dist;
                nearest = item;
            }
        }
    }

    private void UpdatePickupPrompt(bool blocked)
    {
        if (pickupPrompt == null) return;

        bool show = !blocked && nearest != null && held == null;
        pickupPrompt.gameObject.SetActive(show);
        if (show)
            pickupPrompt.text = $"按 F 拾取：{nearest.data.displayName}";
    }

    private void HandleF()
    {
        if (nearest == null) return;
        if (!Input.GetKeyDown(KeyCode.F)) return;

        // 手上已有物品：先丢下旧的，再拾起新的
        if (held != null) DropCurrent(Vector2.zero, false);

        PickUp(nearest);
    }

    private void PickUp(HoldableItem item)
    {
        held = item.data.Clone();
        source = item;
        item.PickUp();

        equipping = false;
        qCharging = false;
        qCharge = 0f;
        showDetails = false;

        if (held.kind == ItemKind.Collectible)
            RegisterCollectible(held);

        if (verboseLog) Debug.Log($"[手持] 拿到：{held.displayName}");
    }

    private void RegisterCollectible(ItemData d)
    {
        if (stats == null) stats = FindFirstObjectByType<LevelStats>();
        if (stats == null) return;

        string id = d.displayName + "#" + d.GetHashCode();
        if (!registeredCollectibles.Add(id)) return;

        stats.AddCollectible(new CollectibleData(id, d.displayName, d.icon));
    }

    // ---- 放下 / 投掷 ----
    private void HandleQ()
    {
        if (held == null)
        {
            qCharging = false;
            qCharge = 0f;
            return;
        }
        if (equipping) return; // 装备中不处理 Q

        if (Input.GetKeyDown(KeyCode.Q))
        {
            qCharging = true;
            qPressTime = Time.time;
            qCharge = 0f;
        }

        if (qCharging && Input.GetKey(KeyCode.Q))
        {
            qCharge = Mathf.Min(throwMaxCharge, Time.time - qPressTime);
        }

        if (qCharging && Input.GetKeyUp(KeyCode.Q))
        {
            float heldTime = Time.time - qPressTime;
            qCharging = false;

            if (heldTime < tapThreshold)
                DropCurrent(Vector2.zero, false);
            else
                DropCurrent(Vector2.zero, true, Mathf.Clamp01(qCharge / throwMaxCharge));

            qCharge = 0f;
        }
    }

    // 把手上物品放回世界（throwIt=true 则按朝向投掷）
    private void DropCurrent(Vector2 dir, bool throwIt, float power = 0f)
    {
        if (held == null) return;

        Vector2 playerPos = player != null ? (Vector2)player.transform.position : Vector2.zero;
        Vector2 placePos = playerPos;

        if (throwIt)
        {
            if (dir.sqrMagnitude < 0.0001f)
                dir = new Vector2(player != null && player.facingSign >= 0f ? 1f : -1f, 0f);
            dir.Normalize();
            float dist = Mathf.Lerp(throwMinDistance, throwMaxDistance, power);
            placePos = playerPos + dir * dist;
        }

        if (source != null)
        {
            source.PlaceInWorld(placePos);
        }
        else
        {
            // 手上物品没有世界来源（例如卸下的气瓶）：新建一个世界物品
            var go = new GameObject("Dropped_" + held.displayName);
            go.transform.position = placePos;
            var item = go.AddComponent<HoldableItem>();
            item.data = held.Clone();
        }

        if (verboseLog)
            Debug.Log($"[手持] {(throwIt ? "投掷" : "放下")}：{held.displayName} @ {placePos}");

        // 丢出气瓶 → 总气量扣除该瓶气体（手上气瓶=卸下的备用瓶时才会计入总气量）
        if (held.kind == ItemKind.AirTank)
        {
            if (oxygen == null) oxygen = FindFirstObjectByType<OxygenComponent>();
            if (oxygen != null) oxygen.ClearHeldTank();
        }

        held = null;
        source = null;
        equipping = false;
        qCharging = false;
        qCharge = 0f;
        showDetails = false;
    }

    // ---- E：装备气瓶 / 查看详情 ----
    private void HandleE()
    {
        if (held == null) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (held.kind == ItemKind.AirTank)
        {
            if (equipping)
            {
                equipping = false;
                if (verboseLog) Debug.Log("[手持] 取消装备");
            }
            else
            {
                equipping = true;
                equipTime = Time.time;
                if (verboseLog) Debug.Log("[手持] 开始装备备用气瓶");
            }
        }
        else
        {
            showDetails = !showDetails;
        }
    }

    private void UpdateEquip(bool blocked)
    {
        if (!equipping) return;

        if (held == null || held.kind != ItemKind.AirTank)
        {
            equipping = false;
            return;
        }

        if (blocked) return;

        float progress = (Time.time - equipTime) / Mathf.Max(0.01f, equipDuration);
        if (progress >= 1f)
        {
            FinishEquip();
        }
    }

    private void FinishEquip()
    {
        equipping = false;

        if (oxygen == null) oxygen = FindFirstObjectByType<OxygenComponent>();
        if (oxygen != null && held != null) oxygen.SetSpareTank(held.gasRemaining);

        if (verboseLog) Debug.Log("[手持] 气瓶已装备到身上");

        held = null;
        source = null;
        qCharging = false;
        qCharge = 0f;
        showDetails = false;
    }

    // 供功能按钮调用：把卸下的备用气瓶交到手上
    public void ReceiveTank(float gasRemaining, float gasMax)
    {
        if (held != null) DropCurrent(Vector2.zero, false);

        held = new ItemData
        {
            kind = ItemKind.AirTank,
            displayName = "备用气瓶",
            description = "备用的呼吸气瓶。按 E 装备到身上。",
            gasRemaining = gasRemaining,
            gasMax = gasMax,
        };
        source = null;
        equipping = false;
        qCharging = false;
        qCharge = 0f;
        showDetails = false;

        if (verboseLog) Debug.Log("[手持] 卸下备用气瓶，拿在手上");
    }

    // ---- UI ----
    private void BuildUi()
    {
        circleSprite = QteUi.SpriteFrom(QteUi.MakeCircleTexture(64));

        Canvas canvas = QteUi.CreateOverlay(transform, "HandheldCanvas", short.MaxValue - 11);
        canvasGo = canvas.gameObject;

        // 物品卡片（右下角）
        var card = QteUi.NewImage(canvas.transform, "ItemCard", cardColor);
        cardGo = card.gameObject;
        QteUi.BottomRight(card.rectTransform, 30f, 30f, 460f, 170f);

        // 图标
        cardIcon = QteUi.NewImage(card.transform, "Icon", new Color(0.25f, 0.28f, 0.34f, 1f));
        QteUi.Place(cardIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(96f, 96f));

        // 「手持」标记
        handLabel = QteUi.NewText(card.transform, "Hand", 18, TextAnchor.MiddleCenter, hintColor);
        handLabel.text = "手持";
        QteUi.Place(handLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -116f), new Vector2(96f, 24f));

        // 名字 / 信息 / 提示
        cardName = QteUi.NewText(card.transform, "Name", 26, TextAnchor.UpperLeft, nameColor);
        QteUi.Place(cardName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -14f), new Vector2(324f, 34f));

        cardInfo = QteUi.NewText(card.transform, "Info", 18, TextAnchor.UpperLeft, infoColor);
        cardInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
        cardInfo.verticalOverflow = VerticalWrapMode.Truncate;
        QteUi.Place(cardInfo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -52f), new Vector2(324f, 72f));

        cardHint = QteUi.NewText(card.transform, "Hint", 17, TextAnchor.UpperLeft, hintColor);
        QteUi.Place(cardHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -128f), new Vector2(324f, 30f));

        // 装备进度条（卡片底部）
        var equipBg = QteUi.NewImage(card.transform, "EquipBarBg", new Color(0f, 0f, 0f, 0.5f));
        equipBarGo = equipBg.gameObject;
        QteUi.Place(equipBg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 10f), new Vector2(432f, 10f));

        var equipFill = QteUi.NewImage(equipBg.transform, "EquipFill", equipColor);
        equipFillRt = equipFill.rectTransform;
        QteUi.Place(equipFillRt, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 10f));

        // 蓄力条（卡片上方）
        var chargeBg = QteUi.NewImage(canvas.transform, "ChargeBarBg", new Color(0f, 0f, 0f, 0.6f));
        chargeBarGo = chargeBg.gameObject;
        QteUi.BottomRight(chargeBg.rectTransform, 30f, 210f, 460f, 18f);

        var chargeFill = QteUi.NewImage(chargeBg.transform, "ChargeFill", chargeColor);
        chargeFillRt = chargeFill.rectTransform;
        QteUi.Place(chargeFillRt, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 18f));

        // 拾取提示（底部居中）
        pickupPrompt = QteUi.NewText(canvas.transform, "PickupPrompt", 26, TextAnchor.MiddleCenter, nameColor);
        QteUi.Place(pickupPrompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(700f, 40f));
        pickupPrompt.gameObject.SetActive(false);

        cardGo.SetActive(false);
        chargeBarGo.SetActive(false);
        equipBarGo.SetActive(false);
    }

    private void RefreshUI()
    {
        bool has = held != null;
        cardGo.SetActive(has);
        if (!has) return;

        // 图标
        if (held.icon != null)
        {
            cardIcon.sprite = held.icon;
            cardIcon.color = Color.white;
            cardIcon.preserveAspect = true;
        }
        else
        {
            cardIcon.sprite = circleSprite;
            cardIcon.color = held.kind == ItemKind.AirTank
                ? new Color(0.35f, 0.75f, 1f, 1f)
                : new Color(0.95f, 0.8f, 0.3f, 1f);
        }

        cardName.text = held.displayName;

        // 信息
        if (equipping)
        {
            float progress = Mathf.Clamp01((Time.time - equipTime) / Mathf.Max(0.01f, equipDuration));
            cardInfo.text = $"正在装备备用气瓶......（再次点按E取消）  {progress * 100f:0}%";
        }
        else
        {
            cardInfo.text = InfoText(held);
        }

        // 操作提示
        cardHint.text = held.kind == ItemKind.AirTank
            ? "轻按Q放下 / 长按Q投掷 · E 装备"
            : "轻按Q放下 / 长按Q投掷 · E 详情";

        // 装备进度条
        equipBarGo.SetActive(equipping);
        if (equipping)
        {
            float progress = Mathf.Clamp01((Time.time - equipTime) / Mathf.Max(0.01f, equipDuration));
            equipFillRt.sizeDelta = new Vector2(432f * progress, 10f);
        }

        // 蓄力条
        bool charging = qCharging && qCharge > 0.001f;
        chargeBarGo.SetActive(charging);
        if (charging)
        {
            float ratio = Mathf.Clamp01(qCharge / throwMaxCharge);
            chargeFillRt.sizeDelta = new Vector2(460f * ratio, 18f);
        }
    }

    private string InfoText(ItemData d)
    {
        if (d.kind == ItemKind.AirTank)
        {
            string gas = d.gasMax > 0f
                ? $"剩余气体：{d.gasRemaining:0} / {d.gasMax:0}"
                : $"剩余气体：{d.gasRemaining:0}";
            return string.IsNullOrEmpty(d.description) ? gas : $"{gas}\n{d.description}";
        }

        return string.IsNullOrEmpty(d.description)
            ? (showDetails ? "（无更多信息）" : "按 E 查看详情")
            : (showDetails ? d.description : "按 E 查看详情");
    }
}
