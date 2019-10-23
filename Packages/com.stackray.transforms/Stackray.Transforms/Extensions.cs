using Unity.Collections;
using Unity.Entities;

namespace Stackray.Transforms {
  public static class Extensions {
    public static NativeArray<SortedEntity> GetAllSortedEntities(this EntityManager entityManager, ComponentSystemBase system, Allocator allocator) {
      return entityManager.GetBuffer<SortedEntity>(system.GetSingletonEntity<SortedEntities>()).ToNativeArray(allocator);
    }
  }
}
