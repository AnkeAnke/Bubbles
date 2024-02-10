﻿using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace Bubbles;

public class GradientEval
{
    private const int NumSamples = 100;
    private readonly Vector2[] _gradients;
    private readonly int _width, _height;

    public GradientEval(int width, int height, byte[] pixels)
    {
        _width = width;
        _height = height;


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

    public float RateCircle(Circle circle)
    {
        float sumGradientStrengths = 0;
        for (var s = 0; s < NumSamples; ++s)
        {
            var radiusVec = new Vector2((float)Math.Sin((double)s / NumSamples * Math.PI * 2),
                (float)Math.Cos((float)s / NumSamples * Math.PI * 2));
            var samplePos = new Vector2(_width * (circle.x + radiusVec.X * circle.radius), _height * (circle.y + radiusVec.Y * circle.radius));
            var gradientStrengthAcrossBorder = Vector2.Dot(SampleGradient(samplePos), radiusVec);
            sumGradientStrengths += Math.Abs(gradientStrengthAcrossBorder);
        }

        return sumGradientStrengths;
    }

    private Vector2 LookupGradient(int x, int y)
    {
        if (x >= _width || y >= _height)
            return Vector2.Zero;
        return _gradients[Index(x, y)];
    }
    
    private Vector2 SampleGradient(Vector2 pos)
    {
        var x = (int)Math.Floor(pos.X);
        var y = (int)Math.Floor(pos.Y);
        return Vector2.Lerp(
            Vector2.Lerp(LookupGradient(x, y), LookupGradient(x + 1, y), pos.X - x),
            Vector2.Lerp(LookupGradient(x, y + 1), LookupGradient(x + 1, y + 1), pos.X - x),
            pos.Y - y);
    }
}