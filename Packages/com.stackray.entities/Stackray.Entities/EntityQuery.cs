using Stackray.Collections;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine.Jobs;

namespace Stackray.Entities {
  public static class EntityQueryExt {
    public static JobHandle ResizeBuffer<TBufferElementData>(
      this EntityQuery entityQuery,
      ComponentSystemBase system,
      int length,
      JobHandle inputDeps)
      where TBufferElementData : struct, IBufferElementData {

      return new ResizeBuffer<TBufferElementData> {
        BufferType = system.GetArchetypeChunkBufferType<TBufferElementData>(false),
        Length = length
      }.Schedule(entityQuery, inputDeps);
    }

    public static JobHandle ResizeBufferDeferred<TBufferElementData>(
      this EntityQuery entityQuery,
      ComponentSystemBase system,
      NativeCounter length,
      JobHandle inputDeps)
      where TBufferElementData : struct, IBufferElementData {

      return new ResizeBufferDeferred<TBufferElementData> {
        BufferType = system.GetArchetypeChunkBufferType<TBufferElementData>(false),
        Length = length
      }.Schedule(entityQuery, inputDeps);
    }

    public static JobHandle ClearBuffer<TBufferElementData>(
      this EntityQuery entityQuery,
      ComponentSystemBase system,
      JobHandle inputDeps)
      where TBufferElementData : struct, IBufferElementData {

      return new ResizeBuffer<TBufferElementData> {
        BufferType = system.GetArchetypeChunkBufferType<TBufferElementData>(false),
        Length = 0
      }.Schedule(entityQuery, inputDeps);
    }

    public static JobHandle CountBufferElements<TBufferElementData>(
      this EntityQuery entityQuery,
      EntityManager entityManager,
      ref NativeCounter counter,
      JobHandle inputDeps)
      where TBufferElementData : struct, IBufferElementData {

      return new CountBufferElements<TBufferElementData> {
        ChunkBufferType = entityManager.GetArchetypeChunkBufferType<TBufferElementData>(true),
        Counter = counter
      }.Schedule(entityQuery, inputDeps);
    }

    public static JobHandle ToDataWithIndex<TData, TComponentData>(
      this EntityQuery entityQuery,
      NativeArray<TData> sourceData,
      ref NativeArray<DataWithIndex<TData>> resultDataWithIndex,
      JobHandle inputDeps)
      where TData : struct, IComparable<TData>
      where TComponentData : struct, IComponentData {

      inputDeps = new ConvertToDataWithIndex<TData> {
        Source = sourceData,
        Target = resultDataWithIndex
      }.Schedule(sourceData.Length, 128, inputDeps);
      return inputDeps;
    }

    public static JobHandle ToDataWithEntity<TData>(
      this EntityQuery entityQuery,
      ComponentSystemBase system,
      NativeArray<TData> sourceData,
      ref NativeArray<DataWithEntity<TData>> resultDataWithEntity,
      JobHandle inputDeps)
      where TData : struct, IComparable<TData> {

      inputDeps = new ConvertToDataWithEntity<TData> {
        ChunkEntityType = system.GetArchetypeChunkEntityType(),
        Source = sourceData,
        Target = resultDataWithEntity
      }.Schedule(entityQuery, inputDeps);
      return inputDeps;
    }

    public static JobHandle ToEntityIndexMap(
      this EntityQuery entityQuery,
      EntityManager entityManager,
      ref NativeHashMap<Entity, int> resultEntityIndexMap,
      JobHandle inputDeps) {

      inputDeps = resultEntityIndexMap.Clear(inputDeps, entityQuery.CalculateEntityCountWithoutFiltering());
      inputDeps = new GatherEntityIndexMap {
        EntityType = entityManager.GetArchetypeChunkEntityType(),
        EntityIndexMap = resultEntityIndexMap.AsParallelWriter()
      }.Schedule(entityQuery, inputDeps);
      return inputDeps;
    }

