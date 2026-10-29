using TMPro;
using UnityEngine;

namespace VRPrototype
{
    public class HandGestureDebugView : MonoBehaviour
    {
        public HandGestureSystem gestureSystem;
        public bool showMetrics = true;
        public bool drawLeftHandSkeleton = true;
        public bool drawRightHandSkeleton = false;
        public float jointSphereRadius = 0.006f;
        public float boneThickness = 0.006f;
        public Color skeletonJointColor = new Color(0.2f, 0.85f, 1f);
        public Color skeletonBoneColor = new Color(0.85f, 0.92f, 1f);

        public float markerSize = 0.025f;
        public Vector3 markerOffset = new Vector3(0f, 0.06f, 0f);
        public Vector3 labelOffset = new Vector3(0f, 0.11f, 0f);
        public float labelFontSize = 0.12f;

        public Color noneColor = new Color(0.9f, 0.9f, 0.9f);
        public Color fistColor = new Color32(0x00, 0xBC, 0xD4, 0xFF);
        public Color scissorsColor = new Color32(0xE9, 0x1E, 0x63, 0xFF);
        public Color knifeColor = new Color32(0x8B, 0xC3, 0x4A, 0xFF);
        public Color torchColor = new Color32(0xFF, 0x98, 0x00, 0xFF);
        public Color rearmColor = new Color(0.35f, 0.35f, 0.35f);

        private HandVisual leftVisual;
        private HandVisual rightVisual;
        private HandSkeletonVisual leftSkeleton;
        private HandSkeletonVisual rightSkeleton;
        private IHandFrameSource frameSource;
        private Camera mainCamera;

        private static readonly int[,] BonePairs = new int[,]
        {
            { (int)HandJoint.Wrist, (int)HandJoint.Palm },
            { (int)HandJoint.Wrist, (int)HandJoint.ThumbMetacarpal },
            { (int)HandJoint.ThumbMetacarpal, (int)HandJoint.ThumbProximal },
            { (int)HandJoint.ThumbProximal, (int)HandJoint.ThumbDistal },
            { (int)HandJoint.ThumbDistal, (int)HandJoint.ThumbTip },

            { (int)HandJoint.Wrist, (int)HandJoint.IndexMetacarpal },
            { (int)HandJoint.IndexMetacarpal, (int)HandJoint.IndexProximal },
            { (int)HandJoint.IndexProximal, (int)HandJoint.IndexIntermediate },
            { (int)HandJoint.IndexIntermediate, (int)HandJoint.IndexDistal },
            { (int)HandJoint.IndexDistal, (int)HandJoint.IndexTip },

            { (int)HandJoint.Wrist, (int)HandJoint.MiddleMetacarpal },
            { (int)HandJoint.MiddleMetacarpal, (int)HandJoint.MiddleProximal },
            { (int)HandJoint.MiddleProximal, (int)HandJoint.MiddleIntermediate },
            { (int)HandJoint.MiddleIntermediate, (int)HandJoint.MiddleDistal },
            { (int)HandJoint.MiddleDistal, (int)HandJoint.MiddleTip },

            { (int)HandJoint.Wrist, (int)HandJoint.RingMetacarpal },
            { (int)HandJoint.RingMetacarpal, (int)HandJoint.RingProximal },
            { (int)HandJoint.RingProximal, (int)HandJoint.RingIntermediate },
            { (int)HandJoint.RingIntermediate, (int)HandJoint.RingDistal },
            { (int)HandJoint.RingDistal, (int)HandJoint.RingTip },

            { (int)HandJoint.Wrist, (int)HandJoint.LittleMetacarpal },
            { (int)HandJoint.LittleMetacarpal, (int)HandJoint.LittleProximal },
            { (int)HandJoint.LittleProximal, (int)HandJoint.LittleIntermediate },
            { (int)HandJoint.LittleIntermediate, (int)HandJoint.LittleDistal },
            { (int)HandJoint.LittleDistal, (int)HandJoint.LittleTip },

            { (int)HandJoint.IndexProximal, (int)HandJoint.MiddleProximal },
            { (int)HandJoint.MiddleProximal, (int)HandJoint.RingProximal },
            { (int)HandJoint.RingProximal, (int)HandJoint.LittleProximal }
        };

