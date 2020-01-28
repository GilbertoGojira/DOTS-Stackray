using Unity.Burst;
using Unity.Jobs;

namespace Stackray.TestCascadeCall {

  [BurstCompile]
  struct MyJob<T> : IJob {
    public void Execute() {
      throw new System.NotImplementedException();
    }
  }

  class Class1 {
    public static void Call<X>() {
      new MyJob<X>().ToString();
    }
  }

  class Class2 {
    public static void Call<T1, T2>() {
      Class1.Call<T1>();
    }
  }

  class Class3 {
    public static void Call<Y1, Y2>() {
      Class2.Call<Y2, Y1>();
    }
  }

  class Class4 {
    public static void Call<Z1, Z2>() {
      Class3.Call<bool, Z1>();
    }
  }

  class Class5<T> {
    public void Call<X>() {
      Class4.Call<T, MyType<X>>();
    }
  }

  class Concrete : Class5<int> {
    void CallConcrete() {
      Call<long>();
    }
  }

  struct MyType<T> { }
}