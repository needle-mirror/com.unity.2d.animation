using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace Unity.U2D.Animation.Sample
{
    [ExecuteAlways]
    public class BlendShapeSample : MonoBehaviour
    {
        static readonly string k_SampleBlendShapeName = "Sample Blend Shape";

        void Start()
        {
            RebindAnimation();
        }

        void Update()
        {
        }

        void OnEnable()
        {
            AddBlendShape();
        }

        void AddBlendShape()
        {
            if (!TryGetComponent(out SpriteRenderer spriteRenderer))
                return;

            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null)
                return;

            if (sprite.GetBlendShapeIndex(k_SampleBlendShapeName) >= 0)
                return;

            Vector3[] positions = sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();
            int vertexCount = positions.Length;

            int index = sprite.AddBlendShape(k_SampleBlendShapeName);

            // Add shrunk shape frame at 50%
            NativeArray<SpriteBlendShapeVertex> blendShapeVertices = new(vertexCount, Allocator.Temp);
            try
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    Vector3 position = positions[i];
                    Vector3 targetPosition = new(position.x * 1.5f, position.y * 0.5f, position.z);

                    blendShapeVertices[i] = new SpriteBlendShapeVertex
                    {
                        index = (uint)i,
                        vertex = targetPosition - position,
                        normal = Vector3.zero,
                        tangent = Vector3.zero,
                    };
                }

                sprite.AddBlendShapeFrame(index, 50.0f, blendShapeVertices);
            }
            finally
            {
                blendShapeVertices.Dispose();
            }

            // Add round shape frame at 100%
            blendShapeVertices = new(vertexCount, Allocator.Temp);
            try
            {
                Bounds bounds = sprite.bounds;
                float radius = Mathf.Max(bounds.size.x, bounds.size.y) / 2.0f;

                for (int i = 0; i < vertexCount; i++)
                {
                    Vector3 position = positions[i];
                    Vector3 targetPosition = position.magnitude > 0
                        ? new Vector3(position.normalized.x * radius, position.normalized.y * radius, position.z)
                        : position;

                    blendShapeVertices[i] = new SpriteBlendShapeVertex
                    {
                        index = (uint)i,
                        vertex = targetPosition - position,
                        normal = Vector3.zero,
                        tangent = Vector3.zero,
                    };
                }

                sprite.AddBlendShapeFrame(index, 100.0f, blendShapeVertices);
            }
            finally
            {
                blendShapeVertices.Dispose();
            }
        }

        void RebindAnimation()
        {
            if (!TryGetComponent(out Animator animator))
                return;

            animator.Rebind();
        }
    }
}
