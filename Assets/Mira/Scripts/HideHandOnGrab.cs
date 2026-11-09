using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3.x. Delete this line on XRI 2.x

public class HideHandOnGrab : MonoBehaviour
{
    [Header("Grab")]
    public XRGrabInteractable grabInteractable;
    public Transform rightHandRoot;

    [Header("Hand hiding")]
    public Renderer[] renderersToHide;
    public float hideDelay = 0.5f;          // seconds after impact before the hand disappears
    public bool showAgainOnRelease = false;

    [Header("Falling object")]
    public Transform fallingObject;         // the object above the grabbable one
    public float fallDistance = 1f;         // how far it drops (world units)
    public float fallDuration = 0.2f;       // lower = faster
    public ParticleSystem particlePrefab;   // spawned where the object lands

    public AudioClip fallSound;
    [Range(0f, 1f)] public float fallSoundVolume = 1f;

    bool hasFallen;
    Coroutine sequence;

    void Reset()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        if (!args.interactorObject.transform.IsChildOf(rightHandRoot)) return;
        if (hasFallen) return;

        hasFallen = true;
        sequence = StartCoroutine(FallSequence());
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (showAgainOnRelease && args.interactorObject.transform.IsChildOf(rightHandRoot))
        {
            if (sequence != null) StopCoroutine(sequence);
            SetVisible(true);
        }
    }

    IEnumerator FallSequence()
    {

        // Play sound as the platform starts to fall
        if (fallSound != null)
            AudioSource.PlayClipAtPoint(fallSound, fallingObject.position, fallSoundVolume);

        // 1. Quick fall (no physics), accelerating like a real drop
        Vector3 start = fallingObject.position;
        Vector3 end = start + Vector3.down * fallDistance;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / fallDuration;
            float eased = t * t; // ease-in
            fallingObject.position = Vector3.LerpUnclamped(start, end, Mathf.Min(eased, 1f));
            yield return null;
        }
        fallingObject.position = end;

        // 2. Particles at the landing spot
        if (particlePrefab != null)
        {
            var fx = Instantiate(particlePrefab, end, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
        }

        // 3. Delay, then hide the hand and forearm
        yield return new WaitForSeconds(hideDelay);
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        foreach (var r in renderersToHide)
            r.enabled = visible;
    }
}