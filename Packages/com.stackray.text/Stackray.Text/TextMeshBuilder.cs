using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Stackray.Mathematics;

namespace Stackray.Text {
  public class TextMeshBuildSystem : JobComponentSystem {
    private EntityQuery m_textGroup;

    protected override void OnCreate() {
      m_textGroup = GetEntityQuery(
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<WorldRenderBounds>(),
                    ComponentType.ReadOnly<TextRenderer>(),
                    ComponentType.ReadOnly<TextData>(),
                    ComponentType.ReadOnly<VertexColor>(),
                    ComponentType.ReadOnly<VertexColorMultiplier>(),
                    ComponentType.ReadWrite<Vertex>(),
                    ComponentType.ReadWrite<VertexIndex>());
    }

    [BurstCompile]
    private struct TextChunkBuilder : IJobChunk {
      [ReadOnly]
      public ArchetypeChunkComponentType<WorldRenderBounds> WorldRenderBoundsType;
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
      public ArchetypeChunkBufferType<Vertex> VertexType;
      [NativeDisableContainerSafetyRestriction]
      public ArchetypeChunkBufferType<VertexIndex> VertexIndexType;

      public uint LastSystemVersion;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        var vertexBufferAccessor = chunk.GetBufferAccessor(VertexType);
        var vertexIndexBufferAccessor = chunk.GetBufferAccessor(VertexIndexType);
        var textDataArray = chunk.GetNativeArray(TextDataType);
        var worldRenderBoundsArray = chunk.GetNativeArray(WorldRenderBoundsType);
        var textRendererArray = chunk.GetNativeArray(TextRendererType);
        var vertexColorArray = chunk.GetNativeArray(ColorValueType);
        var vertexColorMultiplierArray = chunk.GetNativeArray(ColorMultiplierType);
        var localToWorldArray = chunk.GetNativeArray(LocalToWorldType);

        if (!chunk.DidChange(TextDataType, LastSystemVersion) &&
          !chunk.DidChange(TextRendererType, LastSystemVersion) &&
          !chunk.DidChange(ColorValueType, LastSystemVersion) &&
          !chunk.DidChange(ColorMultiplierType, LastSystemVersion) &&
          !chunk.DidChange(WorldRenderBoundsType, LastSystemVersion))
          return;

        for (int i = 0; i < chunk.Count; i++) {
            var vertices = vertexBufferAccessor[i];
            var triangles = vertexIndexBufferAccessor[i];
            var renderBounds = worldRenderBoundsArray[i];
            var textRenderer = textRendererArray[i];
            var localToWorld = localToWorldArray[i];
            var textData = textDataArray[i];
            if (textData.Value.Length != vertices.Length) {
              vertices.ResizeUninitialized(textData.Value.Length * 4);
              triangles.ResizeUninitialized(textData.Value.Length * 6);
            }
            var color = vertexColorArray[i].Value * vertexColorMultiplierArray[i].Value;
            PopulateMesh(renderBounds, localToWorld.Value.Scale().xy, textRenderer, color, textData, ref vertices, ref triangles);
          }
        }

