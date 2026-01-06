using Unity.Mathematics;

/// <summary>
///     Utilitaires pour manipuler le champ scalaire
/// </summary>
public static class ScalarFieldUtility
{
    /// <summary>
    ///     Convertit des coordonnées 3D en index 1D dans le buffer
    /// </summary>
    /// <param name="x">Coordonnée X</param>
    /// <param name="y">Coordonnée Y</param>
    /// <param name="z">Coordonnée Z</param>
    /// <param name="gridSize">Taille de la grille (par défaut 3x3x3)</param>
    /// <returns>Index dans le buffer, ou -1 si hors limites</returns>
    public static int CoordToIndex(int x, int y, int z, int3 gridSize)
    {
        if (x < 0 || x >= gridSize.x ||
            y < 0 || y >= gridSize.y ||
            z < 0 || z >= gridSize.z)
        {
            return -1;
        }

        return x + z * gridSize.x + y * gridSize.x * gridSize.z;
    }

    /// <summary>
    ///     Convertit des coordonnées 3D en index 1D dans le buffer
    /// </summary>
    /// <param name="coord">Coordonnées 3D</param>
    /// <param name="gridSize">Taille de la grille (par défaut 3x3x3)</param>
    /// <returns>Index dans le buffer, ou -1 si hors limites</returns>
    public static int CoordToIndex(int3 coord, int3 gridSize)
    {
        return CoordToIndex(coord.x, coord.y, coord.z, gridSize);
    }

    /// <summary>
    ///     Convertit un index 1D en coordonnées 3D
    /// </summary>
    /// <param name="index">Index dans le buffer</param>
    /// <param name="gridSize">Taille de la grille (par défaut 3x3x3)</param>
    /// <returns>Coordonnées 3D</returns>
    public static int3 IndexToCoord(int index, int3 gridSize)
    {
        int layerSize = gridSize.x * gridSize.z;
        int y = index / layerSize;
        int remainder = index % layerSize;
        int z = remainder / gridSize.x;
        int x = remainder % gridSize.x;

        return new int3(x, y, z);
    }

    /// <summary>
    ///     Vérifie si les coordonnées sont dans les limites de la grille
    /// </summary>
    /// <param name="coord">Coordonnées 3D</param>
    /// <param name="gridSize">Taille de la grille</param>
    /// <returns>True si dans les limites, false sinon</returns>
    public static bool IsInBounds(int3 coord, int3 gridSize)
    {
        return coord.x >= 0 && coord.x < gridSize.x &&
               coord.y >= 0 && coord.y < gridSize.y &&
               coord.z >= 0 && coord.z < gridSize.z;
    }
}