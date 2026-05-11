using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float blendSpeed = 5f;

    private PlayerInputReader input;

    private static readonly int inputXHash = Animator.StringToHash("InputX");

    private float currentBlend;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        // Prüft ob überhaupt Movement Input vorhanden ist
        bool isMoving = input.MovementInput.sqrMagnitude > 0.01f;

        // Zielwert: 0 = Idle, 1 = Walk
        float targetBlend = isMoving ? 1f : 0f;

        // Smooth blend
        currentBlend = Mathf.Lerp(currentBlend, targetBlend, blendSpeed * Time.deltaTime);

        animator.SetFloat(inputXHash, currentBlend);
    }
}