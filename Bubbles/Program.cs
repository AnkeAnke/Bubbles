using System.Collections;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

var imageFile = args[0];
var image = Image.Load<L8>(imageFile);
var numPixels = image.Width * image.Height;
var pixels = new L8[image.Width * image.Height];
image.CopyPixelDataTo(pixels);

var circlesFile = args[1];
var circles = File.ReadAllLines(circlesFile)
    .Select(line => line.Split(","))
    .Select(strings => new Circle()
    {
        x = float.Parse(strings[0]),
        y = float.Parse(strings[1]),
        radius = float.Parse(strings[2])
    }).ToArray();

var circlesForPixel = new BitArray[numPixels];
for (var i = 0; i < numPixels; i++)
    circlesForPixel[i] = new BitArray(circles.Length);

for (var y = 0; y < image.Height; y++)
{
    var fy = y / (float)image.Height; 
    for (var x = 0; x < image.Width; x++)
    {
        var fx = x / (float)image.Width;
        for (var i = 0; i < circles.Length; i++)
        {
            var circle = circles[i];

            if (SqrDist(fx, circle.x, fy, circle.y) <= circle.radius * circle.radius)
                circlesForPixel[x + image.Width * y][i] = true;
        }
    }
}

var circleValues = new Dictionary<BitArray, (long numPixels, long sumColor)>(new BitArrayComparer());
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

var circleAvgValues = circleValues.Select(kvp => (kvp.Key, (byte)(kvp.Value.sumColor/kvp.Value.numPixels))).ToDictionary(new BitArrayComparer());

var outPixels = new byte[image.Width * image.Height];
for (var y = 0; y < image.Height; y++)
{
    for (var x = 0; x < image.Width; x++)
        outPixels[x + image.Width * y] = circleAvgValues[circlesForPixel[x + image.Width * y]];
}

using (Image<L8> outImage = Image.LoadPixelData<L8>(outPixels, image.Width, image.Height))
{
    // Save the grayscale image as a PNG file
    outImage.Save("output.png");
}



Console.WriteLine("Hello, World!");

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

class BitArrayComparer : IEqualityComparer<BitArray>
{
    public bool Equals(BitArray x, BitArray y)
    {
        if (x.Length != y.Length)
            return false;

        for (int i = 0; i < x.Length; i++)
        {
            if (x[i] != y[i])
                return false;
        }

        return true;
    }

    public int GetHashCode(BitArray obj)
    {
        int hash = 17;
        for (int i = 0; i < obj.Length; i++)
        {
            hash = hash * 31 + (obj[i] ? 1 : 0);
        }
        return hash;
    }
}