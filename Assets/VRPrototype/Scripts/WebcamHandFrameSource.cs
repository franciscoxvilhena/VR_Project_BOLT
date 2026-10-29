using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace VRPrototype
{
    public class WebcamHandFrameSource : MonoBehaviour, IHandFrameSource
    {
        public int port = 5065;
        public Transform anchor;
        public float distance = 0.5f;
        public Vector2 viewSize = new Vector2(0.6f, 0.45f);
        public float connectionTimeout = 1f;

        private HandFrame leftFrame = new HandFrame(HandSide.Left);
        private HandFrame rightFrame = new HandFrame(HandSide.Right);
        private byte[] packetBuffer = new byte[4096];
        private Vector3[] landmarks = new Vector3[21];
        private Socket socket;
        private float lastPacketTime = -999f;
        private bool connected;

        public bool IsRunning => connected;

        public HandFrame GetFrame(HandSide side)
        {
            return side == HandSide.Left ? leftFrame : rightFrame;
        }

        private void OnEnable()
        {
            if (anchor == null && Camera.main != null)
                anchor = Camera.main.transform;

            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                socket.Blocking = false;
            }
            catch (SocketException e)
            {
                Debug.LogError($"UDP {port} error: {e.Message}");
                CloseSocket();
            }

            connected = false;
            leftFrame.Clear();
            rightFrame.Clear();
        }

        private void OnDisable()
        {
            CloseSocket();
            connected = false;
            leftFrame.Clear();
            rightFrame.Clear();
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            int length = ReceiveLatest();
            if (length > 0)
            {
                if (!connected)
                {
                    connected = true;
                    leftFrame.Clear();
                    rightFrame.Clear();
                }

                lastPacketTime = now;
                ParsePacket(packetBuffer, length, now);
            }
            else if (connected && now - lastPacketTime > connectionTimeout)
            {
                connected = false;
                leftFrame.Clear();
                rightFrame.Clear();
            }
        }

        private int ReceiveLatest()
        {
            if (socket == null)
                return 0;

            int length = 0;
            try
            {
                while (socket.Available > 0)
                    length = socket.Receive(packetBuffer);
            }
            catch (SocketException)
            {
            }

            return length;
        }

        private void ParsePacket(byte[] data, int length, float now)
        {
            const int headerBytes = 5;
            const int handBytes = 1 + 8 + 21 * 12;

            if (length < headerBytes)
                return;

            int count = data[4];
            if (length < headerBytes + count * handBytes)
                return;

            Vector3 anchorPos = anchor != null ? anchor.position : Vector3.zero;
            Quaternion anchorRot = anchor != null ? anchor.rotation : Quaternion.identity;

            bool leftSeen = false;
            bool rightSeen = false;
            int offset = headerBytes;

            for (int h = 0; h < count; h++, offset += handBytes)
            {
                HandSide side = data[offset] == 1 ? HandSide.Right : HandSide.Left;
                if (side == HandSide.Right ? rightSeen : leftSeen)
                    continue;

                if (side == HandSide.Right) rightSeen = true;
                else leftSeen = true;

                float cx = BitConverter.ToSingle(data, offset + 1);
                float cy = BitConverter.ToSingle(data, offset + 5);
                Vector3 center = new Vector3((cx - 0.5f) * viewSize.x, (0.5f - cy) * viewSize.y, distance);

                int p = offset + 9;
                for (int i = 0; i < 21; i++, p += 12)
                {
                    Vector3 local = center + new Vector3(
                        BitConverter.ToSingle(data, p),
                        -BitConverter.ToSingle(data, p + 4),
                        BitConverter.ToSingle(data, p + 8));
                    landmarks[i] = anchorPos + anchorRot * local;
                }

                HandFrame frame = side == HandSide.Right ? rightFrame : leftFrame;
                frame.BeginFrame(now, true);
                ApplyLandmarks(frame, landmarks, anchorRot);
                frame.FrameId++;
            }

            if (!leftSeen && now - leftFrame.SampleTime > 0.35f)
            {
                leftFrame.BeginFrame(now, false);
                leftFrame.FrameId++;
            }
            if (!rightSeen && now - rightFrame.SampleTime > 0.35f)
            {
                rightFrame.BeginFrame(now, false);
                rightFrame.FrameId++;
            }
        }

        private void ApplyLandmarks(HandFrame frame, Vector3[] pts, Quaternion rot)
        {
            Vector3 wrist = pts[0];
            frame.SetJoint(HandJoint.Wrist, wrist, rot);
            frame.SetJoint(HandJoint.Palm, Vector3.Lerp(wrist, pts[9], 0.5f), rot);

            frame.SetJoint(HandJoint.ThumbMetacarpal, pts[1], rot);
            frame.SetJoint(HandJoint.ThumbProximal, pts[2], rot);
            frame.SetJoint(HandJoint.ThumbDistal, pts[3], rot);
            frame.SetJoint(HandJoint.ThumbTip, pts[4], rot);

            WriteFinger(frame, HandFinger.Index, pts, 5, rot);
            WriteFinger(frame, HandFinger.Middle, pts, 9, rot);
            WriteFinger(frame, HandFinger.Ring, pts, 13, rot);
            WriteFinger(frame, HandFinger.Little, pts, 17, rot);
        }

        private void WriteFinger(HandFrame frame, HandFinger finger, Vector3[] pts, int mcp, Quaternion rot)
        {
            int first = (int)HandPoseAnalyzer.GetFirstJoint(finger);
            frame.SetJoint((HandJoint)first, Vector3.Lerp(pts[0], pts[mcp], 0.15f), rot);
            for (int i = 0; i < 4; i++)
                frame.SetJoint((HandJoint)(first + 1 + i), pts[mcp + i], rot);
        }

        private void CloseSocket()
        {
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
        }

        private void OnDrawGizmos()
        {
            DrawHandGizmo(leftFrame, new Color(1f, 0.63f, 0.24f));
            DrawHandGizmo(rightFrame, new Color(1f, 0.78f, 0.31f));
        }

        private void DrawHandGizmo(HandFrame frame, Color color)
        {
            if (frame == null || !frame.IsTracked)
                return;

            Gizmos.color = color;
            for (int f = 0; f < 5; f++)
            {
                HandFinger finger = (HandFinger)f;
                int first = (int)HandPoseAnalyzer.GetFirstJoint(finger);
                int count = finger == HandFinger.Thumb ? 4 : 5;
                Vector3 prev = frame.GetPosition(HandJoint.Wrist);
                for (int j = 0; j < count; j++)
                {
                    Vector3 pos = frame.GetPosition((HandJoint)(first + j));
                    Gizmos.DrawLine(prev, pos);
                    Gizmos.DrawSphere(pos, 0.004f);
                    prev = pos;
                }
            }
        }
    }
}
