using NUnit.Framework;
using Unity.Mathematics;
using DualContouring.ScalarField;

namespace DualContouring.Tests.EditMode
{
    public class ScalarFieldUtilityTests
    {
        [Test]
        public void CoordToIndex_ValidCoord_ReturnsCorrectIndex()
        {
            int3 gridSize = new int3(4, 4, 4);

            int index = ScalarFieldUtility.CoordToIndex(0, 0, 0, gridSize);
            Assert.AreEqual(0, index);

            index = ScalarFieldUtility.CoordToIndex(1, 0, 0, gridSize);
            Assert.AreEqual(1, index);

            index = ScalarFieldUtility.CoordToIndex(0, 0, 1, gridSize);
            Assert.AreEqual(4, index);

            index = ScalarFieldUtility.CoordToIndex(0, 1, 0, gridSize);
            Assert.AreEqual(16, index);
        }

        [Test]
        public void CoordToIndex_OutOfBounds_ReturnsNegativeOne()
        {
            int3 gridSize = new int3(4, 4, 4);

            Assert.AreEqual(-1, ScalarFieldUtility.CoordToIndex(-1, 0, 0, gridSize));
            Assert.AreEqual(-1, ScalarFieldUtility.CoordToIndex(0, -1, 0, gridSize));
            Assert.AreEqual(-1, ScalarFieldUtility.CoordToIndex(0, 0, -1, gridSize));
            Assert.AreEqual(-1, ScalarFieldUtility.CoordToIndex(4, 0, 0, gridSize));
            Assert.AreEqual(-1, ScalarFieldUtility.CoordToIndex(0, 4, 0, gridSize));
            Assert.AreEqual(-1, ScalarFieldUtility.CoordToIndex(0, 0, 4, gridSize));
        }

        [Test]
        public void CoordToIndex_Int3Overload_MatchesComponentOverload()
        {
            int3 gridSize = new int3(5, 6, 7);
            int3 coord = new int3(2, 3, 4);

            int indexFromComponents = ScalarFieldUtility.CoordToIndex(coord.x, coord.y, coord.z, gridSize);
            int indexFromInt3 = ScalarFieldUtility.CoordToIndex(coord, gridSize);

            Assert.AreEqual(indexFromComponents, indexFromInt3);
        }

        [Test]
        public void IndexToCoord_ValidIndex_ReturnsCorrectCoord()
        {
            int3 gridSize = new int3(4, 4, 4);

            ScalarFieldUtility.IndexToCoord(0, gridSize, out float3 coord0);
            Assert.AreEqual(new float3(0, 0, 0), coord0);

            ScalarFieldUtility.IndexToCoord(1, gridSize, out float3 coord1);
            Assert.AreEqual(new float3(1, 0, 0), coord1);

            ScalarFieldUtility.IndexToCoord(4, gridSize, out float3 coord4);
            Assert.AreEqual(new float3(0, 0, 1), coord4);

            ScalarFieldUtility.IndexToCoord(16, gridSize, out float3 coord16);
            Assert.AreEqual(new float3(0, 1, 0), coord16);
        }

        [Test]
        public void CoordToIndex_And_IndexToCoord_AreInverse()
        {
            int3 gridSize = new int3(5, 6, 7);

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    for (int x = 0; x < gridSize.x; x++)
                    {
                        int3 originalCoord = new int3(x, y, z);
                        int index = ScalarFieldUtility.CoordToIndex(originalCoord, gridSize);
                        ScalarFieldUtility.IndexToCoord(index, gridSize, out float3 recoveredCoord);

                        Assert.AreEqual(originalCoord.x, (int)recoveredCoord.x);
                        Assert.AreEqual(originalCoord.y, (int)recoveredCoord.y);
                        Assert.AreEqual(originalCoord.z, (int)recoveredCoord.z);
                    }
                }
            }
        }

        [Test]
        public void GetWorldPosition_ReturnsCorrectPosition()
        {
            int3 coord = new int3(2, 3, 4);
            float cellSize = 0.5f;
            float3 offset = new float3(10, 20, 30);

            ScalarFieldUtility.GetWorldPosition(coord, cellSize, offset, out float3 worldPos);

            Assert.AreEqual(11f, worldPos.x, 0.0001f);
            Assert.AreEqual(21.5f, worldPos.y, 0.0001f);
            Assert.AreEqual(32f, worldPos.z, 0.0001f);
        }

        [Test]
        public void IsInBounds_ValidCoord_ReturnsTrue()
        {
            int3 gridSize = new int3(4, 5, 6);

            Assert.IsTrue(ScalarFieldUtility.IsInBounds(new int3(0, 0, 0), gridSize));
            Assert.IsTrue(ScalarFieldUtility.IsInBounds(new int3(3, 4, 5), gridSize));
            Assert.IsTrue(ScalarFieldUtility.IsInBounds(new int3(2, 2, 2), gridSize));
        }

        [Test]
        public void IsInBounds_OutOfBounds_ReturnsFalse()
        {
            int3 gridSize = new int3(4, 5, 6);

            Assert.IsFalse(ScalarFieldUtility.IsInBounds(new int3(-1, 0, 0), gridSize));
            Assert.IsFalse(ScalarFieldUtility.IsInBounds(new int3(0, -1, 0), gridSize));
            Assert.IsFalse(ScalarFieldUtility.IsInBounds(new int3(0, 0, -1), gridSize));
            Assert.IsFalse(ScalarFieldUtility.IsInBounds(new int3(4, 0, 0), gridSize));
            Assert.IsFalse(ScalarFieldUtility.IsInBounds(new int3(0, 5, 0), gridSize));
            Assert.IsFalse(ScalarFieldUtility.IsInBounds(new int3(0, 0, 6), gridSize));
        }
    }
}
