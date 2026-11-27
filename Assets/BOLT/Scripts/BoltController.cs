using System;
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

    [Header("Taunt Stunt")]
    public float tauntCloseDistance = 0.42f;
    public float tauntLaughDuration = 1.8f;
    public float wiggleFrequency = 22f;
    public float wiggleYawAngle = 28f;
    public float wiggleRollAngle = 16f;
    public float wiggleSwayAmount = 0.085f;

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

    public void TriggerStandard() => SetEmotion(BoltEmotion.Standard);
    public void TriggerKind() => SetEmotion(BoltEmotion.Kind);
    public void TriggerLaugh() => SetEmotion(BoltEmotion.Laugh);
    public void TriggerAngry() => SetEmotion(BoltEmotion.Angry);
    public void TriggerSad() => SetEmotion(BoltEmotion.Sad);
    public void TriggerTroll() => SetEmotion(BoltEmotion.Troll);

    public void TriggerEmotionByIndex(int index)
    {
        if (index >= 0 && index <= (int)BoltEmotion.Troll)
            SetEmotion((BoltEmotion)index);
    }

    public void TriggerEmotionByName(string emotionName)
    {
        if (Enum.TryParse(emotionName, true, out BoltEmotion emotion))
            SetEmotion(emotion);
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

    public void PlayFaceTaunt(Transform targetHead, Vector3 retreatBodyCenter)
    {
        if (!isActiveAndEnabled)
            return;

        if (flightRoutine != null)
        {
            StopCoroutine(flightRoutine);
            flightRoutine = null;
        }

        flightRoutine = StartCoroutine(FaceTauntRoutine(targetHead, retreatBodyCenter));
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

    private Vector3 GetFaceClosePosition(Transform targetHead)
    {
        if (targetHead == null)
            return transform.position + transform.forward * tauntCloseDistance;

        Vector3 headForward = targetHead.forward;
        headForward.y = 0f;
        if (headForward.sqrMagnitude < 0.0001f)
            headForward = Vector3.forward;
        headForward.Normalize();

        return targetHead.position + headForward * tauntCloseDistance + Vector3.up * (-0.04f - bodyCenterHeight);
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

    private IEnumerator FaceTauntRoutine(Transform targetHead, Vector3 retreatBodyCenter)
    {
        yield return null;

        Vector3 closeTarget = GetFaceClosePosition(targetHead);
        if ((closeTarget - transform.position).magnitude > 0.04f)
        {
            anim.CrossFadeInFixedTime(FlyStartState, flyCrossFade);
            float startLen = ClipLength(FlyStartState, 0.63f);
            float elapsed = 0f;
            float speed = 0f;
            bool looping = false;
            float dashMaxSpeed = maxSpeed * 1.45f;
            float dashAccel = acceleration * 1.6f;

            while (true)
            {
                closeTarget = GetFaceClosePosition(targetHead);
                elapsed += Time.deltaTime;
                if (!looping && elapsed >= startLen)
                {
                    anim.CrossFadeInFixedTime(FlyLoopState, flyCrossFade);
                    looping = true;
                }

                Vector3 toTarget = closeTarget - transform.position;
                float dist = toTarget.magnitude;
                if (dist <= 0.025f)
                    break;

                float brakeSpeed = Mathf.Sqrt(2f * dashAccel * dist);
                speed = Mathf.Min(Mathf.Min(speed + dashAccel * Time.deltaTime, dashMaxSpeed), brakeSpeed);
                transform.position = Vector3.MoveTowards(transform.position, closeTarget, Mathf.Max(speed, 0.04f) * Time.deltaTime);
                Vector3 lookDir = targetHead != null && dist < 0.35f ? targetHead.position - transform.position : toTarget;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, FacingTo(lookDir), turnSpeed * 1.5f * Time.deltaTime);
                yield return null;
            }
        }

        CurrentEmotion = BoltEmotion.Laugh;
        anim.CrossFadeInFixedTime(StateName(BoltEmotion.Laugh), 0.12f);
        float laughTimer = 0f;
        while (laughTimer < tauntLaughDuration)
        {
            laughTimer += Time.deltaTime;
            closeTarget = GetFaceClosePosition(targetHead);
            transform.position = Vector3.Lerp(transform.position, closeTarget, 1f - Mathf.Exp(-10f * Time.deltaTime));
            if (targetHead != null)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, FacingTo(targetHead.position - transform.position), turnSpeed * 1.6f * Time.deltaTime);
            yield return null;
        }

        Vector3 retreatRootTarget = retreatBodyCenter - Vector3.up * bodyCenterHeight;
        if (targetHead != null)
        {
            Vector3 flatFromHead = retreatRootTarget - targetHead.position;
            flatFromHead.y = 0f;
            if (flatFromHead.magnitude < 1.4f)
            {
                Vector3 headForward = targetHead.forward;
                headForward.y = 0f;
                if (headForward.sqrMagnitude < 0.0001f)
                    headForward = Vector3.forward;
                headForward.Normalize();
                retreatRootTarget = targetHead.position + headForward * 1.85f + Vector3.up * (-bodyCenterHeight);
            }
        }

        CurrentEmotion = BoltEmotion.Troll;
        anim.CrossFadeInFixedTime(StateName(BoltEmotion.Troll), 0.1f);

        Vector3 basePos = transform.position;
        Quaternion awayFacing = FacingTo(retreatRootTarget - basePos);
        Quaternion startSpinRot = transform.rotation;
        float spinTimer = 0f;
        while (spinTimer < 0.22f)
        {
            spinTimer += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startSpinRot, awayFacing, spinTimer / 0.22f);
            yield return null;
        }

        Vector3 flatRetreat = retreatRootTarget - basePos;
        flatRetreat.y = 0f;
        Vector3 retreatDir = flatRetreat.sqrMagnitude > 0.0001f ? flatRetreat.normalized : Vector3.forward;
        Vector3 wiggleRight = Vector3.Cross(Vector3.up, retreatDir).normalized;

        float phase = 0f;
        float showButtTimer = 0f;
        while (showButtTimer < 0.5f)
        {
            showButtTimer += Time.deltaTime;
            phase += Time.deltaTime * wiggleFrequency;
            transform.position = basePos + wiggleRight * (Mathf.Sin(phase) * 0.045f);
            transform.rotation = awayFacing * Quaternion.Euler(-16f, Mathf.Sin(phase) * wiggleYawAngle, Mathf.Cos(phase) * wiggleRollAngle);
            yield return null;
        }

        anim.CrossFadeInFixedTime(FlyStartState, flyCrossFade);
        float awayStartLen = ClipLength(FlyStartState, 0.63f);
        float awayElapsed = 0f;
        float awaySpeed = 0f;
        bool awayLooping = false;
        float wiggleMaxSpeed = maxSpeed * 0.85f;

        while (true)
        {
            awayElapsed += Time.deltaTime;
            if (!awayLooping && awayElapsed >= awayStartLen)
            {
                anim.CrossFadeInFixedTime(FlyLoopState, flyCrossFade);
                awayLooping = true;
            }

            Vector3 toRetreat = retreatRootTarget - basePos;
            float dist = toRetreat.magnitude;
            if (dist <= arriveDistance)
                break;

            float brakeSpeed = Mathf.Sqrt(2f * acceleration * dist);
            awaySpeed = Mathf.Min(Mathf.Min(awaySpeed + acceleration * Time.deltaTime, wiggleMaxSpeed), brakeSpeed);
            basePos = Vector3.MoveTowards(basePos, retreatRootTarget, Mathf.Max(awaySpeed, 0.02f) * Time.deltaTime);

            phase += Time.deltaTime * wiggleFrequency;
            float fadeNearEnd = Mathf.Clamp01(dist / 0.4f);

            flatRetreat = toRetreat;
            flatRetreat.y = 0f;
            if (flatRetreat.sqrMagnitude > 0.0001f)
                wiggleRight = Vector3.Cross(Vector3.up, flatRetreat.normalized).normalized;

            transform.position = basePos + wiggleRight * (Mathf.Sin(phase) * wiggleSwayAmount * fadeNearEnd);
            Quaternion baseYaw = FacingTo(toRetreat);
            transform.rotation = baseYaw * Quaternion.Euler(-14f * fadeNearEnd, Mathf.Sin(phase) * wiggleYawAngle * fadeNearEnd, Mathf.Cos(phase) * wiggleRollAngle * fadeNearEnd);
            yield return null;
        }

        transform.position = retreatRootTarget;

        anim.CrossFadeInFixedTime(FlyStopState, flyCrossFade);
        float stopLen = ClipLength(FlyStopState, 0.77f);
        float stopTimer = 0f;
        while (stopTimer < stopLen)
        {
            stopTimer += Time.deltaTime;
            if (targetHead != null)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, FacingTo(targetHead.position - transform.position), turnSpeed * Time.deltaTime);
            yield return null;
        }

        flightRoutine = null;
        anim.CrossFadeInFixedTime(StateName(CurrentEmotion), emotionCrossFade);
        onArrived.Invoke();
    }
}
