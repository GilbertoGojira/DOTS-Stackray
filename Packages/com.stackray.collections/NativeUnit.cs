using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Stackray.Collections {

  [StructLayout(LayoutKind.Sequential)]
  [NativeContainer]
  [NativeContainerSupportsDeallocateOnJobCompletion]
  unsafe public struct NativeUnit<T> where T : struct {
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

    public void Dispose() {
      // Let the dispose sentinel know that the data has been freed so it does not report any memory leaks
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

      UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
      m_Buffer = null;
    }
  }
}