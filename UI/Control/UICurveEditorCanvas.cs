using Leap;
using Leap.Unity.Swizzle;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CurveEditor.UI
{
    public class UICurveEditorCanvas : MaskableGraphic, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private readonly List<CurveLine> _lines = new List<CurveLine>();
        private readonly Dictionary<CurveLine, float> _scrubberPositions = new Dictionary<CurveLine, float>();

        private Vector2 _cameraPosition = Vector2.zero;
        private Vector2 _dragStartPosition = Vector2.zero;
        private Vector2 _dragTranslation = Vector2.zero;
        private float zoomValue = 100;
        private bool _showScrubbers = true;
        private Matrix4x4 _viewMatrix = Matrix4x4.identity;

        private Matrix4x4 _viewMatrixInv => _viewMatrix.inverse;

        public CurveEditorPoint selectedPoint { get; private set; } = null;
        public bool allowViewDragging { get; set; } = true;
        public bool allowViewZooming { get; set; } = true;
        public bool allowKeyboardShortcuts { get; set; } = true;
        public bool readOnly { get; set; }

        public bool showScrubbers
        {
            get { return _showScrubbers; }
            set { _showScrubbers = value; SetVerticesDirty(); }
        }

        protected override void Awake()
        {
            base.Awake();
            UpdateViewMatrix();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var min = _viewMatrixInv.MultiplyPoint3x4(Vector2.zero).xy();
            var max = _viewMatrixInv.MultiplyPoint3x4(rectTransform.sizeDelta).xy();
            var viewBounds = new Bounds((min + max) / 2f, max - min);

            var maxT = _lines.Count > 0 ? _lines[0].curve[_lines[0].curve.length - 1].time : 1f;
            var maxY = 0.25f;

            foreach (var line in _lines)
                line.PopulateMesh(vh, _viewMatrix, viewBounds, maxT, line.maxY);

            if (_showScrubbers)
            {
                //TODO: colors
                foreach (var kv in _scrubberPositions)
                {
                    if (kv.Value < min.x || kv.Value > max.x)
                        continue;

                    vh.DrawLine(new Vector2(kv.Value / maxT * viewBounds.max.x, min.y), new Vector2(kv.Value / maxT * viewBounds.max.x, max.y), 0.02f, Color.black, _viewMatrix);
                }

                foreach (var kv in _scrubberPositions)
                {
                    if (kv.Value < min.x || kv.Value > max.x)
                        continue;

                    vh.DrawCircle(new Vector2(kv.Value, kv.Key.curve.Evaluate(kv.Value)), 0.05f, Color.white, _viewMatrix);
                }
            }
        }

        protected void Update()
        {
            if (!allowKeyboardShortcuts) return;

            if (selectedPoint != null)
            {
                if (!readOnly && Input.GetKeyDown(KeyCode.Delete))
                {
                    selectedPoint.parent.DestroyPoint(selectedPoint);
                    selectedPoint.parent.SetCurveFromPoints();
                    SetSelectedPoint(null);
                    SetVerticesDirty();
                }
            }

            if (allowViewZooming)
            {
                if (Input.GetKeyDown(KeyCode.W))
                {
                    zoomValue *= 1.1f;
                    UpdateViewMatrix();
                    SetVerticesDirty();
                }
                if (Input.GetKeyDown(KeyCode.S))
                {
                    zoomValue /= 1.1f;
                    UpdateViewMatrix();
                    SetVerticesDirty();
                }
            }
        }

        private void UpdateViewMatrix()
            => _viewMatrix = Matrix4x4.TRS(_cameraPosition + _dragTranslation, Quaternion.identity, new Vector3(zoomValue, zoomValue, 1));

        public void SetViewToFit()
        {
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            foreach (var point in _lines.SelectMany(l => l.points))
            {
                max = Vector2.Max(max, point.position);
                min = Vector2.Min(min, point.position);
            }

            //TODO:
        }

        public CurveLine CreateCurve(IStorableAnimationCurve storable, UICurveLineColors colors, float thickness)
        {
            var line = new CurveLine(storable, colors);
            _lines.Add(line);
            _scrubberPositions.Add(line, line.curve.keys.First().time);
            SetVerticesDirty();
            return line;
        }

        public void RemoveCurve(CurveLine line)
        {
            _lines.Remove(line);
            _scrubberPositions.Remove(line);
            SetVerticesDirty();
        }

        public void SetSelectedPoint(CurveEditorPoint point)
        {
            foreach (var line in _lines)
                line.SetSelectedPoint(null);

            if (point != null)
            {
                _lines.Remove(point.parent);
                _lines.Add(point.parent);

                point.parent.SetSelectedPoint(point);
            }

            selectedPoint = point;
            SetVerticesDirty();
        }

        public void SetScrubberPosition(CurveLine line, float time)
        {
            if (!_scrubberPositions.ContainsKey(line))
                return;

            _scrubberPositions[line] = time;
            SetVerticesDirty();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Vector2 position;
            if (!PointerEventToCanvasPosition(eventData, out position))
                return;

            if (selectedPoint?.OnBeginDrag(position) == true)
            {
                selectedPoint.parent.SetCurveFromPoints();
                SetVerticesDirty();
                return;
            }

            var closest = _lines.SelectMany(l => l.points).OrderBy(p => Vector2.Distance(p.position, position)).FirstOrDefault();
            if (closest?.OnBeginDrag(position) == true)
            {
                SetSelectedPoint(closest);
                selectedPoint.parent.SetCurveFromPoints();
                return;
            }

            if (allowViewDragging)
            {
                _dragStartPosition = eventData.position;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 position;
            if (!PointerEventToCanvasPosition(eventData, out position))
                return;

            if (selectedPoint?.OnDrag(position) == true)
            {
                selectedPoint.parent.SetCurveFromPoints();
                SetVerticesDirty();
                return;
            }

            if (allowViewDragging)
            {
                _dragTranslation = eventData.position - _dragStartPosition;
                UpdateViewMatrix();
                SetVerticesDirty();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Vector2 position;
            if (!PointerEventToCanvasPosition(eventData, out position))
                return;

            if (selectedPoint?.OnEndDrag(position) == true)
            {
                selectedPoint.parent.SetCurveFromPoints();
                SetVerticesDirty();
                return;
            }

            if (allowViewDragging)
            {
                _cameraPosition += _dragTranslation;
                _dragTranslation = Vector2.zero;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging)
                return;

            Vector2 position;
            if (!PointerEventToCanvasPosition(eventData, out position))
                return;

            if (selectedPoint?.OnPointerClick(position) == true)
            {
                SetVerticesDirty();
                return;
            }

            var closest = _lines.SelectMany(l => l.points).OrderBy(p => Vector2.Distance(p.position, position)).FirstOrDefault();
            if (closest?.OnPointerClick(position) == true)
            {
                SetSelectedPoint(closest);
                return;
            }

            if (eventData.clickCount > 0 && eventData.clickCount % 2 == 0)
            {
                var closestLine = _lines.OrderBy(l => Mathf.Abs(position.y - l.curve.Evaluate(position.x))).FirstOrDefault();

                SetSelectedPoint(null);
                closestLine.CreatePoint(position);
                closestLine.SetCurveFromPoints();
                SetVerticesDirty();
                return;
            }

            SetSelectedPoint(null);
        }

        private bool PointerEventToCanvasPosition(PointerEventData eventData, out Vector2 position)
        {
            position = Vector2.zero;

            Vector2 localPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPosition))
                return false;

            position = _viewMatrixInv.MultiplyPoint3x4(localPosition);
            return true;
        }
    }
}
