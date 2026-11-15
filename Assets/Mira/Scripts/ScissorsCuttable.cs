using UnityEngine;
using UnityEngine.Events;

public class ScissorsCuttable : MonoBehaviour
{
    [Header("Result")]
    [Tooltip("Optional: object spawned in place (e.g. two cut halves).")]
    [SerializeField] private GameObject cutPrefab;
    [Tooltip("Optional: material swapped onto the object when cut.")]
    [SerializeField] private Material cutMaterial;
    [SerializeField] private bool destroyOriginal = true;

    [Header("Feedback (uses your Outline script)")]
    [SerializeField] private Outline outline;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color cuttingColor = new Color(0.9f, 0.2f, 0.4f);

    public UnityEvent onCut;
    public bool IsCut { get; private set; }

    private void Awake()
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline != null) outline.OutlineColor = idleColor;
    }

    public void SetCutting(bool cutting)
    {
        if (outline != null)
            outline.OutlineColor = cutting ? cuttingColor : idleColor;
    }

    public void Cut()
    {
        if (IsCut) return;
        IsCut = true;

        if (cutPrefab != null)
            Instantiate(cutPrefab, transform.position, transform.rotation);

        if (cutMaterial != null && !destroyOriginal)
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.material = cutMaterial;

        if (outline != null) outline.enabled = false;
        onCut?.Invoke();

        if (destroyOriginal) Destroy(gameObject);
    }
}