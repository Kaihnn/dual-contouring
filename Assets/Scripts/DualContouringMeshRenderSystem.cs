using Unity.Entities;
using UnityEngine;

/// <summary>
///     Système qui rend le mesh généré par le dual contouring
///     Ce système s'exécute en dehors de Burst car il utilise l'API UnityEngine
/// </summary>
public partial class DualContouringMeshRenderSystem : SystemBase
{
    private Mesh _mesh;

    protected override void OnCreate()
    {
        base.OnCreate();
        
        // Créer un mesh réutilisable
        _mesh = new Mesh
        {
            name = "DualContouringMesh"
        };
        
        // Require un singleton DualContouringMaterialReference pour que le système s'exécute
        RequireForUpdate<DualContouringMaterialReference>();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        if (_mesh != null)
        {
            Object.Destroy(_mesh);
        }
    }

    protected override void OnUpdate()
    {
        // Récupérer le matériau depuis le singleton (composant managé)
        var materialRef = SystemAPI.GetSingleton<DualContouringMaterialReference>();
        
        // Mettre à jour le mesh avec les données générées
        foreach (var (vertexBuffer, triangleBuffer) in SystemAPI.Query<
                     DynamicBuffer<DualContouringMeshVertex>,
                     DynamicBuffer<DualContouringMeshTriangle>>())
        {
            UpdateMesh(vertexBuffer, triangleBuffer);
            
            // Dessiner le mesh avec le matériau du singleton
            Graphics.DrawMesh(_mesh, Matrix4x4.identity, materialRef.Material, 0);
        }
    }

    private void UpdateMesh(
        DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
        DynamicBuffer<DualContouringMeshTriangle> triangleBuffer)
    {
        if (vertexBuffer.Length == 0 || triangleBuffer.Length == 0)
        {
            _mesh.Clear();
            return;
        }

        _mesh.Clear();

        // Copier les vertices
        Vector3[] vertices = new Vector3[vertexBuffer.Length];
        Vector3[] normals = new Vector3[vertexBuffer.Length];
        
        for (int i = 0; i < vertexBuffer.Length; i++)
        {
            vertices[i] = vertexBuffer[i].Position;
            normals[i] = vertexBuffer[i].Normal;
        }

        // Copier les triangles
        int[] triangles = new int[triangleBuffer.Length];
        for (int i = 0; i < triangleBuffer.Length; i++)
        {
            triangles[i] = triangleBuffer[i].Index;
        }

        _mesh.vertices = vertices;
        _mesh.normals = normals;
        _mesh.triangles = triangles;
        
        // Recalculer les bounds pour le culling
        _mesh.RecalculateBounds();
    }
}

