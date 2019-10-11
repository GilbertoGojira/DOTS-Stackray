using System;
using Unity.Entities;

namespace Stackray.SpriteRenderer {

  public interface IComponentValue<T> where T : struct {
    T Value { get; set; }
  }

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