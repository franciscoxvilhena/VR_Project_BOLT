using UnityEngine;
using UnityEngine.Events;

namespace VRPrototype
{
    public class ForearmButton : MonoBehaviour
    {
        public HandTransformationController transformationController;
        public ProstheticTool toolToSelect = ProstheticTool.Scissors;
        public HandSide pressingHand = HandSide.Left;
        public float pressRadius = 0.03f;
        public float releaseRadius = 0.045f;
        public float pressCooldown = 0.35f;
        public float pressDepth = 0.008f;
        public float moveSpeed = 12f;
        public UnityEvent onPressed = new UnityEvent();

        private IHandFrameSource frameSource;
        private Vector3 restLocalPosition;
        private bool isPressed;
        private float lastPressTime = -10f;

        private void Awake()
        {
            restLocalPosition = transform.localPosition;

            if (transformationController == null)
                transformationController = GetComponentInParent<HandTransformationController>();

            if (transformationController == null)
                transformationController = FindAnyObjectByType<HandTransformationController>();

            frameSource = FindAnyObjectByType<XRHandsFrameSource>();
            if (frameSource == null)
                frameSource = FindAnyObjectByType<WebcamHandFrameSource>();
        }

        public Vector3 RestLocalPosition
        {
            get => restLocalPosition;
            set => transform.localPosition = restLocalPosition = value;
        }

        private void Update()
        {
            CheckFingerProximity();
            UpdateButtonVisual();
        }

        private void CheckFingerProximity()
        {
            if (frameSource == null || !frameSource.IsRunning)
                return;

            HandFrame frame = frameSource.GetFrame(pressingHand);
            if (frame == null || !frame.IsTracked || !frame.IsValid(HandJoint.IndexTip))
                return;

            Vector3 fingerPos = frame.GetPosition(HandJoint.IndexTip);
            float distance = Vector3.Distance(transform.position, fingerPos);

            if (!isPressed && distance <= pressRadius)
            {
                isPressed = true;
                TriggerPress();
            }
            else if (isPressed && distance >= releaseRadius)
            {
                isPressed = false;
                Debug.Log("Button released: " + toolToSelect);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isPressed)
                return;

            isPressed = true;
            TriggerPress();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!isPressed)
                return;

            isPressed = false;
            Debug.Log("Button released: " + toolToSelect);
        }

        [ContextMenu("Press Button")]
        public void TriggerPress()
        {
            if (Time.time - lastPressTime < pressCooldown)
                return;

            lastPressTime = Time.time;
            Debug.Log("Button pressed: " + toolToSelect);

            if (transformationController != null)
                transformationController.TransformHand(toolToSelect);

            onPressed.Invoke();
        }

        private void UpdateButtonVisual()
        {
            Vector3 target = isPressed
                ? restLocalPosition - new Vector3(0f, pressDepth, 0f)
                : restLocalPosition;

            transform.localPosition = Vector3.Lerp(transform.localPosition, target, Time.deltaTime * moveSpeed);
        }
    }
}
