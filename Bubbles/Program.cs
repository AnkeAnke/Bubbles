using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

const int kNumGreyScales = 5;
int kGreyScaleStep = 255 / kNumGreyScales;

var stopwatch = new Stopwatch();
stopwatch.Start();
var imageFile = args[0];
var image = Image.Load<L8>(imageFile);
var numPixels = image.Width * image.Height;
var pixels = new L8[image.Width * image.Height];
image.CopyPixelDataTo(pixels);
Console.WriteLine(stopwatch.ElapsedMilliseconds);

var circlesFile = args[1];
var circles = File.ReadAllLines(circlesFile)
    .Select(line => line.Split(","))
    .Select(strings => new Circle()
    {
        x = float.Parse(strings[0]),
        y = float.Parse(strings[1]),
        radius = float.Parse(strings[2])
    }).ToArray();

Console.WriteLine("Parse CSV "+stopwatch.ElapsedMilliseconds);

FastBitArray.perInstanceSize = circles.Length / sizeof(ulong) + 1;
FastBitArray.array = new ulong[FastBitArray.perInstanceSize * numPixels];
var circlesForPixel = new FastBitArray[numPixels];
for (var i = 0; i < numPixels; i++)
    circlesForPixel[i] = new FastBitArray();

Console.WriteLine("Allocate "+stopwatch.ElapsedMilliseconds);

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
                circlesForPixel[x + image.Width * y][i] = true;
        }
    }
});

Console.WriteLine("Get Circle coverage "+stopwatch.ElapsedMilliseconds);


var circleValues = new Dictionary<FastBitArray, (long numPixels, long sumColor)>();
for (var y = 0; y < image.Height; y++)
{
    for (var x = 0; x < image.Width; x++)
    {
        var val = circleValues.GetValueOrDefault(circlesForPixel[x + image.Width * y], (0,0));
        val.numPixels++;
        val.sumColor += pixels[x + image.Width * y].PackedValue;
        circleValues[circlesForPixel[x + image.Width * y]] = val;
    }
}
Console.WriteLine("Get Circle color averages1 " + stopwatch.ElapsedMilliseconds);

var circleAvgValues = circleValues.Select(kvp => (kvp.Key, (byte)(((kvp.Value.sumColor/kvp.Value.numPixels)+(kGreyScaleStep/2))/kGreyScaleStep*kGreyScaleStep))).ToDictionary();
Console.WriteLine("Get Circle color averages2 " +stopwatch.ElapsedMilliseconds);

var outPixels = new byte[image.Width * image.Height];
for (var y = 0; y < image.Height; y++)
{
    for (var x = 0; x < image.Width; x++)
        outPixels[x + image.Width * y] = circleAvgValues[circlesForPixel[x + image.Width * y]];
}
Console.WriteLine(stopwatch.ElapsedMilliseconds);

using (Image<L8> outImage = Image.LoadPixelData<L8>(outPixels, image.Width, image.Height))
{
    // Save the grayscale image as a PNG file
    outImage.Save("output.png");
}
Console.WriteLine(stopwatch.ElapsedMilliseconds);

float SqrDist(float x1, float x2, float y1, float y2)
{
    var xdist = x1 - x2;
    var ydist = y1 - y2;
    return xdist * xdist + ydist * ydist;
}

struct Circle
{
    public float x, y, radius;
}

class FastBitArray
{
    public static int perInstanceSize;
    public static ulong[] array;
    
    static int curIndex;
    private int index;
    private int hashCode;
    public FastBitArray()
    {
        index = curIndex;
        curIndex += perInstanceSize;
    }

    public override int GetHashCode()
    {
        if (hashCode == 0)
            hashCode = RecalculateHashCode();
        return hashCode;
    }

    public override bool Equals(object? obj)
    {
        if (obj is FastBitArray fa)
        {
            for (int i = 0; i < perInstanceSize; i++)
            {
                if (array[i + index] != array[i + fa.index])
                    return false;
            }

            return true;
        }

        return false;
    }

    int RecalculateHashCode()
    {
        int hash = 17;
        for (int i = 0; i < perInstanceSize; i++)
        {
            hash = hash * 31 + (int)array[i + index];
        }
        return hash;
        
    }
    
    public bool this[int i]
    {
        //get => (array[index + i / sizeof(ulong)] >> (i % sizeof(ulong)) & 1) == 1;
        set
        {
            if (value)
                array[index + i / sizeof(ulong)] |= (ulong)1 << i % sizeof(ulong);
            else
                array[index + i / sizeof(ulong)] &= ~((ulong)1 << i % sizeof(ulong));
            hashCode = 0;
        }
    }
}
