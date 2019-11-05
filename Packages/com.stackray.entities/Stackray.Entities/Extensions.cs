using Stackray.Jobs;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;

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
        EntityType = entityManager.GetArchetypeChunkEntityType(),
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

    public static NativeHashMap<Entity, T> GetChangedComponentDataFromEntity<T>(
      this EntityQuery query,
      ComponentSystemBase system,
      NativeHashMap<Entity, T> hashMap,
      JobHandle inputDeps,
      out JobHandle outputDeps)
      where T : struct, IComponentData {

      inputDeps = new ClearNativeHashMap<Entity, T> {
        Source = hashMap,
        Capacity = query.CalculateEntityCount()
      }.Schedule(inputDeps);
      inputDeps = new ChangedComponentToEntity<T> {
        EntityType = system.GetArchetypeChunkEntityType(),
        ChunkType = system.GetArchetypeChunkComponentType<T>(true),
        ChangedComponents = hashMap.AsParallelWriter(),
        LastSystemVersion = system.LastSystemVersion
      }.Schedule(query, inputDeps);
      outputDeps = inputDeps;
      return hashMap;
    }

    public static NativeHashMap<Entity, LocalToWorld> GetChangedTransformFromEntity(
      this EntityQuery query,
      ComponentSystemBase system,
      NativeHashMap<Entity, LocalToWorld> hashMap,
      JobHandle inputDeps,
      out JobHandle outputDeps) {

      inputDeps = new ClearNativeHashMap<Entity, LocalToWorld> {
        Source = hashMap,
        Capacity = query.CalculateEntityCount()
      }.Schedule(inputDeps);
      var entities = query.ToEntityArray(Allocator.TempJob, out var toEntityHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, toEntityHandle);
      inputDeps = new ChangedTransformsToEntity {
        Entities = entities,
        LocalToWorldFromEntity = system.GetComponentDataFromEntity<LocalToWorld>(true),
        ChangedComponents = hashMap.AsParallelWriter()
      }.Schedule(query.GetTransformAccessArray(), inputDeps);
      outputDeps = inputDeps;
      return hashMap;
    }

    public static void CopyFromChangedComponentData<T>(
      this EntityQuery query,
      NativeHashMap<Entity, T> changedComponentData,
      JobHandle inputDeps,
      out JobHandle outputDeps)
      where T : struct, IComponentData {

      inputDeps = new CopyFromChangedComponentData<T> {
        ChangedComponentData = changedComponentData
      }.Schedule(query, inputDeps);
      outputDeps = inputDeps;
    }

    public static void UpdateInSystemGroup<T>(this World world, Type systemType) 
      where T : ComponentSystemGroup {

      var system = world.GetOrCreateSystem(systemType);
      var groupsAssignedToSystem = GetSystemGroups(world, system);
      foreach (var groupSys in groupsAssignedToSystem)
        groupSys.RemoveSystemFromUpdateList(system);

      var groupMgr = world.GetOrCreateSystem<T>();
      groupMgr.AddSystemToUpdateList(system);
    }

    public static ComponentSystemBase ForceGetOrCreateSystem(this World world, Type type) {
      var system = world.GetOrCreateSystem(type);
      if (!IsSystemAssignedToAnyGoup(world, system)) {
        var groups = type.GetCustomAttributes(typeof(UpdateInGroupAttribute), true);
        if (groups.Length == 0) {
          var simulationSystemGroup = world.GetOrCreateSystem<SimulationSystemGroup>();
          simulationSystemGroup.AddSystemToUpdateList(system);
        }

        foreach (var g in groups) {
          var group = g as UpdateInGroupAttribute;
          if (group == null)
            continue;

          if (!(typeof(ComponentSystemGroup)).IsAssignableFrom(group.GroupType)) {
            Debug.LogError($"Invalid [UpdateInGroup] attribute for {type}: {group.GroupType} must be derived from ComponentSystemGroup.");
            continue;
          }

          var groupMgr = world.GetOrCreateSystem(group.GroupType);
          if (groupMgr == null) {
            Debug.LogWarning(
                $"Skipping creation of {type} due to errors creating the group {group.GroupType}. Fix these errors before continuing.");
            continue;
          }
          var groupSys = groupMgr as ComponentSystemGroup;
          if (groupSys != null) {
            groupSys.AddSystemToUpdateList(world.GetOrCreateSystem(type) as ComponentSystemBase);
          }
        }
      }
      return system;
    }

    public static T ForceGetOrCreateSystem<T>(this World world) where T : ComponentSystemBase {
      var system = world.GetOrCreateSystem<T>();
      return ForceGetOrCreateSystem(world, typeof(T)) as T;
    }

    private static bool IsSystemAssignedToAnyGoup(World world, ComponentSystemBase system) {
      var groups = world.Systems.Where(s => s is ComponentSystemGroup).Cast<ComponentSystemGroup>();
      return groups.Any(group => group.Systems.Any(s => s == system));
    }

    private static ComponentSystemGroup[] GetSystemGroups(World world, ComponentSystemBase system) {
      var groups = world.Systems.Where(s => s is ComponentSystemGroup).Cast<ComponentSystemGroup>();
      return groups.Where(group => group.Systems.Any(s => s == system)).ToArray();
    }
  }
}