        private class HandVisual
        {
            public GameObject Root;
            public Transform Marker;
            public Renderer MarkerRenderer;
            public TextMeshPro Label;
        }

        private class HandSkeletonVisual
        {
            public GameObject Root;
            public Transform[] Joints = new Transform[HandFrame.JointCount];
            public Transform[] Bones = new Transform[BonePairs.GetLength(0)];
        }

        private void Awake()
        {
            if (gestureSystem == null)
                gestureSystem = GetComponent<HandGestureSystem>();

            frameSource = GetComponent<IHandFrameSource>();
            if (frameSource == null)
                frameSource = FindAnyObjectByType<XRHandsFrameSource>();
            if (frameSource == null)
                frameSource = FindAnyObjectByType<WebcamHandFrameSource>();

            leftVisual = CreateVisual("Left Hand Debug");
            rightVisual = CreateVisual("Right Hand Debug");
            leftSkeleton = CreateSkeleton("Left Hand Skeleton");
            rightSkeleton = CreateSkeleton("Right Hand Skeleton");
        }

        private void OnDisable()
        {
            if (leftVisual?.Root != null) leftVisual.Root.SetActive(false);
            if (rightVisual?.Root != null) rightVisual.Root.SetActive(false);
            if (leftSkeleton?.Root != null) leftSkeleton.Root.SetActive(false);
            if (rightSkeleton?.Root != null) rightSkeleton.Root.SetActive(false);
        }

        private void LateUpdate()
        {
            if (gestureSystem == null)
                return;

            if (mainCamera == null)
                mainCamera = Camera.main;

            UpdateVisual(leftVisual, HandSide.Left);
            UpdateVisual(rightVisual, HandSide.Right);
            UpdateSkeleton(leftSkeleton, HandSide.Left, drawLeftHandSkeleton);
            UpdateSkeleton(rightSkeleton, HandSide.Right, drawRightHandSkeleton);
        }

        private HandVisual CreateVisual(string name)
        {
            var vis = new HandVisual();
            vis.Root = new GameObject(name);
            vis.Root.transform.SetParent(transform, false);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Marker";
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(vis.Root.transform, false);
            sphere.transform.localScale = Vector3.one * markerSize;
            vis.Marker = sphere.transform;
            vis.MarkerRenderer = sphere.GetComponent<Renderer>();

            var textObj = new GameObject("Label");
            textObj.transform.SetParent(vis.Root.transform, false);
            vis.Label = textObj.AddComponent<TextMeshPro>();
            vis.Label.fontSize = labelFontSize;
            vis.Label.alignment = TextAlignmentOptions.Center;
            vis.Label.rectTransform.sizeDelta = new Vector2(0.5f, 0.2f);
            vis.Label.color = Color.white;
            vis.Label.outlineWidth = 0.2f;
            vis.Label.outlineColor = new Color32(0, 0, 0, 200);

            vis.Root.SetActive(false);
            return vis;
        }

        private HandSkeletonVisual CreateSkeleton(string name)
        {
            var skel = new HandSkeletonVisual();
            skel.Root = new GameObject(name);
            skel.Root.transform.SetParent(transform, false);

            for (int i = 0; i < HandFrame.JointCount; i++)
            {
                GameObject jointObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                jointObj.name = ((HandJoint)i).ToString();
                Destroy(jointObj.GetComponent<Collider>());
                jointObj.transform.SetParent(skel.Root.transform, false);
                float r = i == (int)HandJoint.IndexTip ? jointSphereRadius * 1.4f : jointSphereRadius;
                jointObj.transform.localScale = Vector3.one * (r * 2f);

                Renderer rend = jointObj.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = i == (int)HandJoint.IndexTip ? new Color(1f, 0.3f, 0.5f) : skeletonJointColor;

                skel.Joints[i] = jointObj.transform;
            }

            int boneCount = BonePairs.GetLength(0);
            for (int b = 0; b < boneCount; b++)
            {
                GameObject boneObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                boneObj.name = "Bone_" + b;
                Destroy(boneObj.GetComponent<Collider>());
                boneObj.transform.SetParent(skel.Root.transform, false);

                Renderer rend = boneObj.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = skeletonBoneColor;

                skel.Bones[b] = boneObj.transform;
            }

            skel.Root.SetActive(false);
            return skel;
        }

