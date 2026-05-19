using Unity.Profiling;
using UnityEngine.U2D.Animation.Profiler;

namespace UnityEngine.U2D.Animation
{
    [AddComponentMenu("")]
    [DefaultExecutionOrder(UpdateOrder.spriteSkinUpdateOrder)]
    [ExecuteInEditMode]
    internal class DeformationManagerUpdater : MonoBehaviour
    {
        public System.Action<GameObject> onDestroyingComponent { get; set; }

        void OnDestroy() => onDestroyingComponent?.Invoke(gameObject);

        void LateUpdate()
        {
            if (DeformationManager.instance.helperGameObject != gameObject)
            {
                GameObject.DestroyImmediate(gameObject);
                return;
            }
            Animation2DProfilerMarkers.deformationManagerLateUpdateProfilerMarker.Begin();
            DeformationManager.instance.Update();
            Animation2DProfilerMarkers.deformationManagerLateUpdateProfilerMarker.End();
        }
    }
}
