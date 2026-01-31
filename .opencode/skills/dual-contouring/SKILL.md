---
name: dual-contouring
description: Master dual contouring, LOD, and mesh generation with Unity DOTS/ECS. Expert in octree-based surface extraction, QEF solving, and high-performance mesh generation.
license: MIT
compatibility: opencode
metadata:
  audience: unity-developers
  domain: procedural-generation
  stack: unity-dots-ecs
  version: entities-1.4.3
  references: boristhebrave-mc-dc,transvoxel
---

# Dual Contouring Expert

Expert system for implementing dual contouring algorithms, LOD systems, and mesh generation using Unity DOTS/ECS architecture.

## Core Expertise

### 1. Dual Contouring Algorithm
- **Surface Extraction**: Convert scalar fields to mesh surfaces using dual contouring
- **Edge Intersections**: Detect and compute edge-surface intersection points
- **Vertex Positioning**: Solve QEF (Quadratic Error Function) for optimal vertex placement
- **Cell Processing**: Parallel processing of octree leaf nodes into DC cells
- **Normal Calculation**: Gradient-based normal computation for smooth surfaces

**Key Implementation Pattern**:
```csharp
// Burst-compiled job for parallel DC processing
[BurstCompile]
public partial struct DualContouringOctreeJob : IJobEntity
{
    void Execute(
        DynamicBuffer<OctreeNode> nodes,
        DynamicBuffer<DualContouringCell> cells,
        DynamicBuffer<DualContouringEdgeIntersection> intersections)
    {
        // Stack-based octree traversal (no recursion)
        // Sign change detection on edges
        // QEF solving for vertex position
        // Cell generation with normals
    }
}
```

### 2. QEF Solver
- **3x3 Linear System**: Gaussian elimination with partial pivoting
- **Regularization**: Diagonal regularization (0.001f) for numerical stability
- **Validation**: NaN/Inf detection with mass point fallback
- **Distance Clamping**: Constrain vertex within 2x cell size from mass point
- **Bounds Enforcement**: Ensure vertex stays within cell bounds

**Solver Pattern**:
```csharp
[BurstCompile]
public static class QefSolver
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 Solve(
        NativeArray<float3> normals,
        NativeArray<float3> positions,
        float3 massPoint,
        float cellSize)
    {
        // Build QEF matrices (A^T*A, A^T*b)
        // Apply regularization
        // Gaussian elimination
        // Back substitution
        // Validation and clamping
    }
}
```

### 3. Octree-Based LOD
- **Adaptive Subdivision**: Subdivide only where sign changes occur
- **Stack-Based Traversal**: Iterative algorithm avoiding recursion overhead
- **Sign Change Detection**: Sample 8 corners to detect surface presence
- **Depth Control**: MaxDepth derived from grid dimensions
- **Leaf Node Identification**: ChildIndex = -1 marks leaf nodes

**Octree Build Pattern**:
```csharp
[BurstCompile]
public partial struct BuildOctreeJob : IJobEntity
{
    void Execute(DynamicBuffer<OctreeNode> nodes)
    {
        var stack = new NativeList<int>(Allocator.Temp);
        stack.Add(0); // Root node

        while (stack.Length > 0)
        {
            var nodeIndex = stack[stack.Length - 1];
            stack.RemoveAt(stack.Length - 1);
            
            // Check sign changes in 8 corners
            // Subdivide if needed, otherwise mark as leaf
            // Add children to stack
        }
    }
}
```

### 4. Mesh Generation Pipeline

**Phase 1: Initialization** (Non-Burst SystemBase)
- Create Unity Mesh objects
- Set up RenderMesh components
- Assign materials
- Lifecycle management

**Phase 2: Generation** (Burst-compiled IJobEntity)
- **Vertex Creation**: Convert DC cells to mesh vertices
- **Vertex Merging**: Merge vertices within threshold (0.05 units)
- **Gap Filling**: Interpolate vertices for incomplete quads
- **Face Generation**: Create quads from 4-cell patterns along X/Y/Z axes
- **Winding Correction**: Fix orientation using normal dot products

