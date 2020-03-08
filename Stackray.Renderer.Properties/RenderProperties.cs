using System;
using Stackray.Entities;
using Unity.Entities;

namespace Stackray.Renderer {

  public interface IBufferProperty : IComponentData {
    string BufferName { get; }
  }

  public interface IFixedBufferProperty<T> : IBufferProperty
    where T : IComponentData { }

  public interface IBlendable<T> {
    T GetBlendedValue(T startValue, T endValue, float t);
  }

  public interface IDynamicBufferProperty<T> : IComponentData, IBufferProperty, IEquatable<T>, IComponentValue<T>, IBlendable<T>
    where T : struct, IEquatable<T> {

    T Convert(UnityEngine.SpriteRenderer spriteRenderer);
  }
}