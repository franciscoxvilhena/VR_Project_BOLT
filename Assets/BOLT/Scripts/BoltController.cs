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
    public float flyCrossFade = 0.18f;

    [Header("Flight")]
    public float maxSpeed = 1.6f;
    public float acceleration = 3.2f;
    public float turnSpeed = 240f;
    public float arriveDistance = 0.02f;
    public float modelYawOffset = 180f;
    public float bodyCenterHeight = 0.25f;

    [Header("Hover Feel")]
    public Vector3 hoverAmplitude = new Vector3(0.022f, 0.028f, 0.022f);
    public float hoverFrequency = 0.45f;
    public float hoverRollAmplitude = 2.2f;
    public float hoverPitchAmplitude = 1.6f;
    public float flightBankScale = 4.5f;
    public float maxFlightBank = 18f;

    [Header("Taunt Stunt")]
    public float tauntCloseDistance = 0.48f;
    public float tauntDuration = 2.2f;

    public UnityEvent onArrived = new UnityEvent();

    private const string FlyStartState = "BOLT_Fly_Start";
    private const string FlyLoopState = "BOLT_Fly_Loop";
    private const string FlyStopState = "BOLT_Fly_Stop";

    private Animator anim;
    private Coroutine flightRoutine;
    private Vector3 flightTarget;
    private Transform flightFaceAfter;
    private bool retargeted;

    private Vector3 anchorPos;
    private Quaternion anchorRot;
    private bool hasAnchor;
    private Vector3 velocity;
    private float bankAngle;
    private float bankVelocity;
    private float pitchOffset;
    private float pitchVelocity;
    private float noiseSeed;

    public BoltEmotion CurrentEmotion { get; private set; } = BoltEmotion.Standard;
    public bool IsFlying => flightRoutine != null;
    public Vector3 BodyCenter => (hasAnchor ? anchorPos : transform.position) + Vector3.up * bodyCenterHeight;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        anim.applyRootMotion = false;
        noiseSeed = UnityEngine.Random.Range(0f, 100f);
        anchorPos = transform.position;
        anchorRot = transform.rotation;
        hasAnchor = true;
    }

    private void OnEnable()
    {
        if (!hasAnchor)
        {
            anchorPos = transform.position;
            anchorRot = transform.rotation;
            hasAnchor = true;
        }
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
        velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!hasAnchor)
            return;

        if (!IsFlying)
        {
            bankAngle = Mathf.SmoothDamp(bankAngle, 0f, ref bankVelocity, 0.25f);
            pitchOffset = Mathf.SmoothDamp(pitchOffset, 0f, ref pitchVelocity, 0.25f);
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, acceleration * Time.deltaTime);
        }

        float speedRatio = Mathf.Clamp01(velocity.magnitude / Mathf.Max(maxSpeed, 0.1f));
        float hoverScale = Mathf.Lerp(1f, 0.3f, speedRatio);
        float t = Time.time * hoverFrequency + noiseSeed;

        float nx = (Mathf.PerlinNoise(t, 1.37f) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(3.11f, t * 1.15f) - 0.5f) * 1.3f + Mathf.Sin(t * 2.7f) * 0.35f;
        float nz = (Mathf.PerlinNoise(t * 0.9f, 7.23f) - 0.5f) * 2f;

        Vector3 hoverPos = new Vector3(nx * hoverAmplitude.x, ny * hoverAmplitude.y, nz * hoverAmplitude.z) * hoverScale;
        float swayRoll = Mathf.Sin(t * 2.1f) * hoverRollAmplitude * hoverScale;
        float swayPitch = Mathf.Cos(t * 1.6f) * hoverPitchAmplitude * hoverScale;

        transform.position = anchorPos + hoverPos;
        transform.rotation = anchorRot * Quaternion.Euler(swayPitch + pitchOffset, 0f, swayRoll + bankAngle);
    }

    public void TeleportTo(Vector3 bodyCenter, Vector3 lookAtPoint)
    {
        anchorPos = bodyCenter - Vector3.up * bodyCenterHeight;
        anchorRot = FacingTo(lookAtPoint - anchorPos, true);
        hasAnchor = true;
        velocity = Vector3.zero;
        bankAngle = 0f;
        bankVelocity = 0f;
        pitchOffset = 0f;
        pitchVelocity = 0f;
        transform.position = anchorPos;
        transform.rotation = anchorRot;
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
        retargeted = true;

        if (!isActiveAndEnabled)
            return;

        if (flightRoutine != null)
            return;

        if ((flightTarget - anchorPos).magnitude <= arriveDistance)
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

        Quaternion targetRot = FacingTo(worldTarget - anchorPos, true);
        anchorRot = Quaternion.RotateTowards(anchorRot, targetRot, turnSpeed * speedMultiplier * Time.deltaTime);
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

    private Quaternion FacingTo(Vector3 direction, bool allowPitch = false)
    {
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        float horizMag = flat.magnitude;
        if (horizMag < 0.0001f)
            return anchorRot;

        Vector3 lookDir = flat;
        if (allowPitch)
        {
            float clampedY = Mathf.Clamp(direction.y, -horizMag * 0.32f, horizMag * 0.32f);
            lookDir = new Vector3(direction.x, clampedY, direction.z).normalized;
        }
        else
        {
            lookDir = flat / horizMag;
        }

        return Quaternion.LookRotation(lookDir, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
    }

    private Vector3 GetFaceClosePosition(Transform targetHead)
    {
        if (targetHead == null)
            return anchorPos + transform.forward * tauntCloseDistance;

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

        while ((flightTarget - anchorPos).magnitude > arriveDistance)
        {
            retargeted = false;
            yield return ExecuteFlightLeg(flightTarget, flightFaceAfter, 1f, 0.38f, true);
        }

        flightRoutine = null;
        anim.CrossFadeInFixedTime(StateName(CurrentEmotion), emotionCrossFade);
        onArrived.Invoke();
    }

    private IEnumerator ExecuteFlightLeg(Vector3 legTarget, Transform faceAfter, float speedScale, float stopFlareDist, bool allowRetarget)
    {
        Vector3 startPos = anchorPos;
        Vector3 chord = legTarget - startPos;
        float totalDist = chord.magnitude;
        if (totalDist <= arriveDistance)
            yield break;

        Vector3 chordDir = chord / totalDist;
        Vector3 flatSide = Vector3.Cross(Vector3.up, new Vector3(chordDir.x, 0f, chordDir.z));
        if (flatSide.sqrMagnitude < 0.001f)
            flatSide = Vector3.Cross(Vector3.up, Vector3.forward);
        flatSide.Normalize();

        float distScale = Mathf.Clamp01(totalDist / 1.2f);
        float arcVert = UnityEngine.Random.Range(-0.07f, 0.14f) * distScale;
        float arcSide = UnityEngine.Random.Range(-0.18f, 0.18f) * distScale;
        Vector3 arcVector = Vector3.up * arcVert + flatSide * arcSide;

        anim.CrossFadeInFixedTime(FlyStartState, flyCrossFade);
        float startLen = ClipLength(FlyStartState, 0.63f) * 0.72f;
        float elapsed = 0f;
        bool looping = false;
        bool brakingClipPlayed = false;

        float effMaxSpeed = maxSpeed * speedScale;
        float effAccel = acceleration * speedScale;

        while (true)
        {
            if (allowRetarget && retargeted)
            {
                retargeted = false;
                legTarget = flightTarget;
                startPos = anchorPos;
                totalDist = Mathf.Max((legTarget - startPos).magnitude, 0.01f);
            }

            float dt = Time.deltaTime;
            elapsed += dt;

            Vector3 toEnd = legTarget - anchorPos;
            float remainingDist = toEnd.magnitude;

            if (remainingDist <= arriveDistance)
                break;

            if (!brakingClipPlayed && remainingDist <= stopFlareDist && velocity.magnitude > 0.15f)
            {
                anim.CrossFadeInFixedTime(FlyStopState, flyCrossFade);
                brakingClipPlayed = true;
            }
            else if (!brakingClipPlayed && !looping && elapsed >= startLen)
            {
                anim.CrossFadeInFixedTime(FlyLoopState, flyCrossFade);
                looping = true;
            }

            float progress = Mathf.Clamp01(1f - remainingDist / Mathf.Max(totalDist, 0.01f));
            Vector3 waypoint = legTarget + arcVector * Mathf.Sin(progress * Mathf.PI);

            Vector3 toWaypoint = waypoint - anchorPos;
            float wpDist = toWaypoint.magnitude;
            float desiredSpeed = Mathf.Min(effMaxSpeed, Mathf.Sqrt(2f * effAccel * remainingDist) + 0.04f);
            Vector3 desiredVel = (toWaypoint / Mathf.Max(wpDist, 0.0001f)) * desiredSpeed;

            Vector3 prevVel = velocity;
            velocity = Vector3.MoveTowards(velocity, desiredVel, effAccel * 1.35f * dt);

            if (dt > 0.0001f)
            {
                Vector3 worldAccel = (velocity - prevVel) / dt;
                Quaternion invYaw = Quaternion.Inverse(Quaternion.Euler(0f, anchorRot.eulerAngles.y, 0f));
                Vector3 localAccel = invYaw * worldAccel;
                float targetBank = Mathf.Clamp(-localAccel.x * flightBankScale, -maxFlightBank, maxFlightBank);
                float targetPitch = Mathf.Clamp(localAccel.z * 2f, -9f, 9f);
                bankAngle = Mathf.SmoothDamp(bankAngle, targetBank, ref bankVelocity, 0.16f);
                pitchOffset = Mathf.SmoothDamp(pitchOffset, targetPitch, ref pitchVelocity, 0.18f);
            }

            Vector3 step = velocity * dt;
            if (step.magnitude >= remainingDist)
            {
                anchorPos = legTarget;
                break;
            }
            anchorPos += step;

            Vector3 desiredForward = velocity.sqrMagnitude > 0.02f ? velocity.normalized : toEnd.normalized;
            if (faceAfter != null)
            {
                Vector3 toFace = faceAfter.position - anchorPos;
                if (totalDist < 0.65f)
                {
                    desiredForward = Vector3.Slerp(desiredForward, toFace.normalized, 0.68f);
                }
                else if (remainingDist < 0.55f)
                {
                    float blend = 1f - Mathf.Clamp01((remainingDist - arriveDistance) / 0.55f);
                    desiredForward = Vector3.Slerp(desiredForward, toFace.normalized, blend);
                }
            }

            Quaternion targetRot = FacingTo(desiredForward, remainingDist < 0.55f || totalDist < 0.65f);
            anchorRot = Quaternion.RotateTowards(anchorRot, targetRot, turnSpeed * speedScale * dt);

            yield return null;
        }

        anchorPos = legTarget;
        if (!brakingClipPlayed)
            anim.CrossFadeInFixedTime(FlyStopState, flyCrossFade);

        float settleTimer = 0f;
        while (settleTimer < 0.35f)
        {
            settleTimer += Time.deltaTime;
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, effAccel * 2f * Time.deltaTime);
            bankAngle = Mathf.SmoothDamp(bankAngle, 0f, ref bankVelocity, 0.16f);
            pitchOffset = Mathf.SmoothDamp(pitchOffset, 0f, ref pitchVelocity, 0.16f);

            if (faceAfter != null)
                anchorRot = Quaternion.RotateTowards(anchorRot, FacingTo(faceAfter.position - anchorPos, true), turnSpeed * Time.deltaTime);

            yield return null;
        }
    }

    private IEnumerator FaceTauntRoutine(Transform targetHead, Vector3 retreatBodyCenter)
    {
        yield return null;

        yield return ExecuteFlightLeg(GetFaceClosePosition(targetHead), targetHead, 1.35f, 0.28f, false);

        CurrentEmotion = BoltEmotion.Troll;
        anim.CrossFadeInFixedTime(StateName(BoltEmotion.Troll), 0.12f);

        float trollTimer = 0f;
        while (trollTimer < tauntDuration)
        {
            trollTimer += Time.deltaTime;
            Vector3 closeTarget = GetFaceClosePosition(targetHead);
            anchorPos = Vector3.Lerp(anchorPos, closeTarget, 1f - Mathf.Exp(-8f * Time.deltaTime));
            bankAngle = Mathf.SmoothDamp(bankAngle, 0f, ref bankVelocity, 0.18f);
            pitchOffset = Mathf.SmoothDamp(pitchOffset, 0f, ref pitchVelocity, 0.18f);
            if (targetHead != null)
                anchorRot = Quaternion.RotateTowards(anchorRot, FacingTo(targetHead.position - anchorPos, true), turnSpeed * 1.4f * Time.deltaTime);
            yield return null;
        }

        Vector3 retreatRootTarget = retreatBodyCenter - Vector3.up * bodyCenterHeight;
        if (targetHead != null)
        {
            Vector3 flatFromHead = retreatRootTarget - targetHead.position;
            flatFromHead.y = 0f;
            if (flatFromHead.magnitude < 1.35f)
            {
                Vector3 headForward = targetHead.forward;
                headForward.y = 0f;
                if (headForward.sqrMagnitude < 0.0001f)
                    headForward = Vector3.forward;
                headForward.Normalize();
                retreatRootTarget = targetHead.position + headForward * 1.75f + Vector3.up * (-bodyCenterHeight);
            }
        }

        yield return ExecuteFlightLeg(retreatRootTarget, targetHead, 1f, 0.35f, false);

        flightRoutine = null;
        anim.CrossFadeInFixedTime(StateName(CurrentEmotion), emotionCrossFade);
        onArrived.Invoke();
    }
}