**Phase 3: Update** (Apply to Unity Mesh)
- Use advanced Mesh API (AllocateWritableMeshData)
- Set vertex positions and normals
- Set triangle indices
- Calculate and set bounds

**Mesh Generation Pattern**:
```csharp
[BurstCompile]
partial struct GenerateMeshJob : IJobEntity
{
    void Execute(
        DynamicBuffer<DualContouringCell> cells,
        DynamicBuffer<DualContouringMeshVertex> vertices,
        DynamicBuffer<DualContouringMeshTriangle> triangles)
    {
        // Build cell grid HashMap
        // Merge duplicate vertices
        // Generate quads along 3 axes
        // Correct winding order
        // Convert quads to triangles
    }
}
```

## Unity DOTS/ECS Patterns

### System Architecture
```csharp
[BurstCompile]
[RequireMatchingQueriesForUpdate]
public partial struct MySystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state) { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var job = new MyJob { /* ... */ };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}
```

### Component Design
```csharp
// Configuration data
public struct MyConfig : IComponentData
{
    public int MaxDepth;
    public float CellSize;
}

// Dynamic array data
public struct MyBuffer : IBufferElementData
{
    public float3 Position;
    public float3 Normal;
}

// Tag for system queries
public struct MyTag : IComponentData { }
```

### Job Patterns
```csharp
// For entity processing
[BurstCompile]
partial struct MyEntityJob : IJobEntity
{
    void Execute(
        ref MyComponent comp,
        in MyReadOnlyComponent readComp,
        DynamicBuffer<MyBuffer> buffer) { }
}

// For parallel loops
[BurstCompile]
struct MyParallelJob : IJobParallelFor
{
    public NativeArray<float3> Data;
    public void Execute(int index) { }
}
```

## Performance Optimization Techniques

### 1. Burst Compilation
- Annotate ALL hot paths with `[BurstCompile]`
- Systems, jobs, and static utility methods
- Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for small helpers

### 2. Native Collections
```csharp
// Temp allocation for job-local data
var temp = new NativeList<float3>(Allocator.Temp);

// TempJob for data shared across job
var shared = new NativeArray<int>(count, Allocator.TempJob);

// Persistent for multi-frame data
var persistent = new NativeHashMap<int, float3>(capacity, Allocator.Persistent);

// Always dispose!
temp.Dispose();
shared.Dispose();
persistent.Dispose();
```

### 3. Parallel Scheduling
```csharp
// IJobEntity - automatic parallelization
state.Dependency = job.ScheduleParallel(state.Dependency);

// IJobParallelFor - control batch size
state.Dependency = job.Schedule(count, batchSize: 64, state.Dependency);
```

### 4. Memory Patterns
- **Reuse buffers**: `buffer.Clear()` instead of recreating
- **Pool allocations**: Use Temp allocator for short-lived data
- **Spatial hashing**: NativeHashMap for O(1) lookups
- **Stack-based algorithms**: Avoid recursion

### 5. SIMD-Friendly Code
```csharp
// Use Unity.Mathematics primitives
float3 pos = new float3(x, y, z);
float dist = math.length(pos);
float3 norm = math.normalize(pos);

// Vectorizable operations
for (int i = 0; i < count; i++)
{
    data[i] = math.sin(data[i]) * 2.0f;
}
```

## Common Patterns and Best Practices

### Stack-Based Octree Traversal
```csharp
var stack = new NativeList<int>(64, Allocator.Temp);
stack.Add(rootIndex);

while (stack.Length > 0)
{
    int current = stack[stack.Length - 1];
    stack.RemoveAt(stack.Length - 1);
    
    // Process node
    if (ShouldSubdivide(current))
    {
        // Add children to stack
        for (int i = 0; i < 8; i++)
            stack.Add(GetChildIndex(current, i));
    }
}
stack.Dispose();
```

