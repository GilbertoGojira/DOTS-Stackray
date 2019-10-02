using System;
using Unity.Entities;

namespace Stackray.Entities {
  public struct DataWithIndex<TSource> : IComparable<DataWithIndex<TSource>>
    where TSource : struct, IComparable<TSource> {

    public int Index;
    public TSource Value;

    public int CompareTo(DataWithIndex<TSource> other) {
      return Value.CompareTo(other.Value);
    }
  }

  public struct DataWithEntity<TSource> : IComparable<DataWithIndex<TSource>>
    where TSource : struct, IComparable<TSource> {

    public Entity Entity; 
    public int Index;
    public TSource Value;

    public int CompareTo(DataWithIndex<TSource> other) {
      return Value.CompareTo(other.Value);
    }
  }
}
