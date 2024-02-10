using System.Collections.Concurrent;
using System.Diagnostics;
using Bubbles;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg;

static class CircleEvaluator
{
    public static void EvaluateCirclesForImage(string imageFile, string circlesFile, int greyScaleStep)
    {
        var stopwatch = new Stopwatch();
        var stopwatch2 = new Stopwatch();
        stopwatch.Start();
        stopwatch2.Start();

        var image = Image.Load<L8>(imageFile);
        var pixels = new byte[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);

        //var circles = LoadCirclesCSV(circlesFile);

        var circles = new List<Circle>();
        GradientEval eval = new GradientEval(image.Width, image.Height, pixels);

        var stopwatch3 = new Stopwatch();
        stopwatch3.Start();

        const float kMinRating = 800;
        const float kMaxOverlap = 0.3f;
        const float kMinDist = 0.01f;
        const float kMinTotalScore = 15;
        const float kMaxRadius = 0.1f;
        const float kMinRadius = 0.01f;
        while (true)
        {
            var c = Circle.Random(kMinRadius, kMaxRadius);
            var rating = eval.RateCircle(c);
            var overlap = c.MaxOverlapWithOtherCircles(circles);
            var dist = c.MinDistFromOtherCircles(circles);
            if (rating > kMinRating
                && overlap < kMaxOverlap
                && dist > kMinDist 
                && rating * (1.0f - overlap) * dist > kMinTotalScore)
                circles.Add(c);
            
            if (stopwatch3.ElapsedMilliseconds > 5000)
                break;
        }
        

        //Circle
        //circles = circles.Where(c => eval.RateCircle(c) > 1000).ToArray();
        
        Console.WriteLine(circles.Count);
        
        Console.WriteLine("Load files " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        var circlesForPixel = GetCirclesForEachPixel(circles.ToArray(), image);

        //  Console.WriteLine("Get Circle coverage " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        var circleAvgValues = GetCircleIntersectionColors(image, pixels, circlesForPixel, greyScaleStep);

        //   Console.WriteLine("Get Circle color averages " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        var outPixels = CreatePixelsFromCircleIntersectionColors(image, circleAvgValues, circlesForPixel);

        //   Console.WriteLine("Create Pixel output " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        using (Image<L8> outImage = Image.LoadPixelData<L8>(outPixels, image.Width, image.Height))
        {
            // Save the grayscale image as a PNG file
            outImage.Save("output.png");
        }

        WriteSvg(circles, "circles.svg", "output.png");

        Console.WriteLine("Write output " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        CalculateError(outPixels, pixels, greyScaleStep);

        Console.WriteLine("Calculate error " + stopwatch.ElapsedMilliseconds);
        Console.WriteLine("Total time " + stopwatch2.ElapsedMilliseconds);
    }

    private static void WriteSvg(IEnumerable<Circle> circles, string pathName, string pngPath)
    {
        var svgDocument = new SvgDocument();
        
        svgDocument.Width = new SvgUnit(SvgUnitType.Pixel, 1024);
        svgDocument.Height = new SvgUnit(SvgUnitType.Pixel, 1024);

        var imageElement = new SvgImage();
        imageElement.X = new SvgUnit(SvgUnitType.Percentage, 0);
        imageElement.Y = new SvgUnit(SvgUnitType.Percentage, 0);
        imageElement.Width = new SvgUnit(SvgUnitType.Percentage, 100);
        imageElement.Height = new SvgUnit(SvgUnitType.Percentage, 100);
        imageElement.Href = Path.GetFullPath(pngPath);
        svgDocument.Children.Add(imageElement);
        
        foreach (var c in circles)
        {
            var circle = new SvgCircle();
            circle.CenterX = new SvgUnit(SvgUnitType.Percentage, 100 * c.x);
            circle.CenterY = new SvgUnit(SvgUnitType.Percentage, 100 * c.y);
            circle.Radius = new SvgUnit(SvgUnitType.Percentage, 100 * c.radius);
            circle.FillOpacity = 0;
            circle.Stroke = new SvgColourServer(System.Drawing.Color.Blue);
            svgDocument.Children.Add(circle); 
        }
        
        svgDocument.Write(pathName);
    }

    static Circle[] LoadCirclesCSV(string filePath) =>
        File.ReadAllLines(filePath)
            .Select(line => line.Split(","))
            .Select(strings => new Circle()
            {
                x = float.Parse(strings[0]),
                y = float.Parse(strings[1]),
                radius = float.Parse(strings[2])
            }).ToArray();

    static FastBitArray[] GetCirclesForEachPixel(Circle[] circles, Image<L8> image)
    {
        var numPixels = image.Width * image.Height;
        FastBitArray.Init(circles.Length / sizeof(ulong) + 1, numPixels);
        var fastBitArrays = new FastBitArray[numPixels];
        for (var i = 0; i < numPixels; i++)
            fastBitArrays[i] = new FastBitArray();

        Parallel.For(0, circles.Length, i =>
        {
            var circle = circles[i];
            var minY = (int)Math.Floor((circle.y - circle.radius) * image.Height);
            if (minY < 0) minY = 0;
            var maxY = (int)Math.Ceiling((circle.y + circle.radius) * image.Height);
            if (maxY > image.Height) maxY = image.Height;
            for (var y = minY; y < maxY; y++)
            {
                var fy = y / (float)image.Height;

                var minX = (int)Math.Floor((circle.x - circle.radius) * image.Width);
                if (minX < 0) minX = 0;
                var maxX = (int)Math.Ceiling((circle.x + circle.radius) * image.Width);
                if (maxX > image.Width) maxX = image.Width;

                for (var x = minX; x < maxX; x++)
                {
                    var fx = x / (float)image.Width;

                    if (SqrDist(fx, circle.x, fy, circle.y) <= circle.radius * circle.radius)
                        fastBitArrays[x + image.Width * y][i] = true;
                }
            }
        });
        return fastBitArrays;

        float SqrDist(float x1, float x2, float y1, float y2)
        {
            var xdist = x1 - x2;
            var ydist = y1 - y2;
            return xdist * xdist + ydist * ydist;
        }
    }

    static Dictionary<FastBitArray, byte> GetCircleIntersectionColors(Image<L8> image, byte[] bytes,
        FastBitArray[] circlesForPixel, int greyScaleStep)
    {
        var circleValues = new ConcurrentDictionary<FastBitArray, (long numPixels, long sumColor)>();
        Parallel.For(0, image.Height, y =>
        {
            for (var x = 0; x < image.Width; x++)
            {
                var color = bytes[x + image.Width * y];
                circleValues.AddOrUpdate(circlesForPixel[x + image.Width * y], (1, color),
                    (_, tuple) => (tuple.numPixels + 1, tuple.sumColor + color));
            }
        });

        var dictionary = circleValues.Select(kvp => (kvp.Key,
            (byte)(((kvp.Value.sumColor / kvp.Value.numPixels) + (greyScaleStep / 2)) / greyScaleStep *
                   greyScaleStep))).ToDictionary();
        return dictionary;
    }

    static byte[] CreatePixelsFromCircleIntersectionColors(Image<L8> image, Dictionary<FastBitArray, byte> circleAvgValues,
        FastBitArray[] fastBitArrays)
    {
        var outPixels1 = new byte[image.Width * image.Height];
        Parallel.For(0, image.Height, y =>
        {
            for (var x = 0; x < image.Width; x++)
                outPixels1[x + image.Width * y] = circleAvgValues[fastBitArrays[x + image.Width * y]];
        });
        return outPixels1;
    }

    static float CalculateError(byte[] newPixels, byte[] pixels, int greyScaleStep)
    {
        var error = 0;

        for (int i = 0; i < newPixels.Length; i++)
        {
            var srcPixel = pixels[i] + (greyScaleStep / 2) / greyScaleStep * greyScaleStep;
            var diff = Math.Abs(srcPixel - newPixels[i]);
            error += diff;
        }

        var result = (float)error / newPixels.Length;
        Console.WriteLine($"Error: {result}");
        return result;
    }
}


// Scratch


/* int batchSize = 10;
 var errors = new float[circles.Length / batchSize];
 for (var i = 0; i < circles.Length; i += batchSize)
 {
     var newCircles = circles.Take(i).Concat(circles.Skip(i+batchSize)).ToArray();
     Console.WriteLine(newCircles.Length);
     var circlesForPixel = GetCirclesForEachPixel(newCircles, image);

   //  Console.WriteLine("Get Circle coverage " + stopwatch.ElapsedMilliseconds);
     stopwatch.Restart();

     var circleAvgValues = GetCircleIntersectionColors(image, pixels, circlesForPixel, greyScaleStep);

  //   Console.WriteLine("Get Circle color averages " + stopwatch.ElapsedMilliseconds);
     stopwatch.Restart();

     var outPixels = CreatePixelsFromCircleIntersectionColors(image, circleAvgValues, circlesForPixel);

  //   Console.WriteLine("Create Pixel output " + stopwatch.ElapsedMilliseconds);
     stopwatch.Restart();

     errors[i/batchSize] = CalculateError(outPixels, pixels, greyScaleStep);

     Console.WriteLine("Calculate error " + stopwatch.ElapsedMilliseconds);
 }

 var avgError = errors.Average();

 var bestCircles = new List<Circle>();

 for (var i = 0; i < errors.Length; i++)
 {
     if (errors[i] > avgError)
     {
         bestCircles.AddRange(circles.Skip(i*batchSize).Take(batchSize));
     }

 }*/