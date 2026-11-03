using UnityEngine;
using UnityEngine.Events;
using VRPrototype;

public class HammerNail : MonoBehaviour
{
    public HandTransformationController tools;
    public HandGestureSystem gestures;
    public int hitsNeeded = 3;
    public float sinkPerHit = 0.02f;
    public float hitDistance = 0.08f;
    public UnityEvent onNailDone;

    private int hits = 0;
    private bool hammerWasClose = false;

    void Update()
    {
        if (hits >= hitsNeeded) return;
        if (tools.currentTool != ProstheticTool.Hammer) return;
        if (gestures.GetGesture(HandSide.Right) != HandGesture.Fist) return;

        float distance = Vector3.Distance(tools.hammerVisual.transform.position, transform.position);

        if (distance < hitDistance && !hammerWasClose)
        {
            hammerWasClose = true;
            Hit();
        }
        else if (distance > hitDistance)
        {
            hammerWasClose = false;
        }
    }

    [ContextMenu("Test Hit")]
    public void Hit()
    {
        hits = hits + 1;
        transform.position = transform.position + Vector3.down * sinkPerHit;
        Debug.Log("Nail hit " + hits + " of " + hitsNeeded);

        if (hits >= hitsNeeded)
        {
            Debug.Log("Nail is in! Task done.");
            onNailDone.Invoke();
        }
    }
}
