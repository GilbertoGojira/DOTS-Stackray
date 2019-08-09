using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Stackray.Jobs {
  /// <summary>
  /// Function pointer to be used in jobs
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public struct FunctionPointer<T> {

    public static FunctionPointer<T> Create(T function) {
      return new FunctionPointer<T>(Marshal.GetFunctionPointerForDelegate(function));
    }

    [NativeDisableUnsafePtrRestriction]
    private readonly IntPtr _ptr;

    public FunctionPointer(IntPtr ptr) {
      _ptr = ptr;
    }

    public T Invoke => (T)(object)Marshal.GetDelegateForFunctionPointer(_ptr, typeof(T));
  }

  /// <summary>
  /// This job will execute an action
  /// </summary>
  [BurstCompile]
  public struct ActionJob : IJob {

    public static JobHandle Schedule(Action action, JobHandle inputDeps) {
      return new ActionJob {
        Action = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(action))
      }.Schedule(inputDeps);
    }

    FunctionPointer<Action> Action;

    public void Execute() {
      Action.Invoke();
    }
  }
}