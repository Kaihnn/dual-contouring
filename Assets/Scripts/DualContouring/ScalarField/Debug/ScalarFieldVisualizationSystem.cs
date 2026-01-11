using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace DualContouring.ScalarField.Debug
{
    /// <summary>
    ///     Système de visualisation des valeurs du champ scalaire dans l'éditeur
    /// </summary>
    public partial class ScalarFieldVisualizationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.CreateSingleton<ScalarFieldVisualizationOptions>();
        }

        protected override void OnUpdate()
        {
            // Ce système ne fait rien pendant le jeu, seulement pour le debug dans l'éditeur
        }

        public void DrawGizmos()
        {
            var visualizationOptions = SystemAPI.GetSingleton<ScalarFieldVisualizationOptions>();
            if (!visualizationOptions.Enabled)
            {
                return;
            }

            foreach ((DynamicBuffer<ScalarFieldItem> scalarFieldBuffer, RefRO<ScalarFieldInfos> scalarFieldInfos, RefRO<LocalToWorld> localToWorld, RefRO<ScalarFieldSelectedCell> selectedCell) in SystemAPI.Query<
                             DynamicBuffer<ScalarFieldItem>,
                             RefRO<ScalarFieldInfos>,
                             RefRO<LocalToWorld>,
                             RefRO<ScalarFieldSelectedCell>>()
                         .WithAll<ScalarFieldSelected>())
            {
                DrawScalarFieldValues(scalarFieldBuffer, scalarFieldInfos.ValueRO, localToWorld.ValueRO, selectedCell.ValueRO);
            }
        }

        private void DrawScalarFieldValues(DynamicBuffer<ScalarFieldItem> scalarFieldBuffer, ScalarFieldInfos infos, LocalToWorld localToWorld, ScalarFieldSelectedCell selectedCell)
        {
            int index = 0;
            for (int y = 0; y < infos.GridSize.y; y++)
            {
                for (int z = 0; z < infos.GridSize.z; z++)
                {
                    for (int x = 0; x < infos.GridSize.x; x++)
                    {
                        if (index >= scalarFieldBuffer.Length)
                        {
                            return;
                        }

                        int3 cellPosition = new int3(x, y, z);
                        
                        // Ne dessiner que les cellules dans les limites définies
                        if (math.any(cellPosition < selectedCell.Min) || math.any(cellPosition > selectedCell.Max))
                        {
                            index++;
                            continue;
                        }

                        ScalarFieldItem scalarValue = scalarFieldBuffer[index];
                        ScalarFieldUtility.GetWorldPosition(cellPosition, infos.CellSize, infos.ScalarFieldOffset, out float3 localPosition);
                        float3 position = math.transform(localToWorld.Value, localPosition);
                        float value = scalarValue.Value;

                        // < 0 = noir, 0 = rouge, sbyte.MaxValue (127) = vert
                        Color color;
                        if (value < 0)
                        {
                            color = Color.black;
                        }
                        else
                        {
                            // Normaliser de [0, sbyte.MaxValue] vers [0, 1]
                            float t = value / sbyte.MaxValue;
                            // Lerp entre rouge (0) et vert (sbyte.MaxValue)
                            color = Color.Lerp(Color.red, Color.green, t);
                        }

                        Gizmos.color = color;
                        Gizmos.DrawSphere(position, 0.1f); // Utiliser une taille fixe de 0.1f

                        // Afficher l'index en texte
#if UNITY_EDITOR
                        // Calculer un offset qui s'adapte à la distance de la caméra
                        Vector3 worldPos = position;
                        Vector3 offset = Vector3.up * (HandleUtility.GetHandleSize(worldPos) * 0.3f);
                        Handles.Label(worldPos + offset, index.ToString());
#endif

                        index++;
                    }
                }
            }
        }
    }
}