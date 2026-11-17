using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;

namespace VRPrototype
{
    // Puts the arm model (Resources/Arms.fbx) on both hands and drives its bones from hand tracking.
    // Gets added automatically next to the hand frame source in any scene that has one.
    public class TrackedArms : MonoBehaviour
    {
        public Transform head;
        // shoulder position and elbow direction for the right arm, mirrored for the left
        public Vector3 shoulderOffset = new Vector3(0.18f, -0.22f, -0.06f);
        public Vector3 elbowDirection = new Vector3(0.6f, -1f, -0.5f);
        public float bodyTurnSpeed = 4f;
        public float forearmButtonHeight = 0.026f;
        public float trackingTimeout = 0.5f;

        IHandFrameSource frameSource;
        HandTransformationController controller;
        GameObject model;
        readonly ArmRig[] rigs = new ArmRig[2];
        readonly bool[] armVisible = { true, true };
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        Transform forearmAnchor;
        float nextHandScan;
        float bodyYaw;
        bool hasBodyYaw;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AddToScene();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToScene();

        static void AddToScene()
        {
            var source = FindSource();
            if (source != null && FindAnyObjectByType<TrackedArms>() == null)
                source.gameObject.AddComponent<TrackedArms>();
        }

        static MonoBehaviour FindSource() =>
            (MonoBehaviour)FindAnyObjectByType<XRHandsFrameSource>() ?? FindAnyObjectByType<WebcamHandFrameSource>();

        // used by HideHandOnGrab when the player loses the hand
        public void SetArmVisible(HandSide side, bool visible) => armVisible[(int)side] = visible;

        void Start()
        {
            if (!TryGetComponent(out frameSource))
                frameSource = FindSource() as IHandFrameSource;

            var prefab = Resources.Load<GameObject>("Arms");
            if (prefab == null)
            {
                Debug.LogError("TrackedArms: Resources/Arms.fbx not found");
                enabled = false;
                return;
            }

            model = Instantiate(prefab);
            model.name = "TrackedArms (runtime)";
            rigs[0] = ArmRig.Build(model.transform, HandSide.Left);
            rigs[1] = ArmRig.Build(model.transform, HandSide.Right);

            // BasicScene has two controllers. Pick the one with the real tool models and make the
            // other one follow it so there's only one set of forearm buttons.
            var controllers = FindObjectsByType<HandTransformationController>(FindObjectsSortMode.None);
            controller = Array.Find(controllers, c => c.HasCustomToolVisuals())
                      ?? Array.Find(controllers, c => c.gameObject == gameObject)
                      ?? (controllers.Length > 0 ? controllers[0] : null);
            if (controller == null) return;

            foreach (var other in controllers)
                if (other != controller && other.prostheticSide == controller.prostheticSide)
                    other.FollowController(controller);

            if (rigs[(int)controller.prostheticSide] != null)
            {
                forearmAnchor = new GameObject("ProstheticForearmAnchor").transform;
                forearmAnchor.SetParent(model.transform, false);
                controller.UseTrackedArm(forearmAnchor, forearmButtonHeight);
            }
        }

        void OnDestroy()
        {
            if (model != null) Destroy(model);
            foreach (var r in hiddenRenderers)
                if (r != null) r.forceRenderingOff = false;
        }

        void LateUpdate()
        {
            // the sample hand meshes can spawn late, so keep checking for them
            if (Time.unscaledTime >= nextHandScan)
            {
                nextHandScan = Time.unscaledTime + 1f;
                HideDefaultHands();
            }

            if (head == null && Camera.main != null)
                head = Camera.main.transform;

            Quaternion body = Quaternion.identity;
            if (head != null)
            {
                float yaw = head.eulerAngles.y;
                bodyYaw = hasBodyYaw ? Mathf.LerpAngle(bodyYaw, yaw, 1f - Mathf.Exp(-bodyTurnSpeed * Time.deltaTime)) : yaw;
                hasBodyYaw = true;
                body = Quaternion.Euler(0f, bodyYaw, 0f);
            }

            foreach (var rig in rigs)
                if (rig != null) UpdateArm(rig, body);
        }

        void UpdateArm(ArmRig rig, Quaternion body)
        {
            HandFrame frame = frameSource != null && frameSource.IsRunning ? frameSource.GetFrame(rig.Side) : null;
            bool visible = armVisible[(int)rig.Side] && frame != null && frame.IsTracked
                           && Time.realtimeSinceStartup - frame.SampleTime <= trackingTimeout;

            if (visible)
            {
                bool prosthetic = controller != null && controller.prostheticSide == rig.Side;
                rig.HandHidden = prosthetic && controller.currentTool != ProstheticTool.None;
                rig.FitScaleToHand(frame, Time.deltaTime);

                float mirror = rig.Side == HandSide.Left ? -1f : 1f;
                Vector3 shoulder = Vector3.zero;
                Vector3 pole = Vector3.down;
                if (head != null)
                {
                    shoulder = head.position + body * Vector3.Scale(shoulderOffset, new Vector3(mirror, 1f, 1f));
                    pole = body * Vector3.Scale(elbowDirection, new Vector3(mirror, 1f, 1f));
                }

                visible = rig.Apply(frame, head != null, shoulder, pole);

                if (visible && prosthetic && forearmAnchor != null)
                    forearmAnchor.SetPositionAndRotation(rig.WristPosition, rig.ForearmRotation);
            }

            rig.Renderer.enabled = visible;
        }

