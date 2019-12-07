using Unity.Entities;

namespace Stackray.Entities.Editor {

  public interface IComponentEditor<T> {
    void OnInspectorGUI(T target);
  }

  public interface IComponentDataEditor<T> : IComponentEditor<T> 
    where T : struct, IComponentData {  }

  public interface ISharedComponentDataEditor<T> : IComponentEditor<T>
    where T : struct, ISharedComponentData {  }

  public interface IBufferElementDataEditor<T> : IComponentEditor<T> 
    where T : struct, IBufferElementData {  }
}
