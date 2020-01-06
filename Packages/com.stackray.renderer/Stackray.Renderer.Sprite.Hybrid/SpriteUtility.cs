using System;
using System.Collections.Generic;
using System.Linq;
using Stackray.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

using UnityEngine;

namespace Stackray.Renderer {
  public class SpriteUtility {
    private static readonly Dictionary<int, Mesh> m_meshCache = new Dictionary<int, Mesh>();
    private static readonly Dictionary<Texture, Material> m_spriteMaterialCache = new Dictionary<Texture, Material>();
    private static Mesh m_defaultMesh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init() {
      m_meshCache.Clear();
      m_spriteMaterialCache.Clear();
      m_defaultMesh = null;
    }

    public static void CreateSpriteComponent(EntityManager entityManager, Entity entity, UnityEngine.SpriteRenderer spriteRenderer) {
      var sprite = spriteRenderer.sprite;
      var material = GetMaterial(sprite, spriteRenderer.sharedMaterial);
      var renderMesh = new RenderMesh {
        mesh = GetMesh(sprite),
        material = material 
      };

      if (entityManager.HasComponent<RenderMesh>(entity))
        entityManager.SetSharedComponentData(entity, renderMesh);
      else
        entityManager.AddSharedComponentData(entity, renderMesh);

      var tileOffset = default(TileOffsetProperty).Convert(spriteRenderer);
      entityManager.AddComponentData(entity,
        new TileOffsetProperty {
          Value = tileOffset
        });
      var scale = default(ScaleProperty).Convert(spriteRenderer);
      entityManager.AddComponentData(entity,
        new ScaleProperty { Value = scale.xyz });
      var pivot = default(PivotProperty).Convert(spriteRenderer);
      entityManager.AddComponentData(entity,
        new PivotProperty {
          Value = pivot
        });
      entityManager.AddComponentData(entity,
        new ColorProperty {
          Value = default(ColorProperty).Convert(spriteRenderer)
        });
      entityManager.AddComponentData(entity,
        new FlipProperty {
          Value = default(FlipProperty).Convert(spriteRenderer)
        });
      entityManager.AddComponentData(entity, new SpriteBounds {
        Value = renderMesh.mesh.bounds.ToAABB()
      });
      entityManager.AddComponentData(entity, new RenderBounds {
        Value = renderMesh.mesh.bounds.ToAABB()
      });
    }

    private static void CreateDefaultAnimation(EntityManager dstManager, Entity entity, Material material) {
      var animationBufferEntity = dstManager.CreateEntity();
      dstManager.AddSharedComponentData(entity, new SpriteAnimation {
        ClipSetEntity = animationBufferEntity
      });
      
      if (!dstManager.HasComponent<SpriteAnimationClipMaterials>(animationBufferEntity))
        dstManager.AddSharedComponentData(animationBufferEntity, new SpriteAnimationClipMaterials {
          Value = new List<Material>() { material }
        });
    }

    private static Mesh GetMesh(Sprite sprite, bool useQuad = true) {
      var mesh = GetDefaultMesh();
      var hashcode = 17;
      unchecked {
        hashcode = hashcode * 23 + GetArrayHashcode(sprite.vertices);
        hashcode = hashcode * 23 + GetArrayHashcode(sprite.uv);
        hashcode = hashcode * 23 + GetArrayHashcode(sprite.triangles);
      }
      if (!useQuad && !m_meshCache.TryGetValue(hashcode, out mesh)) {
        mesh = new Mesh() {
          vertices = sprite.vertices.Select(v => (Vector3)v).ToArray(),
          uv = sprite.uv,
          triangles = sprite.triangles.Select(t => (int)t).ToArray()
        };
        mesh.name = sprite.name;
        m_meshCache.Add(hashcode, mesh);
      }
      return mesh;
    }

    private static Material GetMaterial(Sprite sprite, Material sourceMaterial) {

      // Packing mode seems to be always Rectangle in the atlas is rectangle so we also check uv length
      if (/*sprite.packingMode != SpritePackingMode.Rectangle || */sprite.uv.Length != 4)
        throw new ArgumentException($"Wrong packing mode for sprite {sprite.name}.\nOnly sprites packed with 'Full Rect' packing mode are supported!");

      if (!m_spriteMaterialCache.TryGetValue(sprite.texture, out var material)) {
        material = new Material(sourceMaterial) {
          enableInstancing = true,
          mainTexture = sprite.texture
        };
        material.name = $"{sourceMaterial.name}-{sprite.texture.name}";
        m_spriteMaterialCache.Add(sprite.texture, material);
      }
      return material;
    }

