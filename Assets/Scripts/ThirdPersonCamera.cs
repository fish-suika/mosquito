using UnityEngine;

namespace MosquitoGame
{
    /// <summary>蚊の後ろを回る三人称カメラ。壁や家具に当たったら手前に寄る。</summary>
    [DefaultExecutionOrder(100)]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 3.5f;
        public float heightOffset = 0.35f;
        public float sensitivity = 2.2f;
        public float minPitch = -55f;
        public float maxPitch = 80f;
        public float collisionRadius = 0.2f;
        public float Yaw;
        public float Pitch = 15f;

        [Tooltip("true にすると Editor から Yaw/Pitch を直接指定できる（マウスを読まない）")]
        public bool scripted;

        float shakeAmp, shakeLeft, shakeTotal;

        /// <summary>画面を揺らす（叩かれた・叩かれそうなときの演出）。</summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude < shakeAmp * (shakeLeft / Mathf.Max(0.01f, shakeTotal))) return;
            shakeAmp = amplitude; shakeLeft = shakeTotal = duration;
        }

        void LateUpdate()
        {
            if (target == null) return;
            var gm = GameManager.Instance;
            bool playing = gm == null || gm.IsPlaying;
            bool locked = gm == null || !gm.manageCursor || Cursor.lockState == CursorLockMode.Locked;
            if (!scripted && playing && locked)
            {
                Yaw += Input.GetAxis("Mouse X") * sensitivity;
                Pitch = Mathf.Clamp(Pitch - Input.GetAxis("Mouse Y") * sensitivity, minPitch, maxPitch);
            }

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * heightOffset;
            Vector3 back = rot * Vector3.back;
            float dist = distance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.3f, hit.distance - 0.05f);

            transform.position = pivot + back * dist;
            transform.rotation = rot;

            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(shakeLeft / shakeTotal);
                transform.position += Random.insideUnitSphere * (shakeAmp * k);
            }
        }
    }
}
