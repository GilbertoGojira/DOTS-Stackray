using Stackray.Collections;
using Stackray.Jobs;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stackray.Entities {

  public class TimeSpanParallelSort<TType> : IDisposable
    where TType : struct, IComparable<TType> {

    public struct SortState {
      public enum EStatus {
        None,
        Sorting,
        Merging,
        Complete
      }
      public EStatus Status;
      public int SortIndex;
      public int SortLength;
      public int ReadBufferIndex;
      public int WriteBufferIndex;
      public int LeftIndex;
      public int RightIndex;
      public int ResultIndex;
      public int BaseSliceCount;
      public int ProcessAmount;
      public int ConcurrentSorts;
      public int Cyles;

      public bool IsComplete(int length) {
        return WriteBufferIndex == length;
      }
    }

    public NativeList<TType> SortedData;

    public bool IsComplete {
      get => m_state.Status == SortState.EStatus.Complete || m_state.Status == SortState.EStatus.None;
    }

    public int Cycles {
      get => m_state.Cyles;
    }

    private NativeList<TType>[] m_requiredBuffers = new NativeList<TType>[0];
    private SortState m_state;

    public TimeSpanParallelSort() {
      SortedData = new NativeList<TType>(Allocator.Persistent);
    }

    public JobHandle Start(NativeArray<TType> sourceArray, int processAmount = 131_072, int concurrentSorts = 0, JobHandle inputDeps = default) {
      concurrentSorts = concurrentSorts == 0 ? Environment.ProcessorCount : concurrentSorts;
      var baseSliceCount = (int)math.ceil((float)sourceArray.Length / processAmount);
      if (baseSliceCount > 1 && baseSliceCount % 2 != 0)
        baseSliceCount--;
      var totalSliceCount = SortUtility.CalculateTotalSlices(baseSliceCount);
      if (m_requiredBuffers.Length != totalSliceCount) {
        foreach (var buffer in m_requiredBuffers)
          buffer.Dispose();
        m_requiredBuffers = new NativeList<TType>[totalSliceCount];
        for (var i = 0; i < m_requiredBuffers.Length; ++i)
          m_requiredBuffers[i] = new NativeList<TType>(Allocator.Persistent);
      }

      // copy unsorted data into final buffer
      inputDeps = SortHelper.PrepareData(sourceArray, SortedData, inputDeps);
      // Copy unsorted data into base slices
      inputDeps = SortHelper.PrepaceBaseSlices(SortedData, sourceArray.Length, baseSliceCount, m_requiredBuffers, inputDeps);

      m_state = new SortState {
        Status = SortState.EStatus.Sorting,
        SortLength = sourceArray.Length,
        BaseSliceCount = baseSliceCount,
        ProcessAmount = processAmount,
        ConcurrentSorts = concurrentSorts
      };

      return inputDeps;
    }

    public JobHandle Update(JobHandle inputDeps = default) {
      m_state.Cyles++;
      if (m_state.Status == SortState.EStatus.Sorting) {
        inputDeps = SortHelper.SortSlices(m_requiredBuffers, ref m_state, inputDeps);
        if (m_state.SortIndex >= m_state.BaseSliceCount)
          m_state.Status = SortState.EStatus.Merging;
      } else if (m_state.Status == SortState.EStatus.Merging) {
        inputDeps = SortHelper.MergeSlices(SortedData, m_requiredBuffers, ref m_state, inputDeps);
        if (m_state.IsComplete(m_requiredBuffers.Length)) {
          m_state.Status = SortState.EStatus.Complete;
          inputDeps = SortHelper.CopySortedData(SortedData, m_requiredBuffers, m_state, inputDeps);
        }
      }
      return inputDeps;
    }

    public void Dispose() {
      foreach (var buffer in m_requiredBuffers)
        buffer.Dispose();
      SortedData.Dispose();
    }

    #region internal helper

    private class SortHelper {

      public static JobHandle PrepareData<T>(NativeArray<T> source, NativeList<T> output, JobHandle inputDeps)
        where T : struct, IComparable<T> {

        inputDeps = new ResizeNativeList<T> {
          Source = output,
          Length = source.Length
        }.Schedule(inputDeps);
        inputDeps = new CopyToNativeArray<T> {
          Source = source,
          Target = output.AsDeferredJobArray()
        }.Schedule(source.Length, 64, inputDeps);
        return inputDeps;
      }

      public static JobHandle PrepaceBaseSlices<T>(NativeList<T> source, int sourceLength, int sliceCount, NativeList<T>[] buffers, JobHandle inputDeps)
        where T : struct, IComparable<T> {

        var sliceLength = (int)math.round((float)sourceLength / sliceCount);
        var copyHandle = default(JobHandle);
        for (var i = 0; i < sliceCount; ++i) {
          var bufferLength = sourceLength < 2 * sliceLength ? sourceLength : sliceLength;
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
          sourceLength -= sliceLength;
        }
        return JobHandle.CombineDependencies(inputDeps, copyHandle);
      }

      public static JobHandle SortSlices<T>(NativeList<T>[] buffers, ref SortState state, JobHandle inputDeps)
        where T : struct, IComparable<T> {

        var sortHandle = default(JobHandle);        
        var maxSorts = math.min(state.BaseSliceCount, state.ConcurrentSorts + state.SortIndex);
        for (var i = state.SortIndex; i < maxSorts; ++i) {
          sortHandle = JobHandle.CombineDependencies(
              sortHandle,
              new SortNativeArray<T> {
                Data = buffers[i]
              }.Schedule(inputDeps));
        }
        state.SortIndex += maxSorts - state.SortIndex;
        return sortHandle;
      }

      public static JobHandle MergeSlices<T>(
        NativeList<T> result,
        NativeList<T>[] buffers,
        ref SortState state,
        JobHandle inputDeps)
        where T : struct, IComparable<T> {

        var proceedToNextBuffer = false;
        var mergeAmount = state.ProcessAmount * state.ConcurrentSorts;
        var leftIndexUnit = new NativeUnit<int>(state.LeftIndex, Allocator.TempJob);
        var rightIndexUnit = new NativeUnit<int>(state.RightIndex, Allocator.TempJob);
        var resultIndexUnit = new NativeUnit<int>(state.ResultIndex, Allocator.TempJob);
        state.WriteBufferIndex += state.WriteBufferIndex == 0 ? state.BaseSliceCount : 0;
        while (state.WriteBufferIndex < buffers.Length && mergeAmount > 0) {
          var mergeInputDeps = default(JobHandle);
          if (state.ResultIndex == 0)
            mergeInputDeps = new ResizeNativeList<T> {
              Source = buffers[state.WriteBufferIndex],
              Length = buffers[state.ReadBufferIndex].Length + buffers[state.ReadBufferIndex + 1].Length
            }.Schedule(inputDeps);
          mergeInputDeps = new Merge<T> {
            LeftArray = buffers[state.ReadBufferIndex].AsDeferredJobArray(),
            RightArray = buffers[state.ReadBufferIndex + 1].AsDeferredJobArray(),
            Target = buffers[state.WriteBufferIndex].AsDeferredJobArray(),
            LeftIndex = leftIndexUnit,
            RightIndex = rightIndexUnit,
            ResultIndex = resultIndexUnit,
            MaxMergeCount = mergeAmount
          }.Schedule(mergeInputDeps);
          mergeInputDeps.Complete();
          mergeAmount -= resultIndexUnit.Value - state.ResultIndex;
          // We've reached the end of the write buffer so we can proceed to the next one
          proceedToNextBuffer = resultIndexUnit.Value == buffers[state.WriteBufferIndex].Length;
          if (proceedToNextBuffer) {
            state.ReadBufferIndex += 2;
            state.WriteBufferIndex++;
            leftIndexUnit.Value = rightIndexUnit.Value = resultIndexUnit.Value = 0;
            state.LeftIndex = state.RightIndex = state.ResultIndex = 0;
          } else
            break;
        }

        state.LeftIndex = proceedToNextBuffer ? 0 : leftIndexUnit.Value;
        state.RightIndex = proceedToNextBuffer ? 0 : rightIndexUnit.Value;
        state.ResultIndex = proceedToNextBuffer ? 0 : resultIndexUnit.Value;

        leftIndexUnit.Dispose();
        rightIndexUnit.Dispose();
        resultIndexUnit.Dispose();
        return inputDeps;
      }

      public static JobHandle CopySortedData<T>(NativeList<T> result, NativeList<T>[] buffers, SortState state, JobHandle inputDeps)
        where T : struct, IComparable<T> {

        if (state.SortLength == 0)
          return inputDeps;
        // The last buffer will always contain the sorted data
        inputDeps = new CopyToNativeArray<T> {
          Source = buffers[buffers.Length - 1].AsDeferredJobArray(),
          Target = result.AsDeferredJobArray()
        }.Schedule(state.SortLength, 128, inputDeps);
        return inputDeps;
      }

      [BurstCompile]
      struct Merge<T> : IJob where T : struct, IComparable<T> {
        [ReadOnly]
        public NativeArray<T> LeftArray;
        [ReadOnly]
        public NativeArray<T> RightArray;
        [WriteOnly]
        public NativeArray<T> Target;
        public NativeUnit<int> LeftIndex;
        public NativeUnit<int> RightIndex;
        public NativeUnit<int> ResultIndex;
        public int MaxMergeCount;
        public void Execute() {
          var leftIndex = LeftIndex.Value;
          var rightIndex = RightIndex.Value;
          var resultIndex = ResultIndex.Value;
          MergeSlices(Target, LeftArray, RightArray, ref leftIndex, ref rightIndex, ref resultIndex, resultIndex + MaxMergeCount);
          LeftIndex.Value = leftIndex;
          RightIndex.Value = rightIndex;
          ResultIndex.Value = resultIndex;
        }

        private static void MergeSlices(NativeArray<T> result, NativeArray<T> left, NativeArray<T> right) {
          var leftIndex = 0;
          var rightIndex = 0;
          var resultIndex = 0;
          MergeSlices(result, left, right, ref leftIndex, ref rightIndex, ref resultIndex);
        }

        private static void MergeSlices(
          NativeArray<T> result,
          NativeArray<T> left,
          NativeArray<T> right,
          ref int leftIndex,
          ref int rightIndex,
          ref int resultIndex,
          int maxMergeCount = 0) {

          while (leftIndex < left.Length || rightIndex < right.Length) {
            if (resultIndex >= maxMergeCount && maxMergeCount > 0)
              return;
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
      }
    }

    #endregion internal helper
  }
}