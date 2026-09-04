using System.Numerics;
using System.Text;

namespace Donut
{
    /// <summary>
    /// Provides the main entry point and rendering logic for a 3D rotating ASCII donut.
    /// </summary>
    public static class Program
    {
        // Rendering specific constants.
        private static readonly char[] Grayscale = ['.', ':', '-', '=', '+', '*', '#', '%', '@'];
        private const int Framerate = 30;
        private const int ScreenSize = 70;
        private const float Fov = 60f;
        private const int DonutCameraDistance = 15;
        private static readonly Vector3 LightVector = new Vector3(0, -1f / MathF.Sqrt(2), -1f / MathF.Sqrt(2));

        // Donut geometry parameter constants.
        private const float CenterRadius = 5f;
        private const float SliceRadius = 1.5f;
        private const int NumberSlicePoints = 300;
        private const int NumberSlices = 300;

        // Donut rotation parameter constants.
        private const float XSpeed = 1f;
        private const float ZSpeed = 0.5f;


        /// <summary>
        /// Executes the main animation loop, continually rotating and projecting the donut model to the console.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the executable.</param>
        public static void Main(string[] args)
        {
            float xAngle = 0f;
            float zAngle = 0f;
            List<(Vector2 point, float theta)> slicePoints = GetSlicePoints(CenterRadius, SliceRadius, NumberSlicePoints);
            List<(Vector3 point, Vector3 normal)> donutPoints = ExpandSlice(slicePoints, NumberSlices);

            while (true)
            {
                List<(Vector3 point, Vector3 normal)> rotatedPoints = RotatePoints(donutPoints, xAngle, zAngle);
                List<(Vector3 point, float luminance)> luminances = PointsLuminances(rotatedPoints, LightVector);
                List<(int x, int y, double z, float luminance)> projectedPoints = ProjectPoints(luminances, DonutCameraDistance);

                PrintPoints(projectedPoints);

                xAngle += XSpeed / Framerate;
                zAngle += ZSpeed / Framerate;
                Thread.Sleep(1000 / Framerate);
            }
        }

        // Generates a 2D cross-section ring (slice) of the donut along the X-Y plane.
        private static List<(Vector2 point, float theta)> GetSlicePoints(float centerRingRadius, float donutSliceRadius, int numberPoints)
        {
            List<(Vector2 point, float theta)> points = new();
            for (float theta = 0f; theta < MathF.Tau; theta += MathF.Tau / numberPoints)
            {
                float x = centerRingRadius + donutSliceRadius * MathF.Cos(theta);
                float y = donutSliceRadius * MathF.Sin(theta);
                Vector2 point = new(x, y);
                points.Add((point, theta));
            }
            return points;
        }

        // Sweeps a 2D cross-section around the Z-axis to construct full 3D torus points and surface normals.
        private static List<(Vector3 point, Vector3 normal)> ExpandSlice(List<(Vector2 point, float theta)> slicePoints, int numberSlices)
        {
            List<(Vector3 point, Vector3 normal)> points = new();
            foreach ((Vector2 point, float theta) slicePoint in slicePoints)
            {
                for (float phi = 0f; phi < MathF.Tau; phi += MathF.Tau / numberSlices)
                {
                    Vector3 point = new(
                        slicePoint.point.X * MathF.Cos(phi),
                        slicePoint.point.X * MathF.Sin(phi),
                        slicePoint.point.Y
                    );

                    Vector3 normal = new(
                        MathF.Cos(slicePoint.theta) * MathF.Cos(phi),
                        MathF.Cos(slicePoint.theta) * MathF.Sin(phi),
                        MathF.Sin(slicePoint.theta)
                    );
                    points.Add((point, normal));
                }
            }
            return points;
        }

        // Rotates all 3D torus points and normals around the X and Z axes.
        private static List<(Vector3 point, Vector3 normal)> RotatePoints(List<(Vector3 point, Vector3 normal)> points, float xAngle, float zAngle)
        {
            List<(Vector3 point, Vector3 normal)> rotatedPoints = new();
            foreach ((Vector3 point, Vector3 normal) pair in points)
            {
                rotatedPoints.Add(
                    (RotatePoint(pair.point, xAngle, zAngle),
                    RotatePoint(pair.normal, xAngle, zAngle)));
            }
            return rotatedPoints;
        }

