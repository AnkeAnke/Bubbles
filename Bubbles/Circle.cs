public struct Circle
{
    public float x, y, radius;

    static Random random = new ();

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
        
        float Sqr(float x) => x * x;
    }

    public static Circle Random()
    {
        var r = (float)random.NextDouble() * 0.15f;
        return new()
        {
            radius = r,
            x = r + (float)random.NextDouble() * (1 - 2*r),
            y = r + (float)random.NextDouble() * (1 - 2*r)
        };
    }
}