        private void UpdateSkeleton(HandSkeletonVisual skel, HandSide side, bool shouldDraw)
        {
            if (!shouldDraw || frameSource == null || !frameSource.IsRunning)
            {
                if (skel.Root.activeSelf)
                    skel.Root.SetActive(false);
                return;
            }

            HandFrame frame = frameSource.GetFrame(side);
            if (frame == null || !frame.IsTracked)
            {
                if (skel.Root.activeSelf)
                    skel.Root.SetActive(false);
                return;
            }

            if (!skel.Root.activeSelf)
                skel.Root.SetActive(true);

            for (int i = 0; i < HandFrame.JointCount; i++)
            {
                bool valid = frame.Valid[i];
                if (skel.Joints[i].gameObject.activeSelf != valid)
                    skel.Joints[i].gameObject.SetActive(valid);

                if (valid)
                {
                    skel.Joints[i].position = frame.Positions[i];
                    skel.Joints[i].rotation = frame.Rotations[i];
                }
            }

            int boneCount = BonePairs.GetLength(0);
            for (int b = 0; b < boneCount; b++)
            {
                int jA = BonePairs[b, 0];
                int jB = BonePairs[b, 1];
                bool valid = frame.Valid[jA] && frame.Valid[jB];

                if (skel.Bones[b].gameObject.activeSelf != valid)
                    skel.Bones[b].gameObject.SetActive(valid);

                if (valid)
                {
                    Vector3 posA = frame.Positions[jA];
                    Vector3 posB = frame.Positions[jB];
                    Vector3 diff = posB - posA;
                    float len = diff.magnitude;

                    skel.Bones[b].position = (posA + posB) * 0.5f;
                    if (len > 0.0001f)
                        skel.Bones[b].up = diff / len;
                    skel.Bones[b].localScale = new Vector3(boneThickness, len * 0.5f, boneThickness);
                }
            }
        }

        private void UpdateVisual(HandVisual vis, HandSide side)
        {
            HandTrackingState state = gestureSystem.GetState(side);
            if (state == HandTrackingState.NotTracked || !gestureSystem.TryGetMetrics(side, out HandPoseMetrics m))
            {
                if (vis.Root.activeSelf)
                    vis.Root.SetActive(false);
                return;
            }

            if (!vis.Root.activeSelf)
                vis.Root.SetActive(true);

            HandGesture gesture = gestureSystem.GetGesture(side);

            vis.Marker.position = m.PalmPosition + markerOffset;
            vis.Label.transform.position = m.PalmPosition + labelOffset;
            if (mainCamera != null)
            {
                vis.Label.transform.rotation = Quaternion.LookRotation(
                    vis.Label.transform.position - mainCamera.transform.position,
                    Vector3.up);
            }

            vis.MarkerRenderer.material.color = GetColor(gesture, state);

            string title = GetTitle(gesture, state);
            if (!showMetrics)
            {
                vis.Label.text = title;
            }
            else
            {
                vis.Label.text = $"{title}\n<size=70%>I {m.IndexCurl:F2}  M {m.MiddleCurl:F2}  R {m.RingCurl:F2}  L {m.LittleCurl:F2}\nT {m.ThumbCurl:F2}  V {m.IndexMiddleAngle:F0}°  S {m.FourFingerSpread:F0}°</size>";
            }
        }

        private Color GetColor(HandGesture gesture, HandTrackingState state)
        {
            if (state != HandTrackingState.Active)
                return rearmColor;

            if (gesture == HandGesture.Fist) return fistColor;
            if (gesture == HandGesture.Scissors) return scissorsColor;
            if (gesture == HandGesture.Knife) return knifeColor;
            if (gesture == HandGesture.Torch) return torchColor;
            return noneColor;
        }

        private string GetTitle(HandGesture gesture, HandTrackingState state)
        {
            if (state != HandTrackingState.Active)
                return "OPEN HAND TO REARM";

            if (gesture == HandGesture.Fist) return "FIST";
            if (gesture == HandGesture.Scissors) return "SCISSORS";
            if (gesture == HandGesture.Knife) return "KNIFE";
            if (gesture == HandGesture.Torch) return "TORCH";
            return "-";
        }
    }
}
