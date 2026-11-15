using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;               // event args
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3.x (for XRI 2.x delete this line)

public enum ToolType { None, Knife, Scissors, Hammer, BlowTorch }
public enum ObjectState { Intact, Cut, Smashed, Burned }

[Serializable]
public class ToolReaction
{
    public ToolType tool;               // which tool affects this object
    public ObjectState resultState;     // state it ends up in
    public Material newMaterial;        // optional: swap look (e.g. charred)
    public Mesh newMesh;                // optional: swap mesh (e.g. cut / smashed)
    public GameObject spawnPrefab;      // optional: debris, halves, particles
    public UnityEvent onApplied;        // optional: sound, animation, etc.
}

[RequireComponent(typeof(XRGrabInteractable))]
public class ToolReactiveObject : MonoBehaviour
{
    // ---------- static API: call these from your existing scripts ----------
    public static ToolType CurrentTool { get; private set; } = ToolType.None;
    static event Action ToolChanged;
    static event Action<ToolType> GestureMade;

    /// Call from your arm-panel script when a tool is picked (None = deselect).
    public static void SetCurrentTool(ToolType tool)
    {
        CurrentTool = tool;
        ToolChanged?.Invoke();
    }

    /// Call from your gesture script when the gesture for a tool is recognised.
    public static void PerformGesture(ToolType gestureTool) => GestureMade?.Invoke(gestureTool);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { CurrentTool = ToolType.None; ToolChanged = null; GestureMade = null; }

    // ---------- per-object settings ----------
    [Header("Outline")]
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Material outlineMaterial;      // red outline material
    [SerializeField] bool outlineOnlyWhenHovered = false;

    [Header("Reactions")]
    [SerializeField] List<ToolReaction> reactions = new();
    public UnityEvent<ObjectState> onStateChanged;

    public ObjectState State { get; private set; } = ObjectState.Intact;

    XRGrabInteractable grab;
    Material[] baseMaterials;
    int hoverCount;
    bool outlined;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (!targetRenderer) targetRenderer = GetComponentInChildren<Renderer>();
        baseMaterials = targetRenderer.sharedMaterials;
    }

    void OnEnable()
    {
        grab.hoverEntered.AddListener(OnHoverEnter);
        grab.hoverExited.AddListener(OnHoverExit);
        ToolChanged += RefreshOutline;
        GestureMade += OnGesture;
        RefreshOutline();
    }

    void OnDisable()
    {
        grab.hoverEntered.RemoveListener(OnHoverEnter);
        grab.hoverExited.RemoveListener(OnHoverExit);
        ToolChanged -= RefreshOutline;
        GestureMade -= OnGesture;
    }

    void OnHoverEnter(HoverEnterEventArgs _) { hoverCount++; RefreshOutline(); }
    void OnHoverExit(HoverExitEventArgs _) { hoverCount = Mathf.Max(0, hoverCount - 1); RefreshOutline(); }

    ToolReaction FindReaction(ToolType tool) =>
        tool == ToolType.None ? null : reactions.Find(r => r.tool == tool);

    bool CanReactTo(ToolType tool) => State == ObjectState.Intact && FindReaction(tool) != null;

    void RefreshOutline()
    {
        bool want = CanReactTo(CurrentTool) && (!outlineOnlyWhenHovered || hoverCount > 0);
        if (want == outlined) return;
        outlined = want;

        if (want)
        {
            var mats = new Material[baseMaterials.Length + 1];
            baseMaterials.CopyTo(mats, 0);
            mats[mats.Length - 1] = outlineMaterial;
            targetRenderer.sharedMaterials = mats;
        }
        else targetRenderer.sharedMaterials = baseMaterials;
    }

    void OnGesture(ToolType gestureTool)
    {
        // gesture must match the selected tool, and a hand must be on this object
        if (gestureTool != CurrentTool || hoverCount == 0 || !CanReactTo(gestureTool)) return;
        Apply(FindReaction(gestureTool));
    }

    void Apply(ToolReaction r)
    {
        State = r.resultState;

        if (r.newMaterial)
        {
            baseMaterials = new[] { r.newMaterial };
            targetRenderer.sharedMaterials = baseMaterials;
            outlined = false;
        }
        if (r.newMesh && TryGetComponent(out MeshFilter mf)) mf.sharedMesh = r.newMesh;
        if (r.spawnPrefab) Instantiate(r.spawnPrefab, transform.position, transform.rotation);

        r.onApplied?.Invoke();
        onStateChanged?.Invoke(State);
        RefreshOutline();   // outline turns off because the object is no longer Intact
    }
}