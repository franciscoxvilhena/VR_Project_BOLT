using System.Collections.Generic;
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
        private readonly Dictionary<GameObject, float> toolReach = new Dictionary<GameObject, float>();

        /// <summary>The point on the wrist the tools are mounted on.</summary>
        public Transform ToolAnchor => anchorRoot;

        /// <summary>Orientation of the hand the tools are mounted on: forward is where the hand points.</summary>
        public Quaternion ToolRotation => anchorRoot != null ? anchorRoot.rotation : transform.rotation;

        /// <summary>How far a tool model sticks out in front of its mounting point, along the pointing direction.</summary>
        public float GetToolReach(GameObject visual)
        {
            if (visual == null)
                return 0f;
            if (toolReach.TryGetValue(visual, out float cached))
                return cached;

            Vector3 origin = visual.transform.position;
            Vector3 forward = ToolRotation * Vector3.forward;
            float reach = 0f;
            foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    reach = Mathf.Max(reach, Vector3.Dot(filter.transform.TransformPoint(corner) - origin, forward));
                }
            }

            toolReach[visual] = reach;
            return reach;
        }

        /// <summary>
        /// The box a tool acts in. It always points where the hand points, however the tool model itself
        /// happens to be rotated, and it is stretched to reach the tip of the model.
        /// With startAtTip the box begins at the tip instead (a flame in front of a torch).
        /// </summary>
        public void GetToolZone(GameObject visual, Vector3 centerOffset, Vector3 halfExtents, bool startAtTip,
            out Vector3 center, out Vector3 half, out Quaternion rotation)
        {
            rotation = ToolRotation;
            half = halfExtents;
            Vector3 origin = visual != null ? visual.transform.position
                : anchorRoot != null ? anchorRoot.position : transform.position;

            float reach = GetToolReach(visual);
            if (startAtTip)
            {
                centerOffset.z = Mathf.Max(centerOffset.z, reach + halfExtents.z);
            }
            else
            {
                float start = centerOffset.z - halfExtents.z;
                float end = Mathf.Max(centerOffset.z + halfExtents.z, reach);
                centerOffset.z = (start + end) * 0.5f;
                half.z = (end - start) * 0.5f;
            }

            center = origin + rotation * centerOffset;
        }

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

            normalHandVisual = SetupVisual(normalHandVisual, "NormalHand_Placeholder", PrimitiveType.Sphere, new Vector3(0.05f, 0.025f, 0.07f), new Color(0.85f, 0.7f, 0.6f));
            scissorsVisual = SetupVisual(scissorsVisual, "Scissors_Placeholder", PrimitiveType.Cube, new Vector3(0.03f, 0.015f, 0.14f), new Color(0.9f, 0.2f, 0.4f));
            hammerVisual = SetupVisual(hammerVisual, "Hammer_Placeholder", PrimitiveType.Cylinder, new Vector3(0.06f, 0.06f, 0.06f), new Color(0.2f, 0.75f, 0.9f));
            knifeVisual = SetupVisual(knifeVisual, "Knife_Placeholder", PrimitiveType.Cube, new Vector3(0.015f, 0.025f, 0.16f), new Color(0.8f, 0.85f, 0.9f));
            torchVisual = SetupVisual(torchVisual, "Torch_Placeholder", PrimitiveType.Capsule, new Vector3(0.035f, 0.07f, 0.035f), new Color(1f, 0.55f, 0.1f));

            if (GetComponentInChildren<ForearmButton>() == null)
            {
                CreateForearmButton("Button_Scissors", ProstheticTool.Scissors, new Vector3(-0.025f, 0.03f, -0.04f), new Color(0.55f, 0.76f, 0.29f));
                CreateForearmButton("Button_Hammer", ProstheticTool.Hammer, new Vector3(0.025f, 0.03f, -0.04f), new Color(0.2f, 0.75f, 0.9f));
                CreateForearmButton("Button_Knife", ProstheticTool.Knife, new Vector3(-0.025f, 0.03f, -0.08f), new Color(0.9f, 0.15f, 0.15f));
                CreateForearmButton("Button_Torch", ProstheticTool.Torch, new Vector3(0.025f, 0.03f, -0.08f), new Color(1f, 0.6f, 0f));
                CreateForearmButton("Button_ResetHand", ProstheticTool.None, new Vector3(0f, 0.03f, -0.12f), new Color(0.8f, 0.8f, 0.8f));
            }
        }

        private GameObject SetupVisual(GameObject customVisual, string objName, PrimitiveType type, Vector3 scale, Color color)
        {
            if (customVisual != null)
            {
                if (!customVisual.scene.IsValid())
                    customVisual = Instantiate(customVisual);

                customVisual.transform.SetParent(anchorRoot, false);
                customVisual.transform.localPosition = toolOffset;
                return customVisual;
            }

            if (!createPlaceholderVisuals)
                return null;

            return CreatePlaceholder(objName, type, scale, color);
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

        /// <summary>
        /// Called by TrackedArms when the custom arm model is in use: drops the placeholder hand
        /// (the arm model is the hand now) and moves the forearm buttons onto the model's forearm.
        /// </summary>
        public void UseTrackedArm(Transform forearmAnchor, float buttonHeight)
        {
            if (normalHandVisual != null && normalHandVisual.name == "NormalHand_Placeholder")
            {
                Destroy(normalHandVisual);
                normalHandVisual = null;
            }

            if (anchorRoot == null || forearmAnchor == null)
                return;

            foreach (ForearmButton button in anchorRoot.GetComponentsInChildren<ForearmButton>(true))
            {
                Vector3 localPos = button.RestLocalPosition;
                localPos.y = buttonHeight;
                button.transform.SetParent(forearmAnchor, false);
                button.SetRestLocalPosition(localPos);
            }
        }

        /// <summary>
        /// Called by TrackedArms when a scene has more than one controller for the same hand: this one
        /// gives up its own forearm buttons and placeholder visuals and just mirrors the main controller,
        /// so there is a single set of buttons and scripts pointing at either controller still work.
        /// </summary>
        public void FollowController(HandTransformationController main)
        {
            if (main == null || main == this)
                return;

            if (anchorRoot != null)
            {
                foreach (ForearmButton button in anchorRoot.GetComponentsInChildren<ForearmButton>(true))
                    Destroy(button.gameObject);
            }

            normalHandVisual = DropPlaceholder(normalHandVisual);
            scissorsVisual = DropPlaceholder(scissorsVisual);
            hammerVisual = DropPlaceholder(hammerVisual);
            knifeVisual = DropPlaceholder(knifeVisual);
            torchVisual = DropPlaceholder(torchVisual);

            // Use the main controller's tool models, so scripts that read e.g. knifeVisual from this
            // controller get the real model on the hand.
            if (normalHandVisual == null) normalHandVisual = main.normalHandVisual;
            if (scissorsVisual == null) scissorsVisual = main.scissorsVisual;
            if (hammerVisual == null) hammerVisual = main.hammerVisual;
            if (knifeVisual == null) knifeVisual = main.knifeVisual;
            if (torchVisual == null) torchVisual = main.torchVisual;

            main.onToolChanged.AddListener(tool => { if (currentTool != tool) TransformHand(tool); });
            onToolChanged.AddListener(tool => { if (main.currentTool != tool) main.TransformHand(tool); });
            if (currentTool != main.currentTool)
                TransformHand(main.currentTool);
        }

        private static GameObject DropPlaceholder(GameObject visual)
        {
            if (visual == null || !visual.name.EndsWith("_Placeholder"))
                return visual;

            Destroy(visual);
            return null;
        }

        public bool HasCustomToolVisuals()
        {
            return IsCustom(scissorsVisual) || IsCustom(hammerVisual) || IsCustom(knifeVisual) || IsCustom(torchVisual);
        }

        private static bool IsCustom(GameObject visual)
        {
            return visual != null && !visual.name.EndsWith("_Placeholder");
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
