using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Tooltip("Mindestzeit zwischen Target-Wechseln.")]
    [SerializeField] private float targetSwitchCooldown = 0.25f;

    [Tooltip("Wie stark ein neues Target in die gewünschte Bildschirmrichtung liegen muss.")]
    [SerializeField] private float minSwitchDirectionDot = 0.35f;

    [Header("Target Switching - Gamepad")]
    [Tooltip("Wie weit der rechte Stick gedrückt werden muss, um ein Target zu wechseln.")]
    [FormerlySerializedAs("targetSwitchThreshold")]
    [SerializeField] private float gamepadTargetSwitchThreshold = 0.65f;

    [Tooltip("Wie weit der Stick zurück in die Mitte muss, bevor erneut gewechselt werden darf.")]
    [FormerlySerializedAs("targetSwitchNeutralThreshold")]
    [SerializeField] private float gamepadTargetSwitchNeutralThreshold = 0.25f;

    [Tooltip("Wenn aktiv, muss der rechte Stick vor dem nächsten Wechsel erst zurück zur Mitte.")]
    [FormerlySerializedAs("requireStickReturnToNeutral")]
    [SerializeField] private bool requireGamepadStickReturnToNeutral = true;

    [Header("Target Switching - Mouse")]
    [Tooltip("Wie viel Mausbewegung gesammelt werden muss, bevor ein Target-Wechsel passiert.")]
    [SerializeField] private float mouseTargetSwitchThreshold = 55f;

    [Tooltip("Unter diesem Mausbewegungswert gilt die Maus als still.")]
    [SerializeField] private float mouseTargetSwitchResetThreshold = 1.5f;

    [Tooltip("Wie lange die Maus still sein muss, bevor erneut gewechselt werden darf.")]
    [SerializeField] private float mouseTargetSwitchRearmTime = 0.08f;

    [Tooltip("Wenn aktiv, muss die Maus nach einem Target-Wechsel kurz stoppen, bevor erneut gewechselt werden darf.")]
    [SerializeField] private bool requireMouseStopBeforeNextSwitch = true;

    [Tooltip("Wie schnell ungenutzte Mausbewegung wieder abgebaut wird.")]
    [SerializeField] private float mouseSwitchAccumulatorDecay = 120f;

    [Header("Lock On Camera")]
    [Tooltip("Wie schnell die Kamera im Lock-on zum Target rotiert.")]
    [SerializeField] private float lockOnCameraRotationSpeed = 12f;

    [Tooltip("Zusätzlicher Pitch-Offset im Lock-on. Positiv schaut meist tiefer, negativ höher.")]
    [SerializeField] private float lockOnCameraPitchOffset = 0f;

    public float LockOnCameraRotationSpeed => lockOnCameraRotationSpeed;
    public float LockOnCameraPitchOffset => lockOnCameraPitchOffset;

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
    private PlayerInputReader _playerInputReader;

    private float nextValidationTime;
    private float nextTargetSwitchTime;

    private bool gamepadTargetSwitchInputLocked;

    private bool mouseTargetSwitchInputLocked;
    private float mouseNeutralStartedTime = -1f;
    private Vector2 accumulatedMouseSwitchInput;

    private readonly Collider[] targetColliders = new Collider[32];

    private void Awake()
    {
        _playerState = GetComponent<PlayerState>();
        _playerInputReader = GetComponent<PlayerInputReader>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Start()
    {
        if (_playerInputReader == null)
        {
            Debug.LogError("PlayerLockOnController: PlayerInputReader wurde nicht gefunden.");
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
        }
    }

    private void Update()
    {
        if (_playerInputReader.LockOnPressed)
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

        ResetTargetSwitchInputState();
    }

    private void ResetTargetSwitchInputState()
    {
        gamepadTargetSwitchInputLocked = false;

        mouseTargetSwitchInputLocked = false;
        mouseNeutralStartedTime = -1f;
        accumulatedMouseSwitchInput = Vector2.zero;

        nextTargetSwitchTime = 0f;
    }

    private void HandleTargetSwitchInput()
    {
        if (!enableTargetSwitching)
            return;

        if (CurrentTarget == null)
            return;

        Vector2 lookInput = _playerInputReader.LookInput;

        if (_playerInputReader.IsLookInputFromGamepad)
        {
            HandleGamepadTargetSwitchInput(lookInput);
        }
        else
        {
            HandleMouseTargetSwitchInput(lookInput);
        }
    }

    private void HandleGamepadTargetSwitchInput(Vector2 lookInput)
    {
        float neutralThresholdSqr =
            gamepadTargetSwitchNeutralThreshold *
            gamepadTargetSwitchNeutralThreshold;

        if (lookInput.sqrMagnitude < neutralThresholdSqr)
        {
            gamepadTargetSwitchInputLocked = false;
            return;
        }

        float switchThresholdSqr =
            gamepadTargetSwitchThreshold *
            gamepadTargetSwitchThreshold;

        if (lookInput.sqrMagnitude < switchThresholdSqr)
            return;

        if (requireGamepadStickReturnToNeutral && gamepadTargetSwitchInputLocked)
            return;

        if (Time.time < nextTargetSwitchTime)
            return;

        Vector2 switchDirection = GetDominantSwitchDirection(lookInput);

        bool switched = ExecuteTargetSwitch(switchDirection);

        if (requireGamepadStickReturnToNeutral && switched)
        {
            gamepadTargetSwitchInputLocked = true;
        }
    }

    private void HandleMouseTargetSwitchInput(Vector2 lookInput)
    {
        float resetThresholdSqr =
            mouseTargetSwitchResetThreshold *
            mouseTargetSwitchResetThreshold;

        bool mouseIsStill =
            lookInput.sqrMagnitude <= resetThresholdSqr;

        if (mouseIsStill)
        {
            if (mouseNeutralStartedTime < 0f)
            {
                mouseNeutralStartedTime = Time.time;
            }

            bool mouseWasStillLongEnough =
                Time.time >= mouseNeutralStartedTime + mouseTargetSwitchRearmTime;

            if (mouseWasStillLongEnough)
            {
                mouseTargetSwitchInputLocked = false;
                accumulatedMouseSwitchInput = Vector2.zero;
            }

            return;
        }

        mouseNeutralStartedTime = -1f;

        if (requireMouseStopBeforeNextSwitch && mouseTargetSwitchInputLocked)
            return;

        if (Time.time < nextTargetSwitchTime)
            return;

        accumulatedMouseSwitchInput = Vector2.MoveTowards(
            accumulatedMouseSwitchInput,
            Vector2.zero,
            mouseSwitchAccumulatorDecay * Time.deltaTime
        );

        if (accumulatedMouseSwitchInput.sqrMagnitude > 0.001f &&
            lookInput.sqrMagnitude > 0.001f)
        {
            float directionDot = Vector2.Dot(
                accumulatedMouseSwitchInput.normalized,
                lookInput.normalized
            );

            if (directionDot < -0.25f)
            {
                accumulatedMouseSwitchInput = Vector2.zero;
            }
        }

        accumulatedMouseSwitchInput += lookInput;

        if (accumulatedMouseSwitchInput.magnitude < mouseTargetSwitchThreshold)
            return;

        Vector2 switchDirection =
            GetDominantSwitchDirection(accumulatedMouseSwitchInput);

        bool switched = ExecuteTargetSwitch(switchDirection);

        accumulatedMouseSwitchInput = Vector2.zero;

        if (requireMouseStopBeforeNextSwitch && switched)
        {
            mouseTargetSwitchInputLocked = true;
        }
    }

    private bool ExecuteTargetSwitch(Vector2 switchDirection)
    {
        if (switchDirection.sqrMagnitude < 0.001f)
            return false;

        bool switched = TrySwitchTarget(switchDirection);

        nextTargetSwitchTime = Time.time + targetSwitchCooldown;

        if (switched)
        {
            Debug.Log($"LockOn: Target gewechselt nach {switchDirection}");
        }

        return switched;
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

            if (directionDot < minSwitchDirectionDot)
                continue;

            float screenDistance = screenDelta.magnitude;

            float worldDistance =
                Vector3.Distance(transform.position, target.AimPosition);

            float normalizedWorldDistance =
                worldDistance / lockOnRadius;

            float priorityBonus =
                1f / Mathf.Max(0.01f, target.Priority);

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