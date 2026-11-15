using UnityEngine;
using UnityEngine.Events;
using VRPrototype;

public class HammerNail : MonoBehaviour
{
    [Header("References")]
    public HandTransformationController tools;
    public HandGestureSystem gestures;

    [Header("Hitting")]
    public int hitsNeeded = 3;
    public float sinkPerHit = 0.02f;
    public float hitCooldown = 0.3f;   // stops one swing counting several times
    public float minHitSpeed = 0.3f;   // m/s, set to 0 to disable the speed check

    [Header("Feedback (uses your Outline script)")]
    [SerializeField] private Outline outline;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color hitColor = Color.yellow;
    [SerializeField] private float flashDuration = 0.15f;

    public UnityEvent onNailDone;

    private int hits = 0;
    private float lastHitTime = -10f;
    private Vector3 lastHammerPos;
    private float hammerSpeed;

    void Awake()
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline != null) outline.OutlineColor = idleColor;
    }

    void Update()
    {
        // Track hammer speed so a slow push doesn't count as a strike
        if (tools != null && tools.hammerVisual != null)
        {
            Vector3 pos = tools.hammerVisual.transform.position;
            if (Time.deltaTime > 0f)
                hammerSpeed = (pos - lastHammerPos).magnitude / Time.deltaTime;
            lastHammerPos = pos;
        }
        else
        {
            hammerSpeed = 0f;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.collider);
    }

    void TryHit(Collider other)
    {
        if (hits >= hitsNeeded) return;
        if (tools == null || tools.hammerVisual == null) return;
        if (tools.currentTool != ProstheticTool.Hammer) return;
        if (gestures.GetGesture(HandSide.Right) != HandGesture.Fist) return;

        // Only count the hammer, not the hand or anything else
        if (!other.transform.IsChildOf(tools.hammerVisual.transform)) return;

        if (Time.time - lastHitTime < hitCooldown) return;
        if (hammerSpeed < minHitSpeed) return;

        lastHitTime = Time.time;
        Hit();
    }

    [ContextMenu("Test Hit")]
    public void Hit()
    {
        hits++;
        transform.position -= transform.up * sinkPerHit;
        Debug.Log("Nail hit " + hits + " of " + hitsNeeded);

        if (hits >= hitsNeeded)
        {
            CancelInvoke(nameof(ResetOutline));
            if (outline != null) outline.enabled = false;

            Debug.Log("Nail is in! Task done.");
            onNailDone.Invoke();
        }
        else
        {
            FlashOutline();
        }
    }

    private void FlashOutline()
    {
        if (outline == null) return;
        outline.OutlineColor = hitColor;
        CancelInvoke(nameof(ResetOutline));
        Invoke(nameof(ResetOutline), flashDuration);
    }

    private void ResetOutline()
    {
        if (outline != null) outline.OutlineColor = idleColor;
    }
}