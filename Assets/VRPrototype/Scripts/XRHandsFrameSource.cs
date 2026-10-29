using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace VRPrototype
{
    public class XRHandsFrameSource : MonoBehaviour, IHandFrameSource
    {
        public Transform trackingSpace;

        private HandFrame leftFrame = new HandFrame(HandSide.Left);
        private HandFrame rightFrame = new HandFrame(HandSide.Right);
        private XRHandSubsystem subsystem;
        private List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();

        public bool IsRunning => subsystem != null && subsystem.running;

        public HandFrame GetFrame(HandSide side)
        {
            return side == HandSide.Left ? leftFrame : rightFrame;
        }

        private void OnEnable()
        {
            FindTrackingSpace();
            leftFrame.Clear();
            rightFrame.Clear();
        }

        private void OnDisable()
        {
            if (subsystem != null)
            {
                subsystem.updatedHands -= OnUpdatedHands;
                subsystem = null;
            }
            leftFrame.Clear();
            rightFrame.Clear();
        }

        private void Update()
        {
            if (subsystem != null && subsystem.running)
                return;

            SubsystemManager.GetSubsystems(subsystems);
            for (int i = 0; i < subsystems.Count; i++)
            {
                if (!subsystems[i].running)
                    continue;

                if (subsystem != null)
                    subsystem.updatedHands -= OnUpdatedHands;

                subsystem = subsystems[i];
                subsystem.updatedHands += OnUpdatedHands;
                FindTrackingSpace();
                leftFrame.Clear();
                rightFrame.Clear();
                break;
            }
        }

        private void FindTrackingSpace()
        {
            if (trackingSpace != null)
                return;

            XROrigin origin = FindAnyObjectByType<XROrigin>();
            if (origin != null)
            {
                trackingSpace = origin.CameraFloorOffsetObject != null
                    ? origin.CameraFloorOffsetObject.transform
                    : origin.transform;
            }
        }

        private void OnUpdatedHands(XRHandSubsystem handSubsystem, XRHandSubsystem.UpdateSuccessFlags flags, XRHandSubsystem.UpdateType updateType)
        {
            if (updateType != XRHandSubsystem.UpdateType.Dynamic)
                return;

            float now = Time.realtimeSinceStartup;
            ReadHand(handSubsystem.leftHand, (flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0, leftFrame, now);
            ReadHand(handSubsystem.rightHand, (flags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0, rightFrame, now);
        }

        private void ReadHand(XRHand hand, bool jointsUpdated, HandFrame frame, float now)
        {
            if (hand.isTracked && !jointsUpdated)
                return;

            frame.BeginFrame(now, hand.isTracked);

            if (hand.isTracked)
            {
                for (int i = 0; i < HandFrame.JointCount; i++)
                {
                    XRHandJoint joint = hand.GetJoint(XRHandJointIDUtility.FromIndex(i));
                    if (!joint.TryGetPose(out Pose pose))
                        continue;

                    if (trackingSpace != null)
                        frame.SetJoint((HandJoint)i, trackingSpace.TransformPoint(pose.position), trackingSpace.rotation * pose.rotation);
                    else
                        frame.SetJoint((HandJoint)i, pose.position, pose.rotation);
                }
            }

            frame.FrameId++;
        }
    }
}
