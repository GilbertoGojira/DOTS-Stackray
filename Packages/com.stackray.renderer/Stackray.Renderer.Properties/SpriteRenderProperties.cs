using System;
using Stackray.Entities;
using Unity.Entities;

namespace Stackray.Renderer {

  public interface IBufferProperty<T> : IComponentData {
    string BufferName { get; }
  }

  public interface IFixedBufferProperty<T> : IBufferProperty<T>
    where T : IComponentData { }

  public interface IDynamicBufferProperty<T> : IComponentData, IBufferProperty<T>, IEquatable<T>, IComponentValue<T>
    where T : struct, IEquatable<T> {

    T Convert(UnityEngine.SpriteRenderer spriteRenderer);
  }
}