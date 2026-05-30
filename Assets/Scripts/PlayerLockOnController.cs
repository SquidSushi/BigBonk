using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLockOnController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Camera playerCamera;

    [Header("Target Search")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private float lockOnRadius = 15f;
    [SerializeField] private float maxLockOnAngle = 70f;

    [Header("Unlock Rules")]
    [SerializeField] private float unlockDistance = 18f;
    [SerializeField] private float targetValidationInterval = 0.1f;

    [Header("Line Of Sight")]
    [SerializeField] private bool requireLineOfSight = false;
    [SerializeField] private LayerMask obstructionLayers;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    public LockOnTarget CurrentTarget { get; private set; }
    public bool IsLockedOn => CurrentTarget != null;

    private PlayerState _playerState;
    private InputAction lockOnAction;

    private float nextValidationTime;

    private readonly Collider[] targetColliders = new Collider[32];

    private void Awake()
    {
        _playerState = GetComponent<PlayerState>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Start()
    {
        lockOnAction = InputSystem.actions.FindAction("LockOn");

        if (lockOnAction == null)
        {
            Debug.LogError("PlayerLockOnController: InputAction 'LockOn' wurde nicht gefunden.");
            enabled = false;
            return;
        }

        if (_playerState == null)
        {
            Debug.LogError("PlayerLockOnController: PlayerState wurde nicht gefunden.");
            enabled = false;
            return;
        }

        if (playerCamera == null)
        {
            Debug.LogError("PlayerLockOnController: Keine Camera gefunden.");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (lockOnAction.WasPressedThisFrame())
        {
            ToggleLockOn();
        }

        if (IsLockedOn && Time.time >= nextValidationTime)
        {
            nextValidationTime = Time.time + targetValidationInterval;
            ValidateCurrentTarget();
        }
    }

    private void ToggleLockOn()
    {
        if (IsLockedOn)
        {
            ClearLockOn();
            return;
        }

        LockOnTarget bestTarget = FindBestTarget();

        if (bestTarget == null)
        {
            Debug.Log("LockOn: Kein gültiges Target gefunden.");
            return;
        }

        SetLockOnTarget(bestTarget);
    }

    private void SetLockOnTarget(LockOnTarget target)
    {
        CurrentTarget = target;

        _playerState.SetPlayerTargetingState(PlayerTargetingState.LockedOn);

        Debug.Log($"LockOn: Target gesetzt auf {target.name}");
    }

    public void ClearLockOn()
    {
        if (CurrentTarget != null)
        {
            Debug.Log($"LockOn: Target gelöst von {CurrentTarget.name}");
        }

        CurrentTarget = null;

        _playerState.SetPlayerTargetingState(PlayerTargetingState.Free);
    }

    private void ValidateCurrentTarget()
    {
        if (CurrentTarget == null)
        {
            ClearLockOn();
            return;
        }

        if (!CurrentTarget.IsTargetable)
        {
            ClearLockOn();
            return;
        }

        float distance = Vector3.Distance(transform.position, CurrentTarget.AimPosition);

        if (distance > unlockDistance)
        {
            ClearLockOn();
            return;
        }

        if (requireLineOfSight && !HasLineOfSight(CurrentTarget))
        {
            ClearLockOn();
            return;
        }
    }

    private LockOnTarget FindBestTarget()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            lockOnRadius,
            targetColliders,
            targetLayers,
            QueryTriggerInteraction.Collide
        );

        LockOnTarget bestTarget = null;
        float bestScore = float.MaxValue;

        HashSet<LockOnTarget> checkedTargets = new HashSet<LockOnTarget>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = targetColliders[i];

            if (hitCollider == null)
                continue;

            LockOnTarget target = hitCollider.GetComponentInParent<LockOnTarget>();

            if (target == null)
                continue;

            if (checkedTargets.Contains(target))
                continue;

            checkedTargets.Add(target);

            if (!IsValidTargetCandidate(target))
                continue;

            float score = GetTargetScore(target);

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    private bool IsValidTargetCandidate(LockOnTarget target)
    {
        if (target == null)
            return false;

        if (!target.IsTargetable)
            return false;

        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 toTarget = target.AimPosition - cameraPosition;

        if (toTarget.sqrMagnitude < 0.001f)
            return false;

        float angle = Vector3.Angle(playerCamera.transform.forward, toTarget.normalized);

        if (angle > maxLockOnAngle)
            return false;

        if (requireLineOfSight && !HasLineOfSight(target))
            return false;

        return true;
    }

    private float GetTargetScore(LockOnTarget target)
    {
        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 toTarget = target.AimPosition - cameraPosition;

        float angle = Vector3.Angle(playerCamera.transform.forward, toTarget.normalized);
        float distance = Vector3.Distance(transform.position, target.AimPosition);

        float normalizedAngle = angle / maxLockOnAngle;
        float normalizedDistance = distance / lockOnRadius;

        float priorityBonus = 1f / Mathf.Max(0.01f, target.Priority);

        return normalizedAngle * 2f + normalizedDistance + priorityBonus * 0.25f;
    }

    private bool HasLineOfSight(LockOnTarget target)
    {
        Vector3 origin = playerCamera.transform.position;
        Vector3 destination = target.AimPosition;
        Vector3 direction = destination - origin;

        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return false;

        if (Physics.Raycast(
                origin,
                direction.normalized,
                out RaycastHit hit,
                distance,
                obstructionLayers,
                QueryTriggerInteraction.Ignore
            ))
        {
            return false;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lockOnRadius);

        if (CurrentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, CurrentTarget.AimPosition);
            Gizmos.DrawWireSphere(CurrentTarget.AimPosition, 0.3f);
        }
    }
}