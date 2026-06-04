using UnityEngine;

public class LockOnTarget : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform lockOnPoint;

    [Tooltip("Kann dieses Target aktuell gelockt werden? Bei Tod/Stagger/Unsichtbarkeit später false setzen.")]
    [SerializeField] private bool isTargetable = true;

    [Tooltip("Optionaler Prioritätswert. Höhere Werte können später bevorzugt werden.")]
    [SerializeField] private float priority = 1f;

    [Header("Debug Target Visual")]
    [SerializeField] private bool showTargetVisual = true;
    [SerializeField] private Color targetVisualColor = Color.yellow;
    [SerializeField] private float targetVisualSize = 14f;
    [SerializeField] private bool drawOnlyInPlayMode = true;

    private bool isCurrentlyTargeted;
    private Texture2D targetVisualTexture;

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

    private void Awake()
    {
        CreateTargetVisualTexture();
    }

    public void SetTargetable(bool value)
    {
        isTargetable = value;

        if (!isTargetable)
            SetTargeted(false);
    }

    public void SetTargeted(bool value)
    {
        isCurrentlyTargeted = value;
    }

    private void OnGUI()
    {
        if (!showTargetVisual)
            return;

        if (!isCurrentlyTargeted)
            return;

        if (!isTargetable)
            return;

        if (drawOnlyInPlayMode && !Application.isPlaying)
            return;

        Camera camera = Camera.main;

        if (camera == null)
            return;

        Vector3 screenPosition = camera.WorldToScreenPoint(AimPosition);

        // Hinter der Kamera nicht zeichnen
        if (screenPosition.z <= 0f)
            return;

        if (targetVisualTexture == null)
            CreateTargetVisualTexture();

        float size = targetVisualSize;

        Rect rect = new Rect(
            screenPosition.x - size * 0.5f,
            Screen.height - screenPosition.y - size * 0.5f,
            size,
            size
        );

        GUI.DrawTexture(rect, targetVisualTexture);
    }

    private void CreateTargetVisualTexture()
    {
        targetVisualTexture = new Texture2D(1, 1);
        targetVisualTexture.SetPixel(0, 0, targetVisualColor);
        targetVisualTexture.Apply();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            CreateTargetVisualTexture();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 drawPosition = AimPosition;

        Gizmos.DrawWireSphere(drawPosition, 0.2f);
        Gizmos.DrawLine(transform.position, drawPosition);
    }
}