using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Stackray.Collections {
  [StructLayout(LayoutKind.Sequential)]
  [NativeContainer]
  public unsafe struct NativeHashSet<T> : IDisposable where T : struct, IEquatable<T> {
    [NativeDisableUnsafePtrRestriction] NativeHashSetData* buffer;
    Allocator allocator;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    AtomicSafetyHandle m_Safety;
    [NativeSetClassTypeToNullOnSchedule] DisposeSentinel m_DisposeSentinel;
#endif

    public NativeHashSet(int capacity, Allocator allocator) {
      NativeHashSetData.AllocateHashSet<T>(capacity, allocator, out buffer);
      this.allocator = allocator;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      DisposeSentinel.Create(
          out m_Safety, out m_DisposeSentinel, callSiteStackDepth: 8, allocator: allocator);
#endif
      Clear();
    }

    [NativeContainer]
    [NativeContainerIsAtomicWriteOnly]
    public struct Concurrent {
      [NativeDisableUnsafePtrRestriction] public NativeHashSetData* buffer;
      [NativeSetThreadIndex] public int threadIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      public AtomicSafetyHandle m_Safety;
#endif

      public int Capacity {
        get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
          AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
          return buffer->Capacity;
        }
      }

      public bool TryAdd(T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        return buffer->TryAddThreaded(ref value, threadIndex);
      }
    }

    public int Capacity {
      get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        return buffer->Capacity;
      }
    }

    public int Length {
      get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        return buffer->Length;
      }
    }

    public bool IsCreated => buffer != null;

    public void Dispose() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
      DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
      NativeHashSetData.DeallocateHashSet(buffer, allocator);
      buffer = null;
    }

    public void Clear() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
      buffer->Clear<T>();
    }

    public Concurrent ToConcurrent() {
      Concurrent concurrent;
      concurrent.threadIndex = 0;
      concurrent.buffer = buffer;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      concurrent.m_Safety = m_Safety;
#endif
      return concurrent;
    }

    public bool TryAdd(T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
      return buffer->TryAdd(ref value, allocator);
    }

    public bool TryRemove(T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
      return buffer->TryRemove(value);
    }

    public bool Contains(T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
      return buffer->Contains(ref value);
    }

    public NativeArray<T> GetValueArray(Allocator allocator) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
      var result = new NativeArray<T>(Length, allocator, NativeArrayOptions.UninitializedMemory);
      buffer->GetValueArray(result);
      return result;
    }
  }


  [StructLayout(LayoutKind.Sequential)]
  public unsafe struct NativeHashSetData {
    byte* values;
    byte* next;
    byte* buckets;
    int valueCapacity;
    int bucketCapacityMask;

    // Adding padding to ensure remaining fields are on separate cache-lines
    fixed byte padding[60];
    fixed int firstFreeTLS[JobsUtility.MaxJobThreadCount * IntsPerCacheLine];
    int allocatedIndexLength;
    const int IntsPerCacheLine = JobsUtility.CacheLineSize / sizeof(int);

    public int Capacity => valueCapacity;

    public int Length {
      get {
        int* nextPtrs = (int*)next;
        int freeListSize = 0;
        for (int tls = 0; tls < JobsUtility.MaxJobThreadCount; ++tls) {
          int freeIdx = firstFreeTLS[tls * IntsPerCacheLine] - 1;
          for (; freeIdx >= 0; freeListSize++, freeIdx = nextPtrs[freeIdx] - 1) { }
        }
        return math.min(valueCapacity, allocatedIndexLength) - freeListSize;
      }
    }

    static int DoubleCapacity(int capacity) => capacity == 0 ? 1 : capacity * 2;

    public static void AllocateHashSet<T>(
    int capacity, Allocator label, out NativeHashSetData* buffer) where T : struct {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
      if (!UnsafeUtility.IsBlittable<T>())
        throw new ArgumentException($"{typeof(T)} used in NativeHashSet<{typeof(T)}> must be blittable");
#endif
      var data = (NativeHashSetData*)UnsafeUtility.Malloc(
          sizeof(NativeHashSetData), UnsafeUtility.AlignOf<NativeHashSetData>(), label);

      int bucketCapacity = math.ceilpow2(capacity * 2);
      data->valueCapacity = capacity;
      data->bucketCapacityMask = bucketCapacity - 1;

      int nextOffset, bucketOffset;
      int totalSize = CalculateDataSize<T>(capacity, bucketCapacity, out nextOffset, out bucketOffset);

      data->values = (byte*)UnsafeUtility.Malloc(totalSize, JobsUtility.CacheLineSize, label);
      data->next = data->values + nextOffset;
      data->buckets = data->values + bucketOffset;
      buffer = data;
    }

    public static void DeallocateHashSet(NativeHashSetData* data, Allocator allocator) {
      UnsafeUtility.Free(data->values, allocator);
      data->values = null;
      data->buckets = null;
      data->next = null;
      UnsafeUtility.Free(data, allocator);
    }

    public void Clear<T>() where T : struct {
      UnsafeUtility.MemClear((int*)buckets, sizeof(int) * (bucketCapacityMask + 1));
      UnsafeUtility.MemClear((int*)next, UnsafeUtility.SizeOf<T>() * valueCapacity);
      fixed (int* firstFreeTLS = this.firstFreeTLS) {
        UnsafeUtility.MemClear(
            firstFreeTLS, sizeof(int) * (JobsUtility.MaxJobThreadCount * IntsPerCacheLine));
      }
      allocatedIndexLength = 0;
    }

    public void GetValueArray<T>(NativeArray<T> result) where T : struct {
      var buckets = (int*)this.buckets;
      var nextPtrs = (int*)next;
      int outputIndex = 0;
      for (int bucketIndex = 0; bucketIndex <= bucketCapacityMask; ++bucketIndex) {
        int valuesIndex = buckets[bucketIndex];
        while (valuesIndex > 0) {
          result[outputIndex] = UnsafeUtility.ReadArrayElement<T>(values, valuesIndex - 1);
          outputIndex++;
          valuesIndex = nextPtrs[valuesIndex - 1];
        }
      }
      Assert.AreEqual(result.Length, outputIndex);
    }

    public bool TryAdd<T>(ref T value, Allocator allocator) where T : struct, IEquatable<T> {
      if (Contains(ref value)) {
        return false;
      }

      int valuesIdx = FindFirstFreeIndex<T>(allocator);
      UnsafeUtility.WriteArrayElement(values, valuesIdx, value);
      // Add the index to the hashset
      int* buckets = (int*)this.buckets;
      int* nextPtrs = (int*)next;
      int bucketIndex = value.GetHashCode() & bucketCapacityMask;
      nextPtrs[valuesIdx] = buckets[bucketIndex];
      buckets[bucketIndex] = valuesIdx + 1;
      return true;
    }

    public bool TryAddThreaded<T>(ref T value, int threadIndex) where T : IEquatable<T> {
      if (Contains(ref value)) {
        return false;
      }
      // Allocate an entry from the free list
      int idx = FindFreeIndexFromTLS(threadIndex);
      UnsafeUtility.WriteArrayElement(values, idx, value);

      // Add the index to the hashset
      int* buckets = (int*)this.buckets;
      int bucket = value.GetHashCode() & bucketCapacityMask;
      int* nextPtrs = (int*)next;
      if (Interlocked.CompareExchange(ref buckets[bucket], idx + 1, 0) != 0) {
        do {
          nextPtrs[idx] = buckets[bucket];
          if (Contains(ref value)) {
            // Put back the entry in the free list if someone else added it while trying to add
            do {
              nextPtrs[idx] = firstFreeTLS[threadIndex * IntsPerCacheLine];
            } while (Interlocked.CompareExchange(
                        ref firstFreeTLS[threadIndex * IntsPerCacheLine], idx + 1,
                        nextPtrs[idx]) != nextPtrs[idx]);
            return false;
          }
        } while (Interlocked.CompareExchange(ref buckets[bucket], idx + 1, nextPtrs[idx]) != nextPtrs[idx]);
      }
      return true;
    }

    public bool Contains<T>(ref T value) where T : IEquatable<T> {
      if (allocatedIndexLength <= 0) {
        return false;
      }
      int* buckets = (int*)this.buckets;
      int* nextPtrs = (int*)next;
      int bucket = value.GetHashCode() & bucketCapacityMask;
      int valuesIdx = buckets[bucket] - 1;
      while (valuesIdx >= 0 && valuesIdx < valueCapacity) {
        if (UnsafeUtility.ReadArrayElement<T>(values, valuesIdx).Equals(value)) {
          return true;
        }
        valuesIdx = nextPtrs[valuesIdx] - 1;
      }
      return false;
    }

    public bool TryRemove<T>(T key) where T : struct, IEquatable<T> {
      int* buckets = (int*)this.buckets;
      int* nextPtrs = (int*)next;
      int bucketIdx = key.GetHashCode() & bucketCapacityMask;
      int valuesIdx = buckets[bucketIdx] - 1;
      int prevValuesIdx = -1;

      while (valuesIdx >= 0 && valuesIdx < valueCapacity) {
        if (UnsafeUtility.ReadArrayElement<T>(values, valuesIdx).Equals(key)) {
          if (prevValuesIdx == -1) {
            // Sets head->next to head->next->next(or -1)
            buckets[bucketIdx] = nextPtrs[valuesIdx];
          } else {
            // Sets prev->next to prev->next(current valuesIdx)->next
            nextPtrs[prevValuesIdx] = nextPtrs[valuesIdx];
          }
          // Mark the index as free
          nextPtrs[valuesIdx] = firstFreeTLS[0];
          firstFreeTLS[0] = valuesIdx + 1;
          return true;
        }
        prevValuesIdx = valuesIdx;
        valuesIdx = nextPtrs[valuesIdx] - 1;
      }
      return false;
    }

    static int CalculateDataSize<T>(
    int capacity, int bucketCapacity, out int nextOffset, out int bucketOffset) where T : struct {
      nextOffset = (UnsafeUtility.SizeOf<T>() * capacity) + JobsUtility.CacheLineSize - 1;
      nextOffset -= nextOffset % JobsUtility.CacheLineSize;

      bucketOffset = nextOffset + (sizeof(int) * capacity) + JobsUtility.CacheLineSize - 1;
      bucketOffset -= bucketOffset % JobsUtility.CacheLineSize;
      return bucketOffset + (UnsafeUtility.SizeOf<int>() * bucketCapacity);
    }

    int FindFirstFreeIndex<T>(Allocator allocator) where T : struct {
      int valuesIdx;
      int* nextPtrs = (int*)next;

      // Try to find an index in another TLS.
      if (allocatedIndexLength >= valueCapacity && firstFreeTLS[0] == 0) {
        for (int tls = 1; tls < JobsUtility.MaxJobThreadCount; ++tls) {
          int tlsIndex = tls * IntsPerCacheLine;
          if (firstFreeTLS[tlsIndex] > 0) {
            valuesIdx = firstFreeTLS[tlsIndex] - 1;
            firstFreeTLS[tlsIndex] = nextPtrs[valuesIdx];
            nextPtrs[valuesIdx] = 0;
            firstFreeTLS[0] = valuesIdx + 1;
            break;
          }
        }
        // No indexes found.
        if (firstFreeTLS[0] == 0) {
          GrowHashSet<T>(DoubleCapacity(valueCapacity), allocator);
        }
      }
      if (firstFreeTLS[0] == 0) {
        valuesIdx = allocatedIndexLength;
        allocatedIndexLength++;
      } else {
        valuesIdx = firstFreeTLS[0] - 1;
        firstFreeTLS[0] = nextPtrs[valuesIdx];
      }
      if (!(valuesIdx >= 0 && valuesIdx < valueCapacity)) {
        throw new InvalidOperationException(
            $"Internal HashSet error, values index: {valuesIdx} not in range of 0 and {valueCapacity}");
      }
      return valuesIdx;
    }

    int FindFreeIndexFromTLS(int threadIndex) {
      int idx;
      int* nextPtrs = (int*)next;
      int thisTLSIndex = threadIndex * IntsPerCacheLine;
      do {
        idx = firstFreeTLS[thisTLSIndex] - 1;
        if (idx < 0) {
          // Mark this TLS index as locked
          Interlocked.Exchange(ref firstFreeTLS[thisTLSIndex], -1);
          // Try to allocate more indexes with this TLS
          if (allocatedIndexLength < valueCapacity) {
            idx = Interlocked.Add(ref allocatedIndexLength, 16) - 16;
            if (idx < valueCapacity - 1) {
              int count = math.min(16, valueCapacity - idx) - 1;
              for (int i = 1; i < count; ++i) {
                nextPtrs[idx + i] = (idx + 1) + i + 1;
              }
              nextPtrs[idx + count] = 0;
              nextPtrs[idx] = 0;
              Interlocked.Exchange(ref firstFreeTLS[thisTLSIndex], (idx + 1) + 1);
              return idx;
            }

            if (idx == valueCapacity - 1) {
              Interlocked.Exchange(ref firstFreeTLS[thisTLSIndex], 0);
              return idx;
            }
          }

          Interlocked.Exchange(ref firstFreeTLS[thisTLSIndex], 0);
          // Could not find an index, try to steal one from another TLS
          for (bool iterateAgain = true; iterateAgain;) {
            iterateAgain = false;
            for (int i = 1; i < JobsUtility.MaxJobThreadCount; i++) {
              int nextTLSIndex = ((threadIndex + i) % JobsUtility.MaxJobThreadCount) * IntsPerCacheLine;
              do {
                idx = firstFreeTLS[nextTLSIndex] - 1;
              } while (idx >= 0 && Interlocked.CompareExchange(
                          ref firstFreeTLS[nextTLSIndex], nextPtrs[idx], idx + 1) != idx + 1);
              if (idx == -1) {
                iterateAgain = true;
              } else if (idx >= 0) {
                nextPtrs[idx] = 0;
                return idx;
              }
            }
          }
          throw new InvalidOperationException("HashSet has reached capacity, cannot add more.");
        }
        if (idx > valueCapacity) {
          throw new InvalidOperationException($"nextPtr idx {idx} beyond capacity {valueCapacity}");
        }
        // Another thread is using this TLS, try again.
      } while (Interlocked.CompareExchange(
                  ref firstFreeTLS[threadIndex * IntsPerCacheLine], nextPtrs[idx], idx + 1) != idx + 1);
      nextPtrs[idx] = 0;
      return idx;
    }


    void GrowHashSet<T>(int newCapacity, Allocator allocator) where T : struct {
      int newBucketCapacity = math.ceilpow2(newCapacity * 2);
      if (newCapacity == valueCapacity && newBucketCapacity == (bucketCapacityMask + 1)) {
        return;
      }
      if (valueCapacity > newCapacity) {
        throw new ArgumentException("Shrinking a hashset is not supported");
      }

      int nextOffset, bucketOffset;
      int totalSize = CalculateDataSize<T>(newCapacity, newBucketCapacity, out nextOffset, out bucketOffset);
      byte* newValues = (byte*)UnsafeUtility.Malloc(totalSize, JobsUtility.CacheLineSize, allocator);
      byte* newNext = newValues + nextOffset;
      byte* newBuckets = newValues + bucketOffset;

      UnsafeUtility.MemClear(newNext, sizeof(int) * newCapacity);
      UnsafeUtility.MemCpy(newValues, values, UnsafeUtility.SizeOf<T>() * valueCapacity);
      UnsafeUtility.MemCpy(newNext, next, UnsafeUtility.SizeOf<int>() * valueCapacity);

      // Re-hash the buckets, first clear the new buckets, then reinsert.
      UnsafeUtility.MemClear(newBuckets, sizeof(int) * newBucketCapacity);
      int* oldBuckets = (int*)buckets;
      int* newNextPtrs = (int*)newNext;
      for (int oldBucket = 0; oldBucket <= bucketCapacityMask; ++oldBucket) {
        int curValuesIdx = oldBuckets[oldBucket] - 1;
        while (curValuesIdx >= 0 && curValuesIdx < valueCapacity) {
          var curValue = UnsafeUtility.ReadArrayElement<T>(values, curValuesIdx);
          int newBucket = curValue.GetHashCode() & newBucketCapacity - 1;
          oldBuckets[oldBucket] = newNextPtrs[curValuesIdx];
          newNextPtrs[curValuesIdx] = ((int*)newBuckets)[newBucket];
          ((int*)newBuckets)[newBucket] = curValuesIdx + 1;
          curValuesIdx = oldBuckets[oldBucket] - 1;
        }
      }

      UnsafeUtility.Free(values, allocator);
      if (allocatedIndexLength > valueCapacity) {
        allocatedIndexLength = valueCapacity;
      }
      values = newValues;
      next = newNext;
      buckets = newBuckets;
      valueCapacity = newCapacity;
      bucketCapacityMask = newBucketCapacity - 1;
    }
  }
}