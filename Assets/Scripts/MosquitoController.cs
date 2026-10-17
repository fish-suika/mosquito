using UnityEngine;

namespace MosquitoGame
{
    /// <summary>蚊の飛行・着地・吸血・回避ダッシュ。</summary>
    [DefaultExecutionOrder(-10)]
    public class MosquitoController : MonoBehaviour
    {
        public enum Mode { Flying, Sucking }

        [Header("飛行")]
        public float flySpeed = 12f;
        public float climbSpeed = 8f;
        public float acceleration = 14f;
        public float radius = 0.3f;

        [Header("回避ダッシュ")]
        public float dashSpeed = 36f;
        public float dashTime = 0.22f;
        public float dashCooldown = 1.2f;

        [Header("吸血")]
        public float landReach = 2.2f;
        public float surfaceOffset = 0.25f;
        public float takeOffBoost = 3f;

        [Header("参照")]
        public Transform wingL;
        public Transform wingR;
        public ThirdPersonCamera cam;
        public Human human;

        [Header("テスト用（Editor から操作を流し込む）")]
        public bool scripted;
        public Vector3 scriptedMove;      // x=右 y=上 z=前（カメラ基準）
        public bool scriptedSuck;
        public bool scriptedDash;         // 1回押したら自動で false に戻る

        public Mode CurrentMode { get; private set; }
        public Vector3 Position => transform.position;
        public bool IsDashing => dashTimer > 0f;
        public float DashReady01 => dashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(dashCdRemaining / dashCooldown);
        public float CurrentSuckRate { get; private set; }
        public PartType? CurrentPart => part != null ? part.type : (PartType?)null;
        public int Streak => streak;

        const float RoomX = 29f, RoomZ = 19f, RoomMinY = 0.3f, RoomMaxY = 25f;

        Vector3 velocity;
        float dashTimer, dashCdRemaining, flap;
        Vector3 dashDir;
        BodyPart part;
        Vector3 localPoint, localNormal;
        PartType? lastType;
        int streak;

        void Update()
        {
            var gm = GameManager.Instance;
            if (human != null) human.ItchRate = 0f;
            CurrentSuckRate = 0f;
            AnimateWings(gm == null || gm.IsPlaying ? 1f : 0.2f);
            if (gm != null && !gm.IsPlaying) return;

            Physics.SyncTransforms();
            float dt = Time.deltaTime;
            ReadInput(out Vector3 move, out bool suckHeld, out bool dashPressed);
            dashCdRemaining = Mathf.Max(0f, dashCdRemaining - dt);

            if (CurrentMode == Mode.Flying)
            {
                if (dashPressed && dashCdRemaining <= 0f) StartDash(move);
                Fly(move, dt);
                if (suckHeld && dashTimer <= 0f) TryLand();
            }
            else
            {
                bool alert = human != null && human.State == HumanState.Alert;
                if (!suckHeld || alert || part == null)
                {
                    TakeOff();
                    if (dashPressed && dashCdRemaining <= 0f) StartDash(move);
                }
                else if (dashPressed && dashCdRemaining <= 0f)
                {
                    TakeOff();
                    StartDash(move);
                }
                else
                {
                    SuckTick(dt);
                }
            }
        }

        void ReadInput(out Vector3 move, out bool suckHeld, out bool dashPressed)
        {
            if (scripted)
            {
                move = scriptedMove; suckHeld = scriptedSuck; dashPressed = scriptedDash;
                scriptedDash = false;
                return;
            }
            var gm = GameManager.Instance;
            bool active = gm == null || !gm.manageCursor || Cursor.lockState == CursorLockMode.Locked;
            if (!active)
            {
                move = Vector3.zero; suckHeld = false; dashPressed = false;
                return;
            }
            move = new Vector3(Axis(KeyCode.D, KeyCode.A), Axis(KeyCode.Space, KeyCode.LeftShift), Axis(KeyCode.W, KeyCode.S));
            suckHeld = Input.GetMouseButton(0);
            dashPressed = Input.GetMouseButtonDown(1);
        }

        static float Axis(KeyCode plus, KeyCode minus)
        {
            return (Input.GetKey(plus) ? 1f : 0f) - (Input.GetKey(minus) ? 1f : 0f);
        }

        Vector3 CamForward()
        {
            Vector3 f = cam != null ? cam.transform.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
        }

        void StartDash(Vector3 move)
        {
            Vector3 f = CamForward();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3 dir = r * move.x + f * move.z + Vector3.up * move.y;
            if (dir.sqrMagnitude < 0.01f) dir = cam != null ? cam.transform.forward : transform.forward;
            dashDir = dir.normalized;
            dashTimer = dashTime;
            dashCdRemaining = dashCooldown;
        }

