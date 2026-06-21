using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("Debug Display")]
    [SerializeField] private bool showDebugStamina = true;
    [SerializeField] private Vector2 debugPosition = new Vector2(10f, 10f);
    
    [Tooltip("Minimum an Stamina, damit eine stamina-kostende Aktion starten darf.")]
    [SerializeField] private float minimumStaminaForAction = 1f;
    
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private bool startWithFullStamina = true;
    [SerializeField] private float currentStamina;

    [Header("Regeneration")]
    [SerializeField] private float staminaRegenPerSecond = 25f;
    [SerializeField] private float staminaRegenDelay = 0.75f;

    [Header("Sprint")]
    [SerializeField] private float sprintStaminaDrainPerSecond = 18f;

    [Tooltip("Wenn Sprint-Stamina auf 0 fällt, darf erst ab diesem Prozentwert wieder gesprintet werden.")]
    [Range(0f, 1f)]
    [SerializeField] private float sprintRecoveryPercent = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool logStaminaUse = false;

    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    public float NormalizedStamina => maxStamina <= 0f ? 0f : currentStamina / maxStamina;

    public bool HasStamina => currentStamina >= minimumStaminaForAction;
    public bool IsSprintExhausted { get; private set; }

    public bool CanUseStaminaAction => HasStamina;
    public bool CanSprint => HasStamina && !IsSprintExhausted;

    private int activeStaminaActionCount;
    private float regenAllowedTime;

    private void Awake()
    {
        if (startWithFullStamina)
        {
            currentStamina = maxStamina;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        if (currentStamina <= 0f)
        {
            IsSprintExhausted = true;
        }
    }

    private void Update()
    {
        UpdateSprintRecoveryLockout();
        RegenerateStamina();
    }

    public void BeginStaminaAction()
    {
        activeStaminaActionCount++;
    }

    public void EndStaminaAction()
    {
        activeStaminaActionCount = Mathf.Max(0, activeStaminaActionCount - 1);

        if (activeStaminaActionCount == 0)
        {
            regenAllowedTime = Time.time + staminaRegenDelay;
        }
    }

    public bool TrySpendStamina(float amount)
    {
        if (!CanUseStaminaAction)
            return false;

        SpendStamina(amount);
        return true;
    }
    
    public bool TryUseInstantStaminaAction(float amount)
    {
        if (!CanUseStaminaAction)
            return false;

        BeginStaminaAction();
        SpendStamina(amount);
        EndStaminaAction();

        return true;
    }

    public void SpendStamina(float amount)
    {
        if (amount <= 0f)
            return;

        float oldStamina = currentStamina;

        currentStamina = Mathf.Max(0f, currentStamina - amount);

        if (currentStamina < minimumStaminaForAction)
        {
            currentStamina = 0f;
        }

        if (currentStamina <= 0f)
        {
            IsSprintExhausted = true;
        }

        if (logStaminaUse)
        {
            Debug.Log($"Stamina: {oldStamina} -> {currentStamina} (-{amount})");
        }
    }

    public void SpendSprintStamina(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (!HasStamina)
            return;

        float drainAmount = sprintStaminaDrainPerSecond * deltaTime;

        SpendStamina(drainAmount);
    }

    private void RegenerateStamina()
    {
        if (activeStaminaActionCount > 0)
            return;

        if (Time.time < regenAllowedTime)
            return;

        if (currentStamina >= maxStamina)
            return;

        currentStamina = Mathf.Min(
            maxStamina,
            currentStamina + staminaRegenPerSecond * Time.deltaTime
        );
    }

    private void UpdateSprintRecoveryLockout()
    {
        if (!IsSprintExhausted)
            return;

        float requiredStamina = maxStamina * sprintRecoveryPercent;

        if (currentStamina >= requiredStamina)
        {
            IsSprintExhausted = false;
        }
    }
    
    private void OnGUI()
    {
        if (!showDebugStamina)
            return;

        GUI.Label(
            new Rect(debugPosition.x, debugPosition.y, 250f, 25f),
            $"Stamina: {currentStamina:0}/{maxStamina:0}"
        );
    }
}