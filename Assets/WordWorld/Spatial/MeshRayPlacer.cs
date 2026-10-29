using UnityEngine;
using WordWorld.Detection;

namespace WordWorld.Spatial
{
    /// <summary>
    /// Resolves a detection ray onto the room mesh. Port of YoloMeshSurface.place from PICO's
    /// yolo-mesh sample: sample nine rays across the box, require a real hit at the centre, fit a
    /// plane in inverse depth, and reject mesh holes and depth edges. Misses are skipped, never
    /// guessed (SPATIALML_SETUP.md section 5).
    /// </summary>
    public static class MeshRayPlacer
    {
        public const float MinDepth = 0.2f;
        public const float MaxDepth = 6f;
        private const int MinSurfaceSupport = 5;
        private const int SampleCount = 9;

        // Centre first; then a 3x3 grid at +/-20% of the box size.
        private static readonly Vector2[] s_offsets =
        {
            new Vector2(0f, 0f),
            new Vector2(-0.2f, -0.2f), new Vector2(0f, -0.2f), new Vector2(0.2f, -0.2f),
            new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f),
            new Vector2(-0.2f, 0.2f), new Vector2(0f, 0.2f), new Vector2(0.2f, 0.2f),
        };

        // Scratch buffers: placement runs for every ray of every frame, so avoid allocations.
        private static readonly Vector3[] s_samples = new Vector3[SampleCount]; // (x, y, depth)
        private static readonly Vector3[] s_supporters = new Vector3[SampleCount];
        private static readonly float[] s_sorted = new float[SampleCount];
        private static readonly double[,] s_fit = new double[3, 4];

        private static double Basis(Vector3 sample, int index) =>
            index == 0 ? 1.0 : index == 1 ? sample.x : sample.y;

        public static bool TryPlace(in DetectionRay ray, int layerMask, out PlacedDetection placed)
        {
            placed = default;
            int count = 0;
            float center = float.NaN;
            for (int i = 0; i < s_offsets.Length; i++)
            {
                Vector2 o = s_offsets[i];
                Vector3 direction = ray.PlaneDirection + ray.CameraRight * (ray.PlaneWidth * o.x) +
                                    ray.CameraUp * (ray.PlaneHeight * o.y);
                float length = direction.magnitude;
                if (length < 1e-4f) continue;
                // Depth is measured along the one-metre-plane direction, so t equals metres of depth.
                if (!Physics.Raycast(ray.Origin, direction / length, out RaycastHit hit,
                        MaxDepth * length * 1.5f, layerMask, QueryTriggerInteraction.Ignore)) continue;
                float depth = hit.distance / length;
                s_samples[count++] = new Vector3(o.x, o.y, depth);
                if (i == 0) center = depth;
            }

            // A wall fitted around an empty object centre must not invent a point on it.
            if (float.IsNaN(center) || count < MinSurfaceSupport) return false;
            for (int i = 0; i < count; i++) s_sorted[i] = s_samples[i].z;
            System.Array.Sort(s_sorted, 0, count);
            float median = s_sorted[count / 2];
            float tolerance = Mathf.Max(0.08f, median * 0.04f);
            if (Mathf.Abs(center - median) > tolerance) return false;

            // Inverse depth is affine over image coordinates for a plane; a nine-ray fit averages
            // mesh bumps without biasing tilted surfaces toward fronto-parallel.
            if (!TryFitSurface(s_samples, count, out Vector3 first)) return false;
            float residual = Mathf.Max(0.025f, median * 0.015f);
            int supporters = CollectSupporters(s_samples, count, first, residual, s_supporters);
            if (supporters < MinSurfaceSupport) return false;
            if (!TryFitSurface(s_supporters, supporters, out Vector3 plane)) return false;
            float fitted = 1f / plane.x;
            if (!float.IsFinite(fitted) || fitted < MinDepth || fitted > MaxDepth ||
                Mathf.Abs(fitted - center) > tolerance) return false;
            if (CountSupport(s_supporters, supporters, plane, residual) < MinSurfaceSupport) return false;

            placed = new PlacedDetection
            {
                ClassId = ray.ClassId,
                Confidence = ray.Confidence,
                Position = ray.Origin + ray.PlaneDirection * fitted,
                Width = ray.PlaneWidth * fitted,
                Height = ray.PlaneHeight * fitted,
            };
            return true;
        }

        private static int CollectSupporters(Vector3[] samples, int count, Vector3 plane, float residual, Vector3[] output)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
                if (Fits(samples[i], plane, residual)) output[n++] = samples[i];
            return n;
        }

        private static int CountSupport(Vector3[] samples, int count, Vector3 plane, float residual)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
                if (Fits(samples[i], plane, residual)) n++;
            return n;
        }

        private static bool Fits(Vector3 sample, Vector3 plane, float residual)
        {
            float predicted = 1f / (plane.x + plane.y * sample.x + plane.z * sample.y);
            return float.IsFinite(predicted) && Mathf.Abs(predicted - sample.z) <= residual;
        }

        /// <summary>Least squares for 1/depth = a + b*x + c*y; returns (a, b, c).</summary>
        private static bool TryFitSurface(Vector3[] samples, int count, out Vector3 plane)
        {
            plane = default;
            double[,] m = s_fit;
            System.Array.Clear(m, 0, m.Length);
            for (int s = 0; s < count; s++)
            {
                double inv = 1.0 / samples[s].z;
                for (int r = 0; r < 3; r++)
                {
                    double br = Basis(samples[s], r);
                    for (int c = 0; c < 3; c++) m[r, c] += br * Basis(samples[s], c);
                    m[r, 3] += br * inv;
                }
            }
            for (int col = 0; col < 3; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < 3; r++)
                    if (System.Math.Abs(m[r, col]) > System.Math.Abs(m[pivot, col])) pivot = r;
                if (System.Math.Abs(m[pivot, col]) < 1e-10) return false;
                if (pivot != col)
                    for (int k = 0; k < 4; k++) { double t = m[col, k]; m[col, k] = m[pivot, k]; m[pivot, k] = t; }
                double div = m[col, col];
                for (int k = col; k < 4; k++) m[col, k] /= div;
                for (int r = 0; r < 3; r++)
                {
                    if (r == col) continue;
                    double f = m[r, col];
                    for (int k = col; k < 4; k++) m[r, k] -= f * m[col, k];
                }
            }
            plane = new Vector3((float)m[0, 3], (float)m[1, 3], (float)m[2, 3]);
            return float.IsFinite(plane.x) && float.IsFinite(plane.y) && float.IsFinite(plane.z);
        }
    }
}