        void Fly(Vector3 move, float dt)
        {
            Vector3 f = CamForward();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            if (dashTimer > 0f)
            {
                velocity = dashDir * dashSpeed;
                dashTimer -= dt;
                if (dashTimer <= 0f) velocity = dashDir * flySpeed;
            }
            else
            {
                Vector3 horiz = r * move.x + f * move.z;
                if (horiz.sqrMagnitude > 1f) horiz.Normalize();
                Vector3 target = horiz * flySpeed + Vector3.up * (Mathf.Clamp(move.y, -1f, 1f) * climbSpeed);
                velocity = Vector3.MoveTowards(velocity, target, acceleration * dt);
            }

            transform.position = ClampToRoom(MoveWithCollision(transform.position, velocity * dt));

            float tilt = Mathf.Clamp(Vector3.Dot(velocity, f) / flySpeed, -1f, 1f) * 25f;
            Quaternion want = Quaternion.Euler(tilt, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 10f * dt);
        }

        Vector3 MoveWithCollision(Vector3 pos, Vector3 delta)
        {
            for (int i = 0; i < 3 && delta.sqrMagnitude > 1e-8f; i++)
            {
                float d = delta.magnitude;
                Vector3 dir = delta / d;
                if (Physics.SphereCast(pos, radius, dir, out RaycastHit hit, d + 0.02f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float travel = Mathf.Max(0f, hit.distance - 0.02f);
                    pos += dir * travel;
                    delta = Vector3.ProjectOnPlane(dir * (d - travel), hit.normal);
                }
                else
                {
                    pos += delta;
                    break;
                }
            }
            return pos;
        }

        static Vector3 ClampToRoom(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -RoomX, RoomX);
            p.z = Mathf.Clamp(p.z, -RoomZ, RoomZ);
            p.y = Mathf.Clamp(p.y, RoomMinY, RoomMaxY);
            return p;
        }

        void TryLand()
        {
            if (human != null && human.State == HumanState.Alert) return;   // 攻撃中は着地できない

            var cols = Physics.OverlapSphere(transform.position, landReach, ~0, QueryTriggerInteraction.Ignore);
            BodyPart best = null;
            RaycastHit bestHit = default;
            float bestDist = float.MaxValue;
            foreach (var c in cols)
            {
                var bp = c.GetComponent<BodyPart>();
                if (bp == null) continue;
                Vector3 p = c.ClosestPoint(transform.position);
                Vector3 to = p - transform.position;
                float dist = to.magnitude;
                if (dist >= bestDist) continue;
                // 実際に見えている表面にだけ着地する（布団の下の足などに突き抜けない）
                Vector3 dir = dist < 1e-3f ? Vector3.down : to / dist;
                if (!Physics.Raycast(transform.position, dir, out RaycastHit hit, dist + 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider != c) continue;
                best = bp; bestHit = hit; bestDist = dist;
            }
            if (best == null) return;

            part = best;
            localPoint = part.transform.InverseTransformPoint(bestHit.point);
            localNormal = part.transform.InverseTransformDirection(bestHit.normal);
            streak = (lastType.HasValue && lastType.Value == part.type) ? streak + 1 : 0;
            lastType = part.type;
            CurrentMode = Mode.Sucking;
            velocity = Vector3.zero;
            dashTimer = 0f;
            FollowSurface();
        }

        void FollowSurface()
        {
            Vector3 n = part.transform.TransformDirection(localNormal).normalized;
            transform.position = part.transform.TransformPoint(localPoint) + n * surfaceOffset;
            Vector3 f = Vector3.ProjectOnPlane(CamForward(), n);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(Vector3.forward, n);
            transform.rotation = Quaternion.LookRotation(f.normalized, n);
        }

        void SuckTick(float dt)
        {
            FollowSurface();
            float rate = StealthRules.SuckPoints(part.type);
            CurrentSuckRate = rate;
            var gm = GameManager.Instance;
            if (gm != null) gm.AddScore(rate * dt);
            if (human != null) human.ItchRate = StealthRules.Itch(part.type) * StealthRules.RepeatMultiplier(streak);
        }

        void TakeOff()
        {
            if (CurrentMode != Mode.Sucking) return;
            Vector3 n = part != null ? part.transform.TransformDirection(localNormal).normalized : Vector3.up;
            CurrentMode = Mode.Flying;
            velocity = n * takeOffBoost;
            part = null;
        }

        void AnimateWings(float speed)
        {
            flap += Time.unscaledDeltaTime * 140f * speed;
            float a = Mathf.Sin(flap) * 35f;
            if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, 20f + a);
            if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, -20f - a);
        }
    }
}
