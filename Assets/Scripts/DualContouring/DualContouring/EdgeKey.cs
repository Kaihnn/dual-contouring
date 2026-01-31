using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    /// <summary>
    /// Identifie une edge de manière unique dans la grille.
    /// Une edge est définie par ses deux sommets (start et end).
    /// Les sommets sont ordonnés pour garantir l'unicité.
    /// </summary>
    public struct EdgeKey : IEquatable<EdgeKey>
    {
        public int3 Start;
        public int3 End;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EdgeKey(int3 p1, int3 p2)
        {
            // Ordonner les points pour garantir l'unicité
            // Comparer d'abord X, puis Y, puis Z
            if (p1.x < p2.x || (p1.x == p2.x && p1.y < p2.y) || (p1.x == p2.x && p1.y == p2.y && p1.z < p2.z))
            {
                Start = p1;
                End = p2;
            }
            else
            {
                Start = p2;
                End = p1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(EdgeKey other)
        {
            return math.all(Start == other.Start) && math.all(End == other.End);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            // Hash robuste combinant les deux points
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Start.x;
                hash = hash * 31 + Start.y;
                hash = hash * 31 + Start.z;
                hash = hash * 31 + End.x;
                hash = hash * 31 + End.y;
                hash = hash * 31 + End.z;
                return hash;
            }
        }

        /// <summary>
        /// Obtient la direction principale de l'edge (0=X, 1=Y, 2=Z)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAxisDirection()
        {
            int3 diff = math.abs(End - Start);
            if (diff.x > 0) return 0; // Axe X
            if (diff.y > 0) return 1; // Axe Y
            return 2; // Axe Z
        }

        /// <summary>
        /// Obtient le centre de l'edge en coordonnées de grille
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 GetCenter()
        {
            return (new float3(Start) + new float3(End)) * 0.5f;
        }
    }
}
