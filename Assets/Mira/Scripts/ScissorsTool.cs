using UnityEngine;
using UnityEngine.Events;
using VRPrototype;

public class ScissorsTool : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    [SerializeField] private HandTransformationController controller;
    [SerializeField] private HandGestureSystem gestureSystem;

    [Header("Snip detection (degrees between index and middle finger)")]
    [Tooltip("Fingers must open past this to arm a snip")]
    [SerializeField] private float openAngle = 20f;
    [Tooltip("Fingers must close below this to complete the snip")]
    [SerializeField] private float closeAngle = 8f;
    [SerializeField] private float minSnipInterval = 0.1f;

    [Header("Cutting")]
    [SerializeField] private int snipsRequired = 3;
    [Tooltip("Progress resets if no snip happens for this long")]
    [SerializeField] private float snipResetTime = 2f;
    [SerializeField] private LayerMask cuttableLayers = ~0;

    [Header("Cut zone (local to scissors visual, meters)")]
    [SerializeField] private Vector3 zoneCenterOffset = new Vector3(0f, 0f, 0.1f);
    [SerializeField] private Vector3 zoneHalfExtents = new Vector3(0.04f, 0.04f, 0.07f);

    [Header("Animation (optional, on your scissors model)")]
    [Tooltip("Leave empty to auto-find an Animator on the scissors visual")]
    [SerializeField] private Animator animator;
    [Tooltip("Trigger parameter fired on every snip")]
    [SerializeField] private string snipTrigger = "Snip";
    [Tooltip("Float parameter 0 (closed) to 1 (open) that follows your fingers")]
    [SerializeField] private string openFloat = "Open";
    [SerializeField] private float openSmoothing = 20f;

    [Header("Events")]
    public UnityEvent onSnip = new UnityEvent();
    public UnityEvent<float> onProgress = new UnityEvent<float>();   // 0..1
    public UnityEvent<ScissorsCuttable> onCutComplete = new UnityEvent<ScissorsCuttable>();

    [Header("Debug (live, read-only)")]
    [SerializeField] private float debugFingerAngle;

    private readonly Collider[] hits = new Collider[16];
    private ScissorsCuttable target;
    private int snipCount;
    private float lastSnipTime = -999f;
    private bool armed;           // fingers have been opened wide
    private float openValue;
    private bool hasTrigger, hasFloat;

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
        if (animator == null && controller != null && controller.scissorsVisual != null)
            animator = controller.scissorsVisual.GetComponentInChildren<Animator>(true);

        // Cache which parameters really exist so Unity doesn't spam warnings
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == snipTrigger && p.type == AnimatorControllerParameterType.Trigger) hasTrigger = true;
                if (p.name == openFloat && p.type == AnimatorControllerParameterType.Float) hasFloat = true;
            }
        }
    }

    private bool ToolSelected()
    {
        return controller != null && controller.currentTool == ProstheticTool.Scissors;
    }

    private bool GestureOn()
    {
        return gestureSystem != null && controller != null &&
               gestureSystem.GetGesture(controller.prostheticSide) == HandGesture.Scissors;
    }

    private Transform ZoneOrigin()
    {
        if (controller != null && controller.scissorsVisual != null)
            return controller.scissorsVisual.transform;
        return controller != null ? controller.transform : transform;
    }

    private void Update()
    {
        if (!ToolSelected())
        {
            ClearTarget();
            armed = false;
            return;
        }

        bool bothMet = GestureOn();
        float angle = 0f;
        bool haveAngle = bothMet &&
                         gestureSystem.TryGetMetrics(controller.prostheticSide, out HandPoseMetrics m) &&
                         ReadAngle(m, out angle);

        debugFingerAngle = angle;
        UpdateAnimatorOpen(haveAngle ? angle : 0f);

        // Target detection
        ScissorsCuttable found = FindTarget();
        if (found != target)
        {
            ClearTarget();
            target = found;
        }
        if (target != null) target.SetCutting(bothMet);

        if (!haveAngle)
        {
            armed = false;
            return;
        }

        // Snip state machine: open wide -> close = one snip
        if (angle >= openAngle)
        {
            armed = true;
        }
        else if (armed && angle <= closeAngle && Time.time - lastSnipTime >= minSnipInterval)
        {
            armed = false;
            lastSnipTime = Time.time;
            RegisterSnip();
        }

        // Reset progress if the player stops snipping
        if (snipCount > 0 && Time.time - lastSnipTime > snipResetTime)
            ResetProgress();
    }

    private bool ReadAngle(HandPoseMetrics m, out float angle)
    {
        angle = m.IndexMiddleAngle;
        return true;
    }

    private void RegisterSnip()
    {
        if (hasTrigger) animator.SetTrigger(snipTrigger);
        onSnip.Invoke();

        if (target == null) return;

        snipCount++;
        onProgress.Invoke(Mathf.Clamp01((float)snipCount / snipsRequired));

        if (snipCount >= snipsRequired)
        {
            ScissorsCuttable done = target;
            target = null;
            ResetProgress();
            done.Cut();
            onCutComplete.Invoke(done);
        }
    }

    private void UpdateAnimatorOpen(float angle)
    {
        if (!hasFloat || animator == null || !animator.isActiveAndEnabled) return;

        float targetOpen = Mathf.InverseLerp(closeAngle, openAngle, angle);
        openValue = Mathf.Lerp(openValue, targetOpen, 1f - Mathf.Exp(-openSmoothing * Time.deltaTime));
        animator.SetFloat(openFloat, openValue);
    }

    private ScissorsCuttable FindTarget()
    {
        Transform t = ZoneOrigin();
        Vector3 center = t.position + t.rotation * zoneCenterOffset;

        int count = Physics.OverlapBoxNonAlloc(center, zoneHalfExtents, hits, t.rotation,
            cuttableLayers, QueryTriggerInteraction.Collide);

        ScissorsCuttable best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var c = hits[i].GetComponentInParent<ScissorsCuttable>();
            if (c == null || c.IsCut) continue;

            float d = (hits[i].ClosestPoint(center) - center).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    private void ClearTarget()
    {
        if (target != null) target.SetCutting(false);
        target = null;
        ResetProgress();
    }

    private void ResetProgress()
    {
        if (snipCount > 0) onProgress.Invoke(0f);
        snipCount = 0;
    }

    private void OnDrawGizmosSelected()
    {
        Transform t = controller != null ? ZoneOrigin() : transform;
        Gizmos.color = new Color(0.9f, 0.2f, 0.4f);
        Gizmos.matrix = Matrix4x4.TRS(t.position + t.rotation * zoneCenterOffset, t.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, zoneHalfExtents * 2f);
    }
}