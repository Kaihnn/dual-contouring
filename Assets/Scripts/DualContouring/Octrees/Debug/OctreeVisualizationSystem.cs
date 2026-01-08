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

                // Calculer la taille initiale de l'octree
                float3 minBounds = scalarFieldBuffer[0].Position;
                float3 maxBounds = scalarFieldBuffer[0].Position;
                for (int i = 1; i < scalarFieldBuffer.Length; i++)
                {
                    minBounds = math.min(minBounds, scalarFieldBuffer[i].Position);
                    maxBounds = math.max(maxBounds, scalarFieldBuffer[i].Position);
                }

                float initialSize = math.cmax(maxBounds - minBounds);

                // Dessiner l'octree récursivement
                DrawOctreeNode(octreeBuffer, 0, initialSize, 0, localToWorld.ValueRO);
            }
        }

        /// <summary>
        ///     Dessine récursivement un nœud de l'octree et ses enfants
        /// </summary>
        private void DrawOctreeNode(
            DynamicBuffer<OctreeNode> octreeBuffer,
            int nodeIndex,
            float size,
            int depth,
            LocalToWorld localToWorld)
        {
            if (nodeIndex < 0 || nodeIndex >= octreeBuffer.Length)
            {
                return;
            }

            OctreeNode node = octreeBuffer[nodeIndex];
            float3 position = math.transform(localToWorld.Value, node.Position);


            // Si le nœud a des enfants, les dessiner récursivement
            if (node.ChildIndex >= 0)
            {
                float childSize = size / 2f;

                // Dessiner les 8 enfants
                for (int i = 0; i < 8; i++)
                {
                    int childIndex = node.ChildIndex + i;
                    DrawOctreeNode(octreeBuffer, childIndex, childSize, depth + 1, localToWorld);
                }
            }
            else
            {
                // Ne dessiner que les nœuds feuilles (sans enfants)
                // Choisir une couleur en fonction de la profondeur
                Color color = GetDepthColor(depth);

                // Si le nœud traverse la surface (changement de signe), utiliser une couleur plus vive
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

                // Dessiner le point central du nœud
                Gizmos.color = node.Value >= 0 ? Color.green : Color.red;
                Gizmos.DrawSphere(position, size * 0.05f);

#if UNITY_EDITOR
                // Afficher la valeur et la profondeur
                Vector3 worldPos = position;
                Vector3 offset = Vector3.up * (HandleUtility.GetHandleSize(worldPos) * 0.2f);
                Handles.Label(worldPos + offset, $"D{depth}\nV:{node.Value:F2}");
#endif
            }
        }

        /// <summary>
        ///     Retourne une couleur en fonction de la profondeur du nœud
        /// </summary>
        private Color GetDepthColor(int depth)
        {
            // Palette de couleurs pour différentes profondeurs
            Color[] depthColors =
            {
                new Color(1f, 1f, 1f, 0.8f), // Profondeur 0: Blanc
                new Color(0f, 0.5f, 1f, 0.7f), // Profondeur 1: Bleu clair
                new Color(1f, 0.5f, 0f, 0.6f), // Profondeur 2: Orange
                new Color(1f, 0f, 1f, 0.5f), // Profondeur 3: Magenta
                new Color(0f, 1f, 1f, 0.4f), // Profondeur 4: Cyan
                new Color(1f, 1f, 0f, 0.3f), // Profondeur 5+: Jaune
            };

            if (depth < depthColors.Length)
            {
                return depthColors[depth];
            }

            return depthColors[depthColors.Length - 1];
        }
    }
}