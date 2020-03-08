using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Stackray.TestGenericOverride {

  abstract class MyAbstractClass<Type> {
    protected abstract void Call();

    void Update() {
      Call();
    }
  }

  class MyImpl01<Type> : MyAbstractClass<Type> {

    [BurstCompile]
    struct MyImpl01Job<T> : IJob {
      public void Execute() {
        throw new System.NotImplementedException();
      }
    }

    protected override void Call() {
      new MyImpl01Job<Type>().Schedule();
    }
  }

  static class Inject {
    static void Call() {
      new MyImpl01<bool>();
      new MyImpl01<short>();
      new MyImpl02<int>();
      new MyImpl01<float>();
    }
  }
  
  class MyImpl02<Type> : MyAbstractClass<Type> {

    [BurstCompile]
    struct MyImpl02Job<T> : IJob {
      public void Execute() {
        throw new System.NotImplementedException();
      }
    }

    protected override void Call() {
      new MyImpl02Job<Type>().Schedule();
    }
  }

}