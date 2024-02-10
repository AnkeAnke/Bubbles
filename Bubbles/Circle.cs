using System.Numerics;

public struct Circle
{
    public float x, y, radius;
    private static readonly Random random = new(42);

    float Sqr(float x) => x * x;

    float OverlapPercentage(Circle other)
    {
        var d = (float)Math.Sqrt(Sqr(x - other.x) + Sqr(y - other.y));

        var area1 = (float)Math.PI * Sqr(radius);
        var area2 = (float)Math.PI * Sqr(other.radius);
        if (d > radius + other.radius)
            return 0;

        if (d <= (radius - other.radius) && radius >= other.radius)
            return area2/area1;

        if (d <= (other.radius - radius) && other.radius >= radius)
            return area1/area2;

        var alpha = (float)Math.Acos((Sqr(radius) + Sqr(d) - Sqr(other.radius)) / (2 * radius * d)) * 2;
        var beta = (float)Math.Acos((Sqr(other.radius) + Sqr(d) - Sqr(radius)) / (2 * other.radius * d))* 2;
        var a1 = 0.5f * beta * Sqr(other.radius)
             - 0.5f * Sqr(other.radius) * (float)Math.Sin(beta);
        var a2 = 0.5f * alpha * Sqr(radius)
             - 0.5f * Sqr(radius) * (float)Math.Sin(alpha);
        return (a1 + a2)/Math.Max(area1, area2);
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

    private float Dist(Circle o) => Sqr(o.x - x) + Sqr(o.y - y) + Sqr(o.radius - radius);

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