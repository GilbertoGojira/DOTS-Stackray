using System;
using System.Linq;
using Unity.Entities;
using UnityEngine;

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

  public static class WorldExt {
    
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

    public static bool IsSystemAssignedToAnyGoup(this World world, ComponentSystemBase system) {
      var groups = world.Systems.Where(s => s is ComponentSystemGroup).Cast<ComponentSystemGroup>();
      return groups.Any(group => group.Systems.Any(s => s == system));
    }

    public static ComponentSystemGroup[] GetSystemGroups(this World world, ComponentSystemBase system) {
      var groups = world.Systems.Where(s => s is ComponentSystemGroup).Cast<ComponentSystemGroup>();
      return groups.Where(group => group.Systems.Any(s => s == system)).ToArray();
    }
  }
}