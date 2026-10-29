using UnityEngine;
using UnityEngine.Events;

namespace VRPrototype
{
    public enum ProstheticTool
    {
        None = 0,
        Scissors = 1,
        Hammer = 2,
        Knife = 3,
        Torch = 4
    }

    public class HandTransformationController : MonoBehaviour
    {
        public HandGestureSystem gestureSystem;
        public HandSide prostheticSide = HandSide.Right;
        public bool followWrist = true;
        public bool createPlaceholderVisuals = true;
        public Vector3 toolOffset = new Vector3(0f, 0f, 0.08f);

        public GameObject normalHandVisual;
        public GameObject scissorsVisual;
        public GameObject hammerVisual;
        public GameObject knifeVisual;
        public GameObject torchVisual;

        public ProstheticTool currentTool = ProstheticTool.None;
        public UnityEvent<ProstheticTool> onToolChanged = new UnityEvent<ProstheticTool>();

        private IHandFrameSource frameSource;
        private Transform anchorRoot;

        private void Awake()
        {
            if (gestureSystem == null)
                gestureSystem = GetComponent<HandGestureSystem>();

            if (gestureSystem == null)
                gestureSystem = FindAnyObjectByType<HandGestureSystem>();

            frameSource = GetComponent<IHandFrameSource>();
            if (frameSource == null)
                frameSource = FindAnyObjectByType<XRHandsFrameSource>();
            if (frameSource == null)
                frameSource = FindAnyObjectByType<WebcamHandFrameSource>();

            SetupAnchorAndPlaceholders();
            ApplyVisuals();
        }

        private void OnEnable()
        {
            if (gestureSystem != null)
                gestureSystem.onGestureChanged.AddListener(OnGestureChanged);
        }

        private void OnDisable()
        {
            if (gestureSystem != null)
                gestureSystem.onGestureChanged.RemoveListener(OnGestureChanged);
        }

        private void LateUpdate()
        {
            if (!followWrist || frameSource == null || !frameSource.IsRunning || anchorRoot == null)
                return;

            HandFrame frame = frameSource.GetFrame(prostheticSide);
            if (frame == null || !frame.IsTracked || !frame.IsValid(HandJoint.Wrist))
                return;

            Vector3 wristPos = frame.GetPosition(HandJoint.Wrist);
            Quaternion wristRot = frame.GetRotation(HandJoint.Wrist);

            if (frame.IsValid(HandJoint.MiddleProximal))
            {
                Vector3 forward = (frame.GetPosition(HandJoint.MiddleProximal) - wristPos).normalized;
                Vector3 up = frame.IsValid(HandJoint.IndexProximal) && frame.IsValid(HandJoint.LittleProximal)
                    ? Vector3.Cross(frame.GetPosition(HandJoint.IndexProximal) - wristPos, frame.GetPosition(HandJoint.LittleProximal) - wristPos).normalized
                    : Vector3.up;

                if (forward.sqrMagnitude > 0.001f && up.sqrMagnitude > 0.001f)
                    wristRot = Quaternion.LookRotation(forward, up);
            }

            anchorRoot.position = wristPos;
            anchorRoot.rotation = wristRot;
        }

        private void SetupAnchorAndPlaceholders()
        {
            GameObject anchorObj = new GameObject("ProstheticWristAnchor");
            anchorObj.transform.SetParent(transform, false);
            anchorRoot = anchorObj.transform;

            if (!createPlaceholderVisuals)
                return;

            if (normalHandVisual == null)
                normalHandVisual = CreatePlaceholder("NormalHand_Placeholder", PrimitiveType.Sphere, new Vector3(0.05f, 0.025f, 0.07f), new Color(0.85f, 0.7f, 0.6f));

            if (scissorsVisual == null)
                scissorsVisual = CreatePlaceholder("Scissors_Placeholder", PrimitiveType.Cube, new Vector3(0.03f, 0.015f, 0.14f), new Color(0.9f, 0.2f, 0.4f));

            if (hammerVisual == null)
                hammerVisual = CreatePlaceholder("Hammer_Placeholder", PrimitiveType.Cylinder, new Vector3(0.06f, 0.06f, 0.06f), new Color(0.2f, 0.75f, 0.9f));

            if (knifeVisual == null)
                knifeVisual = CreatePlaceholder("Knife_Placeholder", PrimitiveType.Cube, new Vector3(0.015f, 0.025f, 0.16f), new Color(0.8f, 0.85f, 0.9f));

            if (torchVisual == null)
                torchVisual = CreatePlaceholder("Torch_Placeholder", PrimitiveType.Capsule, new Vector3(0.035f, 0.07f, 0.035f), new Color(1f, 0.55f, 0.1f));

            if (GetComponentInChildren<ForearmButton>() == null)
            {
                CreateForearmButton("Button_Scissors", ProstheticTool.Scissors, new Vector3(-0.025f, 0.03f, -0.04f), new Color(0.9f, 0.2f, 0.4f));
                CreateForearmButton("Button_Hammer", ProstheticTool.Hammer, new Vector3(0.025f, 0.03f, -0.04f), new Color(0.2f, 0.75f, 0.9f));
                CreateForearmButton("Button_Knife", ProstheticTool.Knife, new Vector3(-0.025f, 0.03f, -0.08f), new Color(0.55f, 0.76f, 0.29f));
                CreateForearmButton("Button_Torch", ProstheticTool.Torch, new Vector3(0.025f, 0.03f, -0.08f), new Color(1f, 0.6f, 0f));
                CreateForearmButton("Button_ResetHand", ProstheticTool.None, new Vector3(0f, 0.03f, -0.12f), new Color(0.8f, 0.8f, 0.8f));
            }
        }

