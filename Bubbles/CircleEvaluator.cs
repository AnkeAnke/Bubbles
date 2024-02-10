using System.Collections.Concurrent;
using System.Diagnostics;
using Bubbles;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg;
using Color = System.Drawing.Color;

internal static class CircleEvaluator
{
    public static void WriteCSV(IEnumerable<Circle> circles, string path)
    {
        File.WriteAllLines(path, circles.Select(c => $"{c.x},{c.y},{c.radius}"));
        Console.WriteLine($"Wrote csv to {path}");
    }

    public static List<List<Circle>> LoadAllCirclesFromFolder(string path)
    {
        var files = Directory.GetFiles(path, "*.csv");
        return files.Select(f => LoadCirclesCSV(f).ToList()).ToList();
    }


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
        var eval = new GradientEval(image.Width, image.Height, pixels);


        // for (var candidate = 0; candidate < 50; ++candidate)
        // {
        //     circles.Clear();
        GenerateCircles(circles, eval);
        //     WriteCSV(circles, Path.GetDirectoryName(circlesFile) + $"Init{candidate}.csv");
        // }

        //Circle
        //circles = circles.Where(c => eval.RateCircle(c) > 1000).ToArray();

        Console.WriteLine(circles.Count);

        Console.WriteLine("Load files " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        EvaluateCircles(greyScaleStep, circles, image, pixels, out var outPixels);

        stopwatch.Restart();

        using (var outImage = Image.LoadPixelData<L8>(outPixels, image.Width, image.Height))
        {
            // Save the grayscale image as a PNG file
            outImage.Save("output.png");
        }

        WriteSvg(circles, "circles.svg", "output.png");

        Console.WriteLine("Write output " + stopwatch.ElapsedMilliseconds);

        Console.WriteLine("Total time " + stopwatch2.ElapsedMilliseconds);
    }

    public static float EvaluateCircles(int greyScaleStep, List<Circle> circles, Image<L8> image,
        byte[] pixels, out byte[] outPixels)
    {
        var stopwatch = new Stopwatch();
        var circlesForPixel = GetCirclesForEachPixel(circles.ToArray(), image);

        //  Console.WriteLine("Get Circle coverage " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        var circleAvgValues = GetCircleIntersectionColors(image, pixels, circlesForPixel, greyScaleStep,
            out var numPixels10thPercentile,
            out var numPixels50thPercentile);

        //   Console.WriteLine("Get Circle color averages " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        outPixels = CreatePixelsFromCircleIntersectionColors(image, circleAvgValues, circlesForPixel);

        //   Console.WriteLine("Create Pixel output " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        var error = CalculateError(outPixels, pixels, greyScaleStep);
        // Console.WriteLine("Calculate error " + stopwatch.ElapsedMilliseconds);
        return error;
    }

    private static void GenerateCircles(List<Circle> circles, GradientEval eval)
    {
        const float kMinRating = 8;
        const float kMaxOverlap = 0.3f;
        const float kMinDist = 0.01f;
        const float kMinTotalScore = 0.15f;
        const float kMaxRadius = 0.1f;
        const float kMinRadius = 0.01f;

        while (circles.Count < 600)
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
        }
    }

    public static void WriteSvg(IEnumerable<Circle> circles, string pathName, string pngPath)
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
            circle.Stroke = new SvgColourServer(c.color switch
            {
                1 => Color.Red,
                2 => Color.Green,
                3 => Color.Blue,
                _ => Color.Yellow,
            });
            svgDocument.Children.Add(circle);
        }

        svgDocument.Write(pathName);
    }

    private static Circle[] LoadCirclesCSV(string filePath)
    {
        return File.ReadAllLines(filePath)
            .Select(line => line.Split(","))
            .Select(strings => new Circle
            {
                x = float.Parse(strings[0]),
                y = float.Parse(strings[1]),
                radius = float.Parse(strings[2])
            }).ToArray();
    }

    private static FastBitArray[] GetCirclesForEachPixel(Circle[] circles, Image<L8> image)
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

    private static Dictionary<FastBitArray, byte> GetCircleIntersectionColors(Image<L8> image, byte[] bytes,
        FastBitArray[] circlesForPixel, int greyScaleStep, out int numPixels10thPercentile,
        out int numPixels50thPercentile)
    {
        var zeroPatch = 1;
        for (var y = 0; y < image.Width; y++)
        for (var x = 0; x < image.Width; x++)
            if (circlesForPixel[x + image.Width * y].isZero && circlesForPixel[x + image.Width * y].zeroPatch == 0)
                FillZeroPatch(x, y, circlesForPixel, image, zeroPatch++);

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

        var histogram = new SortedDictionary<long, int>();
        long numAreas = 0;
        foreach (var np in circleValues.Values.Select(tuple => tuple.numPixels))
        {
            histogram.TryAdd(np, 0);
            histogram[np]++;
            numAreas++;
        }

        var numAreasCounted = 0;
        numPixels10thPercentile = 0;
        numPixels50thPercentile = 0;
        foreach (var kvp in histogram)
        {
            if (numAreasCounted < numAreas * 0.1f && numAreasCounted + kvp.Value > numAreas * 0.1f)
                numPixels10thPercentile = (int)kvp.Key;
            // Console.WriteLine($"10th percentile: {kvp.Key} pixels");
            if (numAreasCounted < numAreas * 0.5f && numAreasCounted + kvp.Value > numAreas * 0.5f)
                numPixels50thPercentile = (int)kvp.Key;
            // Console.WriteLine($"50th percentile: {kvp.Key} pixels");
            numAreasCounted += kvp.Value;
            //Console.WriteLine($"{kvp.Key} pixels: {kvp.Value}");
        }

        var dictionary = circleValues.Select(kvp => (kvp.Key,
            (byte)((kvp.Value.sumColor / kvp.Value.numPixels + greyScaleStep / 2) / greyScaleStep *
                   greyScaleStep))).ToDictionary();
        return dictionary;
    }

    private static void FillZeroPatch(int x, int y, FastBitArray[] circlesForPixel, Image<L8> image, int zeroPatch)
    {
        var index = x + image.Width * y;
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height || !circlesForPixel[index].isZero ||
            circlesForPixel[index].zeroPatch != 0) return;
        circlesForPixel[index].zeroPatch = zeroPatch;
        FillZeroPatch(x + 1, y, circlesForPixel, image, zeroPatch);
        FillZeroPatch(x - 1, y, circlesForPixel, image, zeroPatch);
        FillZeroPatch(x, y + 1, circlesForPixel, image, zeroPatch);
        FillZeroPatch(x, y - 1, circlesForPixel, image, zeroPatch);
    }

    private static byte[] CreatePixelsFromCircleIntersectionColors(Image<L8> image,
        Dictionary<FastBitArray, byte> circleAvgValues,
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

    private static float CalculateError(byte[] newPixels, byte[] pixels, int greyScaleStep)
    {
        var error = 0;

        for (var i = 0; i < newPixels.Length; i++)
        {
            var srcPixel = pixels[i] + greyScaleStep / 2 / greyScaleStep * greyScaleStep;
            var diff = Math.Abs(srcPixel - newPixels[i]);
            error += diff;
        }

        var result = (float)error / newPixels.Length;
        // Console.WriteLine($"Error: {result}");
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