    public static Mesh GetDefaultMesh() {
      m_defaultMesh = m_defaultMesh ?? new Mesh {
        vertices = new Vector3[] {
          new Vector3(0, 1),
          new Vector3(1, 1),
          new Vector3(0, 0),
          new Vector3(1, 0)
        },
        uv = new Vector2[] {
          new Vector2(0, 1),
          new Vector2(1, 1),
          new Vector2(0, 0),
          new Vector2(1, 0)
        },
        triangles = new int[] {
          0, 1, 2,
          2, 1, 3
        }
      };
      return m_defaultMesh;
    }

    public static SpriteAnimationClipBufferElement<TProperty, TData> CreateClipSet<TProperty, TData>(GameObject go, AnimationClip clip, out Material animationMaterial)
      where TProperty : IDynamicBufferProperty<TData>
      where TData : struct, IEquatable<TData> {
      var spriteAnimationClip = SampleAnimationClip<TProperty, TData>(go, clip, out animationMaterial);
      return new SpriteAnimationClipBufferElement<TProperty, TData> {
        ClipName = new NativeString32(clip.name),
        Value = CreateClipSet(spriteAnimationClip, clip)
      };
    }

    public static BlobAssetReference<ClipSet<TProperty, TData>> CreateClipSet<TProperty, TData>(NativeArray<SpriteAnimationClip<TProperty, TData>> data, AnimationClip clip)
      where TProperty : IComponentValue<TData>
      where TData : struct, IEquatable<TData> {
      if (!data.IsCreated)
        return default;
      using (var builder = new BlobBuilder(Allocator.Temp)) {
        ref var root = ref builder.ConstructRoot<ClipSet<TProperty, TData>>();
        root.AnimationLength = clip.length;
        root.Loop = clip.isLooping;
        var clips = builder.Allocate(ref root.Value, data.Length);
        for (var i = 0; i < data.Length; ++i)
          clips[i] = data[i];

        return builder.CreateBlobAssetReference<ClipSet<TProperty, TData>>(Allocator.Persistent);
      }
    }

    public static NativeArray<SpriteAnimationClip<TProperty, TData>> SampleAnimationClip<TProperty, TData>(GameObject go, AnimationClip clip, out Material animationMaterial)
      where TProperty : IDynamicBufferProperty<TData>
      where TData : struct, IEquatable<TData> {

      animationMaterial = null;
      var isPrefab = go.scene.rootCount == 0;
      var rootGameObject = isPrefab ? UnityEngine.Object.Instantiate(go) : go;
      var renderer = rootGameObject.GetComponent<UnityEngine.SpriteRenderer>();
      var changeDetected = false;
      var cache = new SpriteRendererCache(renderer);
      var animationClip = new NativeArray<SpriteAnimationClip<TProperty, TData>>(Mathf.CeilToInt(clip.frameRate * clip.length), Allocator.Temp);
      for (var i = 0; i < animationClip.Length; ++i) {
        var originalData = default(TProperty).Convert(renderer);
        var normalizedTime = (float)i / animationClip.Length;
        var oldWrapMode = clip.wrapMode;
        clip.wrapMode = WrapMode.Clamp;
        clip.SampleAnimation(rootGameObject, normalizedTime);
        clip.wrapMode = oldWrapMode;
        animationMaterial = animationMaterial ?? GetMaterial(renderer.sprite, renderer.sharedMaterial);
        if (!m_spriteMaterialCache.TryGetValue(renderer.sprite.texture, out var spriteMaterial) || spriteMaterial != animationMaterial)
          throw new ArgumentException($"Sprite {renderer.sprite.name} is not in the correct atlas texture of Animation {clip.name}\n" +
            $"Make sure all sprites are on the same atlas texture.");
        animationClip[i] = new SpriteAnimationClip<TProperty, TData> { Value = default(TProperty).Convert(renderer) };
        changeDetected |= !originalData.Equals(animationClip[i].Value);
      }
      cache.Restore(renderer);

      if (isPrefab)
        UnityEngine.Object.DestroyImmediate(rootGameObject);

      return changeDetected ? animationClip : default;
    }

    private static int GetArrayHashcode<T>(T[] array) where T : struct {
      var hashcode = array.Length;
      for (int i = 0; i < array.Length; ++i)
        hashcode = unchecked(hashcode * 17 + array[i].GetHashCode());
      return hashcode;
    }
  }
}