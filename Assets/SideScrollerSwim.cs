using UnityEngine;

public class SideScrollerSwim : MonoBehaviour
{
    [Header("移动")]
    public float swimForce = 9f;
    public float maxSpeed = 6f;

    [Header("转向")]
    public float rotateSpeed = 240f;
    public float rotationOffset = 90f;
    public float maxTilt = 80f;
    public bool flipAngle = true;
    public bool flipMove = true;
    public bool invertZ = true;

    private Rigidbody rb;
    private float facingSign = 1f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.rotation = Quaternion.Euler(0f, 0f, rotationOffset);
    }

    void FixedUpdate()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        bool hasInput = Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f;

        Vector2 faceDir = new Vector2(flipAngle ? -h : h, v);
        if (faceDir.sqrMagnitude > 1f) faceDir.Normalize();

        Vector2 moveDir = new Vector2(flipMove ? -h : h, v);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        if (hasInput)
        {
            Vector2 rel = new Vector2(faceDir.x * facingSign, faceDir.y);
            if (rel.x < -0.2f)
                facingSign = -facingSign;

            rel = new Vector2(faceDir.x * facingSign, faceDir.y);
            float tilt = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
            tilt = Mathf.Clamp(tilt, -maxTilt, maxTilt);

            float yaw = facingSign > 0f ? 0f : 180f;
            float z = rotationOffset - tilt;       // ← 关键是这行
            if (invertZ) z = -z;

            Quaternion target = Quaternion.Euler(0f, yaw, z);
            rb.rotation = Quaternion.RotateTowards(rb.rotation, target,
                                                   rotateSpeed * Time.fixedDeltaTime);
        }

        if (moveDir.sqrMagnitude > 0.01f)
        {
            rb.AddForce(new Vector3(moveDir.x, moveDir.y, 0f) * swimForce, ForceMode.Force);

            Vector3 pv = new Vector3(rb.velocity.x, rb.velocity.y, 0f);
            if (pv.magnitude > maxSpeed) pv = pv.normalized * maxSpeed;
            rb.velocity = new Vector3(pv.x, pv.y, rb.velocity.z);
        }

        transform.position = new Vector3(rb.position.x, rb.position.y, 0f);
        rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y, rb.angularVelocity.z);
    }
}
