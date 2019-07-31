using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

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
}