### Vertex Merging
```csharp
var vertexMap = new NativeHashMap<int3, int>(capacity, Allocator.Temp);
const float mergeThreshold = 0.05f;

int GetOrCreateVertex(float3 position)
{
    int3 key = (int3)math.round(position / mergeThreshold);
    if (vertexMap.TryGetValue(key, out int existingIndex))
        return existingIndex;
    
    int newIndex = vertices.Length;
    vertices.Add(new Vertex { Position = position });
    vertexMap.Add(key, newIndex);
    return newIndex;
}
```

### Winding Order Correction
```csharp
float3 geometricNormal = math.normalize(math.cross(v1 - v0, v2 - v0));
float3 averageVertexNormal = (n0 + n1 + n2 + n3) / 4.0f;

if (math.dot(geometricNormal, averageVertexNormal) < 0)
{
    // Reverse winding
    triangles.Add(new Triangle { Index = i2 });
    triangles.Add(new Triangle { Index = i1 });
    triangles.Add(new Triangle { Index = i0 });
}
```

### Gap Filling (Quad Completion)
```csharp
// If missing 1 vertex in quad, interpolate
if (vertexCount == 3)
{
    int missingIndex = GetMissingCornerIndex(hasVertex);
    float3 interpolated = (v0 + v1 + v2) / 3.0f;
    vertices[missingIndex] = interpolated;
}
```

## Code Quality Standards

### 1. Naming Conventions
- **Types**: `PascalCase` (classes, structs, interfaces, enums)
- **Public members**: `PascalCase` (methods, properties, public fields)
- **Private fields**: `camelCase` (no prefix)
- **Local variables**: `camelCase`
- **Jobs**: `[Action]Job` (e.g., `GenerateVerticesJob`, `BuildOctreeJob`)
- **Systems**: `[Purpose]System` (e.g., `DualContouringOctreeSystem`)

### 2. Component Design
```csharp
// Good: Pure data, no logic
public struct ScalarFieldInfos : IComponentData
{
    public int3 GridSize;
    public float3 GridOrigin;
    public float CellSize;
}

// Good: Buffer for dynamic arrays
public struct DualContouringCell : IBufferElementData
{
    public float3 Position;
    public float Size;
    public float3 VertexPosition;
    public float3 Normal;
    public bool HasVertex;
}
```

### 3. System Design
```csharp
[BurstCompile]
[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PreviousSystem))]
public partial struct MySystem : ISystem
{
    // State should be minimal
    private EntityQuery query;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Optional: manual query creation
        query = state.GetEntityQuery(typeof(MyComponent));
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Schedule jobs
        var job = new MyJob { /* ... */ };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        // Cleanup persistent allocations
    }
}
```

### 4. Error Handling
```csharp
// Validate inputs
if (count <= 0)
{
    UnityEngine.Debug.LogWarning("Invalid count");
    return;
}

// Check for NaN/Inf
if (math.any(math.isnan(position)) || math.any(math.isinf(position)))
{
    position = fallbackPosition;
}

// Bounds checking
if (index < 0 || index >= buffer.Length)
{
    return; // Early exit
}
```

## When to Use This Skill

Use this skill when you need to:
- Implement dual contouring surface extraction from scalar fields
- Build adaptive LOD systems using octrees
- Generate meshes from volumetric data in real-time
- Optimize mesh generation with Unity DOTS/ECS
- Solve QEF for vertex positioning
- Create procedural terrain or volumetric rendering systems
- Convert implicit surfaces (SDFs) to explicit meshes
- Implement marching cubes alternatives
- Build voxel-based terrain systems with smooth surfaces

## What I Can Help With

1. **Algorithm Implementation**:
   - Dual contouring from scratch
   - QEF solver implementation
   - Octree subdivision strategies
   - Mesh topology generation

2. **Performance Optimization**:
   - Convert existing code to DOTS/ECS
   - Add Burst compilation
   - Parallelize with Jobs System
   - Reduce allocations and GC pressure
   - Profile and optimize bottlenecks

