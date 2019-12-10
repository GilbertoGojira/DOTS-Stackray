#if UNITY_2020_1_OR_NEWER
using VerticalAlignmentOptions = TMPro.VerticalAlignmentOptions;
using HorizontalAlignmentOptions = TMPro.HorizontalAlignmentOptions;
#else
using VerticalAlignmentOptions = TMPro._VerticalAlignmentOptions;
using HorizontalAlignmentOptions = TMPro._HorizontalAlignmentOptions;
#endif
using Stackray.Collections;
using Stackray.Mathematics;
using Stackray.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Stackray.Text {
  [UpdateAfter(typeof(SortSystem))]
  public class TextMeshBuildSystem : JobComponentSystem {
    EntityQuery m_textQuery;

    protected override void OnCreate() {
      m_textQuery = GetEntityQuery(
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<LocalRectTransform>(),
                    ComponentType.ReadOnly<TextRenderer>(),
                    ComponentType.ReadOnly<TextData>(),
                    ComponentType.ReadOnly<VertexColor>(),
                    ComponentType.ReadOnly<VertexColorMultiplier>(),
                    ComponentType.ReadWrite<Vertex>(),
                    ComponentType.ReadWrite<VertexIndex>(),
                    ComponentType.ReadWrite<TextLine>());
    }

    [BurstCompile]
    struct TextChunkBuilder : IJobChunk {
      [ReadOnly]
      public ArchetypeChunkComponentType<LocalRectTransform> LocalRectTransformType;
      [ReadOnly]
      public ArchetypeChunkComponentType<VertexColor> ColorValueType;
      [ReadOnly]
      public ArchetypeChunkComponentType<VertexColorMultiplier> ColorMultiplierType;
      [ReadOnly]
      public ArchetypeChunkComponentType<TextRenderer> TextRendererType;
      [ReadOnly]
      public ArchetypeChunkComponentType<LocalToWorld> LocalToWorldType;
      [ReadOnly]
      public ArchetypeChunkComponentType<TextData> TextDataType;
      [ReadOnly]
      public ComponentDataFromEntity<TextFontAsset> FontAssetFromEntity;
      [ReadOnly]
      public BufferFromEntity<FontGlyph> FontGlyphFromEntity;

      [NativeDisableContainerSafetyRestriction]
      [WriteOnly]
      public ArchetypeChunkBufferType<Vertex> VertexType;
      [NativeDisableContainerSafetyRestriction]
      [WriteOnly]
      public ArchetypeChunkBufferType<VertexIndex> VertexIndexType;
      [NativeDisableContainerSafetyRestriction]
      [WriteOnly]
      public ArchetypeChunkBufferType<TextLine> TextLineType;

      public uint LastSystemVersion;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!chunk.DidChange(LocalToWorldType, LastSystemVersion) &&
          !chunk.DidChange(TextDataType, LastSystemVersion) &&
          !chunk.DidChange(TextRendererType, LastSystemVersion) &&
          !chunk.DidChange(ColorValueType, LastSystemVersion) &&
          !chunk.DidChange(ColorMultiplierType, LastSystemVersion) &&
          !chunk.DidChange(LocalRectTransformType, LastSystemVersion))
          return;

        var textDataArray = chunk.GetNativeArray(TextDataType);
        var worldRenderBoundsArray = chunk.GetNativeArray(LocalRectTransformType);
        var textRendererArray = chunk.GetNativeArray(TextRendererType);
        var vertexColorArray = chunk.GetNativeArray(ColorValueType);
        var vertexColorMultiplierArray = chunk.GetNativeArray(ColorMultiplierType);
        var localToWorldArray = chunk.GetNativeArray(LocalToWorldType);

        var vertexBufferAccessor = chunk.GetBufferAccessor(VertexType);
        var vertexIndexBufferAccessor = chunk.GetBufferAccessor(VertexIndexType);
        var textLineBufferAccessor = chunk.GetBufferAccessor(TextLineType);

        for (int i = 0; i < chunk.Count; i++) {
          var textRenderer = textRendererArray[i];
          if (!FontAssetFromEntity.Exists(textRenderer.Font))
            continue;
          var vertices = vertexBufferAccessor[i];
          var vertexIndices = vertexIndexBufferAccessor[i];
          var textData = textDataArray[i];
          var vertexCount = textData.Value.LengthInBytes * 4;
          var vertexIndexCount = textData.Value.LengthInBytes * 6;
          if (vertexCount != vertices.Length) {
            vertices.ResizeUninitialized(vertexCount);
            vertexIndices.ResizeUninitialized(vertexIndexCount);
          }
          var lines = textLineBufferAccessor[i];
          lines.Clear();
          var renderBounds = worldRenderBoundsArray[i];
          var localToWorld = localToWorldArray[i];
          var color = vertexColorArray[i].Value * vertexColorMultiplierArray[i].Value;
          PopulateMesh(renderBounds, localToWorld.Value, textRenderer, color, textData, vertices, vertexIndices, lines);
        }
      }

      private void PopulateMesh(
        LocalRectTransform rectTransform,
        float4x4 localToWorld,
        TextRenderer textRenderer,
        float4 color,
        TextData textData,
        DynamicBuffer<Vertex> vertices,
        DynamicBuffer<VertexIndex> triangles,
        DynamicBuffer<TextLine> lines) {

        var verticalAlignment = (VerticalAlignmentOptions)textRenderer.Alignment;
        var horizontalAlignment = (HorizontalAlignmentOptions)textRenderer.Alignment;

        var font = FontAssetFromEntity[textRenderer.Font];
        var glyphData = FontGlyphFromEntity[textRenderer.Font];

        float2 canvasScale = textRenderer.Size / font.PointSize * 0.1f;

        float stylePadding = 1.25f + (textRenderer.Bold ? font.BoldStyle / 4.0f : font.NormalStyle / 4.0f);
        float styleSpaceMultiplier = 1.0f + (textRenderer.Bold ? font.BoldSpace * 0.01f : font.NormalSpace * 0.01f);

        TextUtility.CalculateLines(rectTransform, canvasScale, styleSpaceMultiplier, glyphData, textData, lines);
        float textBlockHeight = lines.Length * font.LineHeight * canvasScale.y;

        float2 alignedStartPosition = TextUtility.GetAlignedStartPosition(rectTransform, textRenderer, font, textBlockHeight, canvasScale);
        float2 currentCharacter = alignedStartPosition;

        int lineIdx = 0;
        for (int i = 0; i < textData.Value.LengthInBytes; i++) {

          if (lineIdx < lines.Length && i == lines[lineIdx].CharacterOffset) {
            currentCharacter = new float2(
              TextUtility.GetAlignedLinePosition(rectTransform, lines[lineIdx].LineWidth, horizontalAlignment),
              alignedStartPosition.y - font.LineHeight * canvasScale.y * lineIdx);
            lineIdx++;
          }

          var character = textData.Value.GetChar(i);
          if (TextUtility.GetGlyph(character, glyphData, out FontGlyph ch)) {
            int startVertexIndex = i * 4;
            int startTriangleIndex = i * 6;

            float2 uv2 = new float2(ch.Scale, ch.Scale) * math.select(canvasScale, -canvasScale, textRenderer.Bold);

            float3 min = new float3(currentCharacter, 0) +
              new float3(ch.Metrics.horizontalBearingX - stylePadding, ch.Metrics.horizontalBearingY - ch.Metrics.height - stylePadding, 0) *
              new float3(canvasScale, 1f);
            float3 max = min +
              new float3(ch.Metrics.width + stylePadding * 2.0f, ch.Metrics.height + stylePadding * 2.0f, 0) *
              new float3(canvasScale, 1f);

            var v0 = math.mul(localToWorld, float4x4.Translate(min)).Position();
            var v1 = math.mul(localToWorld, float4x4.Translate(new float3(max.x, min.y, min.z))).Position();
            var v2 = math.mul(localToWorld, float4x4.Translate(max)).Position();
            var v3 = math.mul(localToWorld, float4x4.Translate(new float3(min.x, max.y, min.z))).Position();
            var normal = math.mul(
              new float4x4(localToWorld.Rotation(), new float3()),
              float4x4.Translate(new float3(0, 0, -1))).c3;
            float4 uv = new float4(
              ch.Rect.x - stylePadding, ch.Rect.y - stylePadding,
              ch.Rect.x + ch.Rect.width + stylePadding,
              ch.Rect.y + ch.Rect.height + stylePadding) /
              new float4(font.AtlasSize, font.AtlasSize);

            triangles[startTriangleIndex] = startVertexIndex + 2;
            triangles[startTriangleIndex + 1] = startVertexIndex + 1;
            triangles[startTriangleIndex + 2] = startVertexIndex;

            triangles[startTriangleIndex + 3] = startVertexIndex + 3;
            triangles[startTriangleIndex + 4] = startVertexIndex + 2;
            triangles[startTriangleIndex + 5] = startVertexIndex;

            vertices[startVertexIndex] = new Vertex() {
              Position = v0,
              Normal = (half4)normal,
              TexCoord0 = (half2)uv.xy,
              TexCoord1 = (half2)uv2,
              Color = (half4)color
            };
            vertices[startVertexIndex + 1] = new Vertex() {
              Position = v1,
              Normal = (half4)normal,
              TexCoord0 = (half2)uv.zy,
              TexCoord1 = (half2)uv2,
              Color = (half4)color
            };
            vertices[startVertexIndex + 2] = new Vertex() {
              Position = v2,
              Normal = (half4)normal,
              TexCoord0 = (half2)uv.zw,
              TexCoord1 = (half2)uv2,
              Color = (half4)color
            };
            vertices[startVertexIndex + 3] = new Vertex() {
              Position = v3,
              Normal = (half4)normal,
              TexCoord0 = (half2)uv.xw,
              TexCoord1 = (half2)uv2,
              Color = (half4)color
            };
            currentCharacter +=
              new float2(ch.Metrics.horizontalAdvance * styleSpaceMultiplier, 0.0f) * canvasScale;
          }
        }
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {

      inputDeps = new TextChunkBuilder() {
        TextDataType = GetArchetypeChunkComponentType<TextData>(true),
        LocalRectTransformType = GetArchetypeChunkComponentType<LocalRectTransform>(true),
        ColorValueType = GetArchetypeChunkComponentType<VertexColor>(true),
        ColorMultiplierType = GetArchetypeChunkComponentType<VertexColorMultiplier>(true),
        TextRendererType = GetArchetypeChunkComponentType<TextRenderer>(true),
        FontAssetFromEntity = GetComponentDataFromEntity<TextFontAsset>(true),
        FontGlyphFromEntity = GetBufferFromEntity<FontGlyph>(true),
        LocalToWorldType = GetArchetypeChunkComponentType<LocalToWorld>(true),
        VertexType = GetArchetypeChunkBufferType<Vertex>(false),
        VertexIndexType = GetArchetypeChunkBufferType<VertexIndex>(false),
        TextLineType = GetArchetypeChunkBufferType<TextLine>(false),
        LastSystemVersion = LastSystemVersion
      }.Schedule(m_textQuery, inputDeps);

      return inputDeps;
    }
  }
}
