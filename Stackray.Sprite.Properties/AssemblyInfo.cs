using Stackray.Renderer;
using Stackray.Sprite;
using Unity.Entities;
using Unity.Mathematics;

[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<TileOffsetProperty, half4>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<ScaleProperty, half4>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<PivotProperty, half2>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<ColorProperty, half4>))]
[assembly: RegisterGenericComponentType(typeof(SpriteAnimationClipBufferElement<FlipProperty, int2>))]