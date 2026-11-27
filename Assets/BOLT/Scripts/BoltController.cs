using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum BoltEmotion
{
    Standard,
    Kind,
    Laugh,
    Angry,
    Sad,
    Troll
}

[RequireComponent(typeof(Animator))]
public class BoltController : MonoBehaviour
{
    [Header("Animation")]
    public float emotionCrossFade = 0.25f;
    public float flyCrossFade = 0.15f;

    [Header("Flight")]
    public float maxSpeed = 1.6f;
    public float acceleration = 3.2f;
    public float turnSpeed = 270f;
    public float arriveDistance = 0.01f;
    public float modelYawOffset = 180f;
    public float bodyCenterHeight = 0.25f;

    public UnityEvent onArrived = new UnityEvent();

    private const string FlyStartState = "BOLT_Fly_Start";
    private const string FlyLoopState = "BOLT_Fly_Loop";
    private const string FlyStopState = "BOLT_Fly_Stop";

    private Animator anim;
    private Coroutine flightRoutine;
    private Vector3 flightTarget;
    private Transform flightFaceAfter;

    public BoltEmotion CurrentEmotion { get; private set; } = BoltEmotion.Standard;
    public bool IsFlying => flightRoutine != null;
    public Vector3 BodyCenter => transform.position + Vector3.up * bodyCenterHeight;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        anim.applyRootMotion = false;
    }

    private void OnEnable()
    {
        if (anim != null)
            anim.CrossFadeInFixedTime(StateName(CurrentEmotion), 0f);
    }

    private void OnDisable()
    {
        if (flightRoutine != null)
        {
            StopCoroutine(flightRoutine);
            flightRoutine = null;
        }
    }

    public void SetEmotion(BoltEmotion emotion)
    {
        CurrentEmotion = emotion;
        if (!IsFlying && anim != null && isActiveAndEnabled)
            anim.CrossFadeInFixedTime(StateName(emotion), emotionCrossFade);
    }

    public void FlyTo(Vector3 bodyCenterTarget, Transform faceAfterArrival = null)
    {
        flightTarget = bodyCenterTarget - Vector3.up * bodyCenterHeight;
        flightFaceAfter = faceAfterArrival;

        if (!isActiveAndEnabled)
            return;

        if (flightRoutine != null)
            return;

        if ((flightTarget - transform.position).magnitude <= arriveDistance)
            return;

        flightRoutine = StartCoroutine(FlyRoutine());
    }

    public void StopFlight()
    {
        if (flightRoutine == null)
            return;

        StopCoroutine(flightRoutine);
        flightRoutine = null;
        if (anim != null && isActiveAndEnabled)
            anim.CrossFadeInFixedTime(StateName(CurrentEmotion), emotionCrossFade);
    }

    public void FaceTowards(Vector3 worldTarget, float speedMultiplier = 1f)
    {
        if (IsFlying)
            return;

        Quaternion targetRot = FacingTo(worldTarget - transform.position);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * speedMultiplier * Time.deltaTime);
    }

    public static string StateName(BoltEmotion emotion)
    {
        return "BOLT_" + emotion;
    }

    private float ClipLength(string clipName, float fallback)
    {
        RuntimeAnimatorController controller = anim != null ? anim.runtimeAnimatorController : null;
        if (controller == null)
            return fallback;

        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip.name == clipName)
                return clip.length;
        }

        return fallback;
    }

    private Quaternion FacingTo(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return transform.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
    }

    private IEnumerator FlyRoutine()
    {
        yield return null;

        while ((flightTarget - transform.position).magnitude > arriveDistance)
        {
            Quaternion targetFacing = FacingTo(flightTarget - transform.position);
            while (Quaternion.Angle(transform.rotation, targetFacing) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetFacing, turnSpeed * Time.deltaTime);
                yield return null;
                targetFacing = FacingTo(flightTarget - transform.position);
            }

            anim.CrossFadeInFixedTime(FlyStartState, flyCrossFade);
            float startLen = ClipLength(FlyStartState, 0.63f);
            float elapsed = 0f;
            float speed = 0f;
            bool looping = false;

            while (true)
            {
                elapsed += Time.deltaTime;
                if (!looping && elapsed >= startLen)
                {
                    anim.CrossFadeInFixedTime(FlyLoopState, flyCrossFade);
                    looping = true;
                }

                Vector3 toTarget = flightTarget - transform.position;
                float dist = toTarget.magnitude;
                if (dist <= arriveDistance)
                    break;

                float brakeSpeed = Mathf.Sqrt(2f * acceleration * dist);
                speed = Mathf.Min(Mathf.Min(speed + acceleration * Time.deltaTime, maxSpeed), brakeSpeed);
                transform.position = Vector3.MoveTowards(transform.position, flightTarget, Mathf.Max(speed, 0.02f) * Time.deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, FacingTo(toTarget), turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = flightTarget;

            anim.CrossFadeInFixedTime(FlyStopState, flyCrossFade);
            float stopLen = ClipLength(FlyStopState, 0.77f);
            float stopTimer = 0f;
            while (stopTimer < stopLen)
            {
                stopTimer += Time.deltaTime;
                if (flightFaceAfter != null)
                {
                    Quaternion postFacing = FacingTo(flightFaceAfter.position - transform.position);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, postFacing, turnSpeed * Time.deltaTime);
                }
                yield return null;
            }
        }

        flightRoutine = null;
        anim.CrossFadeInFixedTime(StateName(CurrentEmotion), emotionCrossFade);
        onArrived.Invoke();
    }
}