        void HideDefaultHands()
        {
            foreach (var driver in FindObjectsByType<XRHandSkeletonDriver>(FindObjectsSortMode.None))
                foreach (var r in driver.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.forceRenderingOff) continue;
                    r.forceRenderingOff = true;
                    hiddenRenderers.Add(r);
                }
        }
    }

    // One arm of the model. Only uses tracked joint positions (not rotations), so it works with any
    // frame source and doesn't care how the model's bones are oriented.
    public class ArmRig
    {
        // metacarpal -> tip for each finger (HandJoint indices)
        static readonly int[][] Chains =
        {
            new[] { 2, 3, 4, 5 },
            new[] { 6, 7, 8, 9, 10 },
            new[] { 11, 12, 13, 14, 15 },
            new[] { 16, 17, 18, 19, 20 },
            new[] { 21, 22, 23, 24, 25 }
        };
        const int W = (int)HandJoint.Wrist, I = (int)HandJoint.IndexProximal,
                  M = (int)HandJoint.MiddleProximal, L = (int)HandJoint.LittleProximal;

        public HandSide Side { get; private set; }
        public SkinnedMeshRenderer Renderer { get; private set; }
        public bool HandHidden;
        public Vector3 WristPosition { get; private set; }
        public Quaternion ForearmRotation { get; private set; }

        Transform upperArm, forearm, armature;
        Vector3 armatureScale;
        readonly Transform[] bones = new Transform[HandFrame.JointCount];
        readonly Vector3[] restPos = new Vector3[HandFrame.JointCount];
        readonly Quaternion[] restRot = new Quaternion[HandFrame.JointCount];
        readonly Vector3[] restDir = new Vector3[HandFrame.JointCount];
        readonly Vector3[] restScale = new Vector3[HandFrame.JointCount];
        Quaternion restRotUpper, restRotFore, palmRestInv, foreRestInv;
        Vector3 restUpperDir;
        float upperLength, foreLength, handLength;
        float scale = 1f;
        bool hasFit;

        public static ArmRig Build(Transform root, HandSide side)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                byName[t.name] = t;

            string prefix = side + "_";
            var rig = new ArmRig { Side = side };
            var missing = new List<string>();

            Transform Find(string name)
            {
                if (!byName.TryGetValue(prefix + name, out var t)) missing.Add(prefix + name);
                return t;
            }

            rig.upperArm = Find("UpperArm");
            rig.forearm = Find("Forearm");
            foreach (var chain in Chains)
                for (int k = 0; k < chain.Length - 1; k++)
                    rig.bones[chain[k]] = Find(((HandJoint)chain[k]).ToString());

            if (missing.Count > 0)
            {
                Debug.LogError("TrackedArms: arm model is missing bones: " + string.Join(", ", missing));
                return null;
            }

            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (Array.IndexOf(smr.bones, rig.forearm) >= 0)
                    rig.Renderer = smr;

            if (rig.Renderer == null)
            {
                Debug.LogError("TrackedArms: no skinned mesh is bound to " + prefix + "Forearm");
                return null;
            }

            var r = rig.Renderer;
            r.updateWhenOffscreen = true;   // bones move far from where the mesh was modelled
            r.quality = SkinQuality.Bone4;  // low quality levels use 1-2 bones and the knuckles look awful
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Cull"))
                r.material.SetFloat("_Cull", 0f); // some faces point inwards, so draw both sides
            r.enabled = false;

            var animator = root.GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;

            rig.CaptureRestPose(root);
            return rig;
        }

        void CaptureRestPose(Transform space)
        {
            Quaternion spaceInv = Quaternion.Inverse(space.rotation);
            armature = upperArm.parent;
            armatureScale = armature.localScale;

            foreach (var chain in Chains)
            {
                int last = chain.Length - 1;
                for (int k = 0; k < last; k++)
                {
                    Transform bone = bones[chain[k]];
                    restPos[chain[k]] = space.InverseTransformPoint(bone.position);
                    restRot[chain[k]] = spaceInv * bone.rotation;
                    restScale[chain[k]] = bone.localScale;
                }

                // fingertip: the leaf bone Blender exports, or a guess if there isn't one
                Transform distal = bones[chain[last - 1]];
                restPos[chain[last]] = distal.childCount > 0
                    ? space.InverseTransformPoint(distal.GetChild(0).position)
                    : restPos[chain[last - 1]] + (restPos[chain[last - 1]] - restPos[chain[last - 2]]) * 0.8f;

                for (int k = 0; k < last; k++)
                    restDir[chain[k]] = (restPos[chain[k + 1]] - restPos[chain[k]]).normalized;
            }

            // all the model's metacarpals start at the wrist
            Vector3 wrist = restPos[(int)HandJoint.MiddleMetacarpal];
            Vector3 shoulder = space.InverseTransformPoint(upperArm.position);
            Vector3 elbow = space.InverseTransformPoint(forearm.position);
            restRotUpper = spaceInv * upperArm.rotation;
            restRotFore = spaceInv * forearm.rotation;
            restUpperDir = (elbow - shoulder).normalized;
            upperLength = Vector3.Distance(shoulder, elbow);
            foreLength = Vector3.Distance(elbow, wrist);

            palmRestInv = Quaternion.Inverse(PalmFrame(wrist, restPos[I], restPos[M], restPos[L]));
            foreRestInv = Quaternion.Inverse(ForearmFrame((wrist - elbow).normalized, restPos[L] - restPos[I]));

            var mid = Chains[2];
            for (int k = 0; k < mid.Length - 1; k++)
                handLength += Vector3.Distance(restPos[mid[k]], restPos[mid[k + 1]]);
        }

        static Quaternion PalmFrame(Vector3 wrist, Vector3 index, Vector3 middle, Vector3 little) =>
            Quaternion.LookRotation(middle - wrist, Vector3.Cross(index - wrist, little - wrist));

        static Quaternion ForearmFrame(Vector3 dir, Vector3 indexToLittle) =>
            Quaternion.LookRotation(dir, Vector3.Cross(dir, indexToLittle));

        // scale the arm so the middle finger is as long as the tracked one
        public void FitScaleToHand(HandFrame frame, float dt)
        {
            if (!frame.Valid[W]) return;

            var mid = Chains[2];
            float length = 0f;
            int prev = W;
            for (int k = 1; k < mid.Length; k++)
            {
                if (!frame.Valid[mid[k]]) return;
                length += Vector3.Distance(frame.Positions[prev], frame.Positions[mid[k]]);
                prev = mid[k];
            }

            float target = Mathf.Clamp(length / handLength, 0.6f, 1.6f);
            scale = hasFit ? Mathf.Lerp(scale, target, 1f - Mathf.Exp(-2f * dt)) : target;
            hasFit = true;
        }

        // Returns false (and leaves the arm alone) if the palm joints aren't tracked.
        public bool Apply(HandFrame frame, bool hasBody, Vector3 shoulder, Vector3 pole)
        {
            if (!frame.Valid[W] || !frame.Valid[I] || !frame.Valid[M] || !frame.Valid[L])
                return false;

            Vector3 wrist = frame.Positions[W];
            Vector3 index = frame.Positions[I];
            Vector3 little = frame.Positions[L];
            Vector3 palmForward = frame.Positions[M] - wrist;
            Vector3 palmNormal = Vector3.Cross(index - wrist, little - wrist);
            if (palmForward.sqrMagnitude < 1e-8f || palmNormal.sqrMagnitude < 1e-12f)
                return false;

            armature.localScale = armatureScale * scale;
            float upperLen = upperLength * scale;
            float foreLen = foreLength * scale;

            // only the wrist is tracked, the elbow and shoulder are guessed
            Vector3 elbow;
            if (hasBody)
            {
                SolveArm(ref shoulder, wrist, upperLen, foreLen, pole, out elbow);
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

            upperArm.SetPositionAndRotation(shoulder, upperDelta * restRotUpper);
            forearm.SetPositionAndRotation(elbow, foreDelta * restRotFore);
            WristPosition = wrist;
            ForearmRotation = foreFrame;

            // fingers: each bone is rotated from where its parent carries it to where tracking says it points
            Quaternion palmDelta = Quaternion.LookRotation(palmForward, palmNormal) * palmRestInv;

            foreach (var chain in Chains)
            {
                Transform metacarpal = bones[chain[0]];
                metacarpal.localScale = restScale[chain[0]] * (HandHidden ? 0.0001f : 1f);
                if (HandHidden)
                {
                    metacarpal.position = wrist;
                    continue;
                }

                Quaternion parentDelta = palmDelta;
                for (int k = 0; k < chain.Length - 1; k++)
                {
                    int joint = chain[k], next = chain[k + 1];

                    // metacarpals stay on the wrist and just aim at the knuckle, otherwise the palm tears apart
                    Vector3 from = k == 0 ? wrist : frame.Positions[joint];
                    Quaternion delta = parentDelta;
                    if ((k == 0 || frame.Valid[joint]) && frame.Valid[next])
                    {
                        Vector3 dir = frame.Positions[next] - from;
                        if (dir.sqrMagnitude > 1e-10f)
                            delta = Quaternion.FromToRotation(parentDelta * restDir[joint], dir.normalized) * parentDelta;
                    }

                    bones[joint].rotation = delta * restRot[joint];
                    if (k == 0) bones[joint].position = wrist;
                    parentDelta = delta;
                }
            }

            return true;
        }

        // Two-bone IK. The wrist is fixed, so if it's out of reach the shoulder slides towards it instead.
        static void SolveArm(ref Vector3 shoulder, Vector3 wrist, float upperLen, float foreLen, Vector3 pole, out Vector3 elbow)
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
