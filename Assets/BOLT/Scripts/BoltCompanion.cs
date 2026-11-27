using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VRPrototype;

public class BoltCompanion : MonoBehaviour
{
    [Header("References")]
    public BoltController bolt;
    public Transform playerHead;
    public HandTransformationController toolController;
    public HandGestureSystem gestureSystem;
    public PlatformRide platformRide;

    [Header("Test Setup")]
    public bool skipToTutorialRoom = true;

    [Header("Task Triggers")]
    public bool autoReactToSceneTasks = true;
    public float triggeredEmotionHoldTime = 6f;

    [Header("Vision Roaming")]
    public bool roamInVision = true;
    public float minRoamDistance = 1.25f;
    public float maxRoamDistance = 2.2f;
    public float visionHalfAngle = 32f;
    public float minHeightOffset = -0.08f;
    public float maxHeightOffset = 0.32f;
    public float minStepDistance = 0.6f;
    public float minWaitAtPoint = 1.8f;
    public float maxWaitAtPoint = 4.2f;
    public float obstacleCheckRadius = 0.25f;

    [Header("Hand Reactions")]
    public float personalSpaceRadius = 0.32f;
    public float dangerToolRadius = 0.55f;
    public float reactionCooldown = 2f;
    public float idleTeaseInterval = 12f;

    [Header("Voice Clips")]
    public AudioSource voiceSource;
    public AudioClip[] greetingClips;
    public AudioClip[] toolSwitchClips;
    public AudioClip[] dangerClips;
    public AudioClip[] taskDoneClips;
    public AudioClip[] teaseClips;
    public AudioClip[] tauntClips;

    private IHandFrameSource frameSource;
    private float nextRoamTime;
    private float outOfViewTimer;
    private float lastReactionTime = -10f;
    private float emotionLockUntil = -10f;
    private float lastActionTime;
    private Vector3 glanceWorldPoint;
    private float glanceUntil = -10f;
    private int teaseStep;

    private readonly List<HammerNail> nails = new List<HammerNail>();
    private readonly List<KnifeTool> knives = new List<KnifeTool>();
    private readonly List<ScissorsTool> scissorsTools = new List<ScissorsTool>();
    private readonly List<TorchTool> torches = new List<TorchTool>();

    private void Start()
    {
        ResolveReferences();

        if (skipToTutorialRoom && platformRide != null && platformRide.stops != null && platformRide.stops.Length > 2 && platformRide.stops[2] != null)
        {
            platformRide.StopAllCoroutines();
            platformRide.enabled = false;
            platformRide.transform.position = platformRide.stops[2].position;
        }

        if (bolt != null)
            bolt.onArrived.AddListener(OnBoltArrived);

        if (bolt != null && playerHead != null)
        {
            Vector3 startCenter = PickVisionPoint(false);
            bolt.TeleportTo(startCenter, playerHead.position);
        }

        if (autoReactToSceneTasks)
        {
            if (toolController != null)
                toolController.onToolChanged.AddListener(OnToolChanged);

            HookTaskEvents();
        }

        lastActionTime = Time.time;
        nextRoamTime = Time.time + UnityEngine.Random.Range(minWaitAtPoint, maxWaitAtPoint);

        if (bolt != null)
            bolt.SetEmotion(BoltEmotion.Kind);
        PlayRandomClip(greetingClips);
    }

    private void OnDestroy()
    {
        if (bolt != null)
            bolt.onArrived.RemoveListener(OnBoltArrived);

        if (toolController != null)
            toolController.onToolChanged.RemoveListener(OnToolChanged);

        foreach (HammerNail nail in nails)
            if (nail != null) nail.onNailDone.RemoveListener(OnTaskCompleted);

        foreach (KnifeTool knife in knives)
            if (knife != null) knife.onCutComplete.RemoveListener(OnKnifeCut);

        foreach (ScissorsTool scissors in scissorsTools)
            if (scissors != null) scissors.onCutComplete.RemoveListener(OnScissorsCut);

        foreach (TorchTool torch in torches)
            if (torch != null) torch.onBurnComplete.RemoveListener(OnTorchBurn);
    }

    private void Update()
    {
        if (bolt == null)
            return;

        if (playerHead == null && Camera.main != null)
            playerHead = Camera.main.transform;

        HandleTestKeyboard();
        CheckHandProximity();

        if (playerHead != null && !bolt.IsFlying)
        {
            if (Time.time < glanceUntil)
                bolt.FaceTowards(glanceWorldPoint, 1.15f);
            else
                bolt.FaceTowards(playerHead.position, 0.75f);

            if (IsOutOfView())
            {
                outOfViewTimer += Time.deltaTime;
                if (outOfViewTimer >= 0.45f)
                {
                    outOfViewTimer = 0f;
                    FlyToRandomVisionPoint();
                }
            }
            else
            {
                outOfViewTimer = 0f;

                if (roamInVision && Time.time >= nextRoamTime)
                    FlyToRandomVisionPoint();
            }
        }

        if (!bolt.IsFlying && idleTeaseInterval > 0f && Time.time - lastActionTime >= idleTeaseInterval)
            TriggerIdleTease();
    }

