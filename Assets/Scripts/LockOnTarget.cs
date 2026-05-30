using UnityEngine;

public class LockOnTarget : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform lockOnPoint;

    [Tooltip("Kann dieses Target aktuell gelockt werden? Bei Tod/Stagger/Unsichtbarkeit später false setzen.")]
    [SerializeField] private bool isTargetable = true;

    [Tooltip("Optionaler Prioritätswert. Höhere Werte können später bevorzugt werden.")]
    [SerializeField] private float priority = 1f;

    public bool IsTargetable => isTargetable;
    public float Priority => priority;

    public Vector3 AimPosition
    {
        get
        {
            if (lockOnPoint != null)
                return lockOnPoint.position;

            return transform.position + Vector3.up * 1.5f;
        }
    }

    public void SetTargetable(bool value)
    {
        isTargetable = value;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 drawPosition;

        if (lockOnPoint != null)
            drawPosition = lockOnPoint.position;
        else
            drawPosition = transform.position + Vector3.up * 1.5f;

        Gizmos.DrawWireSphere(drawPosition, 0.2f);
        Gizmos.DrawLine(transform.position, drawPosition);
    }
}