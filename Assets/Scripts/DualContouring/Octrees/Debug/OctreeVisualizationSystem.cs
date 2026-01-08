using DualContouring.ScalarField;
using DualContouring.ScalarField.Debug;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace DualContouring.Octrees.Debug
{
    public partial class OctreeVisualizationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.CreateSingleton<OctreeVisualizationOptions>();
        }

        protected override void OnUpdate()
        {
        }

        public void DrawGizmos()
        {
            var visualizationOptions = SystemAPI.GetSingleton<OctreeVisualizationOptions>();
            if (!visualizationOptions.Enabled)
            {
                return;
            }

            foreach ((DynamicBuffer<OctreeNode> octreeBuffer, DynamicBuffer<ScalarFieldItem> scalarFieldBuffer, RefRO<LocalToWorld> localToWorld) in SystemAPI.Query<
                             DynamicBuffer<OctreeNode>,
                             DynamicBuffer<ScalarFieldItem>,
                             RefRO<LocalToWorld>>()
                         .WithAll<ScalarFieldSelected>())
            {
                if (octreeBuffer.Length == 0 || scalarFieldBuffer.Length == 0)
                {
                    continue;
                }

                float3 minBounds = scalarFieldBuffer[0].Position;
                float3 maxBounds = scalarFieldBuffer[0].Position;
                for (int i = 1; i < scalarFieldBuffer.Length; i++)
                {
                    minBounds = math.min(minBounds, scalarFieldBuffer[i].Position);
                    maxBounds = math.max(maxBounds, scalarFieldBuffer[i].Position);
                }

                float initialSize = math.cmax(maxBounds - minBounds);

                DrawOctreeNode(octreeBuffer, 0, initialSize, 0, localToWorld.ValueRO, visualizationOptions);
            }
        }

        private void DrawOctreeNode(
            DynamicBuffer<OctreeNode> octreeBuffer,
            int nodeIndex,
            float size,
            int depth,
            LocalToWorld localToWorld,
            OctreeVisualizationOptions options)
        {
            if (nodeIndex < 0 || nodeIndex >= octreeBuffer.Length)
            {
                return;
            }

            OctreeNode node = octreeBuffer[nodeIndex];
            float3 position = math.transform(localToWorld.Value, node.Position);

            if (node.ChildIndex <= 0 && depth >= options.Depth.x && depth <= options.Depth.y)
            {
                Color color = GetDepthColor(depth);

                if (node.Value >= 0)
                {
                    color = Color.Lerp(color, Color.green, 0.3f);
                }
                else
                {
                    color = Color.Lerp(color, Color.red, 0.3f);
                }

                Gizmos.color = color;
                Gizmos.DrawWireCube(position, new float3(size, size, size));

                Gizmos.color = node.Value >= 0 ? Color.green : Color.red;
                Gizmos.DrawSphere(position, size * 0.05f);

#if UNITY_EDITOR
                Vector3 worldPos = position;
                Vector3 offset = Vector3.up * (HandleUtility.GetHandleSize(worldPos) * 0.2f);
                Handles.Label(worldPos + offset, $"D{depth}\nV:{node.Value:F2}");
#endif
            }

            float childSize = size / 2f;

            if (node.ChildIndex >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    int childIndex = node.ChildIndex + i;
                    DrawOctreeNode(octreeBuffer, childIndex, childSize, depth + 1, localToWorld, options);
                }
            }
        }

        private Color GetDepthColor(int depth)
        {
            Color[] depthColors =
            {
                new Color(1f, 1f, 1f, 0.8f),
                new Color(0f, 0.5f, 1f, 0.7f),
                new Color(1f, 0.5f, 0f, 0.6f),
                new Color(1f, 0f, 1f, 0.5f),
                new Color(0f, 1f, 1f, 0.4f),
                new Color(1f, 1f, 0f, 0.3f),
            };

            if (depth < depthColors.Length)
            {
                return depthColors[depth];
            }

            return depthColors[^1];
        }
    }
}