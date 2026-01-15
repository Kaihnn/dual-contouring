# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity ECS (DOTS) implementation of the Dual Contouring algorithm for procedural 3D mesh generation. The project provides a ready-to-use solution for procedural terrain generation in Unity projects.

## Technical Stack

- **Framework**: Unity with ECS (Entity Component System)
- **Architecture**: Unity Entities (DOTS) 1.4.3
- **Language**: C#
- **Compilation**: Unity Editor only (no dotnet CLI or MSBuild)

## Build and Testing

Compilation is handled exclusively by Unity Editor. Code validation relies on static analysis and IDE IntelliSense. Runtime errors can only be verified by running the Unity Editor.

## Architecture

### Generation Pipeline

1. **Scalar Field** (`DualContouring.ScalarField`) - 3D value grid representing the implicit surface
2. **Octree** (`DualContouring.Octrees`) - Adaptive spatial subdivision from scalar field
3. **Dual Contouring** (`DualContouring.DualContouring`) - Transforms data into triangulated mesh
4. **Mesh Generation** (`DualContouring.MeshGeneration`) - Final mesh output

### Key Components

**Data Structures** (ECS Components):
- `ScalarFieldItem` / `ScalarFieldInfos` - Scalar field values and metadata
- `OctreeNode` / `OctreeInfos` - Octree nodes with Value, ChildIndex, Position
- `DualContouringCell` - Cells containing vertex position, normal, and surface crossing info
- `DualContouringMeshVertex` / `DualContouringMeshTriangle` - Final mesh data

**Systems** (ECS Systems with `[BurstCompile]`):
- `OctreeSystem` - Builds octree from scalar field via `BuildOctreeJob`
- `DualContouringSystem` - Processes scalar field directly (when `DualContouringType.ScalarField`)
- `DualContouringOctreeSystem` - Processes octree data (when `DualContouringType.Octree`)
- `DualContouringMeshGenerationSystem` - Generates mesh triangles from cells
- `DualContouringMeshUpdateSystem` / `DualContouringMeshInitializationSystem` - Mesh lifecycle

**Core Algorithm**:
- `DualContouringHelper` - Edge intersection detection, normal calculation, vertex positioning
- `QefSolver` - Quadric Error Function solver using Gaussian elimination for optimal vertex placement

### Processing Modes

Controlled by `DualContouringOptions.Type`:
- `DualContouringType.ScalarField` - Direct processing of scalar field grid
- `DualContouringType.Octree` - Processing via adaptive octree structure

## Coding Conventions

### Naming
- PascalCase: classes, methods, properties, interfaces, public fields
- camelCase: local variables, parameters, private fields (no prefix)
- No Hungarian notation

### Unity Entities Patterns
- Use `[BurstCompile]` for compatible systems
- Use `partial struct` for systems implementing `ISystem`
- Prefer `RefRO<T>` and `RefRW<T>` for component access
- Use `DynamicBuffer<T>` for collections
- Use `SystemAPI.Query` for ECS queries
- Prefer `float3`, `int3`, `quaternion` from Unity.Mathematics

### Burst Compilation Rules
**IMPORTANT**: In Burst-compiled code:

1. **Struct parameters** (`int3`, `float3`, etc.) must use `in`, `ref`, or `out` keywords
2. **Struct return types** are NOT allowed - use `out` parameter instead

```csharp
// WRONG - BC1064 errors
private static int CoordToIndex(int3 coord, int3 gridSize)
public static int3 WorldPositionToGrid(in float3 pos)

// CORRECT
private static int CoordToIndex(in int3 coord, in int3 gridSize)
public static void WorldPositionToGrid(in float3 pos, out int3 gridPos)
```

This applies to all static methods in `[BurstCompile]` classes/structs.

### File Management
- Never create .meta files - Unity generates them automatically
- Only create .cs files

### Comments
- Only comment complex or non-obvious logic
- Prefer clear variable and method names over comments
