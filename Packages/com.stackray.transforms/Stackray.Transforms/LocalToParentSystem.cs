using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Stackray.Collections;

namespace Stackray.Transforms {
  public abstract class LocalToParentSystem : JobComponentSystem {
    private EntityQuery m_rootsQuery;
    private EntityQuery m_allChildrenQuery;
    private NativeList<ChildInfo> m_childrenEntities;
    private NativeCounter m_topLevelChildrenCounter;

    struct ChildInfo {
      public Entity Child;
      public float4x4 ParentLocalToWorld;
    }

    // LocalToWorld = Parent.LocalToWorld * LocalToParent
    [BurstCompile]
    struct UpdateHierarchy : IJobParallelFor {
      [ReadOnly]
      public BufferFromEntity<Child> ChildFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<LocalToParent> LocalToParentFromEntity;
      [ReadOnly]
      public NativeArray<ChildInfo> Children;
      [NativeDisableContainerSafetyRestriction]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;

      void ChildLocalToWorld(float4x4 parentLocalToWorld, Entity entity) {
        var localToParent = LocalToParentFromEntity[entity];
        var localToWorldMatrix = math.mul(parentLocalToWorld, localToParent.Value);
        LocalToWorldFromEntity[entity] = new LocalToWorld { Value = localToWorldMatrix };

        if (ChildFromEntity.HasComponent(entity)) {
          var children = ChildFromEntity[entity];
          for (int i = 0; i < children.Length; i++) {
            ChildLocalToWorld(localToWorldMatrix, children[i].Value);
          }
        }
      }

      public void Execute(int index) {
        var childInfo = Children[index];
        var childEntity = childInfo.Child;
        if (childEntity == Entity.Null)
          return;
        var localToWorldMatrix = childInfo.ParentLocalToWorld;
        ChildLocalToWorld(localToWorldMatrix, childEntity);
      }
    }

    [BurstCompile]
    struct ExtractChildren : IJobChunk {
      [ReadOnly] public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
      [ReadOnly] public BufferTypeHandle<Child> ChildType;
      [ReadOnly] public BufferFromEntity<Child> ChildFromEntity;

      [WriteOnly]
      [NativeDisableParallelForRestriction]
      public NativeList<ChildInfo> TopLevelChildren;

      public uint LastSystemVersion;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!chunk.DidChange(LocalToWorldType, LastSystemVersion) &&
            !chunk.DidChange(ChildType, LastSystemVersion))
          return;

        var chunkLocalToWorld = chunk.GetNativeArray(LocalToWorldType);
        var chunkChildren = chunk.GetBufferAccessor(ChildType);
        for (var i = 0; i < chunk.Count; i++) {
          var localToWorldMatrix = chunkLocalToWorld[i].Value;
          var children = chunkChildren[i];
          for (var j = 0; j < children.Length; j++)
            TopLevelChildren[firstEntityIndex + i + j] = new ChildInfo { Child = children[j].Value, ParentLocalToWorld = localToWorldMatrix };
        }
      }
    }

    protected override void OnCreate() {
      m_rootsQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[]
          {
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<Child>()
          },
        None = new ComponentType[]
          {
                    typeof(Parent)
          },
        Options = EntityQueryOptions.FilterWriteGroup
      });

      m_allChildrenQuery = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[]
        {
                        ComponentType.ReadOnly<LocalToWorld>(),
                        ComponentType.ReadOnly<Parent>()
        },
        Options = EntityQueryOptions.IncludeDisabled
      });

      m_childrenEntities = new NativeList<ChildInfo>(Allocator.Persistent);
      m_topLevelChildrenCounter = new NativeCounter(Allocator.Persistent);
    }

    protected override void OnDestroy() {
      base.OnDestroy();
      m_childrenEntities.Dispose();
      m_topLevelChildrenCounter.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {

      var possibleChildrenCount = m_allChildrenQuery.CalculateEntityCount();
      inputDeps = m_childrenEntities.Resize(possibleChildrenCount, inputDeps);
      inputDeps = new MemsetNativeArray<ChildInfo> {
        Source = m_childrenEntities.AsDeferredJobArray(),
        Value = default
      }.Schedule(possibleChildrenCount, 128, inputDeps);

      var localToWorldType = GetComponentTypeHandle<LocalToWorld>(true);
      var localToParentType = GetComponentTypeHandle<LocalToParent>(true);
      var childType = GetBufferTypeHandle<Child>(true);
      var childFromEntity = GetBufferFromEntity<Child>(true);
      var localToParentFromEntity = GetComponentDataFromEntity<LocalToParent>(true);
      var localToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>();

      inputDeps = new ExtractChildren {
        ChildFromEntity = childFromEntity,
        ChildType = childType,
        LocalToWorldType = localToWorldType,
        TopLevelChildren = m_childrenEntities,
        LastSystemVersion = LastSystemVersion
      }.Schedule(m_rootsQuery, inputDeps);

      inputDeps = new UpdateHierarchy {
        ChildFromEntity = childFromEntity,
        LocalToParentFromEntity = localToParentFromEntity,
        LocalToWorldFromEntity = localToWorldFromEntity,
        Children = m_childrenEntities.AsDeferredJobArray()
      }.Schedule(possibleChildrenCount, 128, inputDeps);

      return inputDeps;
    }
  }

  [UnityEngine.ExecuteAlways]
  [UpdateInGroup(typeof(TransformSystemGroup))]
  [UpdateAfter(typeof(EndFrameTRSToLocalToParentSystem))]
  public class EndFrameLocalToParentSystem : LocalToParentSystem {
    protected override void OnCreate() {
      base.OnCreate();
      var originalSystem = World.GetOrCreateSystem<Unity.Transforms.EndFrameLocalToParentSystem>();
      originalSystem.Enabled = false;
    }
  }
}
