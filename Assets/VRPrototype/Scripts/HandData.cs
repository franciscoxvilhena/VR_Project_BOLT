using System;
using UnityEngine;

namespace VRPrototype
{
    public enum HandSide
    {
        Left = 0,
        Right = 1
    }

    public enum HandGesture
    {
        None = 0,
        Fist = 1,
        Scissors = 2,
        Knife = 3,
        Torch = 4
    }

    public enum HandTrackingState
    {
        NotTracked = 0,
        AwaitingRearm = 1,
        Active = 2
    }

    public enum HandJoint
    {
        Wrist = 0,
        Palm = 1,

        ThumbMetacarpal = 2,
        ThumbProximal = 3,
        ThumbDistal = 4,
        ThumbTip = 5,

        IndexMetacarpal = 6,
        IndexProximal = 7,
        IndexIntermediate = 8,
        IndexDistal = 9,
        IndexTip = 10,

        MiddleMetacarpal = 11,
        MiddleProximal = 12,
        MiddleIntermediate = 13,
        MiddleDistal = 14,
        MiddleTip = 15,

        RingMetacarpal = 16,
        RingProximal = 17,
        RingIntermediate = 18,
        RingDistal = 19,
        RingTip = 20,

        LittleMetacarpal = 21,
        LittleProximal = 22,
        LittleIntermediate = 23,
        LittleDistal = 24,
        LittleTip = 25
    }

    public enum HandFinger
    {
        Thumb = 0,
        Index = 1,
        Middle = 2,
        Ring = 3,
        Little = 4
    }

    public struct HandPoseMetrics
    {
        public float ThumbCurl;
        public float IndexCurl;
        public float MiddleCurl;
        public float RingCurl;
        public float LittleCurl;
        public float IndexMiddleAngle;
        public float FourFingerSpread;
        public Vector3 PalmPosition;
        public Vector3 PalmNormal;

        public float GetCurl(HandFinger finger)
        {
            switch (finger)
            {
                case HandFinger.Thumb: return ThumbCurl;
                case HandFinger.Index: return IndexCurl;
                case HandFinger.Middle: return MiddleCurl;
                case HandFinger.Ring: return RingCurl;
                default: return LittleCurl;
            }
        }
    }

    [Serializable]
    public class HandGestureSettings
    {
        public float FingerStraightFlexion = 35f;
        public float FingerCurledFlexion = 220f;
        public float ThumbStraightFlexion = 15f;
        public float ThumbCurledFlexion = 110f;

        [Range(0f, 1f)] public float CurledEnter = 0.65f;
        [Range(0f, 1f)] public float CurledExit = 0.5f;
        [Range(0f, 1f)] public float ExtendedEnter = 0.3f;
        [Range(0f, 1f)] public float ExtendedExit = 0.45f;
        public float KnifeMaxSpreadAngle = 25f;
        public float GestureHoldTime = 0.06f;

        public float TrackingTimeout = 0.5f;
        public bool RequireNeutralRearm = false;
        public float RearmNeutralTime = 0.1f;
    }

    public class HandFrame
    {
        public const int JointCount = 26;

        public HandSide Side;
        public Vector3[] Positions = new Vector3[JointCount];
        public Quaternion[] Rotations = new Quaternion[JointCount];
        public bool[] Valid = new bool[JointCount];
        public bool IsTracked;
        public float SampleTime;
        public int FrameId;

        public HandFrame(HandSide side)
        {
            Side = side;
            Clear();
        }

        public Vector3 GetPosition(HandJoint joint) => Positions[(int)joint];
        public Quaternion GetRotation(HandJoint joint) => Rotations[(int)joint];
        public bool IsValid(HandJoint joint) => Valid[(int)joint];

        public void SetJoint(HandJoint joint, Vector3 position, Quaternion rotation)
        {
            int idx = (int)joint;
            Positions[idx] = position;
            Rotations[idx] = rotation;
            Valid[idx] = true;
        }

        public void BeginFrame(float time, bool tracked)
        {
            SampleTime = time;
            IsTracked = tracked;
            for (int i = 0; i < JointCount; i++)
                Valid[i] = false;
        }

        public void Clear()
        {
            for (int i = 0; i < JointCount; i++)
            {
                Positions[i] = Vector3.zero;
                Rotations[i] = Quaternion.identity;
                Valid[i] = false;
            }
            IsTracked = false;
            SampleTime = 0f;
            FrameId = 0;
        }
    }

    public interface IHandFrameSource
    {
        bool IsRunning { get; }
        HandFrame GetFrame(HandSide side);
    }
}
