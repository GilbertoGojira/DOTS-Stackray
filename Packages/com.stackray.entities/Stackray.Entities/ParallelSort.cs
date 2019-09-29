using Stackray.Jobs;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stackray.Entities {

  public struct SortBuffer<T> : IDisposable where T : struct, IComparable<T> {
    private NativeArray<T>[] m_data;

    public NativeArray<T> this[int index] {
      get => m_data[index];
      set => m_data[index] = value;
    }

    public int Lenght {
      get => m_data.Length;
    }

    public SortBuffer(int length) {
      m_data = new NativeArray<T>[length];
    }

    public void Dispose() {
      for (var i = 0; i < m_data.Length; ++i)
        if (m_data[i].IsCreated)
          m_data[i].Dispose();
    }
  }

  public class NativeSort<T> where T : struct, IComparable<T> {
    public JobHandle Sort(NativeArray<T> array, JobHandle inputDeps) {
      return new ParallelSortUtility.SortNativeArrayJob<T> {
        NativeArray = array
      }.Schedule(inputDeps);
    }
  }

  public class ParallelSort<T> : IDisposable
    where T : struct, IComparable<T> {

    private const int THRESHOLD = 1024;
    private SortBuffer<T> m_buffer;

    public ParallelSort() {
      m_buffer = new SortBuffer<T>(0);
    }

    public JobHandle Sort(NativeArray<T> sourceArray, int maxConcurrentJobs, JobHandle inputDeps) {
      maxConcurrentJobs = math.max(1, maxConcurrentJobs);
      var concurrentJobs = sourceArray.Length < THRESHOLD ? 1 : (float)sourceArray.Length / maxConcurrentJobs <= 1 ? 1 : maxConcurrentJobs;
      if (m_buffer.Lenght != concurrentJobs) {
        m_buffer.Dispose();
        m_buffer = new SortBuffer<T>(concurrentJobs);
      }
      inputDeps = ParallelSortUtility.SliceArray(sourceArray, m_buffer, inputDeps);
      inputDeps = ParallelSortUtility.SortSlices(m_buffer, inputDeps);
      inputDeps = ParallelSortUtility.Merge(sourceArray, m_buffer, inputDeps);
      return inputDeps;
    }

    public void Dispose() {
      m_buffer.Dispose();
    }
  }

  public class ParallelSortUtility {

    public static JobHandle SliceArray<T>(NativeArray<T> source, SortBuffer<T> buffer, JobHandle inputDeps) where T : struct, IComparable<T> {
      var length = source.Length;
      var sliceLength = source.Length / buffer.Lenght;
      var copyHandle = new JobHandle();
      buffer.Dispose();
      for (var i = 0; i < buffer.Lenght; ++i) {
        buffer[i] = new NativeArray<T>(math.min(sliceLength, length), Allocator.TempJob);
        copyHandle = JobHandle.CombineDependencies(
            copyHandle,
            new CopySliceToArray<T> {
              Source = source.Slice(i * sliceLength, buffer[i].Length),
              Target = buffer[i]
            }.Schedule(buffer[i].Length, 64, inputDeps));
        length -= sliceLength;
      }
      return copyHandle;
    }

    public static JobHandle SortSlices<T>(SortBuffer<T> buffer, JobHandle inputDeps) where T : struct, IComparable<T> {
      var sortHandle = new JobHandle();
      for (var i = 0; i < buffer.Lenght; ++i) {
        sortHandle = JobHandle.CombineDependencies(
            sortHandle,
            new SortNativeArrayJob<T> {
              NativeArray = buffer[i]
            }.Schedule(inputDeps));
      }
      return sortHandle;
    }

    public static JobHandle Merge<TSource>(NativeArray<TSource> result, SortBuffer<TSource> buffer, JobHandle inputDeps)
      where TSource : struct, IComparable<TSource> {

      var count = 0;
      for (var i = 0; i < buffer.Lenght; ++i) {
        var output = new NativeArray<TSource>(count, Allocator.TempJob);
        inputDeps = new CopyToNativeArrayJob<TSource> {
          Source = result,
          Target = output
        }.Schedule(output.Length, 64, inputDeps);
        inputDeps = new MergeBucket<TSource> {
          LeftArray = output,
          RightArray = buffer[i],
          Target = result
        }.Schedule(inputDeps);
        count += buffer[i].Length;
      }
      return inputDeps;
    }

    public static void Merge<TSource>(NativeArray<TSource> result, NativeArray<TSource> left, NativeArray<TSource> right, int offSet = 0)
      where TSource : struct, IComparable<TSource> {

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
    public struct CopySliceToArray<T> : IJobParallelFor where T : struct {
      [ReadOnly]
      public NativeSlice<T> Source;
      [WriteOnly]
      public NativeArray<T> Target;

      public void Execute(int index) {
        Target[index] = Source[index];
      }
    }
    /// <summary>
    /// Sorts a native array
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [BurstCompile]
    public struct SortNativeArrayJob<T> : IJob where T : struct, IComparable<T> {
      public NativeArray<T> NativeArray;
      public void Execute() {
        NativeArray.Sort();
      }
    }

    [BurstCompile]
    public struct MergeBucket<T> : IJob where T : struct, IComparable<T> {
      [ReadOnly]
      [DeallocateOnJobCompletion]
      public NativeArray<T> LeftArray;
      [ReadOnly]
      public NativeArray<T> RightArray;
      [WriteOnly]
      public NativeArray<T> Target;
      public void Execute() {
        ParallelSortUtility.Merge(Target, LeftArray, RightArray);
      }
    }
  }
}