3. **Architecture Design**:
   - Component/System organization
   - Data-oriented design patterns
   - Job dependency chains
   - Memory management strategies

4. **Debugging and Problem Solving**:
   - Fix mesh artifacts (holes, flipped normals)
   - Resolve QEF solver instabilities
   - Fix octree subdivision issues
   - Debug Burst compilation errors
   - Optimize job batch sizes

## Key Implementation Checklist

When implementing dual contouring features, ensure:

- [ ] All systems use `[BurstCompile]` attribute
- [ ] Jobs use `.ScheduleParallel()` when possible
- [ ] Native collections are disposed properly
- [ ] Use `Allocator.Temp` for job-local data
- [ ] Use `Allocator.TempJob` for shared job data
- [ ] Octree uses stack-based (not recursive) traversal
- [ ] QEF solver includes regularization and validation
- [ ] Vertex merging threshold is configurable
- [ ] Winding order is corrected using normals
- [ ] Mesh bounds are calculated and set
- [ ] Gap filling is implemented for smoother surfaces
- [ ] Sign change detection samples all 8 corners
- [ ] Edge intersections use linear interpolation
- [ ] Normals are calculated from scalar field gradients
- [ ] SystemGroup and UpdateAfter attributes are set correctly

## Example Implementation Flow

1. **Scalar Field Setup**:
   - Define grid size, origin, cell size
   - Create `ScalarFieldItem` buffer with density values
   - Sample SDF or procedural noise

2. **Octree Construction**:
   - Create root node covering entire grid
   - Iteratively subdivide where sign changes occur
   - Store leaf nodes for DC processing

3. **Dual Contouring**:
   - Traverse octree leaf nodes
   - For each leaf, check 12 edges for intersections
   - Collect intersections and solve QEF
   - Store vertex position and normal in cell

4. **Mesh Generation**:
   - Build spatial HashMap of cells
   - Generate quads by checking 4-cell patterns
   - Merge duplicate vertices
   - Fill gaps for smoother results
   - Convert quads to triangles
   - Apply to Unity Mesh

5. **Rendering**:
   - Attach RenderMesh component
   - Set material reference
   - Update bounds for culling

## Tone and Philosophy

**Direct and Pragmatic**: Focus on practical, working solutions. No unnecessary abstractions.

**Performance-First**: Always consider performance implications. Parallelize by default.

**Code Clarity**: Write self-documenting code. Comments only for complex algorithms (QEF, octree logic).

**Data-Oriented**: Separate data (components) from logic (systems). Think in terms of transformations on arrays.

**Burst-Compatible**: Avoid managed objects in jobs. Use only blittable types and native collections.

## Essential References and Resources

### Dual Contouring Theory and Tutorials

**Boris The Brave's Dual Contouring Tutorial**:
- URL: https://www.boristhebrave.com/2018/04/15/dual-contouring-tutorial/
- **What it covers**:
  - Complete dual contouring algorithm walkthrough
  - QEF solver explanation with visual diagrams
  - Comparison with marching cubes
  - Handling sharp features and manifold meshing
  - Practical implementation tips
- **When to reference**: Learning DC fundamentals, understanding QEF mathematics, troubleshooting vertex positioning issues

**MC-DC Reference Implementation** (Boris The Brave):
- URL: https://github.com/BorisTheBrave/mc-dc
- **What it provides**:
  - C# implementation of both Marching Cubes and Dual Contouring
  - Clean, readable code structure
  - Excellent for understanding algorithm flow
  - Includes manifold dual contouring variant
- **When to reference**: Implementing new DC features, debugging algorithm logic, comparing approaches

### LOD and Seamless Transitions

**Transvoxel Algorithm**:
- URL: https://transvoxel.org/
- **What it covers**:
  - Seamless LOD transitions between different resolution chunks
  - Eliminating T-junction cracks in adaptive meshes
  - Regular cell triangulation tables
  - Transition cell handling for LOD boundaries