        private GameObject CreatePlaceholder(string objName, PrimitiveType type, Vector3 scale, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = objName;
            Destroy(obj.GetComponent<Collider>());
            obj.transform.SetParent(anchorRoot, false);
            obj.transform.localPosition = toolOffset;
            obj.transform.localScale = scale;

            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = color;

            return obj;
        }

        private void CreateForearmButton(string objName, ProstheticTool tool, Vector3 localPos, Color color)
        {
            GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            btnObj.name = objName;
            btnObj.transform.SetParent(anchorRoot, false);
            btnObj.transform.localPosition = localPos;
            btnObj.transform.localScale = new Vector3(0.03f, 0.008f, 0.03f);

            Collider col = btnObj.GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            Renderer rend = btnObj.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = color;

            ForearmButton btn = btnObj.AddComponent<ForearmButton>();
            btn.transformationController = this;
            btn.toolToSelect = tool;
            btn.pressingHand = prostheticSide == HandSide.Right ? HandSide.Left : HandSide.Right;
        }

        [ContextMenu("Transform To Normal Hand")]
        public void SelectNone() => TransformHand(ProstheticTool.None);

        [ContextMenu("Transform To Scissors")]
        public void SelectScissors() => TransformHand(ProstheticTool.Scissors);

        [ContextMenu("Transform To Hammer")]
        public void SelectHammer() => TransformHand(ProstheticTool.Hammer);

        [ContextMenu("Transform To Knife")]
        public void SelectKnife() => TransformHand(ProstheticTool.Knife);

        [ContextMenu("Transform To Torch")]
        public void SelectTorch() => TransformHand(ProstheticTool.Torch);

        public void TransformHand(ProstheticTool newTool)
        {
            if (currentTool == newTool)
            {
                Debug.Log("Tool already active: " + currentTool);
                return;
            }

            currentTool = newTool;
            ApplyVisuals();

            Debug.Log("Hand transformed to: " + currentTool);
            onToolChanged.Invoke(currentTool);
        }

        private void ApplyVisuals()
        {
            if (normalHandVisual != null)
                normalHandVisual.SetActive(currentTool == ProstheticTool.None);

            if (scissorsVisual != null)
                scissorsVisual.SetActive(currentTool == ProstheticTool.Scissors);

            if (hammerVisual != null)
                hammerVisual.SetActive(currentTool == ProstheticTool.Hammer);

            if (knifeVisual != null)
                knifeVisual.SetActive(currentTool == ProstheticTool.Knife);

            if (torchVisual != null)
                torchVisual.SetActive(currentTool == ProstheticTool.Torch);
        }

        private void OnGestureChanged(HandSide side, HandGesture gesture)
        {
            if (side != prostheticSide)
                return;

            if (gesture == HandGesture.None)
            {
                if (currentTool == ProstheticTool.Torch)
                    Debug.Log("Torch flame OFF (index finger curled or pose released)");
                return;
            }

            if (gesture == HandGesture.Scissors)
            {
                if (currentTool == ProstheticTool.Scissors)
                    Debug.Log("Using scissors");
                else
                    Debug.Log("Scissors gesture detected, but active tool is: " + currentTool);
            }
            else if (gesture == HandGesture.Fist)
            {
                if (currentTool == ProstheticTool.Hammer)
                    Debug.Log("Using hammer");
                else
                    Debug.Log("Fist gesture detected, but active tool is: " + currentTool);
            }
            else if (gesture == HandGesture.Knife)
            {
                if (currentTool == ProstheticTool.Knife)
                    Debug.Log("Using knife");
                else
                    Debug.Log("Knife gesture detected, but active tool is: " + currentTool);
            }
            else if (gesture == HandGesture.Torch)
            {
                if (currentTool == ProstheticTool.Torch)
                    Debug.Log("Using torch (flame ON)");
                else
                    Debug.Log("Torch gesture detected, but active tool is: " + currentTool);
            }
        }
    }
}
