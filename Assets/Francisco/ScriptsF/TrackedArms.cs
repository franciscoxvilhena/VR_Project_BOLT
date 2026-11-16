using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;

namespace VRPrototype
{
    /// <summary>
    /// Shows the custom arm model (Assets/Francisco/ModelsF/Resources/Arms.fbx, exported from Arms1.blend by Assets/Francisco/ToolsF/export_arms.py)
    /// on both hands and drives its bones from the hand tracking data of an IHandFrameSource.
    ///
    /// It is added automatically next to the hand frame source in every scene that has one, so no scene
    /// setup is needed. Add the component by hand only if you want to change its settings for a scene.
    /// </summary>
    public class TrackedArms : MonoBehaviour
    {
        /// <summary>Set to false before a scene loads to stop the arms being added automatically.</summary>
        public static bool autoCreate = true;

        [Header("Model")]
        [Tooltip("Leave empty to use Resources/Arms (the FBX exported from Arms1.blend).")]
        public GameObject armsModel;
        [Tooltip("Optional material for the arms. Leave empty to keep the imported one.")]
        public Material armMaterial;
        public bool showLeftArm = true;
        public bool showRightArm = true;

        [Header("Fit")]
        [Tooltip("Resize the arm so the model's hand is as long as the tracked hand.")]
        public bool autoScaleToHand = true;
        [Range(0.5f, 1.5f)] public float extraScale = 1f;
        [Tooltip("Off: fingers only rotate, so the model keeps its own shape (looks best). On: finger bones are also moved onto the tracked joints, so fingertips line up exactly with the real hand but the mesh stretches.")]
        public bool matchJointPositions = false;
        [Tooltip("Draw both sides of every face, so the arm has no holes even if some faces of the model point inwards.")]
        public bool doubleSided = true;

        [Header("Arm (hand tracking has no elbow or shoulder, so these are estimated from the head)")]
        public Transform head;
        [Tooltip("Shoulder position relative to the head, for the right arm (mirrored for the left).")]
        public Vector3 shoulderOffset = new Vector3(0.18f, -0.22f, -0.06f);
        [Tooltip("Direction the elbow points, for the right arm (mirrored for the left).")]
        public Vector3 elbowDirection = new Vector3(0.6f, -1f, -0.5f);
        public float bodyTurnSpeed = 4f;

        [Header("Integration")]
        [Tooltip("Hide the Unity sample hand meshes so only the custom arms are visible.")]
        public bool hideDefaultHandVisuals = true;
        [Tooltip("Collapse the hand of the prosthetic arm while a tool is active, leaving the arm ending at the wrist.")]
        public bool hideHandWhenToolActive = true;
        [Tooltip("Height of the forearm buttons above the forearm bone (the model's skin is about 0.02 above it).")]
        public float forearmButtonHeight = 0.026f;
        public float trackingTimeout = 0.5f;

