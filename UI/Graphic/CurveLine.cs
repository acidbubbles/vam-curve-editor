using System;
using System.Collections.Generic;
using CurveEditor.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CurveEditor.UI
{
    public class CurveLine
    {
        private readonly IStorableAnimationCurve _storable;
        private readonly UICurveLineColors _colors;
        private CurveEditorPoint _selectedPoint;
        private DrawScaleOffset _drawScale = new DrawScaleOffset();
        public readonly List<CurveEditorPoint> points;

        public DrawScaleOffset drawScale
        {
            get { return _drawScale; }
            set { _drawScale = value; SetPointsFromCurve(); }
        }

        public float thickness { get; set; } = 0.04f;
        public int evaluateCount { get; set; } = 200;
        public AnimationCurve curve => _storable.val;

        public CurveLine(IStorableAnimationCurve storable, UICurveLineColors colors = null)
        {
            points = new List<CurveEditorPoint>();

            _storable = storable;
            _colors = colors ?? new UICurveLineColors();

            SetPointsFromCurve();
        }

        public void PopulateMesh(VertexHelper vh, Matrix4x4 viewMatrix, Bounds viewBounds)
        {
            var curvePoints = new List<Vector2>();
            var minT = _drawScale.Reverse(viewBounds.min).x;
            var maxT = _drawScale.Reverse(viewBounds.max).x;
            for (var i = 0; i < evaluateCount; i++)
            {
                //TODO: clip Y?
                var t = Mathf.Lerp(minT, maxT, (float)i / (evaluateCount - 1));
                if (t < minT || t > maxT)
                    continue;

                var point = _drawScale.Apply(new Vector2(t, curve.Evaluate(t)));
                curvePoints.Add(point);
            }

            vh.AddLine(curvePoints, thickness, _colors.lineColor, viewMatrix);
            foreach (var point in points)
                point.PopulateMesh(vh, viewMatrix, viewBounds);
        }

        public void PopulateScrubberLine(VertexHelper vh, Matrix4x4 viewMatrix, Bounds viewBounds, float x)
        {
            var min = _drawScale.Reverse(viewBounds.min);
            var max = _drawScale.Reverse(viewBounds.max);
            if (x < min.x || x > max.x)
                return;

            vh.AddLine(_drawScale.Apply(new Vector2(x, min.y)), _drawScale.Apply(new Vector2(x, max.y)), 0.02f, Color.black, viewMatrix);
        }

        public void PopulateScrubberPoints(VertexHelper vh, Matrix4x4 viewMatrix, Bounds viewBounds, float x)
        {
            var min = _drawScale.Reverse(viewBounds.min);
            var max = _drawScale.Reverse(viewBounds.max);
            if (x + 0.06f < min.x || x - 0.06f > max.x)
                return;

            var y = curve.Evaluate(x);
            if (y + 0.06f < min.y || y - 0.06f > max.y)
                return;

            vh.AddCircle(_drawScale.Apply(new Vector2(x, y)), 0.03f, Color.white, viewMatrix);
        }

        public void SetCurveFromPoints()
        {
            points.Sort(new UICurveEditorPointComparer());
            while (curve.length > points.Count)
                curve.RemoveKey(0);

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                var position = _drawScale.Reverse(point.position);
                var outPosition = _drawScale.Reverse(point.outHandlePosition);
                var inPosition = _drawScale.Reverse(point.inHandlePosition);

                var key = new Keyframe(position.x, position.y);
                key.weightedMode = (WeightedMode)(point.inHandleMode | point.outHandleMode << 1);

                if (Mathf.Abs(inPosition.x) < 0.0001f)
                {
                    key.inTangent = Mathf.Infinity;
                    key.inWeight = 0f;
                }
                else
                {
                    key.inTangent = inPosition.y / inPosition.x;

                    var prev = i > 0 ? points[i - 1] : null;
                    if (prev != null)
                    {
                        var prevPosition = prev.position;
                        var dx = position.x - prevPosition.x;
                        key.inWeight = Mathf.Clamp(Mathf.Abs(inPosition.x / dx), 0f, 1f);
                    }
                }

                if (Mathf.Abs(outPosition.x) < 0.0001f)
                {
                    key.outTangent = Mathf.Infinity;
                    key.outWeight = 0f;
                }
                else
                {
                    key.outTangent = outPosition.y / outPosition.x;

                    var next = i < points.Count - 1 ? points[i + 1] : null;
                    if (next != null)
                    {
                        var nextPosition = next.position;
                        var dx = nextPosition.x - position.x;
                        key.outWeight = Mathf.Clamp(Mathf.Abs(outPosition.x / dx), 0f, 1f);
                    }
                }

                if (i >= curve.length)
                    curve.AddKey(key);
                else
                    curve.MoveKey(i, key);
            }

            _storable.NotifyUpdated();
        }

        public void SetPointsFromCurve()
        {
            while (points.Count > curve.length)
                DestroyPoint(points[0]);
            while (points.Count < curve.length)
                CreatePoint();

            for (var i = 0; i < curve.length; i++)
            {
                var point = points[i];
                var key = curve[i];

                point.position = _drawScale.Apply(new Vector2(key.time, key.value));

                if (key.inTangent != key.outTangent)
                    point.handleMode = 1;

                if (((int)key.weightedMode & 1) > 0) point.inHandleMode = 1;
                if (((int)key.weightedMode & 2) > 0) point.outHandleMode = 1;

                var outHandleNormal = _drawScale.Apply(MathUtils.VectorFromAngle(Mathf.Atan(key.outTangent)).normalized);
                if (point.outHandleMode == 1 && i < curve.length - 1)
                {
                    var x = key.outWeight * (curve[i + 1].time - key.time);
                    var y = x * (outHandleNormal.y / outHandleNormal.x);
                    var length = Mathf.Sqrt(x * x + y * y);
                    point.outHandlePosition = outHandleNormal * length;
                }
                else
                {
                    point.outHandlePosition = outHandleNormal * point.outHandleLength;
                }

                var inHandleNormal = _drawScale.Apply(-MathUtils.VectorFromAngle(Mathf.Atan(key.inTangent)).normalized);
                if (point.inHandleMode == 1 && i > 0)
                {
                    var x = key.inWeight * (key.time - curve[i - 1].time);
                    var y = x * (inHandleNormal.y / inHandleNormal.x);
                    var length = Mathf.Sqrt(x * x + y * y);
                    point.inHandlePosition = inHandleNormal * length;
                }
                else
                {
                    point.inHandlePosition = inHandleNormal * point.inHandleLength;
                }

                SetHandleMode(point, point.handleMode);
                SetOutHandleMode(point, point.outHandleMode);
                SetInHandleMode(point, point.inHandleMode);
            }
        }

        public CurveEditorPoint CreatePoint(Vector2 position = new Vector2())
        {
            var point = new CurveEditorPoint(this)
            {
                pointColor = _colors.pointColor,
                inHandleColor = _colors.inHandleColor,
                outHandleColor = _colors.outHandleColor,
                lineColor = _colors.handleLineColor,
                position = position
            };

            points.Add(point);
            return point;
        }

        public void DestroyPoint(CurveEditorPoint point)
        {
            if (points.Count > 1)
                points.Remove(point);
        }

        public void SetSelectedPoint(CurveEditorPoint point)
        {
            if (_selectedPoint != null)
            {
                _selectedPoint.pointColor = _colors.pointColor;
                _selectedPoint.showHandles = false;
                _selectedPoint = null;
            }

            if (point != null)
            {
                point.pointColor = _colors.selectedPointColor;
                point.showHandles = true;
                _selectedPoint = point;
            }
        }

        //TODO: should be in point
        public void SetHandleMode(CurveEditorPoint point, int mode)
        {
            point.handleMode = mode;
            point.lineColor = mode == 0 ? _colors.handleLineColor : _colors.handleLineColorFree;
        }

        //TODO: should be in point
        public void SetOutHandleMode(CurveEditorPoint point, int mode)
        {
            point.outHandleMode = mode;
            point.outHandleColor = mode == 0 ? _colors.outHandleColor : _colors.outHandleColorWeighted;
        }

        //TODO: should be in point
        public void SetInHandleMode(CurveEditorPoint point, int mode)
        {
            point.inHandleMode = mode;
            point.inHandleColor = mode == 0 ? _colors.inHandleColor : _colors.inHandleColorWeighted;
        }

        public float DistanceToPoint(Vector2 point)
        {
            point = _drawScale.Reverse(point);
            return Mathf.Abs(point.y - curve.Evaluate(point.x));
        }

        private class UICurveEditorPointComparer : IComparer<CurveEditorPoint>
        {
            public int Compare(CurveEditorPoint x, CurveEditorPoint y)
                => Comparer<float>.Default.Compare(x.position.x, y.position.x);
        }
    }
}

