using Stackray.Jobs;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Entities {

  /// <summary>
  /// Temp struct until Burst supports ValueTuple<>
  /// </summary>
  /// <typeparam name="T1"></typeparam>
  /// <typeparam name="T2"></typeparam>
  /// <typeparam name="T3"></typeparam>
  public struct VTuple<T1, T2, T3>
  where T1 : struct
  where T2 : struct
  where T3 : struct {

    public T1 Item1;
    public T2 Item2;
    public T3 Item3;

    public VTuple(T1 item1, T2 item2, T3 item3) {
      Item1 = item1;
      Item2 = item2;
      Item3 = item3;
    }

    public override string ToString() {
      return $"({Item1}, {Item2}, {Item3})";
    }
  }

  public static class Extensions {

    public static JobHandle GetChangedChunks<T>(
      this ComponentSystemBase system,
      EntityQuery query,
      Allocator allocator,
      out NativeArray<int> indicesState,
      out NativeQueue<VTuple<int, int, int>> changedSlices,
      JobHandle inputDeps,
      bool changeAll = false,
      int offset = 0)
      where T : struct, IComponentData {

      var chunks = query.CreateArchetypeChunkArray(allocator, out var createChunksHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, createChunksHandle);
      indicesState = new NativeArray<int>(chunks.Length, allocator);
      inputDeps = new MemsetNativeArray<int> {
        Source = indicesState,
        Value = -1
      }.Schedule(indicesState.Length, 64, inputDeps);
      inputDeps = new GatherChunkChanged<T> {
        ChunkType = system.GetArchetypeChunkComponentType<T>(true),
        ChangedIndices = indicesState,
        LastSystemVersion = system.LastSystemVersion,
        ForceChange = changeAll
      }.Schedule(query, inputDeps);

      changedSlices = new NativeQueue<VTuple<int, int, int>>(Allocator.TempJob);
      inputDeps = new ExtractChangedSlicesFromChunks {
        Source = indicesState,
        Chunks = chunks,
        Slices = changedSlices.AsParallelWriter(),
        Offset = offset
      }.Schedule(indicesState.Length, 64, inputDeps);
      return inputDeps;
    }

    public static NativeArray<DataWithIndex<TData>> ToDataWithIndex<TData, TComponentData>(
      this EntityQuery entityQuery,
      NativeArray<TData> sourceData,
      Allocator allocator,
      JobHandle inputDeps,
      out JobHandle outputDeps)
      where TData : struct, IComparable<TData>
      where TComponentData : struct, IComponentData {

      var result = new NativeArray<DataWithIndex<TData>>(sourceData.Length, allocator);
      var componentData = entityQuery.ToComponentDataArray<TComponentData>(allocator, out var jobHandle);
      outputDeps = JobHandle.CombineDependencies(jobHandle, inputDeps);
      outputDeps = new ConvertToDataWithIndex<TData> {
        Source = sourceData,
        Target = result
      }.Schedule(sourceData.Length, 128, outputDeps);
      return result;
    }

    public static NativeArray<DataWithEntity<TData>> ToDataWithEntity<TData, TComponentData>(
      this EntityQuery entityQuery,
      NativeArray<TData> sourceData,
      Allocator allocator,
      JobHandle inputDeps,
      out JobHandle outputDeps)
      where TData : struct, IComparable<TData>
      where TComponentData : struct, IComponentData {

      var result = new NativeArray<DataWithEntity<TData>>(sourceData.Length, allocator);
      var componentData = entityQuery.ToComponentDataArray<TComponentData>(allocator, out var jobHandle);
      outputDeps = JobHandle.CombineDependencies(jobHandle, inputDeps);
      outputDeps = new ConvertToDataWithEntity<TData, TComponentData> {
        Source = sourceData,
        Target = result
      }.Schedule(entityQuery, outputDeps);
      return result;
    }

    public static NativeHashMap<Entity, int> ToEntityIndexMap(
      this EntityQuery entityQuery,
      EntityManager entityManager,
      Allocator allocator,
      JobHandle inputDeps,
      out JobHandle outputDeps) {

      var result = new NativeHashMap<Entity, int>(entityQuery.CalculateEntityCountWithoutFiltering(), allocator);
      outputDeps = new GatherEntityIndexMap {
        EntityType =  entityManager.GetArchetypeChunkEntityType(),
        EntityIndexMap = result.AsParallelWriter()
      }.Schedule(entityQuery, inputDeps);
      return result;
    }

    public static NativeHashMap<Entity, int> ToEntityIndexMap(
      this EntityQuery entityQuery,
      EntityManager entityManager,
      NativeHashMap<Entity, int> targetEntityIndexMap,
      JobHandle inputDeps,
      out JobHandle outputDeps) {

      inputDeps = new ClearNativeHashMap<Entity, int> {
        Source = targetEntityIndexMap,
        Capacity = entityQuery.CalculateEntityCountWithoutFiltering()
      }.Schedule(inputDeps);
      outputDeps = new GatherEntityIndexMap {
        EntityType = entityManager.GetArchetypeChunkEntityType(),
        EntityIndexMap = targetEntityIndexMap.AsParallelWriter()
      }.Schedule(entityQuery, inputDeps);
      return targetEntityIndexMap;
    }

    public static JobHandle DestroyEntityOnly(
      this EntityManager entityManager, 
      ComponentSystemBase system, 
      EntityQuery query, 
      EntityCommandBuffer entityCommandBuffer,
      JobHandle inputDeps) {

      return new DestroyEntitiesOnly {
        CmdBuffer = entityCommandBuffer.ToConcurrent(),
        EntityOnlyArchetype = entityManager.GetEntityOnlyArchetype(),
        EntityType = system.GetArchetypeChunkEntityType()
      }.Schedule(query, inputDeps);
    }
  }
}