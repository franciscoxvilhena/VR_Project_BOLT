using UnityEngine;

namespace VRPrototype
{
    public static class HandPoseAnalyzer
    {
        public static bool TryAnalyze(HandFrame frame, HandGestureSettings settings, out HandPoseMetrics metrics)
        {
            metrics = default;
            if (frame == null || !frame.IsTracked)
                return false;

            for (int i = 0; i < HandFrame.JointCount; i++)
            {
                if (i != (int)HandJoint.Palm && !frame.Valid[i])
                    return false;
            }

            metrics.ThumbCurl = Mathf.InverseLerp(settings.ThumbStraightFlexion, settings.ThumbCurledFlexion, GetFingerFlexion(frame, HandFinger.Thumb));
            metrics.IndexCurl = Mathf.InverseLerp(settings.FingerStraightFlexion, settings.FingerCurledFlexion, GetFingerFlexion(frame, HandFinger.Index));
            metrics.MiddleCurl = Mathf.InverseLerp(settings.FingerStraightFlexion, settings.FingerCurledFlexion, GetFingerFlexion(frame, HandFinger.Middle));
            metrics.RingCurl = Mathf.InverseLerp(settings.FingerStraightFlexion, settings.FingerCurledFlexion, GetFingerFlexion(frame, HandFinger.Ring));
            metrics.LittleCurl = Mathf.InverseLerp(settings.FingerStraightFlexion, settings.FingerCurledFlexion, GetFingerFlexion(frame, HandFinger.Little));

            Vector3 wrist = frame.GetPosition(HandJoint.Wrist);
            Vector3 indexKnuckle = frame.GetPosition(HandJoint.IndexProximal);
            Vector3 middleKnuckle = frame.GetPosition(HandJoint.MiddleProximal);
            Vector3 littleKnuckle = frame.GetPosition(HandJoint.LittleProximal);

            Vector3 normal = Vector3.Cross(indexKnuckle - wrist, littleKnuckle - wrist);
            metrics.PalmNormal = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up;
            metrics.PalmPosition = frame.IsValid(HandJoint.Palm)
                ? frame.GetPosition(HandJoint.Palm)
                : (wrist + middleKnuckle) * 0.5f;

            Vector3 indexDir = Vector3.ProjectOnPlane(frame.GetPosition(HandJoint.IndexTip) - indexKnuckle, metrics.PalmNormal);
            Vector3 middleDir = Vector3.ProjectOnPlane(frame.GetPosition(HandJoint.MiddleTip) - middleKnuckle, metrics.PalmNormal);
            metrics.IndexMiddleAngle = Vector3.Angle(indexDir, middleDir);
            return true;
        }

        public static float GetFingerFlexion(HandFrame frame, HandFinger finger)
        {
            int first = (int)GetFirstJoint(finger);
            int count = finger == HandFinger.Thumb ? 4 : 5;

            float total = 0f;
            Vector3 prevBone = frame.GetPosition((HandJoint)(first + 1)) - frame.GetPosition((HandJoint)first);
            for (int i = 2; i < count; i++)
            {
                Vector3 bone = frame.GetPosition((HandJoint)(first + i)) - frame.GetPosition((HandJoint)(first + i - 1));
                total += Vector3.Angle(prevBone, bone);
                prevBone = bone;
            }

            return total;
        }

        public static HandGesture Classify(HandPoseMetrics metrics, HandGestureSettings settings, HandGesture held)
        {
            bool holdingFist = held == HandGesture.Fist;
            if (IsCurled(metrics.IndexCurl, holdingFist, settings) &&
                IsCurled(metrics.MiddleCurl, holdingFist, settings) &&
                IsCurled(metrics.RingCurl, holdingFist, settings) &&
                IsCurled(metrics.LittleCurl, holdingFist, settings))
            {
                return HandGesture.Fist;
            }

            bool holdingScissors = held == HandGesture.Scissors;
            if (IsExtended(metrics.IndexCurl, holdingScissors, settings) &&
                IsExtended(metrics.MiddleCurl, holdingScissors, settings) &&
                IsCurled(metrics.RingCurl, holdingScissors, settings) &&
                IsCurled(metrics.LittleCurl, holdingScissors, settings))
            {
                return HandGesture.Scissors;
            }

            return HandGesture.None;
        }

        private static bool IsCurled(float curl, bool holding, HandGestureSettings settings)
        {
            return curl >= (holding ? settings.CurledExit : settings.CurledEnter);
        }

        private static bool IsExtended(float curl, bool holding, HandGestureSettings settings)
        {
            return curl <= (holding ? settings.ExtendedExit : settings.ExtendedEnter);
        }

        public static HandJoint GetFirstJoint(HandFinger finger)
        {
            switch (finger)
            {
                case HandFinger.Thumb: return HandJoint.ThumbMetacarpal;
                case HandFinger.Index: return HandJoint.IndexMetacarpal;
                case HandFinger.Middle: return HandJoint.MiddleMetacarpal;
                case HandFinger.Ring: return HandJoint.RingMetacarpal;
                default: return HandJoint.LittleMetacarpal;
            }
        }
    }

    public class HandGestureDetector
    {
        public HandSide Side;
        public HandTrackingState State;
        public HandGesture Gesture;
        public HandPoseMetrics Metrics;
        public bool HasMetrics;

        private int lastFrameId;
        private float lastSampleTime;
        private bool hasSample;
        private HandGesture candidate;
        private float candidateSince;

        public HandGestureDetector(HandSide side)
        {
            Side = side;
        }

        public bool UpdateDetector(HandFrame frame, bool sourceRunning, float now, HandGestureSettings settings)
        {
            HandTrackingState prevState = State;
            HandGesture prevGesture = Gesture;

            Step(frame, sourceRunning, now, settings);

            return prevState != State || prevGesture != Gesture;
        }

        public void Reset()
        {
            State = HandTrackingState.NotTracked;
            Gesture = HandGesture.None;
            HasMetrics = false;
            hasSample = false;
            lastFrameId = 0;
            candidate = HandGesture.None;
        }

        private void Step(HandFrame frame, bool sourceRunning, float now, HandGestureSettings settings)
        {
            if (!sourceRunning || frame == null)
            {
                Reset();
                return;
            }

            float timeout = Mathf.Max(settings.TrackingTimeout, 0.5f);

            if (frame.FrameId == 0 || frame.FrameId == lastFrameId)
            {
                if (State != HandTrackingState.NotTracked && (!hasSample || now - lastSampleTime > timeout))
                    Reset();
                return;
            }

            lastFrameId = frame.FrameId;

            if (!HandPoseAnalyzer.TryAnalyze(frame, settings, out HandPoseMetrics newMetrics))
            {
                if (State != HandTrackingState.NotTracked && (!hasSample || now - lastSampleTime > timeout))
                    Reset();
                return;
            }

            Metrics = newMetrics;
            lastSampleTime = frame.SampleTime;
            hasSample = true;
            HasMetrics = true;

            if (State != HandTrackingState.Active)
            {
                State = HandTrackingState.Active;
                candidate = HandGesture.None;
                candidateSince = frame.SampleTime;
            }

            HandGesture raw = HandPoseAnalyzer.Classify(Metrics, settings, Gesture);
            if (raw != candidate)
            {
                candidate = raw;
                candidateSince = frame.SampleTime;
            }

            float heldTime = frame.SampleTime - candidateSince;
            if (candidate != Gesture && heldTime >= settings.GestureHoldTime)
                Gesture = candidate;
        }
    }
}
