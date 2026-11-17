using UnityEngine;
using UnityEngine.Events;

namespace VRPrototype
{
    public class KnifeTool : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HandTransformationController controller;
        [SerializeField] private HandGestureSystem gestureSystem;

        [Header("Cutting")]
        [SerializeField] private float holdDuration = 2f;
        [Tooltip("Grace period so a brief tracking flicker doesn't reset progress")]
        [SerializeField] private float graceTime = 0.2f;
        [SerializeField] private LayerMask cuttableLayers = ~0;

        [Header("Blade detection box (world-space meters)")]
        [SerializeField] private Vector3 boxCenterOffset = new Vector3(0f, 0f, 0.05f);
        [SerializeField] private Vector3 boxHalfExtents = new Vector3(0.04f, 0.04f, 0.08f);

        [Header("Events")]
        public UnityEvent<float> onProgress = new UnityEvent<float>();   // 0..1
        public UnityEvent<KnifeCuttable> onCutComplete = new UnityEvent<KnifeCuttable>();

        private readonly Collider[] hits = new Collider[16];
        private KnifeCuttable target;
        private float holdTimer;
        private float lostTimer;

        private void Awake()
        {
            if (controller == null)
                controller = FindAnyObjectByType<HandTransformationController>();
            if (gestureSystem == null && controller != null)
                gestureSystem = controller.gestureSystem;
            if (gestureSystem == null)
                gestureSystem = FindAnyObjectByType<HandGestureSystem>();
        }

        private bool BothConditionsMet()
        {
            if (controller == null || gestureSystem == null) return false;

            bool toolSelected = controller.currentTool == ProstheticTool.Knife;
            bool gestureOk = gestureSystem.GetGesture(controller.prostheticSide) == HandGesture.Knife;
            return toolSelected && gestureOk;
        }

        private KnifeCuttable FindTarget()
        {
            // The zone points where the hand points and covers the whole blade
            controller.GetToolZone(controller.knifeVisual, boxCenterOffset, boxHalfExtents, false,
                out Vector3 center, out Vector3 half, out Quaternion rotation);

            int count = Physics.OverlapBoxNonAlloc(center, half, hits, rotation,
                cuttableLayers, QueryTriggerInteraction.Collide);

            KnifeCuttable best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var c = hits[i].GetComponentInParent<KnifeCuttable>();
                if (c == null || c.IsCut) continue;

                float d = (hits[i].ClosestPoint(center) - center).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        private void Update()
        {
            // Only look for / keep targets while the knife is actually equipped
            if (controller == null || controller.currentTool != ProstheticTool.Knife)
            {
                ClearTarget();
                return;
            }

            KnifeCuttable found = FindTarget();

            if (found != target)
            {
                ClearTarget();
                target = found;
            }

            if (target == null) return;

            if (BothConditionsMet())
            {
                lostTimer = 0f;
                holdTimer += Time.deltaTime;
                target.SetCutting(true);
                onProgress.Invoke(Mathf.Clamp01(holdTimer / holdDuration));

                if (holdTimer >= holdDuration)
                {
                    KnifeCuttable done = target;
                    target = null;
                    ResetProgress();
                    done.Cut();
                    onCutComplete.Invoke(done);
                }
            }
            else
            {
                lostTimer += Time.deltaTime;
                if (lostTimer > graceTime)
                {
                    target.SetCutting(false);
                    ResetProgress();
                }
            }
        }

        private void ClearTarget()
        {
            if (target != null) target.SetCutting(false);
            target = null;
            ResetProgress();
        }

        private void ResetProgress()
        {
            if (holdTimer > 0f) onProgress.Invoke(0f);
            holdTimer = 0f;
            lostTimer = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            if (controller == null) return;
            controller.GetToolZone(controller.knifeVisual, boxCenterOffset, boxHalfExtents, false,
                out Vector3 center, out Vector3 half, out Quaternion rotation);
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, half * 2f);
        }
    }
}