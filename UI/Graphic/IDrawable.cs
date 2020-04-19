using UnityEngine;
using UnityEngine.UI;

namespace CurveEditor.UI
{
    public interface IDrawable
    {
        void PopulateMesh(VertexHelper vh, Matrix4x4 viewMatrix, Bounds viewBounds, float maxT, float maxY);
    }
}
