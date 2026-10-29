import argparse
import os
import socket
import struct
import sys
import time
import urllib.request

import cv2
import mediapipe as mp

LANDMARKS = 21
MODEL_URL = "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/latest/hand_landmarker.task"
MODEL_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "hand_landmarker.task")
BONES = [
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (5, 9), (9, 10), (10, 11), (11, 12),
    (9, 13), (13, 14), (14, 15), (15, 16),
    (13, 17), (0, 17), (17, 18), (18, 19), (19, 20),
]


class LegacyTracker:
    def __init__(self):
        self.hands = mp.solutions.hands.Hands(
            max_num_hands=2,
            model_complexity=1,
            min_detection_confidence=0.6,
            min_tracking_confidence=0.5,
        )

    def process(self, rgb, timestamp_ms):
        result = self.hands.process(rgb)
        hands = []
        if not result.multi_hand_landmarks:
            return hands
        for image_lm, world_lm, handedness in zip(
            result.multi_hand_landmarks, result.multi_hand_world_landmarks, result.multi_handedness
        ):
            hands.append((
                handedness.classification[0].label,
                [(p.x, p.y) for p in image_lm.landmark],
                [(p.x, p.y, p.z) for p in world_lm.landmark],
            ))
        return hands

    def close(self):
        self.hands.close()


class TasksTracker:
    def __init__(self):
        from mediapipe.tasks.python import BaseOptions
        from mediapipe.tasks.python import vision

        if not os.path.exists(MODEL_PATH):
            print("Downloading hand_landmarker.task ...")
            urllib.request.urlretrieve(MODEL_URL, MODEL_PATH)
        options = vision.HandLandmarkerOptions(
            base_options=BaseOptions(model_asset_path=MODEL_PATH),
            running_mode=vision.RunningMode.VIDEO,
            num_hands=2,
            min_hand_detection_confidence=0.6,
            min_tracking_confidence=0.5,
        )
        self.landmarker = vision.HandLandmarker.create_from_options(options)

    def process(self, rgb, timestamp_ms):
        image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        result = self.landmarker.detect_for_video(image, timestamp_ms)
        hands = []
        for image_lm, world_lm, handedness in zip(result.hand_landmarks, result.hand_world_landmarks, result.handedness):
            hands.append((
                handedness[0].category_name,
                [(p.x, p.y) for p in image_lm],
                [(p.x, p.y, p.z) for p in world_lm],
            ))
        return hands

    def close(self):
        self.landmarker.close()


def create_tracker():
    solutions = getattr(mp, "solutions", None)
    if solutions is not None and hasattr(solutions, "hands"):
        return LegacyTracker()
    return TasksTracker()


def pack(hands):
    data = bytearray(struct.pack("<IB", 1, len(hands)))
    for label, image_points, world_points in hands:
        cx = sum(p[0] for p in image_points) / LANDMARKS
        cy = sum(p[1] for p in image_points) / LANDMARKS
        data += struct.pack("<Bff", 1 if label == "Right" else 0, cx, cy)
        for x, y, z in world_points:
            data += struct.pack("<fff", x, y, z)
    return bytes(data)


def draw(frame, hands, fps):
    height, width = frame.shape[:2]
    for label, image_points, _ in hands:
        color = (80, 200, 255) if label == "Right" else (255, 160, 60)
        pixels = [(int(x * width), int(y * height)) for x, y in image_points]
        for a, b in BONES:
            cv2.line(frame, pixels[a], pixels[b], color, 2)
        for point in pixels:
            cv2.circle(frame, point, 4, (255, 255, 255), -1)
        cv2.putText(frame, label, (pixels[0][0] - 20, pixels[0][1] + 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, color, 2)
    cv2.putText(frame, f"{fps:4.1f} fps  hands: {len(hands)}  [Q] quit", (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)


def open_camera(index):
    if sys.platform == "win32":
        capture = cv2.VideoCapture(index, cv2.CAP_DSHOW)
        if capture.isOpened():
            return capture
    return cv2.VideoCapture(index)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5065)
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--height", type=int, default=720)
    parser.add_argument("--no-preview", action="store_true")
    args = parser.parse_args()

    capture = open_camera(args.camera)
    if not capture.isOpened():
        print(f"Camera {args.camera} not found. Try --camera 1")
        return 1
    capture.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    capture.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)

    tracker = create_tracker()
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)
    print(f"Streaming to udp://{args.host}:{args.port}")

    start = time.monotonic()
    last = start
    fps = 0.0
    last_timestamp = -1
    try:
        while True:
            ok, frame = capture.read()
            if not ok:
                time.sleep(0.01)
                continue
            frame = cv2.flip(frame, 1)
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            timestamp = max(int((time.monotonic() - start) * 1000), last_timestamp + 1)
            last_timestamp = timestamp
            hands = tracker.process(rgb, timestamp)
            sock.sendto(pack(hands), target)

            now = time.monotonic()
            fps = fps * 0.9 + (1.0 / max(now - last, 1e-6)) * 0.1
            last = now
            if not args.no_preview:
                draw(frame, hands, fps)
                cv2.imshow("Webcam Hands", frame)
                key = cv2.waitKey(1) & 0xFF
                if key in (ord("q"), 27):
                    break
    finally:
        sock.sendto(struct.pack("<IB", 1, 0), target)
        tracker.close()
        capture.release()
        cv2.destroyAllWindows()
    return 0


if __name__ == "__main__":
    sys.exit(main())
