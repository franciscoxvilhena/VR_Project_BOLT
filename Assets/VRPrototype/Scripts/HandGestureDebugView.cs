using TMPro;
using UnityEngine;

namespace VRPrototype
{
    public class HandGestureDebugView : MonoBehaviour
    {
        public HandGestureSystem gestureSystem;
        public bool showMetrics = true;
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
        private Camera mainCamera;

        private class HandVisual
        {
            public GameObject Root;
            public Transform Marker;
            public Renderer MarkerRenderer;
            public TextMeshPro Label;
        }

        private void Awake()
        {
            if (gestureSystem == null)
                gestureSystem = GetComponent<HandGestureSystem>();

            leftVisual = CreateVisual("Left Hand Debug");
            rightVisual = CreateVisual("Right Hand Debug");
        }

        private void OnDisable()
        {
            if (leftVisual?.Root != null) leftVisual.Root.SetActive(false);
            if (rightVisual?.Root != null) rightVisual.Root.SetActive(false);
        }

        private void LateUpdate()
        {
            if (gestureSystem == null)
                return;

            if (mainCamera == null)
                mainCamera = Camera.main;

            UpdateVisual(leftVisual, HandSide.Left);
            UpdateVisual(rightVisual, HandSide.Right);
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