    public static JobHandle ToEntityComponentMap<T>(
      this EntityQuery entityQuery,
      EntityManager entityManager,
      ref NativeHashMap<Entity, T> resultEntityComponentMap,
      JobHandle inputDeps)
      where T : struct, IComponentData {

      inputDeps = resultEntityComponentMap.Clear(inputDeps, entityQuery.CalculateEntityCountWithoutFiltering());
      inputDeps = new GatherEntityComponentMap<T> {
        ChunkEntityType = entityManager.GetArchetypeChunkEntityType(),
        ChunkDataType = entityManager.GetArchetypeChunkComponentType<T>(true),
        Result = resultEntityComponentMap.AsParallelWriter()
      }.Schedule(entityQuery, inputDeps);
      return inputDeps;
    }

    public static JobHandle GetChangedComponentDataFromEntity<T>(
      this EntityQuery query,
      ComponentSystemBase system,
      ref NativeHashMap<Entity, T> resultHashMap,
      JobHandle inputDeps)
      where T : struct, IComponentData {

      inputDeps = resultHashMap.Clear(inputDeps, query.CalculateEntityCount());
      inputDeps = new ChangedComponentToEntity<T> {
        EntityType = system.GetArchetypeChunkEntityType(),
        ChunkType = system.GetArchetypeChunkComponentType<T>(true),
        ChangedComponents = resultHashMap.AsParallelWriter(),
        LastSystemVersion = system.LastSystemVersion
      }.Schedule(query, inputDeps);
      return inputDeps;
    }

    public static JobHandle GetChangedTransformFromEntity(
      this EntityQuery query,
      ComponentSystemBase system,
      ref NativeHashMap<Entity, LocalToWorld> resultHashMap,
      JobHandle inputDeps) {

      inputDeps = resultHashMap.Clear(inputDeps, query.CalculateEntityCount());
      var entities = query.ToEntityArrayAsync(Allocator.TempJob, out var toEntityHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, toEntityHandle);
      inputDeps = new ChangedTransformsToEntity {
        Entities = entities,
        LocalToWorldFromEntity = system.GetComponentDataFromEntity<LocalToWorld>(true),
        ChangedComponents = resultHashMap.AsParallelWriter()
      }.Schedule(query.GetTransformAccessArray(), inputDeps);
      return inputDeps;
    }

    public static JobHandle CopyFromChangedComponentData<T>(
      this EntityQuery query,
      ComponentSystemBase system,
      ref NativeHashMap<Entity, T> resultChangedComponentData,
      JobHandle inputDeps)
      where T : struct, IComponentData {

      inputDeps = new CopyFromChangedComponentData<T> {
        EntityType = system.GetArchetypeChunkEntityType(),
        ChunkType = system.GetArchetypeChunkComponentType<T>(false),
        ChangedComponentData = resultChangedComponentData
      }.Schedule(query, inputDeps);
      return inputDeps;
    }

    public static JobHandle DestroyEntityOnly(
      this EntityQuery query,
      ComponentSystemBase system,
      EntityCommandBuffer entityCommandBuffer,
      JobHandle inputDeps) {

      return new DestroyEntitiesOnly {
        CmdBuffer = entityCommandBuffer.ToConcurrent(),
        EntityOnlyArchetype = system.EntityManager.CreateArchetype(),
        EntityType = system.GetArchetypeChunkEntityType()
      }.Schedule(query, inputDeps);
    }

    public static JobHandle GetChangedChunks<T>(
      this EntityQuery query,
      ComponentSystemBase system,
      Allocator allocator,
      ref NativeQueue<VTuple<int, int>>.ParallelWriter changedEntitySlices,
      JobHandle inputDeps,
      bool changeAll = false)
      where T : struct, IComponentData {

      var chunks = query.CreateArchetypeChunkArrayAsync(allocator, out var createChunksHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, createChunksHandle);
      var indicesState = new NativeArray<int>(chunks.Length, Allocator.TempJob);
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

      inputDeps = new ExtractChangedSlicesFromChunks {
        Source = indicesState,
        Chunks = chunks,
        Slices = changedEntitySlices
      }.Schedule(indicesState.Length, 64, inputDeps);
      inputDeps = indicesState.Dispose(inputDeps);
      return inputDeps;
    }
  }
}
