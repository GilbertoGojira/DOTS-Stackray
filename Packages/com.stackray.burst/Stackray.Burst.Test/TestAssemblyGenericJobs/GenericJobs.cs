﻿using Unity.Burst;
using Unity.Jobs;

namespace Stackray.TestGenericJobs {

  [BurstCompile]
  struct DetachedGenericJob<T> : IJob {
    public void Execute() {
      throw new System.NotImplementedException();
    }
  }

  struct MyData<X, Y> { }

  /// <summary>
  /// Test class with scheduled generic jobs
  /// </summary>
  /// <typeparam name="Type1"></typeparam>
  /// <typeparam name="Type2"></typeparam>
  public class GenericJobs<Type1, Type2> {

    /// <summary>
    /// Test against this value of jobs
    /// </summary>
    public const int GENERIC_JOB_ENTRIES = 11;
    public const int GENERIC_UNIQUE_JOB_ENTRIES = 12;
    public const int CONCRETE_UNIQUE_JOB_ENTRIES = 13;

    public class NestedClass {

      [BurstCompile]
      public struct NestedGenericJob1<T> : IJob {
        public void Execute() {
          throw new System.NotImplementedException();
        }
      }

      public void Play<T1, T2, T3>() {
        new NestedGenericJob1<T3>().Schedule();
      }
    }

    [BurstCompile]
    struct GenericJob1<T> : IJob {
      public void Execute() {
        throw new System.NotImplementedException();
      }
    }

    [BurstCompile]
    struct GenericJob2<T1, T2> : IJob {
      public void Execute() {
        throw new System.NotImplementedException();
      }
    }

    // This must also be detected as a generic Job
    [BurstCompile]
    struct GenericJob3 : IJob {
      public Type1 Value;

      public void Execute() {
        Value = default;
        throw new System.NotImplementedException();
      }
    }

    [BurstCompile]
    public struct ConcreteJob : IJob {
      public void Execute() {
        throw new System.NotImplementedException();
      }
    }

    // ****** 5 concrete Jobs generated by this entry
    void DummyCall() {
      new GenericJob1<Type1>().Schedule();           // j1 
      new GenericJob1<Type2>().Schedule();           // j2  
      new GenericJob1<Type1>().Schedule();           // j3 same as j1 
      new GenericJob2<Type1, Type2>().Schedule();    // j4  
      new GenericJob2<Type2, Type1>().Schedule();    // j5 
      new GenericJob3().Schedule();
      new GenericJob1<MyData<Type1, Type1>>().Schedule();   // j6
    }

    // ****** 1 concrete Job generated by this entry
    // 2 jobs but only 1 one real concrete job will be generated in the end
    // That job will be `NestedGenericJob1<Type2>` where Type2 will be resolved to the concrete class instance
    void DummyCallToNested() {      
      new NestedClass().Play<Type1, Type1, Type2>();
      new NestedClass().Play<Type1, Type2, Type2>();
    }

    // ****** 1 concrete Job generated by this entry
    void DummyCallToDetached() {
      new DetachedGenericJob<Type1>().Schedule();
    }

    void DummyCall<X>() {
      new NestedClass().Play<bool, float, X>();
    }

    void DummyCall<X, Y>() {      
      new NestedClass().Play<bool, X, Y>();
      new GenericJob1<MyData<Y, X>>().Schedule();
      new NestedClass.NestedGenericJob1<Type1>().Schedule();
    }

    // ****** 2 concrete Jobs generated by this entry
    void DummyCallConcrete2() {
      DummyCall<int, byte>();
    }

    //// ****** 1 concrete Job generated by this entry
    void DummyCallConcrete() {
      DummyCall<int>();
    }
  }

  public class GenericClass<T1, T2> : GenericJobs<T2, float> {

    // ****** 1 concrete Job generated by this entry
    void CallRandomDetach() {
      new DetachedGenericJob<T1>().Schedule();
    }
  }

  public class ConcreteClass : GenericClass<int, bool> { }
}
