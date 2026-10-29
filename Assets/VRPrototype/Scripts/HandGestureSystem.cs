using UnityEngine;
using UnityEngine.Events;

namespace VRPrototype
{
    public class HandGestureSystem : MonoBehaviour
    {
        public HandGestureSettings settings = new HandGestureSettings();
        public UnityEvent<HandSide, HandGesture> onGestureChanged = new UnityEvent<HandSide, HandGesture>();
        public bool logChanges;

        private HandGestureDetector leftHand = new HandGestureDetector(HandSide.Left);
        private HandGestureDetector rightHand = new HandGestureDetector(HandSide.Right);
        private IHandFrameSource frameSource;

        public HandGesture GetGesture(HandSide side)
        {
            return side == HandSide.Left ? leftHand.Gesture : rightHand.Gesture;
        }

        public HandTrackingState GetState(HandSide side)
        {
            return side == HandSide.Left ? leftHand.State : rightHand.State;
        }

        public bool TryGetMetrics(HandSide side, out HandPoseMetrics metrics)
        {
            HandGestureDetector detector = side == HandSide.Left ? leftHand : rightHand;
            metrics = detector.Metrics;
            return detector.HasMetrics;
        }

        private void Awake()
        {
            frameSource = GetComponent<IHandFrameSource>();
        }

        private void OnDisable()
        {
            leftHand.Reset();
            rightHand.Reset();
        }

        private void Update()
        {
            if (frameSource == null)
                frameSource = GetComponent<IHandFrameSource>();

            float now = Time.realtimeSinceStartup;
            bool running = frameSource != null && frameSource.IsRunning;

            UpdateHand(leftHand, running ? frameSource.GetFrame(HandSide.Left) : null, running, now);
            UpdateHand(rightHand, running ? frameSource.GetFrame(HandSide.Right) : null, running, now);
        }

        private void UpdateHand(HandGestureDetector detector, HandFrame frame, bool running, float now)
        {
            HandGesture prevGesture = detector.Gesture;
            if (!detector.UpdateDetector(frame, running, now, settings))
                return;

            if (logChanges)
                Debug.Log($"{detector.Side}: {prevGesture} -> {detector.Gesture} ({detector.State})");

            if (prevGesture != detector.Gesture)
                onGestureChanged.Invoke(detector.Side, detector.Gesture);
        }
    }
}
