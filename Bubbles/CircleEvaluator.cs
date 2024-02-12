using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
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

        EvaluateCircles(greyScaleStep, circles, image, pixels, out var outPixels, out var _, out var _);

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

    public static void GenerateBaseImages(string imageFile, string circlesFile, int greyScaleStep, int numFiles)
    {
        Console.WriteLine("Generating initial generation");
        var image = Image.Load<L8>(imageFile);
        var pixels = new byte[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);


        var circles = new List<Circle>();
        var eval = new GradientEval(image.Width, image.Height, pixels);


        for (var candidate = 0; candidate < numFiles; ++candidate)
        {
            circles.Clear();
            GenerateCircles(circles, eval);
            WriteCSV(circles, circlesFile + $"Init{candidate}.csv");
        }

        // EvaluateCircles(greyScaleStep, circles, image, pixels, out var outPixels);

        // using (var outImage = Image.LoadPixelData<L8>(outPixels, image.Width, image.Height))
        // {
        //     // Save the grayscale image as a PNG file
        //     outImage.Save("output.png");
        // }
        //
        // WriteSvg(circles, "circles.svg", "output.png");
        //
        // Console.WriteLine("Write output " + stopwatch.ElapsedMilliseconds);
        //
        // Console.WriteLine("Total time " + stopwatch2.ElapsedMilliseconds);
    }

    public static float EvaluateCircles(int greyScaleStep, List<Circle> circles, Image<L8> image,
        byte[] pixels, out byte[] outPixels, out int numSegments, out IEnumerable<CircleIntersectionInfo> circleIntersectionInfos)
    {
        var stopwatch = new Stopwatch();
        var circlesForPixel = GetCirclesForEachPixel(circles.ToArray(), image);

        //  Console.WriteLine("Get Circle coverage " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        var circleAvgValues = GetCircleIntersectionColors(image, pixels, circlesForPixel, greyScaleStep);

        //   Console.WriteLine("Get Circle color averages " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        outPixels = CreatePixelsFromCircleIntersectionColors(image, circleAvgValues, circlesForPixel);

        //   Console.WriteLine("Create Pixel output " + stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();

        var error = CalculateError(outPixels, pixels, greyScaleStep);
        // Console.WriteLine("Calculate error " + stopwatch.ElapsedMilliseconds);
        numSegments = circleAvgValues.Count();
        circleIntersectionInfos = circleAvgValues.Values;
        return error + numSegments / 500.0f;
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

    public static void WriteSvg(IEnumerable<Circle> circles, string pathName, string pngPath, IEnumerable<CircleIntersectionInfo> circleIntersectionInfos = null, int greyScaleStep = 0)
    {
        var svgDocument = new SvgDocument();

        svgDocument.Width = new SvgUnit(SvgUnitType.Pixel, 1024);
        svgDocument.Height = new SvgUnit(SvgUnitType.Pixel, 1024);

        var imageElement = new SvgImage();
        imageElement.X = new SvgUnit(SvgUnitType.Percentage, 0);
        imageElement.Y = new SvgUnit(SvgUnitType.Percentage, 0);
        imageElement.Width = new SvgUnit(SvgUnitType.Percentage, 100);
        imageElement.Height = new SvgUnit(SvgUnitType.Percentage, 100);
        imageElement.Href = pngPath;
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
                _ => Color.Yellow
            });
            svgDocument.Children.Add(circle);
        }

        if (circleIntersectionInfos != null)
        {
            foreach (var cii in circleIntersectionInfos)
            {
                if (cii.color != 255 && cii.size > 10)
                {
                    var text = new SvgText();
                    text.X = new SvgUnitCollection() { new SvgUnit(SvgUnitType.Pixel, cii.center.X * 2) };
                    text.Y = new SvgUnitCollection() { new SvgUnit(SvgUnitType.Pixel, (cii.center.Y + 1) * 2) };
                    text.TextAnchor = SvgTextAnchor.Middle;
                    text.FontSize = new SvgUnit(SvgUnitType.Percentage, 50);
                    text.Text = ((int)(cii.color / greyScaleStep)).ToString();
                    
                    text.Fill = new SvgColourServer(System.Drawing.Color.DimGray);
                    svgDocument.Children.Add(text);
                }
            }
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

    public struct CircleIntersectionInfo
    {
        public byte color;
        public Vector2 center;
        public float size;
    }
    
    private static Dictionary<FastBitArray, CircleIntersectionInfo> GetCircleIntersectionColors(Image<L8> image, byte[] bytes,
        FastBitArray[] circlesForPixel, int greyScaleStep)
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
                var v = new Vector2(x, y);
                circleValues.AddOrUpdate(circlesForPixel[x + image.Width * y], (1, color, v, v),
                    (_, tuple) => (
                        tuple.numPixels + 1, 
                        tuple.sumColor + color,
                        Vector2.Min(tuple.minPos, v),
                        Vector2.Max(tuple.maxPos, v)));
            }
        });

        var dictionary = circleValues.Select(kvp => 
            (
                kvp.Key, 
                new CircleIntersectionInfo {
                    color = (byte)((kvp.Value.sumColor / kvp.Value.numPixels + greyScaleStep / 2) / greyScaleStep *
                           greyScaleStep), 
                    center = (kvp.Value.minPos + kvp.Value.maxPos)*0.5f,
                    size = (kvp.Value.maxPos.X - kvp.Value.minPos.X) * (kvp.Value.maxPos.Y - kvp.Value.minPos.Y)
                }
            )
        ).ToDictionary();
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
        Dictionary<FastBitArray, CircleIntersectionInfo> circleAvgValues,
        FastBitArray[] fastBitArrays)
    {
        var outPixels1 = new byte[image.Width * image.Height];
        Parallel.For(0, image.Height, y =>
        {
            for (var x = 0; x < image.Width; x++)
                outPixels1[x + image.Width * y] = circleAvgValues[fastBitArrays[x + image.Width * y]].color;
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