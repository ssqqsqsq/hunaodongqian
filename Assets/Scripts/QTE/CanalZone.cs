using UnityEngine;

// 产道判定区：狭长通道地形。玩家进入后强制开始「穿越产道」QTE，
// QTE 期间由 CanalQTEController 接管角色移动（前进 / 后退 / 进出）。
//
// 放置方式：把本组件挂到产道地形上，调 BoxCollider 覆盖整条通道。
//   · moveAxis 指「入口 → 出口」方向（默认向右 (1,0)）；
//   · 入口 = 判定区沿 moveAxis 负方向的一侧，出口 = 正方向一侧；
//   · 玩家从哪头进入都行，QTE 会自动以离得近的那头作为起点。
[RequireComponent(typeof(BoxCollider))]
public class CanalZone : TriggerZoneBase
{
    [Header("QTE 依赖（留空自动在场景里找）")]
    [SerializeField] private CanalQTEController canalQTE;

    [Header("穿越设置")]
    [Tooltip("总进度：穿越产道需要完成的校准次数")]
    [SerializeField] private int totalProgress = 4;
    [Tooltip("移动轴向（局部空间，单位向量），入口 → 出口方向；默认沿物体本地 X 轴 (1,0)")]
    [SerializeField] private Vector2 moveAxis = new Vector2(1f, 0f);
    [Tooltip("结束后把玩家送出判定区多远（世界单位），确保真正离开地形")]
    [SerializeField] private float exitOffset = 1.5f;

    [Header("调试")]
    [SerializeField] private bool logEvents = true;

    // 本区当前是否正在跑 QTE
    public bool IsRunning { get; private set; }

    // 产道入口 / 出口（世界坐标）
    public Vector2 EntryPoint { get; private set; }
    public Vector2 ExitPoint { get; private set; }

    public Vector2 MoveAxis => moveAxis;
    public float ExitOffset => exitOffset;

    private void Start()
    {
        // 运行时自动创建的 CanalQTEController 可能晚于本区 Awake，这里懒解析
        if (canalQTE == null) canalQTE = FindFirstObjectByType<CanalQTEController>();
        RecomputePoints();
    }

    // 玩家进入判定区 → 启动穿越 QTE
    protected override void OnPlayerEnter(PlayerSM p)
    {
        if (IsRunning) return;

        if (canalQTE == null) canalQTE = FindFirstObjectByType<CanalQTEController>();
        if (canalQTE == null)
        {
            Debug.LogError("CanalZone：场景里没有 CanalQTEController，产道 QTE 无法开始");
            return;
        }

        RecomputePoints();

        if (logEvents)
            Debug.Log($"[产道] 玩家进入产道判定区（入口 {EntryPoint} → 出口 {ExitPoint}），开始穿越 QTE");

        if (canalQTE.StartCanalQTE(this, p, totalProgress))
            IsRunning = true;
    }

    protected override void OnPlayerExit(PlayerSM p)
    {
        // QTE 进行中的进出（前后挪步可能蹭到边界）一律忽略
        if (IsRunning) return;
    }

    // CanalQTEController 在 QTE 结束时回调
    public void OnQTEEnded(bool success)
    {
        IsRunning = false;
    }

    // 根据 BoxCollider 重算入口 / 出口（跟随判定区物体自身的旋转，实现"按角度平行穿越"）
    public void RecomputePoints()
    {
        BoxCollider box = Box != null ? Box : GetComponent<BoxCollider>();
        if (box == null) return;

        // moveAxis 是「局部空间」方向，跟着判定区物体的旋转走
        Vector3 localAxis = new Vector3(moveAxis.x, moveAxis.y, 0f);
        if (localAxis.sqrMagnitude < 1e-6f) localAxis = Vector3.right;
        localAxis.Normalize();

        Vector3 center = transform.TransformPoint(box.center);

        // 沿 localAxis 的盒子半长（局部轴对齐）
        Vector3 half = box.size * 0.5f;
        float halfLenLocal = Mathf.Abs(half.x * localAxis.x) + Mathf.Abs(half.y * localAxis.y);

        // TransformVector 会同时应用旋转和缩放，这样调 BoxCollider.Size 或 Transform.Scale
        // 都能让入口/出口（即移动轨迹长度）跟着判定区一起变化
        Vector3 worldOffset = transform.TransformVector(localAxis * halfLenLocal);
        worldOffset.z = 0f;

        EntryPoint = (Vector2)(center - worldOffset);
        ExitPoint = (Vector2)(center + worldOffset);
    }

    // TriggerZoneBase 的 OnDrawGizmos 是私有的，派生类会遮蔽它，这里一并画出判定框 + 入口/出口
    private void OnDrawGizmos()
    {
        RecomputePoints();

        BoxCollider box = Box != null ? Box : GetComponent<BoxCollider>();
        if (box != null)
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = PlayerInside
                ? new Color(1f, 0.55f, 0.1f, 0.25f)
                : new Color(0.2f, 0.85f, 1f, 0.15f);
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = PlayerInside
                ? new Color(1f, 0.55f, 0.1f, 1f)
                : new Color(0.2f, 0.85f, 1f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);

            Gizmos.matrix = old;
        }

        if (EntryPoint == ExitPoint) return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f); // 入口：黄
        Gizmos.DrawSphere(EntryPoint, 0.35f);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 1f);  // 出口：绿
        Gizmos.DrawSphere(ExitPoint, 0.35f);
        Gizmos.color = new Color(1f, 1f, 1f, 0.7f);
        Gizmos.DrawLine(EntryPoint, ExitPoint);
    }
}
