using System;
using UnityEngine;

namespace VARCO_Workshop
{
    /// <summary>플레이어 뒤·위를 따라가는 TPS. 마우스로 요/피치(orbit), 벽에 가릴 시 거리 축소. <see cref="PlayerController_ThirdPerson"/>과 함께 쓰기(카메라 기준 이동).</summary>
    [DefaultExecutionOrder(12000)]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("타겟")]
        [Tooltip("비우면 Start에서 Tag=Player 검색")]
        public Transform target;
        [Tooltip("따라다닐 머리/어깨 높이(월드)")]
        public Vector3 pivotOffset = new Vector3(0, 1.25f, 0);

        [Header("거리 / 각도")]
        public float distance     = 5.2f;
        [Tooltip("벽에 붙을 때 이거리 이하로는 줄이지 않음")]
        public float minDistance  = 1.2f;
        [Range(-15f, 80f)]
        public float minPitch     = -5f;
        [Range(-15f, 80f)]
        public float maxPitch     = 45f;

        [Header("마우스")]
        [Tooltip("우클릭을 누른 동안에만 시점 회전(없이면 항상 회전)")]
        public bool  orbitWhileRightMouseButtonOnly;
        public float sensX         = 2f;
        public float sensY         = 2f;
        [Tooltip("Play 시 커서 잠금(ESC 토글)")]
        public bool  lockCursorOnStart = true;

        [Header("가림(벽)")]
        public bool  useWallClipping  = true;
        public float wallSkin         = 0.28f;
        public float collisionRadius  = 0.25f;
        public float positionSharpness = 12f;
        public float pivotSharpness = 14f;
        public float distanceSharpness = 9f;
        [Tooltip("레이캐스트(플레이어 루트는 자동으로 건너뜀)")]
        public LayerMask obstructionMask = ~0;

        const float DistanceDeadZone = 0.08f;
        const float FloorNormalCutoff = 0.55f;

        float _yaw   = 180f;
        float _pitch = 10f;
        Vector3 _currentPos;
        Vector3 _currentPivot;
        float _currentDistance;

        public void ApplyViewPreset(float yaw, float pitch, float newDistance, Vector3 newPivotOffset, float newMinPitch, float newMaxPitch, bool rightMouseOnly, bool snap = true)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, newMinPitch, newMaxPitch);
            distance = newDistance;
            pivotOffset = newPivotOffset;
            minPitch = newMinPitch;
            maxPitch = newMaxPitch;
            orbitWhileRightMouseButtonOnly = rightMouseOnly;

            if (!snap || !target) return;
            var pivot = target.position + pivotOffset;
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var pos = pivot + rot * Vector3.back * distance;
            _currentPivot = pivot;
            _currentPos = pos;
            _currentDistance = distance;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pivot - pos, Vector3.up));
        }

        void Start()
        {
            ApplyWorkshopDefaultsIfUnset();

            if (target == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) target = p.transform;
            }
            if (target)
            {
                InitializeAnglesFromCurrentView();
            }
            else
            {
                _yaw   = transform.eulerAngles.y;
                _pitch = transform.eulerAngles.x;
            }
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }

            _currentPos = transform.position;
            _currentPivot = target ? target.position + pivotOffset : transform.position + transform.forward;
            _currentDistance = distance;
        }

        void InitializeAnglesFromCurrentView()
        {
            var pivot = target.position + pivotOffset;
            var lookDir = pivot - transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                var euler = Quaternion.LookRotation(lookDir, Vector3.up).eulerAngles;
                _yaw = euler.y;
                _pitch = Mathf.Clamp(NormalizeAngle(euler.x), minPitch, maxPitch);
                return;
            }

            _yaw = target.eulerAngles.y;
        }

        static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        void ApplyWorkshopDefaultsIfUnset()
        {
            if (Mathf.Approximately(pivotOffset.y, 1.45f))
                pivotOffset = new Vector3(pivotOffset.x, 1.25f, pivotOffset.z);
            if (Mathf.Approximately(distance, 4.2f))
                distance = 5.2f;
            if (minDistance < 1f)
                minDistance = 1.2f;
            if (maxPitch > 50f)
                maxPitch = 45f;
            if (collisionRadius <= 0f)
                collisionRadius = 0.25f;
            if (positionSharpness <= 0f || positionSharpness > 12f)
                positionSharpness = 12f;
            if (pivotSharpness <= 0f || pivotSharpness > 14f)
                pivotSharpness = 14f;
            if (distanceSharpness <= 0f || distanceSharpness > 9f)
                distanceSharpness = 9f;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible   = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible   = false;
                }
            }
        }

        void LateUpdate()
        {
            if (!target) return;

            var canOrbit = !orbitWhileRightMouseButtonOnly || Input.GetMouseButton(1);
            if (canOrbit)
            {
                _yaw   += Input.GetAxis("Mouse X") * sensX;
                _pitch -= Input.GetAxis("Mouse Y") * sensY;
                _pitch =  Mathf.Clamp(_pitch, minPitch, maxPitch);
            }
            var targetPivot = target.position + pivotOffset;
            var pivotT = 1f - Mathf.Exp(-pivotSharpness * Time.deltaTime);
            _currentPivot = Vector3.Lerp(_currentPivot, targetPivot, pivotT);
            var pivot   = _currentPivot;
            var rot     = Quaternion.Euler(_pitch, _yaw, 0f);
            var back    = rot * Vector3.back;
            var wantLen = distance;
            if (useWallClipping)
                wantLen = GetUnobstructedDistance(pivot, back, wantLen, target.root);

            if (Mathf.Abs(wantLen - _currentDistance) > DistanceDeadZone)
            {
                var distanceT = 1f - Mathf.Exp(distanceSharpness * -Time.deltaTime);
                _currentDistance = Mathf.Lerp(_currentDistance, wantLen, distanceT);
            }

            var finalPos = pivot + back * _currentDistance;
            var t = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            _currentPos = Vector3.Lerp(_currentPos, finalPos, t);
            var lookDir = pivot - _currentPos;
            if (lookDir.sqrMagnitude > 0.0001f)
                transform.SetPositionAndRotation(_currentPos, Quaternion.LookRotation(lookDir, Vector3.up));
        }

        float GetUnobstructedDistance(Vector3 from, Vector3 dir, float maxDist, Transform playerRoot)
        {
            dir.Normalize();
            var hits = Physics.SphereCastAll(from, collisionRadius, dir, maxDist, obstructionMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return maxDist;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.transform.root == playerRoot) continue;
                if (h.collider.isTrigger) continue;
                if (h.normal.y > FloorNormalCutoff) continue;
                if (h.collider.GetComponentInParent<PlayerHealth>() != null) continue;
                if (h.collider.GetComponentInParent<EnemyHealth>() != null) continue;
                return Mathf.Max(minDistance, h.distance - wallSkin);
            }
            return maxDist;
        }
    }
}