        private IHandFrameSource frameSource;
        private HandTransformationController controller;
        private GameObject modelInstance;
        private ArmRig leftRig;
        private ArmRig rightRig;
        private Transform forearmAnchor;
        private readonly bool[] sideVisible = { true, true };
        private readonly List<Renderer> hiddenDefaultRenderers = new List<Renderer>();
        private float nextDefaultVisualScan;
        private float bodyYaw;
        private bool hasBodyYaw;

        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hooked = false;
            autoCreate = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => EnsureInScene();
            }
            EnsureInScene();
        }

        private static void EnsureInScene()
        {
            if (!autoCreate || FindAnyObjectByType<TrackedArms>() != null)
                return;

            MonoBehaviour source = FindAnyObjectByType<XRHandsFrameSource>();
            if (source == null)
                source = FindAnyObjectByType<WebcamHandFrameSource>();
            if (source != null)
                source.gameObject.AddComponent<TrackedArms>();
        }

        /// <summary>Show or hide one arm (used e.g. when the player loses the hand).</summary>
        public void SetArmVisible(HandSide side, bool visible)
        {
            sideVisible[(int)side] = visible;
        }

        public ArmRig GetRig(HandSide side)
        {
            return side == HandSide.Left ? leftRig : rightRig;
        }

        private void Start()
        {
            frameSource = GetComponent<IHandFrameSource>();
            if (frameSource == null)
                frameSource = FindAnyObjectByType<XRHandsFrameSource>();
            if (frameSource == null)
                frameSource = FindAnyObjectByType<WebcamHandFrameSource>();

            GameObject model = armsModel != null ? armsModel : Resources.Load<GameObject>("Arms");
            if (model == null)
            {
                Debug.LogError("TrackedArms: no arm model. Expected Assets/Francisco/ModelsF/Resources/Arms.fbx.");
                enabled = false;
                return;
            }

            modelInstance = Instantiate(model, Vector3.zero, Quaternion.identity);
            modelInstance.name = "TrackedArms (runtime)";
            leftRig = BuildRig(HandSide.Left);
            rightRig = BuildRig(HandSide.Right);

            // A scene can contain more than one controller (e.g. BasicScene has two XRHandsSystem objects).
            // Use the one with real tool visuals as the main one; the others follow it, so there is
            // only one set of forearm buttons.
            HandTransformationController[] controllers = FindObjectsByType<HandTransformationController>(FindObjectsSortMode.None);
            controller = Array.Find(controllers, c => c.HasCustomToolVisuals());
            if (controller == null)
                controller = GetComponent<HandTransformationController>();
            if (controller == null && controllers.Length > 0)
                controller = controllers[0];

            foreach (HandTransformationController other in controllers)
            {
                if (other != controller && other.prostheticSide == controller.prostheticSide)
                    other.FollowController(controller);
            }

            if (controller != null && GetRig(controller.prostheticSide) != null)
            {
                forearmAnchor = new GameObject("ProstheticForearmAnchor").transform;
                forearmAnchor.SetParent(modelInstance.transform, false);
                controller.UseTrackedArm(forearmAnchor, forearmButtonHeight);
            }
        }

        private ArmRig BuildRig(HandSide side)
        {
            ArmRig rig = ArmRig.Build(modelInstance.transform, side, out string error);
            if (rig == null)
            {
                Debug.LogError("TrackedArms: " + error);
                return null;
            }

            if (armMaterial != null)
                rig.Renderer.sharedMaterial = armMaterial;
            if (doubleSided && rig.Renderer.sharedMaterial != null && rig.Renderer.sharedMaterial.HasProperty("_Cull"))
                rig.Renderer.material.SetFloat("_Cull", 0f);
            rig.Renderer.enabled = false;
            return rig;
        }

        private void OnDestroy()
        {
            if (modelInstance != null)
                Destroy(modelInstance);

            for (int i = 0; i < hiddenDefaultRenderers.Count; i++)
            {
                if (hiddenDefaultRenderers[i] != null)
                    hiddenDefaultRenderers[i].forceRenderingOff = false;
            }
        }

        private void LateUpdate()
        {
            if (hideDefaultHandVisuals && Time.unscaledTime >= nextDefaultVisualScan)
            {
                nextDefaultVisualScan = Time.unscaledTime + 1f;
                HideDefaultHandVisuals();
            }

            if (head == null && Camera.main != null)
                head = Camera.main.transform;

            Quaternion bodyRotation = Quaternion.identity;
            if (head != null)
            {
                float headYaw = head.eulerAngles.y;
                bodyYaw = hasBodyYaw ? Mathf.LerpAngle(bodyYaw, headYaw, 1f - Mathf.Exp(-bodyTurnSpeed * Time.deltaTime)) : headYaw;
                hasBodyYaw = true;
                bodyRotation = Quaternion.Euler(0f, bodyYaw, 0f);
            }

            UpdateArm(leftRig, showLeftArm, bodyRotation);
            UpdateArm(rightRig, showRightArm, bodyRotation);
        }

        private void UpdateArm(ArmRig rig, bool show, Quaternion bodyRotation)
        {
            if (rig == null)
                return;

            bool running = frameSource != null && frameSource.IsRunning;
            HandFrame frame = running ? frameSource.GetFrame(rig.Side) : null;
            bool tracked = frame != null && frame.IsTracked && Time.realtimeSinceStartup - frame.SampleTime <= trackingTimeout;
            bool visible = show && sideVisible[(int)rig.Side] && tracked;

            if (visible)
            {
                if (autoScaleToHand)
                    rig.FitScaleToHand(frame, Time.deltaTime);
                rig.ExtraScale = extraScale;

                bool isProsthetic = controller != null && controller.prostheticSide == rig.Side;
                rig.HandHidden = hideHandWhenToolActive && isProsthetic && controller.currentTool != ProstheticTool.None;

                float sideSign = rig.Side == HandSide.Left ? -1f : 1f;
                Vector3 shoulder = Vector3.zero;
                Vector3 pole = Vector3.down;
                if (head != null)
                {
                    shoulder = head.position + bodyRotation * new Vector3(sideSign * shoulderOffset.x, shoulderOffset.y, shoulderOffset.z);
                    pole = bodyRotation * new Vector3(sideSign * elbowDirection.x, elbowDirection.y, elbowDirection.z);
                }

                visible = rig.Apply(frame, head != null, shoulder, pole, matchJointPositions);

                if (visible && isProsthetic && forearmAnchor != null)
                    forearmAnchor.SetPositionAndRotation(rig.WristPosition, rig.ForearmRotation);
            }

            if (rig.Renderer.enabled != visible)
                rig.Renderer.enabled = visible;
        }

        private void HideDefaultHandVisuals()
        {
            XRHandSkeletonDriver[] drivers = FindObjectsByType<XRHandSkeletonDriver>(FindObjectsSortMode.None);
            for (int i = 0; i < drivers.Length; i++)
            {
                Renderer[] renderers = drivers[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r].forceRenderingOff)
                        continue;
                    renderers[r].forceRenderingOff = true;
                    hiddenDefaultRenderers.Add(renderers[r]);
                }
            }
        }
    }

    /// <summary>
    /// One arm of the model. Poses its bones from tracked joint positions only (no joint rotations needed),
    /// so it works with any IHandFrameSource and does not depend on how the model's bones are oriented.
    /// </summary>
    public class ArmRig
    {
        // Joint chains per finger: metacarpal ... tip (indices into HandJoint).
        private static readonly int[][] Chains =
        {
            new[] { 2, 3, 4, 5 },
            new[] { 6, 7, 8, 9, 10 },
            new[] { 11, 12, 13, 14, 15 },
            new[] { 16, 17, 18, 19, 20 },
            new[] { 21, 22, 23, 24, 25 }
        };

        private const float CollapsedScale = 0.0001f;

        public HandSide Side { get; private set; }
        public SkinnedMeshRenderer Renderer { get; private set; }
        public Transform UpperArm { get; private set; }
        public Transform Forearm { get; private set; }
        public float ExtraScale = 1f;
        public bool HandHidden;

        /// <summary>Tracked wrist position from the last Apply.</summary>
        public Vector3 WristPosition { get; private set; }
        /// <summary>Forward runs along the forearm to the wrist, up is the same side of the arm as the wrist anchor's up.</summary>
        public Quaternion ForearmRotation { get; private set; }
        public Vector3 ElbowPosition { get; private set; }
        public Vector3 ShoulderPosition { get; private set; }
        public float Scale => fitScale * ExtraScale;

        private Transform armature;
        private Vector3 armatureBaseScale;
        private readonly Transform[] bones = new Transform[HandFrame.JointCount];
        private readonly Vector3[] restPos = new Vector3[HandFrame.JointCount];
        private readonly Quaternion[] restRot = new Quaternion[HandFrame.JointCount];
        private readonly Vector3[] restDir = new Vector3[HandFrame.JointCount];
        private readonly Vector3[] restLocalScale = new Vector3[HandFrame.JointCount];
        private Quaternion restRotUpper;
        private Quaternion restRotFore;
        private Vector3 restUpperDir;
        private Quaternion palmRestInv;
        private Quaternion foreRestInv;
        private float upperLength;
        private float foreLength;
        private float handRestLength;
        private float fitScale = 1f;
        private bool hasFit;

        public Transform GetBone(HandJoint joint) => bones[(int)joint];
        public Vector3 GetRestPosition(HandJoint joint) => restPos[(int)joint];
        public Vector3 RestElbow { get; private set; }
        public Vector3 RestShoulder { get; private set; }
        public float UpperArmLength => upperLength * Scale;
        public float ForearmLength => foreLength * Scale;

        public static ArmRig Build(Transform modelRoot, HandSide side, out string error)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in modelRoot.GetComponentsInChildren<Transform>(true))
                byName[t.name] = t;

            string prefix = side == HandSide.Left ? "Left_" : "Right_";
            var rig = new ArmRig { Side = side };
            var missing = new List<string>();

            Transform Find(string boneName)
            {
                if (byName.TryGetValue(prefix + boneName, out Transform t))
                    return t;
                missing.Add(prefix + boneName);
                return null;
            }

            rig.UpperArm = Find("UpperArm");
            rig.Forearm = Find("Forearm");
            foreach (int[] chain in Chains)
            {
                for (int k = 0; k < chain.Length - 1; k++)
                    rig.bones[chain[k]] = Find(((HandJoint)chain[k]).ToString());
            }

            if (missing.Count > 0)
            {
                error = "arm model is missing bones: " + string.Join(", ", missing);
                return null;
            }

            foreach (SkinnedMeshRenderer smr in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (Array.IndexOf(smr.bones, rig.Forearm) >= 0)
                    rig.Renderer = smr;
            }

            if (rig.Renderer == null)
            {
                error = "no skinned mesh is bound to " + prefix + "Forearm";
                return null;
            }

            // The bones are moved far from where the mesh was modelled, so keep its bounds up to date.
            rig.Renderer.updateWhenOffscreen = true;
            // Always blend 4 bones per vertex. The project's lower quality levels use 1 or 2, which makes
            // the fingers and knuckles deform harshly.
            rig.Renderer.quality = SkinQuality.Bone4;

            Animator animator = modelRoot.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.enabled = false;

            rig.CaptureRestPose(modelRoot);
            error = null;
            return rig;
        }

        private void CaptureRestPose(Transform space)
        {
            Quaternion spaceInv = Quaternion.Inverse(space.rotation);
            armature = UpperArm.parent;
            armatureBaseScale = armature.localScale;

            foreach (int[] chain in Chains)
            {
                int last = chain.Length - 1;
                for (int k = 0; k < last; k++)
                {
                    Transform bone = bones[chain[k]];
                    restPos[chain[k]] = space.InverseTransformPoint(bone.position);
                    restRot[chain[k]] = spaceInv * bone.rotation;
                    restLocalScale[chain[k]] = bone.localScale;
                }

                // Fingertip: the leaf bone the FBX exporter adds, or an estimate if it is missing.
                Transform distal = bones[chain[last - 1]];
                if (distal.childCount > 0)
                    restPos[chain[last]] = space.InverseTransformPoint(distal.GetChild(0).position);
                else
                    restPos[chain[last]] = restPos[chain[last - 1]] + (restPos[chain[last - 1]] - restPos[chain[last - 2]]) * 0.8f;

                for (int k = 0; k < last; k++)
                    restDir[chain[k]] = (restPos[chain[k + 1]] - restPos[chain[k]]).normalized;
            }

            Vector3 wrist = restPos[(int)HandJoint.MiddleMetacarpal];
            restPos[(int)HandJoint.Wrist] = wrist;
            Vector3 index = restPos[(int)HandJoint.IndexProximal];
            Vector3 middle = restPos[(int)HandJoint.MiddleProximal];
            Vector3 little = restPos[(int)HandJoint.LittleProximal];
            restPos[(int)HandJoint.Palm] = (wrist + middle) * 0.5f;

            RestShoulder = space.InverseTransformPoint(UpperArm.position);
            RestElbow = space.InverseTransformPoint(Forearm.position);
            restRotUpper = spaceInv * UpperArm.rotation;
            restRotFore = spaceInv * Forearm.rotation;
            restUpperDir = (RestElbow - RestShoulder).normalized;
            upperLength = Vector3.Distance(RestShoulder, RestElbow);
            foreLength = Vector3.Distance(RestElbow, wrist);

            palmRestInv = Quaternion.Inverse(PalmFrame(wrist, index, middle, little));
            foreRestInv = Quaternion.Inverse(ForearmFrame((wrist - RestElbow).normalized, little - index));

            int[] mid = Chains[2];
            handRestLength = 0f;
            for (int k = 0; k < mid.Length - 1; k++)
                handRestLength += Vector3.Distance(restPos[mid[k]], restPos[mid[k + 1]]);
        }

        private static Quaternion PalmFrame(Vector3 wrist, Vector3 index, Vector3 middle, Vector3 little)
        {
            return Quaternion.LookRotation(middle - wrist, Vector3.Cross(index - wrist, little - wrist));
        }

        private static Quaternion ForearmFrame(Vector3 direction, Vector3 indexToLittle)
        {
            return Quaternion.LookRotation(direction, Vector3.Cross(direction, indexToLittle));
        }

        /// <summary>Resizes the arm so the middle finger chain is as long as the tracked one.</summary>
        public void FitScaleToHand(HandFrame frame, float deltaTime)
        {
            int[] mid = Chains[2];
            if (!frame.Valid[(int)HandJoint.Wrist])
                return;

            float length = Vector3.Distance(frame.Positions[(int)HandJoint.Wrist], frame.Positions[mid[1]]);
            for (int k = 1; k < mid.Length; k++)
            {
                if (!frame.Valid[mid[k]])
                    return;
                if (k > 1)
                    length += Vector3.Distance(frame.Positions[mid[k - 1]], frame.Positions[mid[k]]);
            }

            float restLength = handRestLength - Vector3.Distance(restPos[mid[0]], restPos[mid[1]]) + Vector3.Distance(restPos[(int)HandJoint.Wrist], restPos[mid[1]]);
            float target = Mathf.Clamp(length / restLength, 0.6f, 1.6f);
            fitScale = hasFit ? Mathf.Lerp(fitScale, target, 1f - Mathf.Exp(-2f * deltaTime)) : target;
            hasFit = true;
        }

        /// <summary>
        /// Poses the arm from a tracked hand frame. Returns false (and leaves the arm as it was) when the
        /// joints needed to place the palm are missing.
        /// </summary>
        public bool Apply(HandFrame frame, bool hasBody, Vector3 shoulder, Vector3 elbowPole, bool matchJointPositions)
        {
            const int W = (int)HandJoint.Wrist, I = (int)HandJoint.IndexProximal, M = (int)HandJoint.MiddleProximal, L = (int)HandJoint.LittleProximal;
            if (!frame.Valid[W] || !frame.Valid[I] || !frame.Valid[M] || !frame.Valid[L])
                return false;

            Vector3 wrist = frame.Positions[W];
            Vector3 index = frame.Positions[I];
            Vector3 middle = frame.Positions[M];
            Vector3 little = frame.Positions[L];
            Vector3 palmForward = middle - wrist;
            Vector3 palmNormal = Vector3.Cross(index - wrist, little - wrist);
            if (palmForward.sqrMagnitude < 1e-8f || palmNormal.sqrMagnitude < 1e-12f)
                return false;

            float scale = Scale;
            armature.localScale = armatureBaseScale * scale;
            float upperLen = upperLength * scale;
            float foreLen = foreLength * scale;

            // Arm: the wrist is tracked; the elbow and shoulder are estimated.
            Vector3 elbow;
            if (hasBody)
            {
                SolveArm(ref shoulder, wrist, upperLen, foreLen, elbowPole, out elbow);
            }
            else
            {
                Vector3 back = -palmForward.normalized;
                elbow = wrist + back * foreLen;
                shoulder = elbow + back * upperLen;
            }

            Vector3 foreDir = (wrist - elbow).normalized;
            Vector3 lateral = little - index;
            if (Vector3.Cross(foreDir, lateral).sqrMagnitude < 1e-10f)
                lateral = Vector3.Cross(palmNormal, foreDir);

            Quaternion foreFrame = ForearmFrame(foreDir, lateral);
            Quaternion foreDelta = foreFrame * foreRestInv;
            Quaternion upperDelta = Quaternion.FromToRotation(foreDelta * restUpperDir, (elbow - shoulder).normalized) * foreDelta;

            UpperArm.SetPositionAndRotation(shoulder, upperDelta * restRotUpper);
            Forearm.SetPositionAndRotation(elbow, foreDelta * restRotFore);

            WristPosition = wrist;
            ForearmRotation = foreFrame;
            ElbowPosition = elbow;
            ShoulderPosition = shoulder;

            // Hand: every bone swings from where its parent would carry it to where tracking says it points.
            Quaternion palmDelta = Quaternion.LookRotation(palmForward, palmNormal) * palmRestInv;

            foreach (int[] chain in Chains)
            {
                Transform metacarpal = bones[chain[0]];
                if (HandHidden)
                {
                    metacarpal.localScale = restLocalScale[chain[0]] * CollapsedScale;
                    metacarpal.position = wrist;
                    continue;
                }

                metacarpal.localScale = restLocalScale[chain[0]];

                Quaternion parentDelta = palmDelta;
                for (int k = 0; k < chain.Length - 1; k++)
                {
                    int joint = chain[k];
                    int next = chain[k + 1];
                    Transform bone = bones[joint];

                    // The model's finger roots (metacarpals) all start at the wrist, so they stay there and
                    // aim at the tracked knuckle. Moving them onto the tracked metacarpal joints, which are
                    // spread across the palm, would tear the palm apart.
                    bool hasPosition = k == 0 || frame.Valid[joint];
                    Vector3 position = k == 0 ? wrist : (frame.Valid[joint] ? frame.Positions[joint] : bone.position);

                    Quaternion delta = parentDelta;
                    if (hasPosition && frame.Valid[next])
                    {
                        Vector3 direction = frame.Positions[next] - position;
                        if (direction.sqrMagnitude > 1e-10f)
                            delta = Quaternion.FromToRotation(parentDelta * restDir[joint], direction.normalized) * parentDelta;
                    }

                    bone.rotation = delta * restRot[joint];
                    if (k == 0 || (matchJointPositions && hasPosition))
                        bone.position = position;

                    parentDelta = delta;
                }
            }

            return true;
        }

        /// <summary>
        /// Two-bone IK. The wrist is fixed (it is tracked); if it is out of the arm's reach the shoulder
        /// slides towards it so the hand never detaches from the arm.
        /// </summary>
        private static void SolveArm(ref Vector3 shoulder, Vector3 wrist, float upperLen, float foreLen, Vector3 pole, out Vector3 elbow)
        {
            Vector3 toWrist = wrist - shoulder;
            float distance = toWrist.magnitude;
            Vector3 axis = distance > 1e-5f ? toWrist / distance : Vector3.forward;

            float reach = Mathf.Clamp(distance, Mathf.Abs(upperLen - foreLen) + 0.01f, (upperLen + foreLen) * 0.995f);
            shoulder = wrist - axis * reach;

            float along = (upperLen * upperLen - foreLen * foreLen + reach * reach) / (2f * reach);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLen * upperLen - along * along));

            Vector3 bend = pole - axis * Vector3.Dot(pole, axis);
            if (bend.sqrMagnitude < 1e-8f)
                bend = Vector3.Cross(axis, Vector3.right);

            elbow = shoulder + axis * along + bend.normalized * height;
        }
    }
}
