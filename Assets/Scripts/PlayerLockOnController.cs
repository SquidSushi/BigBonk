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

    [Header("Target Switching")]
    [SerializeField] private bool enableTargetSwitching = true;
    
    [Header("Lock On Camera")]
    [Tooltip("Wie schnell die Kamera im Lock-on zum Target rotiert.")]
    [SerializeField] private float lockOnCameraRotationSpeed = 12f;

    [Tooltip("Zusätzlicher Pitch-Offset im Lock-on. Positiv schaut meist tiefer, negativ höher.")]
    [SerializeField] private float lockOnCameraPitchOffset = 0f;

    public float LockOnCameraRotationSpeed => lockOnCameraRotationSpeed;
    public float LockOnCameraPitchOffset => lockOnCameraPitchOffset;

    [Tooltip("Wie weit der rechte Stick gedrückt werden muss, um ein Target zu wechseln.")]
    [SerializeField] private float targetSwitchThreshold = 0.65f;

    [Tooltip("Wie weit der Stick zurück in die Mitte muss, bevor erneut gewechselt werden darf.")]
    [SerializeField] private float targetSwitchNeutralThreshold = 0.25f;

    [Tooltip("Mindestzeit zwischen Target-Wechseln.")]
    [SerializeField] private float targetSwitchCooldown = 0.25f;

    [Tooltip("Wie stark ein neues Target in die gewünschte Bildschirmrichtung liegen muss.")]
    [SerializeField] private float minSwitchDirectionDot = 0.35f;

    [Tooltip("Wenn aktiv, muss der rechte Stick vor dem nächsten Wechsel erst zurück zur Mitte.")]
    [SerializeField] private bool requireStickReturnToNeutral = true;

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
    private InputAction lookAction;

    private float nextValidationTime;
    private float nextTargetSwitchTime;

    private bool targetSwitchInputLocked;

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
        lookAction = InputSystem.actions.FindAction("Look");

        if (lockOnAction == null)
        {
            Debug.LogError("PlayerLockOnController: InputAction 'LockOn' wurde nicht gefunden.");
            enabled = false;
            return;
        }

        if (lookAction == null)
        {
            Debug.LogWarning("PlayerLockOnController: InputAction 'Look' wurde nicht gefunden. Target-Switching mit rechtem Stick funktioniert dadurch nicht.");
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

        if (IsLockedOn)
        {
            HandleTargetSwitchInput();
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
        if (target == null)
            return;

        CurrentTarget = target;

        _playerState.SetPlayerTargetingState(PlayerTargetingState.LockedOn);

        nextValidationTime = Time.time + targetValidationInterval;

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

        targetSwitchInputLocked = false;
    }

    private void HandleTargetSwitchInput()
    {
        if (!enableTargetSwitching)
            return;

        if (lookAction == null)
            return;

        if (CurrentTarget == null)
            return;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        float neutralThresholdSqr =
            targetSwitchNeutralThreshold *
            targetSwitchNeutralThreshold;

        if (lookInput.sqrMagnitude < neutralThresholdSqr)
        {
            targetSwitchInputLocked = false;
            return;
        }

        float switchThresholdSqr =
            targetSwitchThreshold *
            targetSwitchThreshold;

        if (lookInput.sqrMagnitude < switchThresholdSqr)
            return;

        if (requireStickReturnToNeutral && targetSwitchInputLocked)
            return;

        if (Time.time < nextTargetSwitchTime)
            return;

        Vector2 switchDirection = GetDominantSwitchDirection(lookInput);

        bool switched = TrySwitchTarget(switchDirection);

        nextTargetSwitchTime = Time.time + targetSwitchCooldown;

        if (requireStickReturnToNeutral)
            targetSwitchInputLocked = true;

        if (switched)
        {
            Debug.Log($"LockOn: Target gewechselt nach {switchDirection}");
        }
    }

    private Vector2 GetDominantSwitchDirection(Vector2 input)
    {
        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
        {
            return new Vector2(Mathf.Sign(input.x), 0f);
        }

        return new Vector2(0f, Mathf.Sign(input.y));
    }

    private bool TrySwitchTarget(Vector2 switchDirection)
    {
        LockOnTarget newTarget = FindBestSwitchTarget(switchDirection);

        if (newTarget == null)
            return false;

        SetLockOnTarget(newTarget);
        return true;
    }

    private LockOnTarget FindBestSwitchTarget(Vector2 switchDirection)
    {
        if (CurrentTarget == null)
            return null;

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

        Vector3 currentTargetViewport3 =
            playerCamera.WorldToViewportPoint(CurrentTarget.AimPosition);

        Vector2 currentTargetViewport =
            new Vector2(
                currentTargetViewport3.x,
                currentTargetViewport3.y
            );

        // Falls das aktuelle Target aus irgendeinem Grund nicht sauber im View liegt,
        // nehmen wir die Bildschirmmitte als Fallback.
        if (currentTargetViewport3.z <= 0f)
        {
            currentTargetViewport = new Vector2(0.5f, 0.5f);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = targetColliders[i];

            if (hitCollider == null)
                continue;

            LockOnTarget target =
                hitCollider.GetComponentInParent<LockOnTarget>();

            if (target == null)
                continue;

            if (target == CurrentTarget)
                continue;

            if (checkedTargets.Contains(target))
                continue;

            checkedTargets.Add(target);

            if (!IsValidTargetCandidate(target))
                continue;

            Vector3 candidateViewport3 =
                playerCamera.WorldToViewportPoint(target.AimPosition);

            // Hinter der Kamera nicht berücksichtigen.
            if (candidateViewport3.z <= 0f)
                continue;

            Vector2 candidateViewport =
                new Vector2(
                    candidateViewport3.x,
                    candidateViewport3.y
                );

            Vector2 screenDelta =
                candidateViewport - currentTargetViewport;

            if (screenDelta.sqrMagnitude < 0.0001f)
                continue;

            Vector2 screenDirection =
                screenDelta.normalized;

            float directionDot =
                Vector2.Dot(screenDirection, switchDirection);

            // Target liegt nicht ausreichend in der gewünschten Richtung.
            if (directionDot < minSwitchDirectionDot)
                continue;

            float screenDistance = screenDelta.magnitude;

            float worldDistance =
                Vector3.Distance(transform.position, target.AimPosition);

            float normalizedWorldDistance =
                worldDistance / lockOnRadius;

            float priorityBonus =
                1f / Mathf.Max(0.01f, target.Priority);

            // Niedriger Score gewinnt.
            // Wichtigste Faktoren:
            // 1. Liegt es klar in der gedrückten Richtung?
            // 2. Ist es nah am aktuellen Target auf dem Bildschirm?
            // 3. Ist es nicht absurd weit weg?
            float score =
                (1f - directionDot) * 2f +
                screenDistance * 1.25f +
                normalizedWorldDistance * 0.35f +
                priorityBonus * 0.15f;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }

        return bestTarget;
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

        float distance =
            Vector3.Distance(transform.position, CurrentTarget.AimPosition);

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

            LockOnTarget target =
                hitCollider.GetComponentInParent<LockOnTarget>();

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

        float angle =
            Vector3.Angle(
                playerCamera.transform.forward,
                toTarget.normalized
            );

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

        float angle =
            Vector3.Angle(
                playerCamera.transform.forward,
                toTarget.normalized
            );

        float distance =
            Vector3.Distance(transform.position, target.AimPosition);

        float normalizedAngle = angle / maxLockOnAngle;
        float normalizedDistance = distance / lockOnRadius;

        float priorityBonus =
            1f / Mathf.Max(0.01f, target.Priority);

        return
            normalizedAngle * 2f +
            normalizedDistance +
            priorityBonus * 0.25f;
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