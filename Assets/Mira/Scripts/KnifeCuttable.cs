using UnityEngine;
using UnityEngine.Events;

public class KnifeCuttable : MonoBehaviour
{
    [Header("Result")]
    [Tooltip("Optional: object to spawn in place (e.g. cut halves). Leave empty to just swap material.")]
    [SerializeField] private GameObject cutPrefab;
    [SerializeField] private Material cutMaterial;
    [SerializeField] private bool destroyOriginal = true;

    [Header("Feedback (uses your Outline script)")]
    [SerializeField] private Outline outline;
    [SerializeField] private Color idleColor;
    [SerializeField] private Color cuttingColor;

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

        if (destroyOriginal && cutPrefab != null) Destroy(gameObject);
    }
}