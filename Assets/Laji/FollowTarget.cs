using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    [Header("要跟随的目标（把那个方块拖进来）")]
    public Transform target;

    [Header("位置偏移（微调光照位置）")]
    public Vector3 offset = Vector3.zero;

    void LateUpdate()
    {
        if (target != null)
        {
            // 每一帧把自身的位置设为：目标位置 + 偏移量
            transform.position = target.position + offset;

            // 注意：这里我们绝对不写 transform.rotation = ... 
            // 所以它的旋转（朝向）会永远保持你在场景里调好的初始状态！
        }
    }
}