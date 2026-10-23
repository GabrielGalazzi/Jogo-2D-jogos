using UnityEngine;

namespace RicochetPoC
{
    /// <summary>Side-scroller camera: follows the target horizontally, clamped to the level, at a fixed height.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.15f;
        public float levelMinX;
        public float levelMaxX = 80f;
        public float height = 6f;

        Camera m_Camera;
        float m_VelocityX;

        void Awake() => m_Camera = GetComponent<Camera>();

        void LateUpdate()
        {
            if (target == null)
                return;

            float halfWidth = m_Camera.orthographicSize * m_Camera.aspect;
            float minX = levelMinX + halfWidth, maxX = levelMaxX - halfWidth;
            float desired = minX < maxX ? Mathf.Clamp(target.position.x, minX, maxX) : (levelMinX + levelMaxX) * 0.5f;

            float x = Mathf.SmoothDamp(transform.position.x, desired, ref m_VelocityX, smoothTime);
            transform.position = new Vector3(x, height, transform.position.z);
        }

        public void SnapToTarget()
        {
            m_VelocityX = 0f;
            float halfWidth = m_Camera.orthographicSize * m_Camera.aspect;
            float x = target != null ? Mathf.Clamp(target.position.x, levelMinX + halfWidth, levelMaxX - halfWidth) : transform.position.x;
            transform.position = new Vector3(x, height, transform.position.z);
        }
    }
}