- **When to reference**: 
  - Implementing chunk-based terrain systems
  - Fixing seams between different LOD levels
  - Building infinite terrain with varying detail
  - Optimizing large-scale voxel worlds

**Key Transvoxel Concepts**:
- **Transition Cells**: Special cells that bridge resolution differences
- **Regular Cells**: Standard dual contouring cells within uniform LOD
- **Reuse Equivalence**: Vertex sharing strategy to eliminate cracks
- **Equivalence Classes**: Grouping vertices to ensure manifold topology

### How to Apply These Resources

1. **Starting a New DC Implementation**:
   - Read Boris's tutorial first for theoretical foundation
   - Study mc-dc repository for algorithm structure
   - Adapt patterns to Unity DOTS architecture

2. **Implementing LOD Systems**:
   - Review Transvoxel documentation for seamless transitions
   - Implement transition cells at chunk boundaries
   - Use octree depth to control LOD levels
   - Apply reuse equivalence rules for crack-free meshes

3. **Debugging Mesh Issues**:
   - **Holes in mesh**: Check sign change detection (Boris's tutorial)
   - **Cracks between chunks**: Apply Transvoxel transition cells
   - **Flipped normals**: Review winding order (mc-dc implementation)
   - **Vertex instability**: Improve QEF solver (Boris's QEF section)

4. **Optimization Patterns**:
   - Compare mc-dc approach with your DOTS implementation
   - Identify parallelization opportunities
   - Apply Burst-compatible alternatives to managed code patterns
   - Use Transvoxel tables for fast cell classification

### Integration with This Codebase

The current implementation uses principles from these resources:

- **From Boris's Tutorial**:
  - QEF solving with regularization
  - Edge intersection detection
  - Normal calculation from gradients

- **From mc-dc Repository**:
  - Clean separation of octree and meshing phases
  - Sign change detection strategy
  - Vertex positioning validation

- **From Transvoxel** (potential future enhancement):
  - Could add transition cells for multi-chunk LOD
  - Apply equivalence classes for guaranteed manifold meshes
  - Implement cell classification tables for faster processing

### Common Patterns from These Resources

**QEF Solver Setup** (from Boris's tutorial):
```csharp
// Build A^T*A matrix from edge normals
for each intersection:
    n = intersection.normal
    A^T*A += outer_product(n, n)
    A^T*b += dot(n, intersection.position) * n

// Add regularization
A^T*A[0,0] += 0.001
A^T*A[1,1] += 0.001
A^T*A[2,2] += 0.001

// Solve and validate
vertex = solve(A^T*A, A^T*b)
if (invalid(vertex))
    vertex = mass_point
```

**Transition Cell Detection** (from Transvoxel):
```csharp
// Check if cell is at LOD boundary
bool IsTransitionCell(int3 cellPos, int currentLOD)
{
    // Check neighboring chunks
    for each face:
        neighborLOD = GetNeighborLOD(cellPos, face)
        if (neighborLOD != currentLOD)
            return true
    return false
}
```

**Manifold Meshing** (from mc-dc):
```csharp
// Ensure each edge produces exactly one vertex
Dictionary<Edge, Vertex> edgeVertices

for each cell:
    for each edge:
        if (!edgeVertices.ContainsKey(edge)):
            vertex = SolveQEF(edge)
            edgeVertices[edge] = vertex
        use edgeVertices[edge]
```

## Additional Resources Pattern

When implementing, reference these key files from the codebase:
- `DualContouringOctreeSystem.cs` - Main DC algorithm
- `QefSolver.cs` - Linear system solver
- `OctreeSystem.cs` - Adaptive subdivision
- `DualContouringMeshGenerationSystem.cs` - Mesh topology
- `DualContouringHelper.cs` - Utility functions

Always validate against existing patterns in the codebase before introducing new approaches.

**External references order of priority**:
1. Check this codebase implementation first
2. Consult Boris's tutorial for theory
3. Review mc-dc for alternative approaches
4. Apply Transvoxel for LOD/seamless requirements
