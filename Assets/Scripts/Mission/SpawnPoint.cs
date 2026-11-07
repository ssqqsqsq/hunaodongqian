using System;
using UnityEngine;

// 出生点：关卡出生位置（用 BoxCollider 划定"离开判定区"，可在场景里直接拖动）。
// 出生位置就是物体自身的坐标，固定、不带随机偏移。
// 玩家离开判定区后，撤离区据此开始检测。
[RequireComponent(typeof(BoxCollider))]
public class SpawnPoint : MonoBehaviour
{
    [Header("出生设置")]
    [Tooltip("进入关卡时把玩家放到出生点（物体自身坐标，不带偏移）")]
    [SerializeField] private bool teleportPlayerOnStart = true;

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;

    // 玩家离开出生区时广播一次
    public event Action OnPlayerLeftSpawn;

    // 玩家当前是否在出生区内
    public bool PlayerInside { get; private set; }

    // 玩家是否已经离开过出生区（撤离区的解锁条件）
    public bool HasPlayerLeft { get; private set; }

    // 出生位置：物体自身坐标（不随机、不带 BoxCollider 中心偏移）
    public Vector3 SpawnPosition => transform.position;

    private BoxCollider box;
    private PlayerSM player;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerSM>();
        if (player == null)
        {
            Debug.LogWarning("SpawnPoint：场景里找不到 PlayerSM，出生与离场判定不会生效");
            return;
        }

        if (teleportPlayerOnStart)
        {
            Respawn(player);
        }
        else if (!IsPlayerInside())
        {
            // 玩家开局就不在出生区：视为已经离开
            MarkPlayerLeft();
        }
        else
        {
            PlayerInside = true;
            if (verboseLog) Debug.Log("[结算][出生点] 玩家开局在出生区内");
        }
    }

    private void Update()
    {
        if (player == null) return;

        if (IsPlayerInside())
        {
            PlayerInside = true;
            return;
        }

        if (PlayerInside)
        {
            PlayerInside = false;
            if (verboseLog) Debug.Log("[结算][出生点] 玩家离开出生区，撤离区可以解锁了");
            MarkPlayerLeft();
        }
    }

    // 把玩家放回出生点（也供死亡重生等调用）
    public void Respawn(PlayerSM target)
    {
        if (target == null) return;

        Vector3 pos = SpawnPosition;
        pos.z = target.transform.position.z;

        if (target.rb != null)
        {
            target.rb.velocity = Vector3.zero;
            target.rb.angularVelocity = Vector3.zero;
            target.rb.position = pos;
        }
        else
        {
            target.transform.position = pos;
        }

        PlayerInside = true;
        HasPlayerLeft = false;

        if (verboseLog) Debug.Log($"[结算][出生点] 玩家已传送到出生点 {pos}");
    }

    // 玩家（中心点）是否在矩形内；忽略 Z 轴差异
    public bool IsPlayerInside()
    {
        if (player == null) return false;

        Vector3 p = player.transform.position;
        p.z = box.bounds.center.z;
        return box.bounds.Contains(p);
    }

    private void MarkPlayerLeft()
    {
        if (HasPlayerLeft) return;
        HasPlayerLeft = true;
        if (verboseLog) Debug.Log("[结算][出生点] 标记：玩家已离开出生区（HasPlayerLeft=true）");
        OnPlayerLeftSpawn?.Invoke();
    }

    private void OnDrawGizmos()
    {
        BoxCollider b = box != null ? box : GetComponent<BoxCollider>();
        if (b == null) return;

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.15f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 1f);
        Gizmos.DrawWireCube(b.center, b.size);

        Gizmos.matrix = old;
    }
}
