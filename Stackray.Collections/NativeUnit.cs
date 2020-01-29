using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Stackray.Collections {

  [StructLayout(LayoutKind.Sequential)]
  [NativeContainer]
  [NativeContainerSupportsDeallocateOnJobCompletion]
  unsafe public struct NativeUnit<T> : IDisposable where T : struct {
    // The actual pointer to the allocated count needs to have restrictions relaxed so jobs can be scheduled with this container
    [NativeDisableUnsafePtrRestriction]
    int* m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    AtomicSafetyHandle m_Safety;
    // The dispose sentinel tracks memory leaks. It is a managed type so it is cleared to null when scheduling a job
    // The job cannot dispose the container, and no one else can dispose it until the job has run, so it is ok to not pass it along
    // This attribute is required, without it this NativeContainer cannot be passed to a job; since that would give the job access to a managed object
    [NativeSetClassTypeToNullOnSchedule]
    DisposeSentinel m_DisposeSentinel;
#endif

    // Keep track of where the memory for this was allocated
    Allocator m_AllocatorLabel;

    public NativeUnit(Allocator label) {
      m_AllocatorLabel = label;

      // Allocate native memory for a single type T
      m_Buffer = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>(), 4, label);

      // Create a dispose sentinel to track memory leaks. This also creates the AtomicSafetyHandle
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, m_AllocatorLabel);
#endif
      // Initialize the count to 0 to avoid uninitialized data
      Value = default;
    }

    public NativeUnit(T value, Allocator label) {
      m_AllocatorLabel = label;

      // Allocate native memory for a single type T
      m_Buffer = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>(), 4, label);

      // Create a dispose sentinel to track memory leaks. This also creates the AtomicSafetyHandle
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, m_AllocatorLabel);
#endif
      Value = value;
    }

    public T Value {
      get {
        // Verify that the caller has read permission on this data. 
        // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        T result;
        UnsafeUtility.CopyPtrToStructure(m_Buffer, out result);
        return result;
      }
      set {
        // Verify that the caller has write permission on this data. This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        UnsafeUtility.CopyStructureToPtr(ref value, m_Buffer);
      }
    }

    public bool IsCreated {
      get { return m_Buffer != null; }
    }

    void Deallocate() {
      UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
      m_Buffer = null;
    }

    public void Dispose() {
      // Let the dispose sentinel know that the data has been freed so it does not report any memory leaks
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

      Deallocate();
    }

    /// <summary>
    /// Safely disposes of this container and deallocates its memory when the jobs that use it have completed.
    /// </summary>
    /// <remarks>You can call this function dispose of the container immediately after scheduling the job. Pass
    /// the [JobHandle](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) returned by
    /// the [Job.Schedule](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobExtensions.Schedule.html)
    /// method using the `jobHandle` parameter so the job scheduler can dispose the container after all jobs
    /// using it have run.</remarks>
    /// <param name="jobHandle">The job handle or handles for any scheduled jobs that use this container.</param>
    /// <returns>A new job handle containing the prior handles as well as the handle for the job that deletes
    /// the container.</returns>
    public JobHandle Dispose(JobHandle inputDeps) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      // [DeallocateOnJobCompletion] is not supported, but we want the deallocation
      // to happen in a thread. DisposeSentinel needs to be cleared on main thread.
      // AtomicSafetyHandle can be destroyed after the job was scheduled (Job scheduling
      // will check that no jobs are writing to the container).
      DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
      var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.Release(m_Safety);
#endif
      m_Buffer = null;

      return jobHandle;
    }

    [BurstCompile]
    struct DisposeJob : IJob {
      public NativeUnit<T> Container;

      public void Execute() {
        Container.Deallocate();
      }
    }
  }
}