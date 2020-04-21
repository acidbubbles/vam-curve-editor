using System;
using UnityEngine;

namespace CurveEditor.UI
{
    public class DrawScaleOffset
    {
        public Vector2 offset { get; private set; } = new Vector2(0, 0);
        public Vector2 ratio { get; private set; } = new Vector2(1f, 1f);

        public static DrawScaleOffset FromViewBounds(Bounds valueBounds, Bounds viewBounds)
        {
            return new DrawScaleOffset()
            {
                offset = -new Vector2(valueBounds.min.x * viewBounds.size.x / valueBounds.size.x, valueBounds.min.y * viewBounds.size.y / valueBounds.size.y),
                ratio = new Vector2(viewBounds.size.x / valueBounds.size.x, viewBounds.size.y / valueBounds.size.y)
            };
        }

        public static DrawScaleOffset FromOffsetRatio(Vector2 offset, Vector2 ratio)
        {
            return new DrawScaleOffset()
            {
                offset = offset,
                ratio = ratio
            };
        }
        public static DrawScaleOffset Resize(DrawScaleOffset drawScale, float v)
        {
            return new DrawScaleOffset()
            {
                offset = drawScale.offset,
                ratio = drawScale.ratio * v
            };
        }

        public Vector2 Apply(Vector2 value) => value * ratio + offset;
        public Vector2 Reverse(Vector2 value) => (value - offset) / ratio;
        public float ApplyX(float x) => x * ratio.x / offset.x;
        public float ApplyY(float y) => y * ratio.y / offset.y;
        public float ReverseX(float x) => (x - offset.x) / ratio.x;
        public float ReverseY(float y) => (y - offset.y) / ratio.y;
    }
}
