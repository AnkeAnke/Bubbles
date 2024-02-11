using System.Numerics;

public struct Circle
{
    public float x, y, radius;
    public int color;
    private static readonly Random random = new(42);

    private float Sqr(float x)
    {
        return x * x;
    }

    private float OverlapPercentage(Circle other)
    {
        var d = (float)Math.Sqrt(Sqr(x - other.x) + Sqr(y - other.y));

        var area1 = (float)Math.PI * Sqr(radius);
        var area2 = (float)Math.PI * Sqr(other.radius);
        if (d > radius + other.radius)
            return 0;

        if (d <= radius - other.radius && radius >= other.radius)
            return area2 / area1;

        if (d <= other.radius - radius && other.radius >= radius)
            return area1 / area2;

        var alpha = (float)Math.Acos((Sqr(radius) + Sqr(d) - Sqr(other.radius)) / (2 * radius * d)) * 2;
        var beta = (float)Math.Acos((Sqr(other.radius) + Sqr(d) - Sqr(radius)) / (2 * other.radius * d)) * 2;
        var a1 = 0.5f * beta * Sqr(other.radius)
                 - 0.5f * Sqr(other.radius) * (float)Math.Sin(beta);
        var a2 = 0.5f * alpha * Sqr(radius)
                 - 0.5f * Sqr(radius) * (float)Math.Sin(alpha);
        return (a1 + a2) / Math.Max(area1, area2);
    }

    public float MaxOverlapWithOtherCircles(IEnumerable<Circle> otherCircles)
    {
        var circle = this;
        return otherCircles.Select(o => circle.OverlapPercentage(o)).Prepend(0).Max();
    }

    public float MinDistFromOtherCircles(IEnumerable<Circle> otherCircles)
    {
        var circle = this;
        return (float)Math.Sqrt(otherCircles.Select(o => circle.Dist(o)).Prepend(float.MaxValue).Min());
    }

    public float Dist(Circle o)
    {
        return Sqr(o.x - x) + Sqr(o.y - y) + Sqr(o.radius - radius);
    }

    public int AddIntersections(Circle other, List<Vector2> intersections)
    {
        // var centerA = new Vector2(x, y);
        // var centerB = new Vector2(other.x, other.y);
        // var radA = radius;
        // var radB = other.radius; // TODO
        // if (Vector2.Distance(centerA, centerB) > Sqr(radius + other.radius))
        //     return false;
        //
        // var v = centerA - centerB;
        // var d = (float)Math.Sqrt(Vector2.Dot(v, v));
        //
        // if (d > radA + radB || d == 0)
        //     return false;
        //
        // var u = v / d;
        // var xVec = centerA + u * (Sqr(d) - Sqr(radB) + Sqr(radA)) / (2 * d);
        //
        // var uPerp = new Vector2(u.Y, -u.X);
        // var a = (float)Math.Sqrt((-d + radB - radA) * (-d - radB + radA) * (-d + radB + radA) * (d + radB + radA)) / d;
        // if (float.IsNaN(a)) return false;
        // intersections.Add(xVec + uPerp * a / 2);
        // intersections.Add(xVec - uPerp * a / 2);
        // return true;
        var numIntersections =
            FindCircleCircleIntersections(x, y, radius, other.x, other.y, other.radius, out var i0, out var i1);
        if (numIntersections > 0) intersections.Add(i0);
        if (numIntersections > 1) intersections.Add(i1);
        return numIntersections;
    }

    // Find the points where the two circles intersect.
    private int FindCircleCircleIntersections(
        float cx0, float cy0, float radius0,
        float cx1, float cy1, float radius1,
        out Vector2 intersection1, out Vector2 intersection2)
    {
        // Find the distance between the centers.
        var dx = cx0 - cx1;
        var dy = cy0 - cy1;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        // See how many solutions there are.
        if (dist > radius0 + radius1)
        {
            // No solutions, the circles are too far apart.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
            return 0;
        }

        if (dist < Math.Abs(radius0 - radius1))
        {
            // No solutions, one circle contains the other.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
            return 0;
        }

        if (dist == 0 && radius0 == radius1)
        {
            // No solutions, the circles coincide.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
            return 0;
        }

        // Find a and h.
        var a = (radius0 * radius0 -
            radius1 * radius1 + dist * dist) / (2 * dist);
        var h = Math.Sqrt(radius0 * radius0 - a * a);

        // Find P2.
        var cx2 = cx0 + a * (cx1 - cx0) / dist;
        var cy2 = cy0 + a * (cy1 - cy0) / dist;

        // Get the points P3.
        intersection1 = new Vector2(
            (float)(cx2 + h * (cy1 - cy0) / dist),
            (float)(cy2 - h * (cx1 - cx0) / dist));
        intersection2 = new Vector2(
            (float)(cx2 - h * (cy1 - cy0) / dist),
            (float)(cy2 + h * (cx1 - cx0) / dist));

        // See if we have 1 or 2 solutions.
        if (dist == radius0 + radius1) return 1;
        return 2;
    }

    public static Circle Random(float minRadius, float maxRadius)
    {
        var r = minRadius + (float)random.NextDouble() * (maxRadius - minRadius);
        return new Circle
        {
            radius = r,
            x = r + (float)random.NextDouble() * (1 - 2 * r),
            y = r + (float)random.NextDouble() * (1 - 2 * r)
        };
    }

    public Circle Add(Vector3 offset)
    {
        return new Circle { x = x + offset.X, y = y + offset.Y, radius = radius + offset.Z };
    }
}