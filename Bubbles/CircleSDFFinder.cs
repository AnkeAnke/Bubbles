using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace Bubbles;

public class CircleSDFFinder
{
    public void FindCirclesForImage(string imageFile)
    {
        var greyScaleStep = 255 / 5;
        var image = Image.Load<L8>(imageFile);
        var width = image.Width;
        var height = image.Height;
        var pixels = new byte[width * height];
        image.CopyPixelDataTo(pixels);


        var imageBlurred = image.Clone();
        imageBlurred.Mutate(new GaussianBlurProcessor(5));

        var pixelsBucketed = new byte[width * height];
        imageBlurred.CopyPixelDataTo(pixelsBucketed);

        for (int i = 0; i < width * height; i++)
            pixelsBucketed[i] = (byte)(((pixelsBucketed[i] + greyScaleStep/2) / greyScaleStep) * greyScaleStep);
        var edgeImage = Image.LoadPixelData<L8>(pixelsBucketed, width, height);
        edgeImage.Save("buckets.png");

        edgeImage.Mutate(new EdgeDetectorProcessor(EdgeDetectorKernel.LaplacianOfGaussian, true));
        edgeImage.Save("edges.png");
        var edgeData = new byte[width * height];
        edgeImage.CopyPixelDataTo(edgeData);

        var distanceField = new float[width, height];
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
                distanceField[x, y] = ComputeDistance(x, y, edgeData, width, height);
        });
        
        var normalizeDistanceMap = NormalizeDistanceMap(distanceField);
        var outData = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                outData[x + width * y] = (byte)(Math.Clamp(normalizeDistanceMap[x, y], 0, 1) * 255);
        }        
        using (var outImage = Image.LoadPixelData<L8>(outData, width, height))
        {
            outImage.Save("sdf.png");
        }

        var originalDistanceField = new float[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                originalDistanceField[x, y] = distanceField[x, y];
        }        

        var circles = new List<Circle>();
        for (var pass = 0; pass < 10; pass++)
        {
            var circlesPerSize = 6 * (pass+1);

            for (var x = 0; x < circlesPerSize; x++)
            {
                for (var y = 0; y < circlesPerSize; y++)
                {
                    if (DropCircleOntoSDF((float)x / circlesPerSize, (float)y / circlesPerSize, width, height,
                            distanceField, originalDistanceField,  out var circle, (0.08f / (pass+1))))
                        circles.Add(circle);
                }
            }
        }

        Console.WriteLine($"circles {circles.Count} {circles[0].x}, {circles[0].y}, {circles[0].radius}");
        
        CircleEvaluator.EvaluateCircles(greyScaleStep, circles, image, pixels, out var outPixels, out _);

        using (var outImage = Image.LoadPixelData<L8>(outPixels, image.Width, image.Height))
        {
            outImage.Save("sdf_output.png");
        }

        CircleEvaluator.WriteSvg(circles, "sdf_output.svg", "sdf_output.png");

    }

    private bool DropCircleOntoSDF(float x, float y, int width, int height, float[,] distanceField, float[,] originalDistanceField, out Circle c, float minRadius)
    {
        Console.WriteLine($"Drop circle {x} {y}");
        c = new Circle();

        int sdfX = (int)(x * width);
        int sdfY = (int)(y * height);
        while (true)
        {
            if (sdfX == 0 || sdfY == 0 || sdfX == width - 1 || sdfY == height - 1)
                return false;
            float sdfValue = distanceField[sdfX, sdfY];
            if (distanceField[sdfX + 1, sdfY] > sdfValue)
                sdfX++;
            else if (distanceField[sdfX - 1, sdfY] > sdfValue)
                sdfX--;
            else if (distanceField[sdfX, sdfY + 1] > sdfValue)
                sdfY++;
            else if (distanceField[sdfX, sdfY - 1] > sdfValue)
                sdfY--;
            else
            {
                sdfValue += 2;
                c = new Circle()
                {
                    x = sdfX/(float)width,
                    y = sdfY/(float)height,
                    radius = sdfValue/(float)width,
                    color = 1
                };
                if (c.radius < minRadius)
                    return false;

                if (originalDistanceField[sdfX, sdfY] > distanceField[sdfX, sdfY])
                    return false;
                
                var center = new Vector2(sdfX, sdfY);
                Console.WriteLine($"Got circle {sdfX} {sdfY} {sdfValue}");
                for (int cx = (int)(sdfX - sdfValue); cx < sdfX + sdfValue; cx++)
                {
                    for (int cy = (int)(sdfY - sdfValue); cy < sdfY + sdfValue; cy++)
                    {
                        if (cx >= 0 && cy >= 0 && cx < width && cy < height)
                        {
                            float dist = Vector2.Distance(new Vector2(cx, cy), center);
                            if (dist < sdfValue)
                                distanceField[cx, cy] -= sdfValue - dist;
                        }
                    }
                }


                return true;
            }
        }
    }

    private static float[,] NormalizeDistanceMap(float[,] distanceMap)
    {
        var normalizedDistanceMap = new float[distanceMap.GetLength(0), distanceMap.GetLength(1)];
        var maxAbsValue = (from float value in distanceMap select Math.Abs(value)).Prepend(0).Max();

        // Normalize values
        for (var y = 0; y < distanceMap.GetLength(1); y++)
        {
            for (var x = 0; x < distanceMap.GetLength(0); x++)
                normalizedDistanceMap[x, y] = distanceMap[x,y]/maxAbsValue;
        }

        return normalizedDistanceMap;
    }
    
    private static float ComputeDistance(int x, int y, byte[] grayscaleArray, int width, int height)
    {
        byte value = grayscaleArray[x + y * width];

        if (value == 255)
        {
            return 0; // Inside the boundary
        }

        float minDistance = float.MaxValue;
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                if (grayscaleArray[i + width * j] != 255) continue;
                var distance = Vector2.Distance(new Vector2(i, j), new Vector2(x, y));
                minDistance = Math.Min(minDistance, distance);
            }
        }

        return minDistance;
    }

}