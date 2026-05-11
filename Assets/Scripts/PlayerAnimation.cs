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
        float targetBlend = 0f;

        if (input.MovementInput.sqrMagnitude > 0.01f)
        {
            targetBlend = input.SprintToggledOn ? 1f : 0.5f;
        }

        currentBlend = Mathf.Lerp(currentBlend, targetBlend, blendSpeed * Time.deltaTime);

        animator.SetFloat(inputXHash, currentBlend);
    }
}