      private void PopulateMesh(WorldRenderBounds renderBounds, float2 scale, TextRenderer textRenderer, float4 color, TextData textData, ref DynamicBuffer<Vertex> vertices, ref DynamicBuffer<VertexIndex> triangles) {
        _VerticalAlignmentOptions verticalAlignment = (_VerticalAlignmentOptions)textRenderer.Alignment;
        _HorizontalAlignmentOptions horizontalAlignment = (_HorizontalAlignmentOptions)textRenderer.Alignment;

        var font = FontAssetFromEntity[textRenderer.Font];
        var glyphData = FontGlyphFromEntity[textRenderer.Font];

        float2 canvasScale = textRenderer.Size * scale / font.PointSize;


        float stylePadding = 1.25f + (textRenderer.Bold ? font.BoldStyle / 4.0f : font.NormalStyle / 4.0f);
        float styleSpaceMultiplier = 1.0f + (textRenderer.Bold ? font.BoldSpace * 0.01f : font.NormalSpace * 0.01f);

        NativeList<TextUtility.TextLineInfo> lines = new NativeList<TextUtility.TextLineInfo>(Allocator.Temp);
        TextUtility.CalculateLines(renderBounds, canvasScale, styleSpaceMultiplier, glyphData, textData, lines);
        float textBlockHeight = lines.Length * font.LineHeight * canvasScale.y;

        float2 alignedStartPosition = TextUtility.GetAlignedStartPosition(renderBounds, textRenderer, font, textBlockHeight, canvasScale);
        float2 currentCharacter = alignedStartPosition;

        int lineIdx = 0;
        for (int i = 0; i < textData.Value.Length; i++) {
          if (lineIdx < lines.Length && i == lines[lineIdx].CharacterOffset) {
            currentCharacter = new float2(TextUtility.GetAlignedLinePosition(renderBounds, lines[lineIdx].LineWidth, horizontalAlignment),
                alignedStartPosition.y - font.LineHeight * canvasScale.y * lineIdx);
            lineIdx++;
          }

          var character = textData.Value[i];
          if (TextUtility.GetGlyph(character, glyphData, out FontGlyph ch)) {
            int startVertexIndex = i * 4;
            int startTriangleIndex = i * 6;

            float2 uv2 = new float2(ch.Scale, ch.Scale) * math.select(canvasScale, -canvasScale, textRenderer.Bold);

            float2 vMin = currentCharacter + new float2(ch.Metrics.horizontalBearingX - stylePadding, ch.Metrics.horizontalBearingY - ch.Metrics.height - stylePadding) * canvasScale;
            float2 vMax = vMin + new float2(ch.Metrics.width + stylePadding * 2.0f, ch.Metrics.height + stylePadding * 2.0f) * canvasScale;

            float4 uv = new float4(ch.Rect.x - stylePadding, ch.Rect.y - stylePadding, (ch.Rect.x + ch.Rect.width + stylePadding), (ch.Rect.y + ch.Rect.height + stylePadding)) / new float4(font.AtlasSize, font.AtlasSize);
            var vertexPos = new float4(vMin, vMax);
            triangles[startTriangleIndex] = new VertexIndex() { Value = startVertexIndex + 2 };
            triangles[startTriangleIndex + 1] = new VertexIndex() { Value = startVertexIndex + 1 };
            triangles[startTriangleIndex + 2] = new VertexIndex() { Value = startVertexIndex };

            triangles[startTriangleIndex + 3] = new VertexIndex() { Value = startVertexIndex + 3 };
            triangles[startTriangleIndex + 4] = new VertexIndex() { Value = startVertexIndex + 2 };
            triangles[startTriangleIndex + 5] = new VertexIndex() { Value = startVertexIndex };

            vertices[startVertexIndex] = new Vertex() {
              Position = new float3(vertexPos.xy, 0.0f),
              Normal = new float3(0.0f, 0.0f, -1.0f),
              TexCoord0 = uv.xy,
              TexCoord1 = uv2,
              Color = color
            };
            vertices[startVertexIndex + 1] = new Vertex() {
              Position = new float3(vertexPos.zy, 0.0f),
              Normal = new float3(0.0f, 0.0f, -1.0f),
              TexCoord0 = uv.zy,
              TexCoord1 = uv2,
              Color = color
            };
            vertices[startVertexIndex + 2] = new Vertex() {
              Position = new float3(vertexPos.zw, 0.0f),
              Normal = new float3(0.0f, 0.0f, -1.0f),
              TexCoord0 = uv.zw,
              TexCoord1 = uv2,
              Color = color
            };
            vertices[startVertexIndex + 3] = new Vertex() {
              Position = new float3(vertexPos.xw, 0.0f),
              Normal = new float3(0.0f, 0.0f, -1.0f),
              TexCoord0 = uv.xw,
              TexCoord1 = uv2,
              Color = color
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
        WorldRenderBoundsType = GetArchetypeChunkComponentType<WorldRenderBounds>(true),
        ColorValueType = GetArchetypeChunkComponentType<VertexColor>(true),
        ColorMultiplierType = GetArchetypeChunkComponentType<VertexColorMultiplier>(true),
        TextRendererType = GetArchetypeChunkComponentType<TextRenderer>(true),
        FontAssetFromEntity = GetComponentDataFromEntity<TextFontAsset>(true),
        FontGlyphFromEntity = GetBufferFromEntity<FontGlyph>(true),
        LocalToWorldType = GetArchetypeChunkComponentType<LocalToWorld>(true),
        VertexType = GetArchetypeChunkBufferType<Vertex>(false),
        VertexIndexType = GetArchetypeChunkBufferType<VertexIndex>(false),
        LastSystemVersion = LastSystemVersion
      }.Schedule(m_textGroup, inputDeps);

      return inputDeps;
    }
  }
}
