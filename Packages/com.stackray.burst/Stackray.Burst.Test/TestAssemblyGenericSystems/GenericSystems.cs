using Unity.Burst;
using Unity.Jobs;

namespace Stackray.TestGenericSystems {

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

  class GenericClass<T> {
    public void Call() {
      Helper.Call<T>();
    }
  }

  class Helper {
    public static void Call<T>() {
      new MyJob<T>().ToString();
    }
  }
}