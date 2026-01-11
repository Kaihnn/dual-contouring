using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    [BurstCompile]
    public static class QefSolver
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SolveQef(in NativeArray<float3> positions, in NativeArray<float3> normals, int count, in float3 massPoint, out float3 result)
        {
            BuildQefMatrices(positions, normals, count, massPoint, out float3x3 ata, out float3 atb);
            ApplyRegularization(ref ata, 0.001f);
            SolveLinearSystem3X3(in ata, in atb, out float3 offset);

            if (IsValidSolution(in offset))
            {
                result = massPoint + offset;
            }
            else
            {
                result = massPoint;
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BuildQefMatrices(
            in NativeArray<float3> positions,
            in NativeArray<float3> normals,
            int count,
            in float3 massPoint,
            out float3x3 ata,
            out float3 atb)
        {
            ata = float3x3.zero;
            atb = float3.zero;

            for (int i = 0; i < count; i++)
            {
                float3 n = normals[i];
                float3 p = positions[i];

                ata.c0 += n * n.x;
                ata.c1 += n * n.y;
                ata.c2 += n * n.z;

                float distance = math.dot(n, p - massPoint);
                atb += n * distance;
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyRegularization(ref float3x3 matrix, float regularization)
        {
            matrix.c0.x += regularization;
            matrix.c1.y += regularization;
            matrix.c2.z += regularization;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidSolution(in float3 solution)
        {
            return !math.any(math.isnan(solution)) && !math.any(math.isinf(solution));
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SolveLinearSystem3X3(in float3x3 a, in float3 b, out float3 result)
        {
            const float epsilon = 1e-10f;

            var row0 = new float3(a.c0.x, a.c1.x, a.c2.x);
            var row1 = new float3(a.c0.y, a.c1.y, a.c2.y);
            var row2 = new float3(a.c0.z, a.c1.z, a.c2.z);
            float3 rhs = b;

            GaussianEliminationColumn0(ref row0, ref row1, ref row2, ref rhs, epsilon);
            GaussianEliminationColumn1(ref row1, ref row2, ref rhs, epsilon);

            PerformBackSubstitution(in row0, in row1, in row2, in rhs, epsilon, out result);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GaussianEliminationColumn0(ref float3 row0, ref float3 row1, ref float3 row2, ref float3 rhs, float epsilon)
        {
            SelectPivotForColumn0(ref row0, ref row1, ref row2, ref rhs);

            if (math.abs(row0.x) > epsilon)
            {
                EliminateColumn0(ref row0, ref row1, ref row2, ref rhs);
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SelectPivotForColumn0(ref float3 row0, ref float3 row1, ref float3 row2, ref float3 rhs)
        {
            if (math.abs(row1.x) > math.abs(row0.x))
            {
                SwapRows(ref row0, ref row1, ref rhs.x, ref rhs.y);
            }

            if (math.abs(row2.x) > math.abs(row0.x))
            {
                SwapRows(ref row0, ref row2, ref rhs.x, ref rhs.z);
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EliminateColumn0(ref float3 row0, ref float3 row1, ref float3 row2, ref float3 rhs)
        {
            float factor1 = row1.x / row0.x;
            row1 -= row0 * factor1;
            rhs.y -= rhs.x * factor1;

            float factor2 = row2.x / row0.x;
            row2 -= row0 * factor2;
            rhs.z -= rhs.x * factor2;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GaussianEliminationColumn1(ref float3 row1, ref float3 row2, ref float3 rhs, float epsilon)
        {
            if (math.abs(row2.y) > math.abs(row1.y))
            {
                SwapRows(ref row1, ref row2, ref rhs.y, ref rhs.z);
            }

            if (math.abs(row1.y) > epsilon)
            {
                float factor = row2.y / row1.y;
                row2 -= row1 * factor;
                rhs.z -= rhs.y * factor;
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SwapRows(ref float3 row1, ref float3 row2, ref float rhsValue1, ref float rhsValue2)
        {
            float3 tempRow = row1;
            row1 = row2;
            row2 = tempRow;

            float tempRhs = rhsValue1;
            rhsValue1 = rhsValue2;
            rhsValue2 = tempRhs;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PerformBackSubstitution(in float3 row0, in float3 row1, in float3 row2, in float3 rhs, float epsilon, out float3 result)
        {
            result = float3.zero;

            if (math.abs(row2.z) > epsilon)
            {
                result.z = rhs.z / row2.z;
            }

            if (math.abs(row1.y) > epsilon)
            {
                result.y = (rhs.y - row1.z * result.z) / row1.y;
            }

            if (math.abs(row0.x) > epsilon)
            {
                result.x = (rhs.x - row0.y * result.y - row0.z * result.z) / row0.x;
            }
        }
    }
}

