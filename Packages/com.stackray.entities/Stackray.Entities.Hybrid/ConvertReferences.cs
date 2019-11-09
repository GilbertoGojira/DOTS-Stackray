using Stackray.Entities;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Entities {

  [ScriptOrder(-100)]
  public abstract class ConvertReferences : MonoBehaviour {
    protected Dictionary<GameObject, Entity> m_referencedEntities = new Dictionary<GameObject, Entity>();

    protected void AddReference(GameObject go) {
      if (!ValidateObject(go, out var targetObjectConversion))
        return;
      var objRef = go.AddComponent<OutOfHierarchyGameObjectReference>();
      var awakeMethod = typeof(ConvertToEntity).GetMethod("Awake", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      awakeMethod.Invoke(targetObjectConversion, Array.Empty<object>());
      m_referencedEntities.Add(objRef.gameObject, objRef.Entity);
      DestroyImmediate(targetObjectConversion);
    }

    protected Entity GetPrimaryEntity(GameObject go) {
      if (!m_referencedEntities.TryGetValue(go, out var entity))
        Debug.LogWarning($"GetPrimaryEntity({go}) was not included in the conversion and will be ignored.");
      return entity;
    }

    private bool ValidateObject(GameObject go, out ConvertToEntity convertToEntity) {
      convertToEntity = go.GetComponentInParent<ConvertToEntity>() ?? go.GetComponent<ConvertToEntity>();
      if (convertToEntity == null) {
        Debug.LogWarning($"{go} does not have {nameof(convertToEntity)} attached and will be ignored");
        return false;
      }
      return true;
    }

    #region helper conversion
    class OutOfHierarchyGameObjectReference : MonoBehaviour, IConvertGameObjectToEntity {
      public Entity Entity { get; private set; }

      public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
        Entity = entity;
      }
    }
    #endregion helper conversion
  }
}