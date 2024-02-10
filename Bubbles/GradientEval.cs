using System.Numerics;

namespace Bubbles;

public class GradientEval
{
    private const int NumSamples = 200;

    // private const float MinSaliency = 0.2f;
    // private const float MaxSaliency = 1.0f;
    // private readonly byte[] _saliency;
    private readonly Vector2[] _gradients;

    private readonly int _width, _height;

    public GradientEval(int width, int height, byte[] pixels) //, byte[] saliencyPixels)
    {
        _width = width;
        _height = height;
        // _saliency = saliencyPixels;


        _gradients = new Vector2[width * height];
        for (var y = 1; y < height - 1; ++y)
        for (var x = 1; x < width - 1; ++x)
            _gradients[x + y * width] =
                new Vector2((pixels[Index(x + 1, y)] - pixels[Index(x - 1, y)]) * 0.5f,
                    (pixels[Index(x, y + 1)] - pixels[Index(x, y - 1)]) * 0.5f);
    }

    private int Index(int x, int y)
    {
        return x + y * _width;
    }

    public float RateCircle(Circle circle) //, bool withSaliency = false)
    {
        float sumGradientStrengths = 0;
        for (var s = 0; s < NumSamples; ++s)
        {
            var radiusVec = new Vector2((float)Math.Sin((double)s / NumSamples * Math.PI * 2),
                (float)Math.Cos((float)s / NumSamples * Math.PI * 2));
            var samplePos = new Vector2(_width * (circle.x + radiusVec.X * circle.radius),
                _height * (circle.y + radiusVec.Y * circle.radius));
            var gradientStrengthAcrossBorder = Vector2.Dot(SampleGradient(samplePos), radiusVec);
            // if (withSaliency) gradientStrengthAcrossBorder *= SampleSaliency(samplePos);
            sumGradientStrengths += Math.Abs(gradientStrengthAcrossBorder);
        }

        return sumGradientStrengths / NumSamples;
    }

    private Vector2 LookupGradient(int x, int y)
    {
        if (x >= _width || y >= _height || x < 0 || y < 0)
            return Vector2.Zero;
        return _gradients[Index(x, y)];
    }

    // private float LookupSaliency(int x, int y)
    // {
    //     if (x >= _width || y >= _height || x < 0 || y < 0)
    //         return 0.0f;
    //     var sampledSaliency = (float)_saliency[Index(x, y)] / 255;
    //     return sampledSaliency * (MaxSaliency - MinSaliency) + MinSaliency;
    // }

    private Vector2 SampleGradient(Vector2 pos)
    {
        var x = (int)Math.Floor(pos.X);
        var y = (int)Math.Floor(pos.Y);
        return Vector2.Lerp(
            Vector2.Lerp(LookupGradient(x, y), LookupGradient(x + 1, y), pos.X - x),
            Vector2.Lerp(LookupGradient(x, y + 1), LookupGradient(x + 1, y + 1), pos.X - x),
            pos.Y - y);
    }

    private float Lerp(float firstFloat, float secondFloat, float by)
    {
        return firstFloat * (1 - by) + secondFloat * by;
    }

    // private float SampleSaliency(Vector2 pos)
    // {
    //     var x = (int)Math.Floor(pos.X);
    //     var y = (int)Math.Floor(pos.Y);
    //     return Lerp(
    //         Lerp(LookupSaliency(x, y), LookupSaliency(x + 1, y), pos.X - x),
    //         Lerp(LookupSaliency(x, y + 1), LookupSaliency(x + 1, y + 1), pos.X - x),
    //         pos.Y - y);
    // }

    private Vector3 EstimateCircleParamGradient(Circle circle)
    {
        return new Vector3(
            (RateCircle(circle with { x = circle.x + 1.0f / _width }) -
             RateCircle(circle with { x = circle.x - 1.0f / _width })) * 0.5f
            / _width,
            (RateCircle(circle with { y = circle.y + 1.0f / _height }) -
             RateCircle(circle with { y = circle.y - 1.0f / _height })) * 0.5f / _height,
            (RateCircle(circle with { radius = circle.radius + 1.0f / _height }) -
             RateCircle(circle with { radius = circle.radius - 1.0f / _height })) * 0.5f / _height);
    }

    public Circle OptimizeCircle(Circle circle, int numSteps, float stepSize, out float bestRating)
    {
        var bestCircle = circle;
        bestRating = RateCircle(bestCircle);
        for (var s = 0; s < numSteps; ++s)
        {
            var grad = EstimateCircleParamGradient(circle);
            circle = circle.Add(grad * stepSize);
            // if (s == 0) Console.WriteLine($"Grad: {grad * stepSize}");
            var rating = RateCircle(circle);
            if (!(rating > bestRating)) continue;
            bestRating = rating;
            bestCircle = circle;
        }

        return bestCircle;
    }

    public void OptimizeCircles(List<Circle> circles, int numSteps, float stepSize)
    {
        for (var c = 0; c < circles.Count; ++c)
            circles[c] = OptimizeCircle(circles[c], numSteps, stepSize, out var rating);
    }
}