using DualContouring.DualContouring;
using DualContouring.MeshGeneration;
using DualContouring.ScalarField.Debug;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using DualContouringVisualizationSystem = DualContouring.DualContouring.Debug.DualContouringVisualizationSystem;
using ScalarFieldVisualizationSystem = DualContouring.ScalarField.Debug.ScalarFieldVisualizationSystem;

namespace DualContouring.ScalarField
{
    /// <summary>
    ///     Baker pour créer une entité avec un champ scalaire
    /// </summary>
    public class ScalarFieldAuthoring : MonoBehaviour
    {
        [FormerlySerializedAs("origin")]
        [Header("Grid Settings")]
        public float3 Origin = float3.zero;
        public float CellSize = 1f;
        [Tooltip("Taille de la grille (nombre de points dans chaque dimension)")]
        public int3 GridSize = new int3(3, 3, 3);
        public sbyte[] Values;

        private void OnDrawGizmos()
        {
            // Visualiser les valeurs du champ scalaire et les cellules pendant le jeu
            if (Application.isPlaying && World.DefaultGameObjectInjectionWorld != null)
            {
                var scalarFieldVisualizationSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<ScalarFieldVisualizationSystem>();
                scalarFieldVisualizationSystem?.DrawGizmos();

                var dualContouringVisualizationSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<DualContouringVisualizationSystem>();
                dualContouringVisualizationSystem?.DrawGizmos();
            }
        }

        private class Baker : Baker<ScalarFieldAuthoring>
        {
            public override void Bake(ScalarFieldAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity,
                    new ScalarFieldSelectedCell
                    {
                        Min = int3.zero,
                        Max = authoring.GridSize - new int3(2, 2, 2)  // GridSize - 2 pour les cellules (car cellules = points - 1)
                    });
                AddComponent(entity,
                    new ScalarFieldInfos
                    {
                        GridSize = authoring.GridSize,
                        CellSize = authoring.CellSize,
                        ScalarFieldOffset = authoring.Origin,
                    });

                AddBuffer<DualContouringCell>(entity);
                AddBuffer<DualContouringEdgeIntersection>(entity);
                AddBuffer<DualContouringMeshVertex>(entity);
                AddBuffer<DualContouringMeshTriangle>(entity);
                DynamicBuffer<ScalarFieldItem> buffer = AddBuffer<ScalarFieldItem>(entity);

                // Générer les valeurs scalaires selon le type
                int index = 0;
                for (int y = 0; y < authoring.GridSize.y; y++)
                {
                    for (int z = 0; z < authoring.GridSize.z; z++)
                    {
                        for (int x = 0; x < authoring.GridSize.x; x++)
                        {
                            float value = authoring.Values != null && index < authoring.Values.Length ? authoring.Values[index] : 0f;
                            buffer.Add(new ScalarFieldItem { Value = value });
                            index++;
                        }
                    }
                }
            }
        }
    }
}