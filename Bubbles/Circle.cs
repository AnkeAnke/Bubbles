using System.Numerics;

public struct Circle
{
    public float x, y, radius;
    private static readonly Random random = new(42);

    public float MinDistToOtherCircles(IEnumerable<Circle> otherCircles)
    {
        var minDist = float.MaxValue;
        foreach (var o in otherCircles)
        {
            var dist = Sqr(o.x - x) + Sqr(o.y - y) + Sqr(o.radius - radius);
            if (dist < minDist)
                minDist = dist;
        }

        return (float)Math.Sqrt(minDist);

        float Sqr(float x)
        {
            return x * x;
        }
    }

    public static Circle Random()
    {
        var r = (float)random.NextDouble() * 0.15f;
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