    private void ResolveReferences()
    {
        if (bolt == null)
            bolt = GetComponent<BoltController>();

        if (playerHead == null && Camera.main != null)
            playerHead = Camera.main.transform;

        if (toolController == null)
        {
            HandTransformationController[] controllers = FindObjectsByType<HandTransformationController>(FindObjectsSortMode.None);
            foreach (HandTransformationController c in controllers)
            {
                if (c.HasCustomToolVisuals())
                {
                    toolController = c;
                    break;
                }
            }
            if (toolController == null && controllers.Length > 0)
                toolController = controllers[0];
        }

        if (gestureSystem == null && toolController != null)
            gestureSystem = toolController.gestureSystem;
        if (gestureSystem == null)
            gestureSystem = FindAnyObjectByType<HandGestureSystem>();

        if (gestureSystem != null)
            frameSource = gestureSystem.GetComponent<IHandFrameSource>();
        if (frameSource == null)
            frameSource = FindAnyObjectByType<XRHandsFrameSource>();
        if (frameSource == null)
            frameSource = FindAnyObjectByType<WebcamHandFrameSource>();

        if (platformRide == null)
            platformRide = FindAnyObjectByType<PlatformRide>();

        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();
    }

    private void HookTaskEvents()
    {
        foreach (HammerNail nail in FindObjectsByType<HammerNail>(FindObjectsSortMode.None))
        {
            nails.Add(nail);
            nail.onNailDone.AddListener(OnTaskCompleted);
        }

        foreach (KnifeTool knife in FindObjectsByType<KnifeTool>(FindObjectsSortMode.None))
        {
            knives.Add(knife);
            knife.onCutComplete.AddListener(OnKnifeCut);
        }

        foreach (ScissorsTool scissors in FindObjectsByType<ScissorsTool>(FindObjectsSortMode.None))
        {
            scissorsTools.Add(scissors);
            scissors.onCutComplete.AddListener(OnScissorsCut);
        }

        foreach (TorchTool torch in FindObjectsByType<TorchTool>(FindObjectsSortMode.None))
        {
            torches.Add(torch);
            torch.onBurnComplete.AddListener(OnTorchBurn);
        }
    }

    private Vector3 GetHeadFlatForward()
    {
        if (playerHead == null)
            return Vector3.right;

        Vector3 forward = playerHead.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            return Vector3.right;

        return forward.normalized;
    }

    private bool IsOutOfView()
    {
        if (playerHead == null || bolt == null)
            return false;

        Vector3 toBolt = bolt.BodyCenter - playerHead.position;
        float dist = toBolt.magnitude;
        if (dist < 0.7f || dist > maxRoamDistance + 0.8f)
            return true;

        toBolt.y = 0f;
        if (toBolt.sqrMagnitude < 0.001f)
            return false;

        float angle = Vector3.Angle(GetHeadFlatForward(), toBolt.normalized);
        return angle > visionHalfAngle + 20f;
    }

