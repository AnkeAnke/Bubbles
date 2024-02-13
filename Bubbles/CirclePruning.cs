using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bubbles;

public static class CirclePruning
{
    private static readonly float MinDistance3d = 0.02f;
    public static readonly float MaxDistanceEquality = 0.0012f;
    private static readonly float MinDistanceIntersection = 0.005f;
    private static readonly float MinDistanceSegments = 0.005f;
    private static readonly float MinCircleRadius = 0.005f;

    private static readonly float MaxCircleRadius = 0.1f;

    // private static readonly float WeightRadius = 10.0f;

    private static readonly float MinRating = 4.0f;
    private static readonly int NumSizeBuckets = 10;
    private static readonly float BucketWeight = 5.0f;

    private static readonly Random random = new(42);

    public static List<Circle> PruneRatedCircles(List<RatedCircle> circles, bool wriggle,
        out List<Vector2> intersectionPoints)
    {
        if (wriggle)
            return PruneCircles(
                circles.Select(c => c with { Rating = c.Rating + (float)random.NextDouble() * 1.0f })
                    .OrderDescending(new RatedCircle.FitnessComparer()).Select(rc => rc.Circle).ToList(),
                out intersectionPoints);
        return PruneCircles(
            circles.OrderDescending(new RatedCircle.FitnessComparer()).Select(rc => rc.Circle).ToList(),
            out intersectionPoints);
    }

    // Assuming circles are ordered by descending "goodness" (e.g., rating).
    public static List<Circle> PruneCircles(List<Circle> circleCandidates, out List<Vector2> intersectionPoints)
    {
        intersectionPoints = new List<Vector2>();
        var acceptedCircles = new List<Circle>();

        // for (var numAccepted = 0; numAccepted < circles.Count(); ++numAccepted)
        while (circleCandidates.Any())
        {
            var currentCandidate = circleCandidates.First();
            circleCandidates = circleCandidates.Skip(1).ToList();

            // Check whether any of the new intersection points are a problem.
            if (!CheckAndUpdateIntersections(intersectionPoints, acceptedCircles, currentCandidate))
                continue; // TODO: use?!

            acceptedCircles.Add(currentCandidate with { color = 1 });
            // Throw ou all further circles that are too similar to this one.
            circleCandidates = circleCandidates.Where(c => AcceptablePair(c, currentCandidate))
                .ToList();
        }

        return acceptedCircles;
    }

    public static bool AcceptablePair(Circle a, Circle b)
    {
        var distance3D = a.Dist(b);
        var minDistanceByRadius = Math.Min(a.radius, b.radius);
        if (distance3D < MinDistance3d * MinDistance3d ||
            distance3D < minDistanceByRadius * minDistanceByRadius) return false;

        // Look at maximal width of the segments.
        var rad = Vector2.Normalize(new Vector2(a.x - b.x, a.y - b.y));
        var a0 = new Vector2(a.x, a.y) + rad * a.radius;
        var a1 = new Vector2(a.x, a.y) - rad * a.radius;

        var b0 = new Vector2(b.x, b.y) + rad * b.radius;
        var b1 = new Vector2(b.x, b.y) - rad * b.radius;

        var dists = new (Vector2 a, Vector2 b)[]
        {
            (a0, b0), (a0, b1), (a1, b0), (a1, b1)
        };
        foreach (var d in dists)
        {
            var distance = Vector2.DistanceSquared(d.a, d.b);
            if (distance < MinDistanceSegments * MinDistanceSegments &&
                distance > MaxDistanceEquality * MaxDistanceEquality) return false;
        }

        return true;
    }

    public static bool CheckAndUpdateIntersections(List<Vector2> currentIntersections, List<Circle> currentCircles,
        Circle candidateCircle)
    {
        var newIntersections = new List<Vector2>();
        for (var c = 0; c < currentCircles.Count(); ++c)
        {
            // Look at new intersections, if any.
            var numIntersections = candidateCircle.AddIntersections(currentCircles[c], newIntersections);
            if (numIntersections > 0)
            {
                var radiusdist = currentCircles[c].radius / candidateCircle.radius;
                if (radiusdist < 1.0f) radiusdist = 1.0f / radiusdist;
                if (radiusdist < 1.05f) return false;

                for (var i = newIntersections.Count() - numIntersections; i < newIntersections.Count(); ++i)
                {
                    var newIntersection = newIntersections[i];

                    foreach (var oldIntersection in currentIntersections)
                    {
                        var pointDistanceSq = Vector2.DistanceSquared(newIntersection, oldIntersection);
                        if (pointDistanceSq < MinDistanceIntersection * MinDistanceIntersection &&
                            pointDistanceSq > MaxDistanceEquality * MaxDistanceEquality)
                            // Found a point that's interfering with this circle. Abort.
                            return false;
                    }
                }
            }
        }

        currentIntersections.AddRange(newIntersections);
        return true;
    }

    // public static static Vector3 CircleAsPoint(Circle c)
    // {
    //     return new Vector3(c.x, c.y, c.radius);
    // }
    public static List<RatedCircle> GenerateCirclesWithGoodRating(string imageFile, int greyScaleStep, int numCircles,
        out Image<L8> image, out byte[] pixels)
    {
        image = Image.Load<L8>(imageFile);
        pixels = new byte[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);

        var circles = new List<RatedCircle>();
        var eval = new GradientEval(image.Width, image.Height, pixels);

        while (circles.Count < numCircles)
        {
            var c = Circle.Random(MinCircleRadius, MaxCircleRadius);
            var pureRating = eval.RateCircle(c);
            var rating = pureRating +
                         (int)((c.radius - MinCircleRadius) * NumSizeBuckets / (MaxCircleRadius - MinCircleRadius)) *
                         BucketWeight;
            // +c.radius * WeightRadius;
            if (pureRating > MinRating)
                circles.Add(new RatedCircle { Circle = c, Rating = rating });
        }

        // eval.OutputStats();

        return circles;
    }

    public static List<Circle> GenerateTestSet()
    {
        var circles = new List<Circle>();
        for (var y = 0.1f; y < 0.9f; y += 0.2f)
        {
            var x = 0.1f;
            while (x < 0.9f)
            {
                circles.Add(new Circle
                {
                    x = x, y = y,
                    radius = float.Lerp((float)Math.Sqrt(MinDistanceIntersection * MinDistanceIntersection), 0.1f,
                        (float)random.NextDouble()),
                    color = 1
                });
                x += (float)random.NextDouble() * 0.1f;
            }
        }

        return circles;
    }

    public static List<Circle> GenerateRandom()
    {
        var circles = new List<Circle>();
        for (var c = 0; c < 1000; ++c)
        {
            var rad = float.Lerp((float)Math.Sqrt(MinDistanceIntersection * MinDistanceIntersection), 0.1f,
                (float)random.NextDouble());
            circles.Add(
                new Circle
                {
                    x = float.Lerp(rad, 1.0f - rad, (float)random.NextDouble()),
                    y = float.Lerp(rad, 1.0f - rad, (float)random.NextDouble()), radius = rad, color = 3
                });
        }

        return circles;
    }

    public struct RatedCircle
    {
        public Circle Circle;
        public float Rating;

        public class FitnessComparer : IComparer<RatedCircle>
        {
            public int Compare(RatedCircle x, RatedCircle y)
            {
                return x.Rating.CompareTo(y.Rating);
            }
        }
    }
}