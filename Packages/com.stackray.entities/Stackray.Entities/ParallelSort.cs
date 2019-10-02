using Stackray.Jobs;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stackray.Entities {



  public class ParallelSort<TType> : IDisposable
    where TType : struct, IComparable<TType> {

    private NativeList<TType>[] m_requiredBuffers = new NativeList<TType>[0];

    public NativeList<TType> SortedData;

    public ParallelSort() {
      SortedData = new NativeList<TType>(Allocator.Persistent);
    }

    public JobHandle Sort(NativeArray<TType> sourceArray, int length, int sliceCount, JobHandle inputDeps = default) {

      sliceCount = math.min(length, math.max(1, sliceCount));
      if (sliceCount > 1 && sliceCount % 2 != 0)
        sliceCount--;
      var totalSliceCount = SortUtility.CalculateTotalSlices(sliceCount);
      if (m_requiredBuffers.Length != totalSliceCount) {
        inputDeps.Complete();
        foreach (var buffer in m_requiredBuffers)
          buffer.Dispose();
        m_requiredBuffers = new NativeList<TType>[totalSliceCount];
        for (var i = 0; i < m_requiredBuffers.Length; ++i)
          m_requiredBuffers[i] = new NativeList<TType>(Allocator.Persistent);
      }

      inputDeps = SortHelper.PrepareData(sourceArray, length, SortedData, inputDeps);
      inputDeps = SortHelper.SliceArray(SortedData, length, sliceCount, m_requiredBuffers, inputDeps);
      inputDeps = SortHelper.SortSlices(m_requiredBuffers, sliceCount, inputDeps);
      inputDeps = SortHelper.MergeSlices(SortedData, length, sliceCount, m_requiredBuffers, inputDeps);

      return inputDeps;
    }

    public void Dispose() {
      foreach (var buffer in m_requiredBuffers)
        buffer.Dispose();
      SortedData.Dispose();
    }

    #region sort helper
    private class SortHelper {

      public static JobHandle PrepareData<T>(NativeArray<T> source, int length, NativeList<T> output, JobHandle inputDeps)
        where T : struct, IComparable<T> {

        inputDeps = new ResizeNativeList<T> {
          Source = output,
          Length = length
        }.Schedule(inputDeps);
        inputDeps = new CopyToNativeArray<T> {
          Source = source,
          Target = output.AsDeferredJobArray()
        }.Schedule(length, 128, inputDeps);
        return inputDeps;
      }

      public static JobHandle SliceArray<T>(NativeList<T> source, int length, int sliceCount, NativeList<T>[] buffers, JobHandle inputDeps) where T : struct, IComparable<T> {
        var sliceLength = (int)math.ceil((float)length / sliceCount);
        var copyHandle = inputDeps;
        var offset = 0;

        for (var i = 0; i < sliceCount; ++i) {
          var bufferLength = math.min(sliceLength, length);
          inputDeps = new ResizeNativeList<T> {
            Source = buffers[i],
            Length = bufferLength
          }.Schedule(inputDeps);
          copyHandle = JobHandle.CombineDependencies(
              copyHandle,
              new CopyToNativeArray<T> {
                Source = source.AsDeferredJobArray(),
                Target = buffers[i].AsDeferredJobArray(),
                SourceOffset = i * sliceLength,
              }.Schedule(bufferLength, 64, inputDeps));
          length -= sliceLength;
          offset += bufferLength;
        }
        return copyHandle;
      }

      public static JobHandle SortSlices<T>(NativeList<T>[] buffers, int sliceCount, JobHandle inputDeps) where T : struct, IComparable<T> {
        var sortHandle = inputDeps;
        for (var i = 0; i < sliceCount; ++i) {
          sortHandle = JobHandle.CombineDependencies(
              sortHandle,
              new SortNativeArray<T> {
                Data = buffers[i].AsDeferredJobArray()
              }.Schedule(inputDeps));
        }
        return sortHandle;
      }

      public static JobHandle MergeSlices<T>(NativeList<T> result, int length, int sliceCount, NativeList<T>[] buffers, JobHandle inputDeps)
        where T : struct, IComparable<T> {

        var concurrentMerges = inputDeps;
        var readBufferIndex = 0;
        var writeBufferIndex = sliceCount;
        while (readBufferIndex < buffers.Length - 1) {
          var concurrent = readBufferIndex < sliceCount;
          var mergeHandle = new Merge<T> {
            LeftArray = buffers[readBufferIndex].AsDeferredJobArray(),
            RightArray = buffers[readBufferIndex + 1].AsDeferredJobArray(),
            Target = buffers[writeBufferIndex],
          }.Schedule(concurrent ? inputDeps : concurrentMerges);
          concurrentMerges = concurrent ?
            JobHandle.CombineDependencies(concurrentMerges, mergeHandle) :
            mergeHandle;

          readBufferIndex += 2;
          writeBufferIndex++;
        }
        inputDeps = concurrentMerges;

        if (length > 0)
          inputDeps = new CopyToNativeArray<T> {
            Source = buffers[writeBufferIndex - 1].AsDeferredJobArray(),
            Target = result.AsDeferredJobArray()
          }.Schedule(length, 128, inputDeps);
        return inputDeps;
      }

      private static void MergeSlices<T>(NativeArray<T> result, NativeArray<T> left, NativeArray<T> right, int offSet = 0)
        where T : struct, IComparable<T> {

        var leftIndex = 0;
        var rightIndex = 0;
        var resultIndex = offSet;
        while (leftIndex < left.Length || rightIndex < right.Length) {
          if (leftIndex < left.Length && rightIndex < right.Length) {
            if (left[leftIndex].CompareTo(right[rightIndex]) <= 0) {
              result[resultIndex] = left[leftIndex];
              leftIndex++;
              resultIndex++;
            } else {
              result[resultIndex] = right[rightIndex];
              rightIndex++;
              resultIndex++;
            }
          } else if (leftIndex < left.Length) {
            result[resultIndex] = left[leftIndex];
            leftIndex++;
            resultIndex++;
          } else if (rightIndex < right.Length) {
            result[resultIndex] = right[rightIndex];
            rightIndex++;
            resultIndex++;
          }
        }
      }

      [BurstCompile]
      struct Merge<T> : IJob where T : struct, IComparable<T> {
        [ReadOnly]
        public NativeArray<T> LeftArray;
        [ReadOnly]
        public NativeArray<T> RightArray;
        public NativeList<T> Target;
        public void Execute() {
          Target.ResizeUninitialized(LeftArray.Length + RightArray.Length);
          MergeSlices(Target, LeftArray, RightArray);
        }
      }
    }
    #endregion sort helper
  }
}