        // Applies Euler angle rotations along X and Z axes for a single 3D vector.
        private static Vector3 RotatePoint(Vector3 point, float xAngle, float zAngle)
        {
            float x =
                MathF.Cos(zAngle) * point.X
                - MathF.Sin(zAngle) *
                  (MathF.Cos(xAngle) * point.Y - MathF.Sin(xAngle) * point.Z);

            float y =
                MathF.Sin(zAngle) * point.X
                + MathF.Cos(zAngle) *
                  (MathF.Cos(xAngle) * point.Y - MathF.Sin(xAngle) * point.Z);

            float z =
                MathF.Sin(xAngle) * point.Y
                + MathF.Cos(xAngle) * point.Z;

            return new Vector3(x, y, z);
        }

        // Computes directional lighting (luminance) for each point using dot products.
        private static List<(Vector3 point, float luminance)> PointsLuminances(List<(Vector3 point, Vector3 normal)> points, Vector3 lightVector)
        {
            List<(Vector3 point, float luminance)> luminancePoints = new();
            foreach ((Vector3 point, Vector3 normal) pair in points)
            {
                float luminance = Vector3.Dot(pair.normal, lightVector);
                luminance = MathF.Max(0, luminance);
                luminancePoints.Add((pair.point, luminance));
            }
            return luminancePoints;
        }

        // Projects 3D points onto a 2D viewport, applying perspective divide and depth buffering.
        private static List<(int x, int y, double z, float luminance)> ProjectPoints(List<(Vector3 point, float luminance)> luminencePoints, float donutCameraDistance)
        {
            List<(int x, int y, double z, float luminance)> projectedPoints = new();
            float scale = ScreenSize / 2.0f / MathF.Tan(Fov * MathF.PI / 180f / 2.0f);

            foreach ((Vector3 point, float luminance) pair in luminencePoints)
            {
                float zDistance = pair.point.Z + donutCameraDistance;
                if (zDistance <= 0f)
                {
                    continue;
                }
                float x = pair.point.X / zDistance;
                float y = pair.point.Y / zDistance;

                int screenX = (int) Math.Round(x * scale) + ScreenSize / 2;
                int screenY = (int) Math.Round(y * scale / 2f) + ScreenSize / 2;
                if (screenX <= 0 || screenX >= ScreenSize || screenY <= 0 || screenY >= ScreenSize)
                {
                    continue;
                }

                projectedPoints.Add((screenX, screenY, zDistance, pair.luminance));
            }

            // Deduplicate screen positions by retaining only the closest surface (minimum Z distance).
            return projectedPoints.GroupBy(p => (p.x, p.y)).Select(g => g.MinBy(p => p.z)).ToList();
        }

        // Maps calculated luminances to ASCII character glyphs and renders the frame to the console.
        private static void PrintPoints(List<(int x, int y, double z, float luminance)> points)
        {
            char[,] screen = new char[ScreenSize, ScreenSize];
            foreach ((int x, int y, double z, float luminance) point in points)
            {
                char letter = Grayscale[(int) Math.Clamp(Math.Round(point.luminance * (Grayscale.Length - 1)), 0, Grayscale.Length - 1)];
                screen[point.x, point.y] = letter;
            }

            Console.SetCursorPosition(0, 0);
            Console.CursorVisible = false;
            StringBuilder output = new(ScreenSize * ScreenSize + ScreenSize);
            for (int y = 0; y < ScreenSize; y++)
            {
                for (int x = 0; x < ScreenSize; x++)
                {
                    if (screen[x, y] == '\0')
                    {
                        output.Append(" ");
                        continue;
                    }
                    output.Append(screen[x, y]);
                }
                output.AppendLine();
            }
            Console.Write(output);
        }
    }
}
