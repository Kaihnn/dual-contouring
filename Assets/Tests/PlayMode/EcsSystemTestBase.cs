using System;
using NUnit.Framework;
using Unity.Entities;

namespace DualContouring.Tests.PlayMode
{
    public abstract class EcsSystemTestBase
    {
        protected World World { get; private set; }
        protected EntityManager EntityManager => World.EntityManager;

        [SetUp]
        public virtual void SetUp()
        {
            World = new World("TestWorld");
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (World != null && World.IsCreated)
            {
                World.Dispose();
            }
        }

        protected SystemHandle GetOrCreateSystem<T>() where T : unmanaged, ISystem
        {
            return World.GetOrCreateSystem<T>();
        }

        protected void UpdateSystem<T>() where T : unmanaged, ISystem
        {
            var systemHandle = World.GetOrCreateSystem<T>();
            systemHandle.Update(World.Unmanaged);
        }

        protected Entity CreateEntity(params ComponentType[] types)
        {
            return EntityManager.CreateEntity(types);
        }

        protected void AddComponent<T>(Entity entity, T component) where T : unmanaged, IComponentData
        {
            EntityManager.AddComponentData(entity, component);
        }

        protected T GetComponent<T>(Entity entity) where T : unmanaged, IComponentData
        {
            return EntityManager.GetComponentData<T>(entity);
        }

        protected DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            return EntityManager.AddBuffer<T>(entity);
        }

        protected DynamicBuffer<T> GetBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            return EntityManager.GetBuffer<T>(entity);
        }
    }
}
