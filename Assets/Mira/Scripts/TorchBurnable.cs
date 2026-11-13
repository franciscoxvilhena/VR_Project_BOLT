using UnityEngine;
using UnityEngine.Events;

public class TorchBurnable : MonoBehaviour
{
    [Header("Result")]
    [Tooltip("Optional: object to spawn in place (e.g. burnt/melted version).")]
    [SerializeField] private GameObject burnedPrefab;
    [Tooltip("Optional: material swapped onto the object when burned.")]
    [SerializeField] private Material burnedMaterial;
    [SerializeField] private bool destroyOriginal = false;

    [Header("Feedback (uses your Outline script)")]
    [SerializeField] private Outline outline;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color heatingColor = new Color(1f, 0.5f, 0f);

    public UnityEvent onBurn;
    public bool IsBurned { get; private set; }

    private void Awake()
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline != null) outline.OutlineColor = idleColor;
    }

    public void SetHeating(bool heating)
    {
        if (outline != null)
            outline.OutlineColor = heating ? heatingColor : idleColor;
    }

    public void Burn()
    {
        if (IsBurned) return;
        IsBurned = true;

        if (burnedPrefab != null)
            Instantiate(burnedPrefab, transform.position, transform.rotation);

        if (burnedMaterial != null && !destroyOriginal)
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.material = burnedMaterial;

        if (outline != null) outline.enabled = false;
        onBurn?.Invoke();

        if (destroyOriginal) Destroy(gameObject);
    }
}