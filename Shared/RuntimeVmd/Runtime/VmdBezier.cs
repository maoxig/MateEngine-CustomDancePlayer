using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    public static class VmdBezier
    {
        private const int MmdIterations = 12;

        public static float Evaluate(VmdBezierCurve curve, float time)
        {
            float normalizedTime = Mathf.Clamp01(time);
            if (normalizedTime <= 0f) return 0f;
            if (normalizedTime >= 1f) return 1f;
            byte rawX1 = curve.X1 > 127 ? (byte)127 : curve.X1;
            byte rawY1 = curve.Y1 > 127 ? (byte)127 : curve.Y1;
            byte rawX2 = curve.X2 > 127 ? (byte)127 : curve.X2;
            byte rawY2 = curve.Y2 > 127 ? (byte)127 : curve.Y2;
            if (rawX1 == rawY1 && rawX2 == rawY2) return normalizedTime;

            float x1 = rawX1 / 127f;
            float y1 = rawY1 / 127f;
            float x2 = rawX2 / 127f;
            float y2 = rawY2 / 127f;

            // MMD/mmd-anim use a fixed binary refinement. Matching that order
            // matters for very long camera-distance segments: a tiny curve
            // parameter difference can otherwise become a visible jump.
            float step = 0.5f;
            float parameter = step;
            for (int iteration = 0; iteration < MmdIterations; iteration++)
            {
                float x = Cubic(parameter, x1, x2);
                float difference = x - normalizedTime;
                if (difference == 0f) return Cubic(parameter, y1, y2);
                step *= 0.5f;
                parameter += difference < 0f ? step : -step;
            }
            return Cubic(parameter, y1, y2);
        }

        private static float Cubic(float t, float control1, float control2)
        {
            float inverse = 1f - t;
            return 3f * inverse * inverse * t * control1
                + 3f * inverse * t * t * control2
                + t * t * t;
        }

    }
}
