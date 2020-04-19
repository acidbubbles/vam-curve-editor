using CurveEditor.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace CurveEditor.UI
{
    public class CurveEditorPoint : IDrawable
    {
        private float _pointRadius = 0.08f;
        private float _pointSkin = 0.05f;
        private float _handleRadius = 0.07f;
        private float _handleSkin = 0.04f;
        private float _shellSize = 0.2f;
        private float _handleThickness = 0.04f;
        private float _outHandleLength = 0.5f;
        private float _inHandleLength = 0.5f;
        private bool _isDraggingPoint = false;
        private bool _isDraggingOutHandle = false;
        private bool _isDraggingInHandle = false;
        private int _handleMode = 0;    // 0 = both, 1 = free
        private int _inHandleMode = 0;  // 0 = constant, 1 = weighted
        private int _outHandleMode = 0; // 0 = constant, 1 = weighted

        private Vector2 _outHandlePosition = Vector2.right * 0.5f;
        private Vector2 _inHandlePosition = Vector2.left * 0.5f;

        public CurveLine parent { get; private set; } = null;
        public bool showHandles { get; set; } = false;
        public Color pointColor { get; set; } = new Color(1, 1, 1);
        public Color lineColor { get; set; } = new Color(0.5f, 0.5f, 0.5f);
        public Color inHandleColor { get; set; } = new Color(0, 0, 0);
        public Color outHandleColor { get; set; } = new Color(0, 0, 0);
        public Vector2 position { get; set; } = Vector2.zero;

        public int handleMode
        {
            get { return _handleMode; }
            set { _handleMode = value; SetOutHandlePosition(_outHandlePosition); }
        }

        public int inHandleMode
        {
            get { return _inHandleMode; }
            set { _inHandleMode = value; SetInHandlePosition(_inHandlePosition); }
        }

        public int outHandleMode
        {
            get { return _outHandleMode; }
            set { _outHandleMode = value; SetOutHandlePosition(_outHandlePosition); }
        }

        public float outHandleLength
        {
            get { return _outHandleLength; }
            set { _outHandleLength = value; SetOutHandlePosition(_outHandlePosition); }
        }

        public float inHandleLength
        {
            get { return _inHandleLength; }
            set { _inHandleLength = value; SetInHandlePosition(_inHandlePosition); }
        }

        public Vector2 outHandlePosition
        {
            get { return _outHandlePosition; }
            set { SetOutHandlePosition(value); }
        }

        public Vector2 inHandlePosition
        {
            get { return _inHandlePosition; }
            set { SetInHandlePosition(value); }
        }

        public CurveEditorPoint(CurveLine parent)
        {
            this.parent = parent;
        }

        public void PopulateMesh(VertexHelper vh, Matrix4x4 viewMatrix, Bounds viewBounds, float maxT, float maxY)
        {
            position = new Vector2(position.x / maxT * viewBounds.max.x, position.y / maxY * viewBounds.max.y);
            if (showHandles)
            {
                vh.DrawLine(position, position + _outHandlePosition, _handleThickness, lineColor, viewMatrix);
                vh.DrawLine(position, position + _inHandlePosition, _handleThickness, lineColor, viewMatrix);
            }

            vh.DrawCircle(position, _pointRadius, pointColor, viewMatrix);

            if (showHandles)
            {
                vh.DrawCircle(position + _outHandlePosition, _handleRadius, outHandleColor, viewMatrix);
                vh.DrawCircle(position + _inHandlePosition, _handleRadius, inHandleColor, viewMatrix);
            }
        }

        public bool OnBeginDrag(Vector2 point)
        {
            if (Vector2.Distance(point, position) <= _pointRadius + _pointSkin)
            {
                _isDraggingPoint = true;
                position = point;
                return true;
            }

            if (!showHandles)
                return false;

            if (Vector2.Distance(point, position + _outHandlePosition) <= _handleRadius + _handleSkin)
            {
                _isDraggingOutHandle = true;
                SetOutHandlePosition(point - position);
                return true;
            }
            else if (Vector2.Distance(point, position + _inHandlePosition) <= _handleRadius + _handleSkin)
            {
                _isDraggingInHandle = true;
                SetInHandlePosition(point - position);
                return true;
            }

            return false;

        }

        public bool OnDrag(Vector2 point)
        {
            if (!_isDraggingPoint && !_isDraggingOutHandle && !_isDraggingInHandle)
                return false;

            if (_isDraggingPoint) position = point;
            else if (_isDraggingOutHandle) SetOutHandlePosition(point - position);
            else if (_isDraggingInHandle) SetInHandlePosition(point - position);

            return true;
        }

        public bool OnEndDrag(Vector2 point)
        {
            if (!_isDraggingPoint && !_isDraggingOutHandle && !_isDraggingInHandle)
                return false;

            _isDraggingPoint = false;
            _isDraggingOutHandle = false;
            _isDraggingInHandle = false;

            return true;
        }

        public bool OnPointerClick(Vector2 point)
        {
            if (Vector2.Distance(point, position) <= _pointRadius + _pointSkin) return true;

            if (!showHandles) return false;
            if (Vector2.Distance(point, position + _outHandlePosition) <= _handleRadius + _handleSkin) return true;
            if (Vector2.Distance(point, position + _inHandlePosition) <= _handleRadius + _handleSkin) return true;

            return !IsPositionOutsideShell(point);
        }

        public void SetOutHandlePosition(Vector2 handlePosition)
        {
            if (handlePosition.x < 0)
                _outHandlePosition = Vector2.up * _outHandleLength;
            else
                _outHandlePosition = handlePosition.normalized * (_outHandleMode == 1 ? handlePosition.magnitude : _outHandleLength);

            if (_handleMode == 0)
            {
                if (_inHandleMode == 0 || handlePosition.x < 0)
                    _inHandlePosition = -_outHandlePosition.normalized * _inHandleLength;
                else
                    _inHandlePosition = -_outHandlePosition.normalized * _inHandlePosition.magnitude;
            }
        }

        public void SetInHandlePosition(Vector2 handlePosition)
        {
            if (handlePosition.x > 0)
                _inHandlePosition = Vector2.down * _inHandleLength;
            else
                _inHandlePosition = handlePosition.normalized * (_inHandleMode == 1 ? handlePosition.magnitude : _inHandleLength);

            if (_handleMode == 0)
            {
                if (_outHandleMode == 0 || handlePosition.x > 0)
                    _outHandlePosition = -_inHandlePosition.normalized * _outHandleLength;
                else
                    _outHandlePosition = -_inHandlePosition.normalized * _outHandlePosition.magnitude;
            }
        }

        public bool IsPositionOutsideShell(Vector2 point)
        {
            return MathUtils.DistanceToLine(point, position, position + _inHandlePosition) > _shellSize
                && MathUtils.DistanceToLine(point, position, position + _outHandlePosition) > _shellSize;
        }
    }
}
