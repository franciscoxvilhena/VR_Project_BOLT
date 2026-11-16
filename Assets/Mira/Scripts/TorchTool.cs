using UnityEngine;
using UnityEngine.Events;
using VRPrototype;

public class TorchTool : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    [SerializeField] private HandTransformationController controller;
    [SerializeField] private HandGestureSystem gestureSystem;

    [Header("Burning")]
    [SerializeField] private float heatDuration = 3f;
    [Tooltip("Grace period so a brief tracking flicker doesn't reset progress")]
    [SerializeField] private float graceTime = 0.2f;
    [SerializeField] private LayerMask burnableLayers = ~0;

    [Header("Flame")]
    [Tooltip("Optional: your own flame object/particle system. Leave empty to auto-create a simple placeholder.")]
    [SerializeField] private GameObject flameVisual;
    [SerializeField] private bool createPlaceholderFlame = true;

    [Header("Flame zone (local to the torch visual, meters)")]
    [SerializeField] private Vector3 zoneCenterOffset = new Vector3(0f, 0f, 0.12f);
    [SerializeField] private Vector3 zoneHalfExtents = new Vector3(0.03f, 0.03f, 0.06f);

    [Header("Events")]
    public UnityEvent<float> onProgress = new UnityEvent<float>();   // 0..1
    public UnityEvent<TorchBurnable> onBurnComplete = new UnityEvent<TorchBurnable>();

    private readonly Collider[] hits = new Collider[16];
    private TorchBurnable target;
    private float heatTimer;
    private float lostTimer;

    private void Awake()
    {
        if (controller == null)
            controller = FindAnyObjectByType<HandTransformationController>();
        if (gestureSystem == null && controller != null)
            gestureSystem = controller.gestureSystem;
        if (gestureSystem == null)
            gestureSystem = FindAnyObjectByType<HandGestureSystem>();
    }

    private void Start()
    {
        EnsureFlame();
    }

    // Also called from Update: the torch model can be swapped after Start (duplicate controllers
    // hand over to the main one), which destroys a placeholder flame attached to the old model.
    private void EnsureFlame()
    {
        if (flameVisual == null && createPlaceholderFlame && controller != null && controller.torchVisual != null)
        {
            flameVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flameVisual.name = "Torch_Flame_Placeholder";
            Destroy(flameVisual.GetComponent<Collider>());
            flameVisual.transform.SetParent(controller.torchVisual.transform, false);
            // In front of the torch's tip, in the direction the hand points
            controller.GetToolZone(controller.torchVisual, zoneCenterOffset, zoneHalfExtents, true,
                out Vector3 flameCenter, out _, out _);
            flameVisual.transform.position = flameCenter;
            // 6 cm across, whatever the scale of the torch model it is attached to
            float parentScale = Mathf.Max(controller.torchVisual.transform.lossyScale.x, 1e-4f);
            flameVisual.transform.localScale = Vector3.one * (0.06f / parentScale);

            var rend = flameVisual.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(1f, 0.5f, 0.1f);
        }

        if (flameVisual != null) flameVisual.SetActive(false);
    }

    private bool ToolSelected()
    {
        return controller != null && controller.currentTool == ProstheticTool.Torch;
    }

    private bool GestureOn()
    {
        return gestureSystem != null && controller != null &&
               gestureSystem.GetGesture(controller.prostheticSide) == HandGesture.Torch;
    }

    private Transform BladeOrigin()
    {
        if (controller != null && controller.torchVisual != null)
            return controller.torchVisual.transform;
        return controller != null ? controller.transform : transform;
    }

    private TorchBurnable FindTarget()
    {
        // The flame zone starts at the torch's tip and points where the hand points
        controller.GetToolZone(controller.torchVisual, zoneCenterOffset, zoneHalfExtents, true,
            out Vector3 center, out Vector3 half, out Quaternion rotation);

        int count = Physics.OverlapBoxNonAlloc(center, half, hits, rotation,
            burnableLayers, QueryTriggerInteraction.Collide);

        TorchBurnable best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var b = hits[i].GetComponentInParent<TorchBurnable>();
            if (b == null || b.IsBurned) continue;

            float d = (hits[i].ClosestPoint(center) - center).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = b; }
        }
        return best;
    }

    private void Update()
    {
        if (flameVisual == null) EnsureFlame();

        bool bothMet = ToolSelected() && GestureOn();

        // Flame is on whenever the torch is equipped AND the gesture is held
        if (flameVisual != null && flameVisual.activeSelf != bothMet)
            flameVisual.SetActive(bothMet);

        if (!ToolSelected())
        {
            ClearTarget();
            return;
        }

        TorchBurnable found = FindTarget();
        if (found != target)
        {
            ClearTarget();
            target = found;
        }

        if (target == null) return;

        if (bothMet)
        {
            lostTimer = 0f;
            heatTimer += Time.deltaTime;
            target.SetHeating(true);
            onProgress.Invoke(Mathf.Clamp01(heatTimer / heatDuration));

            if (heatTimer >= heatDuration)
            {
                TorchBurnable done = target;
                target = null;
                ResetProgress();
                done.Burn();
                onBurnComplete.Invoke(done);
            }
        }
        else
        {
            lostTimer += Time.deltaTime;
            if (lostTimer > graceTime)
            {
                target.SetHeating(false);
                ResetProgress();
            }
        }
    }

    private void ClearTarget()
    {
        if (target != null) target.SetHeating(false);
        target = null;
        ResetProgress();
    }

    private void ResetProgress()
    {
        if (heatTimer > 0f) onProgress.Invoke(0f);
        heatTimer = 0f;
        lostTimer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (controller == null) return;
        controller.GetToolZone(controller.torchVisual, zoneCenterOffset, zoneHalfExtents, true,
            out Vector3 center, out Vector3 half, out Quaternion rotation);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, half * 2f);
    }
}