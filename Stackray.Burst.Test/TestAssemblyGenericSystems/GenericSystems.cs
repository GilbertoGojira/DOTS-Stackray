using Unity.Burst;
using Unity.Jobs;

namespace Stackray.TestGenericSystems {

  public struct MyData<T> { }

  [BurstCompile]
  struct MyJob<T> : IJob {
    public void Execute() {
      throw new System.NotImplementedException();
    }
  }

  class MySystem {
    GenericClass<int> m_myGenericClass = new GenericClass<int>();

    void Call() {
      m_myGenericClass.Call();
    }
  }

  public class GenericClass<T> where T : struct {

    public struct SomeData { }

    T m_data = default;
    public void Call() {
      var value = default(SomeData);
      Helper.Call(new[] { m_data }, ref value, default);
    }

    class Helper {
      public static void Call<X>(X[] dataCollection, ref SomeData someOtherValue, JobHandle inputDeps) where X : struct {
        foreach (var data in dataCollection)
          data.Call();
      }
    }
  }


  static class Extension {
    public static void Call<T>(this T data) where T : struct {
      new MyJob<T>().ToString();
    }
  }
}