    private Vector3 PickVisionPoint(bool preferFar)
    {
        if (playerHead == null)
            return bolt != null ? bolt.BodyCenter : transform.position;

        Vector3 headForward = GetHeadFlatForward();
        Vector3 toCurrent = bolt != null ? bolt.BodyCenter - playerHead.position : headForward;
        toCurrent.y = 0f;
        float currentSide = toCurrent.sqrMagnitude > 0.001f
            ? Vector3.SignedAngle(headForward, toCurrent.normalized, Vector3.up)
            : 0f;

        float distMin = preferFar ? Mathf.Lerp(minRoamDistance, maxRoamDistance, 0.7f) : minRoamDistance;

        for (int i = 0; i < 10; i++)
        {
            float yaw;
            if (i < 4 || UnityEngine.Random.value < 0.6f)
                yaw = currentSide >= 0f ? UnityEngine.Random.Range(-visionHalfAngle, -4f) : UnityEngine.Random.Range(4f, visionHalfAngle);
            else
                yaw = UnityEngine.Random.Range(-visionHalfAngle, visionHalfAngle);

            float dist = UnityEngine.Random.Range(distMin, maxRoamDistance);
            float height = UnityEngine.Random.Range(minHeightOffset, maxHeightOffset);
            Vector3 dir = (Quaternion.Euler(0f, yaw, 0f) * headForward).normalized;
            Vector3 candidate = playerHead.position + dir * dist + Vector3.up * height;

            Vector3 rayVec = candidate - playerHead.position;
            float rayLen = rayVec.magnitude;
            if (rayLen > 0.01f && Physics.Raycast(playerHead.position, rayVec / rayLen, out RaycastHit hit, rayLen + obstacleCheckRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (bolt == null || !hit.transform.IsChildOf(bolt.transform))
                    candidate = hit.point - (rayVec / rayLen) * (obstacleCheckRadius + 0.1f);
            }

            bool hasClearance = !Physics.CheckSphere(candidate, obstacleCheckRadius, ~0, QueryTriggerInteraction.Ignore);
            bool farEnough = bolt == null || Vector3.Distance(candidate, bolt.BodyCenter) >= minStepDistance;
            if (hasClearance && farEnough)
                return candidate;
        }

        return playerHead.position + headForward * (preferFar ? maxRoamDistance : minRoamDistance) + Vector3.up * 0.05f;
    }

    private void OnBoltArrived()
    {
        outOfViewTimer = 0f;
        nextRoamTime = Time.time + UnityEngine.Random.Range(minWaitAtPoint, maxWaitAtPoint);
    }

    private void OnToolChanged(ProstheticTool tool)
    {
        if (!autoReactToSceneTasks)
            return;

        if (toolController != null && TryGetHandPoint(toolController.prostheticSide, out Vector3 handPoint))
        {
            glanceWorldPoint = handPoint;
            glanceUntil = Time.time + 0.9f;
        }

        if (tool == ProstheticTool.Hammer)
            TriggerEmotion(BoltEmotion.Laugh);
        else if (tool == ProstheticTool.Scissors)
            TriggerEmotion(BoltEmotion.Kind);
        else if (tool == ProstheticTool.Knife || tool == ProstheticTool.Torch)
            TriggerEmotion(BoltEmotion.Troll);
        else
            TriggerEmotion(BoltEmotion.Standard);

        PlayRandomClip(toolSwitchClips);
    }

    private void OnKnifeCut(KnifeCuttable cuttable) => OnTaskCompleted();
    private void OnScissorsCut(ScissorsCuttable cuttable) => OnTaskCompleted();
    private void OnTorchBurn(TorchBurnable burnable) => OnTaskCompleted();

    private void OnTaskCompleted()
    {
        if (!autoReactToSceneTasks)
            return;

        TriggerEmotion(BoltEmotion.Laugh);
        FlyToRandomVisionPoint(false);
        PlayRandomClip(taskDoneClips);
    }

    private void CheckHandProximity()
    {
        if (frameSource == null || !frameSource.IsRunning)
            return;

        if (bolt.IsFlying || Time.time - lastReactionTime < reactionCooldown)
            return;

        bool dangerActive = IsDangerToolActive();
        Vector3 boltCenter = bolt.BodyCenter;

        HandSide[] sides = { HandSide.Right, HandSide.Left };
        foreach (HandSide side in sides)
        {
            if (!TryGetHandPoint(side, out Vector3 handPos))
                continue;

            float dist = Vector3.Distance(handPos, boltCenter);
            bool isProstheticDanger = dangerActive && toolController != null && side == toolController.prostheticSide;

            if (isProstheticDanger && dist <= dangerToolRadius)
            {
                TriggerEmotion(BoltEmotion.Angry);
                Vector3 away = boltCenter - handPos;
                away.y = 0f;
                if (away.sqrMagnitude < 0.001f)
                    away = bolt.transform.position - (playerHead != null ? playerHead.position : Vector3.zero);
                away.y = 0f;
                away.Normalize();
                nextRoamTime = Time.time + UnityEngine.Random.Range(minWaitAtPoint, maxWaitAtPoint);
                bolt.FlyTo(boltCenter + away * 0.7f + Vector3.up * 0.1f, playerHead);
                PlayRandomClip(dangerClips);
                return;
            }

            if (dist <= personalSpaceRadius)
            {
                if (handPos.y > boltCenter.y + 0.05f)
                {
                    TriggerEmotion(BoltEmotion.Kind);
                }
                else
                {
                    TriggerEmotion(BoltEmotion.Troll);
                    FlyToRandomVisionPoint(false);
                }
                return;
            }
        }
    }

    private bool IsDangerToolActive()
    {
        if (toolController == null)
            return false;

        if (toolController.currentTool == ProstheticTool.Knife)
            return true;

        if (toolController.currentTool == ProstheticTool.Torch && gestureSystem != null)
            return gestureSystem.GetGesture(toolController.prostheticSide) == HandGesture.Torch;

        return false;
    }

    private bool TryGetHandPoint(HandSide side, out Vector3 point)
    {
        HandFrame frame = frameSource != null && frameSource.IsRunning ? frameSource.GetFrame(side) : null;
        if (frame == null || !frame.IsTracked)
        {
            point = Vector3.zero;
            return false;
        }

        if (frame.IsValid(HandJoint.IndexTip))
        {
            point = frame.GetPosition(HandJoint.IndexTip);
            return true;
        }

        if (frame.IsValid(HandJoint.Wrist))
        {
            point = frame.GetPosition(HandJoint.Wrist);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private void TriggerIdleTease()
    {
        BoltEmotion[] teaseEmotions = { BoltEmotion.Troll, BoltEmotion.Sad, BoltEmotion.Laugh };
        TriggerEmotion(teaseEmotions[teaseStep % teaseEmotions.Length]);
        teaseStep++;

        FlyToRandomVisionPoint(false);
        PlayRandomClip(teaseClips);
    }

    private void HandleTestKeyboard()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.digit1Key.wasPressedThisFrame) TriggerStandard();
        else if (kb.digit2Key.wasPressedThisFrame) TriggerKind();
        else if (kb.digit3Key.wasPressedThisFrame) TriggerLaugh();
        else if (kb.digit4Key.wasPressedThisFrame) TriggerAngry();
        else if (kb.digit5Key.wasPressedThisFrame) TriggerSad();
        else if (kb.digit6Key.wasPressedThisFrame) TriggerTroll();
        else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) TriggerFaceTaunt();
        else if (kb.gKey.wasPressedThisFrame) FlyToRandomVisionPoint();
    }

