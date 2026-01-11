# Copilot Instructions

## Project Description

This project aims to create 3D meshes using the Dual Contouring algorithm.

### Generation Pipeline
1. **Scalar Field**: Generate a 3D scalar field (value grid) representing the implicit surface
2. **Octree**: Convert the scalar field into an adaptive octree OR directly configure a parameterized octree
3. **Dual Contouring**: Transform the octree into a triangulated mesh

### Goal
Provide a ready-to-use solution for other Unity projects requiring procedural terrain generation.

## Technical Context
- Framework: Unity with ECS (Entity Component System)
- Language: C#
- Architecture: Unity Entities (DOTS)
- **Unity Entities Version**: 1.4.3 (com.unity.entities@1.4.3)

### Compilation
- Compilation is handled exclusively by Unity Editor
- Cannot be tested via standard dotnet CLI or MSBuild tools
- Code validation must rely on static analysis and IDE IntelliSense
- Runtime errors can only be verified by running the Unity Editor

### File Management
- **Never create .meta files** - Unity generates them automatically
- Only create .cs files and let Unity handle metadata

## Coding Rules

### C# Naming Conventions
- PascalCase for classes, methods, properties, interfaces
- camelCase for local variables and parameters
- PascalCase for public fields
- camelCase for private fields (no prefix)
- No Hungarian notation

### Comments
- Avoid obvious or redundant comments
- Only comment complex or non-obvious logic
- No comments for self-explanatory code
- Prefer clear variable and method names over comments

### Unity Entities Best Practices
- Use `[BurstCompile]` for compatible systems
- Prefer `RefRO<T>` and `RefRW<T>` for component access
- Use `DynamicBuffer<T>` for collections
- Use `SystemAPI.Query` for ECS queries
- Data types: prefer `float3`, `int3`, `quaternion` from Unity.Mathematics
- Use `partial struct` for systems implementing `ISystem`

### General Style
- Concise and readable code
- Explicit variable and method names
- Avoid comments like "this method does X" when the name already indicates it
- Prefer immutability when possible
- Write all code in English (variable names, method names, comments, etc.)

