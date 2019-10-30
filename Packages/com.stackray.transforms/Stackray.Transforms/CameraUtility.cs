using Stackray.Mathematics;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Transforms {
  public class CameraUtility {

    private static float m_screenDpi = -1f;
    private static float m_fallbackDpi = 96f;

    public static float ScreenDpi {
      get {
        m_screenDpi = m_screenDpi < 0f ? Screen.dpi : m_screenDpi;
        return math.max(m_screenDpi, m_fallbackDpi);
      }
    }

    /// <summary>
    /// Thread safe calc Screen rect
    /// </summary>
    /// <param name="projectionMatrix"></param>
    /// <param name="worldToCameraMatrix"></param>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static float2 WorldToScreenPoint([ReadOnly]float4x4 projectionMatrix, [ReadOnly]float4x4 worldToCameraMatrix, float3 pos) {
      var world2Screen = math.mul(projectionMatrix, worldToCameraMatrix);
      var screenPos = world2Screen.MultiplyPoint(pos);
      // (-1, 1)'s clip => (0 ,1)'s viewport
      screenPos = new float3(screenPos.x + 1f, screenPos.y + 1f, screenPos.z + 1f) / 2f;
      // viewport => screen
      return new float2(screenPos.x * Screen.width, screenPos.y * Screen.height);
    }

    public static float3 ScreenToWorldPoint([ReadOnly]float4x4 projectionMatrix, [ReadOnly]float4x4 worldToCameraMatrix, [ReadOnly]float4x4 localToWorldMatrix, [ReadOnly]float3 screenPos) {
      var world2Screen = math.mul(math.mul(projectionMatrix, worldToCameraMatrix), localToWorldMatrix);
      var screen2World = math.inverse(math.mul(projectionMatrix, worldToCameraMatrix));
      var depth = world2Screen.MultiplyPoint(screenPos).z;
      // viewport pos (0 ,1)
      var viewPos = new float3(screenPos.x / Screen.width, screenPos.y / Screen.height, (depth + 1f) / 2f);
      // clip pos (-1, 1) 
      var clipPos = viewPos * 2f - new float3(1);
      // world pos
      return screen2World.MultiplyPoint(clipPos);
    }

    public static Ray ScreenPointToRay(float4x4 projectionMatrix, float4x4 worldToCameraMatrix, float4x4 localToWorldMatrix, float3 forward, float3 screenPos) {
      return new Ray(ScreenToWorldPoint(projectionMatrix, worldToCameraMatrix, localToWorldMatrix, screenPos), forward);
    }

    /// <summary>
    ///  Get how many world points is a physical value
    /// </summary>
    /// <param name="worldPos">the position in the world where are calculating</param>
    /// <param name="physicalValue">the physical value that we pretend to convert into world points (in inches)</param>
    /// <param name="fallbackDpi">the dpi that we want to fallback into</param>
    /// <returns></returns>
    public static float WorldUnitsPerInch(
      [ReadOnly]float4x4 projectionMatrix,
      [ReadOnly]float4x4 worldToCameraMatrix,
      [ReadOnly]float4x4 localToWorldMatrix,
      float3 worldPos,
      float dpi,
      float fallbackDpi = 96f) {
      return WorldPointsPerPixel(projectionMatrix, worldToCameraMatrix, localToWorldMatrix, worldPos) * math.max(dpi, fallbackDpi);
    }

    /// <summary>
    /// Gets how many world points is a pixel at a given position related to the camera
    /// </summary>
    /// <param name="position">position to calculate the ppp</param>
    /// <returns></returns>
    public static float WorldPointsPerPixel(
      [ReadOnly]float4x4 projectionMatrix,
      [ReadOnly]float4x4 worldToCameraMatrix,
      [ReadOnly]float4x4 localToWorldMatrix,
      float3 worldPos) {
      var distanceToCamera = new Plane(localToWorldMatrix.Forward(), localToWorldMatrix.Position()).GetDistanceToPoint(worldPos);
      var ppp = ScreenToWorldPoint(projectionMatrix, worldToCameraMatrix, localToWorldMatrix, new float3(1, 0, distanceToCamera))
        - ScreenToWorldPoint(projectionMatrix, worldToCameraMatrix, localToWorldMatrix, new float3(0, 0, distanceToCamera));
      return math.length(ppp);
    }

    public static float WorldPointsPerPixelFactor(
    [ReadOnly]float4x4 projectionMatrix,
    [ReadOnly]float4x4 worldToCameraMatrix,
    [ReadOnly]float4x4 localToWorldMatrix) {
      var position = localToWorldMatrix.Position();
      var forward = localToWorldMatrix.Forward();
      return math.abs(WorldPointsPerPixel(projectionMatrix, worldToCameraMatrix, localToWorldMatrix, position + forward)
        - WorldPointsPerPixel(projectionMatrix, worldToCameraMatrix, localToWorldMatrix, position + forward * 2));
    }

    public static Mesh GenerateQuad(Sprite sprite) {
      return sprite ? GenerateQuad(sprite, Quaternion.identity) : default;
    }

    public static Mesh GenerateQuad(Sprite sprite, quaternion rotation) {
      var mesh = new Mesh();
      mesh.name = sprite.name;
      var vertices = Array.ConvertAll(sprite.vertices, v => new Vector3(v.x, v.y, 0)).ToArray();
      var center = RotateVertrex(Vector3.zero, rotation, sprite.bounds.center);
      RotateVertices(Vector3.zero, rotation, vertices);
      mesh.SetVertices(vertices.ToList());
      mesh.SetUVs(0, sprite.uv.ToList());
      mesh.SetTriangles(Array.ConvertAll(sprite.triangles, t => (int)t), 0);
      return mesh;
    }

    public static void RotateVertices(Vector3 center, Quaternion rotation, Vector3[] vertices) {
      for (var i = 0; i < vertices.Length; ++i)
        vertices[i] = RotateVertrex(center, rotation, vertices[i]);
    }

    public static Vector3 RotateVertrex(Vector3 center, Quaternion rotation, Vector3 vertex) {
      return rotation * (vertex - center) + center;
    }

    public static float DistanceToEdge(AABB bounds, float3 origin, float3 direction) {
      var v = direction * float.MaxValue;
      var extents = bounds.Extents - origin;
      var x = math.clamp(v.x, -extents.x, extents.x);
      v = x == 0 ? v : v * x / v.x;
      var y = math.clamp(v.y, -extents.y, extents.y);
      v = y == 0 ? v : v * y / v.y;
      var z = math.clamp(v.z, -extents.z, extents.z);
      v = z == 0 ? v : v * z / v.z;
      return math.length(v);
    }

    public static float4x4 GetBestCameraPosition(float fieldOfView, AABB bounds, float bufferAmt) {
      return GetBestCameraPosition(fieldOfView, bounds, new float3(0, 0, 1), new float3(0, 1, 0), bufferAmt);
    }

    public static float4x4 GetBestCameraPosition(float fieldOfView, AABB bounds, float3 forward, float3 up, float bufferAmt) {
      // Control how much space you want around your target with bufferAmt
      var distance = bounds.Size.y / math.tan(math.radians(fieldOfView * 0.5f)) * bufferAmt;
      var position = bounds.Center - forward * distance;
      var rotation = quaternion.LookRotation(forward, up);
      return new float4x4(rotation, position);
    }

    public static Sprite CreateSprite(int2 size) {
      return CreateSprite(new Texture2D(size.x, size.y));
    }

    public static Sprite CreateSprite(Texture2D texture) {
      return Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
    }

    public static int GetHashCode<T>(DynamicBuffer<T> buffer) where T : struct {
      unchecked // disable overflow, for the unlikely possibility that you
      {         // are compiling with overflow-checking enabled
        int hash = 27;
        for (var i = 0; i < buffer.Length; ++i)
          hash = (13 * hash) + buffer[i].GetHashCode();
        return hash;
      }
    }

    public static AABB GetAABB(Bounds bounds) {
      return new AABB { Center = bounds.center, Extents = bounds.extents };
    }

    public static float3 GetRandomValueInRadius(ref Unity.Mathematics.Random random, float3 center, float radius) {
      return center + random.NextFloat3(new float3(-1), new float3(1)) * radius;
    }
  }
}
