# Dual Contouring Skill

Expert skill for dual contouring, LOD systems, and mesh generation with Unity DOTS/ECS.

## Usage

In OpenCode, this skill is automatically discovered. Invoke it with:

```
/skill dual-contouring
```

Or let the agent automatically load it when needed.

## What This Skill Provides

- Complete dual contouring algorithm implementation guidance
- QEF (Quadratic Error Function) solver patterns
- Octree-based LOD system architecture
- High-performance mesh generation with Unity DOTS/ECS
- Burst compilation optimization techniques
- Job System parallelization patterns

## Key Topics Covered

1. **Dual Contouring Algorithm**
   - Surface extraction from scalar fields
   - Edge intersection detection
   - Vertex positioning with QEF

2. **LOD Systems**
   - Adaptive octree subdivision
   - Stack-based traversal (no recursion)
   - Sign change detection

3. **Mesh Generation**
   - Three-phase pipeline (Init, Generate, Update)
   - Vertex merging and gap filling
   - Winding order correction
   - Quad-to-triangle conversion

4. **Unity DOTS/ECS Patterns**
   - System architecture with `ISystem`
   - Job patterns (`IJobEntity`, `IJobParallelFor`)
   - Component design (`IComponentData`, `IBufferElementData`)
   - Native collections management

5. **Performance Optimization**
   - Burst compilation best practices
   - Parallel scheduling strategies
   - Memory allocation patterns
   - SIMD-friendly code

## External References

This skill includes references to:

- **Boris The Brave's DC Tutorial**: https://www.boristhebrave.com/2018/04/15/dual-contouring-tutorial/
  - Theory and QEF mathematics
  
- **MC-DC Repository**: https://github.com/BorisTheBrave/mc-dc
  - Reference implementation in C#
  
- **Transvoxel Algorithm**: https://transvoxel.org/
  - Seamless LOD transitions
  - Crack-free mesh generation

## When to Use

Load this skill when:
- Implementing dual contouring from scratch
- Optimizing existing DC code with Unity DOTS
- Debugging mesh generation issues
- Building LOD systems for voxel/terrain
- Converting marching cubes to dual contouring
- Solving QEF stability problems

## Compatibility

- **Unity Version**: 2022.3+
- **Entities Package**: 1.4.3+
- **Burst Compiler**: 1.8+
- **Mathematics**: 1.3+

## License

MIT