    private void PlayRandomClip(AudioClip[] clips)
    {
        if (voiceSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip != null)
            voiceSource.PlayOneShot(clip);
    }

    public void TriggerEmotion(BoltEmotion emotion)
    {
        lastActionTime = Time.time;
        lastReactionTime = Time.time;
        emotionLockUntil = Time.time + triggeredEmotionHoldTime;

        if (bolt != null)
            bolt.SetEmotion(emotion);
    }

    public void TriggerEmotionByName(string emotionName)
    {
        if (string.IsNullOrWhiteSpace(emotionName))
            return;

        string clean = emotionName.Trim();
        if (string.Equals(clean, "Taunt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(clean, "FaceTaunt", StringComparison.OrdinalIgnoreCase))
        {
            TriggerFaceTaunt();
            return;
        }

        if (Enum.TryParse(clean, true, out BoltEmotion emotion))
            TriggerEmotion(emotion);
    }

    [ContextMenu("Emotion/Standard")]
    public void TriggerStandard() => TriggerEmotion(BoltEmotion.Standard);

    [ContextMenu("Emotion/Kind")]
    public void TriggerKind() => TriggerEmotion(BoltEmotion.Kind);

    [ContextMenu("Emotion/Laugh")]
    public void TriggerLaugh() => TriggerEmotion(BoltEmotion.Laugh);

    [ContextMenu("Emotion/Angry")]
    public void TriggerAngry() => TriggerEmotion(BoltEmotion.Angry);

    [ContextMenu("Emotion/Sad")]
    public void TriggerSad() => TriggerEmotion(BoltEmotion.Sad);

    [ContextMenu("Emotion/Troll")]
    public void TriggerTroll() => TriggerEmotion(BoltEmotion.Troll);

    [ContextMenu("Fly/Face Taunt")]
    public void TriggerFaceTaunt()
    {
        lastActionTime = Time.time;
        lastReactionTime = Time.time;
        emotionLockUntil = Time.time + triggeredEmotionHoldTime;
        nextRoamTime = Time.time + UnityEngine.Random.Range(minWaitAtPoint, maxWaitAtPoint);
        outOfViewTimer = 0f;
        glanceUntil = -10f;

        if (tauntClips != null && tauntClips.Length > 0)
            PlayRandomClip(tauntClips);
        else
            PlayRandomClip(teaseClips);

        if (bolt != null)
            bolt.PlayFaceTaunt(playerHead, PickVisionPoint(true));
    }

    [ContextMenu("Fly/Next Random Point")]
    public void FlyToRandomVisionPoint()
    {
        FlyToRandomVisionPoint(true);
    }

    private void FlyToRandomVisionPoint(bool varyEmotion)
    {
        if (bolt == null)
            return;

        nextRoamTime = Time.time + UnityEngine.Random.Range(minWaitAtPoint, maxWaitAtPoint);
        outOfViewTimer = 0f;

        if (varyEmotion && Time.time >= emotionLockUntil && Time.time - lastReactionTime >= reactionCooldown && UnityEngine.Random.value < 0.45f)
        {
            BoltEmotion[] roamEmotions = { BoltEmotion.Standard, BoltEmotion.Kind, BoltEmotion.Troll, BoltEmotion.Laugh };
            bolt.SetEmotion(roamEmotions[UnityEngine.Random.Range(0, roamEmotions.Length)]);
        }

        bolt.FlyTo(PickVisionPoint(false), playerHead);
    }
}
