using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public class GeneticAlgorithm
{
    private const float kMaxRadius = 0.1f;
    private const float kMinRadius = 0.01f;
    private static readonly Random random = new(42);

    private static readonly int NumCirclesPerCandidate = 600;
    private static readonly int NumCandidatesPerGeneration = 50;

    private static readonly int NumCirclesMutated = 100;
    private static readonly float MaximalMutation = 0.01f;
    private static readonly int NumCirclesRandomized = 10;

    // private static readonly int NumParentsSelected = 20;
    private static readonly int NumParentsMin = 2;
    private static readonly int NumParentsMax = 4;
    private static readonly int NumElitesKept = 3;

    private static int MaxID;

    private Generation _currentGeneration;
    private int _kGreyScaleStep;

    private Image<L8> image;
    private byte[] pixels;

    public Candidate GetBestCandidate()
    {
        return _currentGeneration[0];
    }

    public void OutputBestCandidate(string dir)
    {
        var error = CircleEvaluator.EvaluateCircles(_kGreyScaleStep, _currentGeneration[0].Circles.ToList(), image,
            pixels, out var outPixels, out var _);
        Console.WriteLine($"Best error: {error}");
        using (var outImage = Image.LoadPixelData<L8>(outPixels, image.Width, image.Height))
        {
            // Save the grayscale image as a PNG file
            outImage.Save(dir + "output.png");
        }

        CircleEvaluator.WriteSvg(_currentGeneration[0].Circles, dir + "circles.svg", dir + "output.png");
        Console.WriteLine("Output done");
    }

    public void LoadFirstGeneration(string imageFile, string fileDirectory, int kGreyScaleStep)
    {
        _kGreyScaleStep = kGreyScaleStep;
        var allCircles = CircleEvaluator.LoadAllCirclesFromFolder(fileDirectory);
        if (allCircles.Count < NumCandidatesPerGeneration)
            throw new ArgumentException(
                $"Too few csv files in given folder, require at least {NumCandidatesPerGeneration}",
                nameof(fileDirectory));

        MaxID = 0;

        image = Image.Load<L8>(imageFile);
        pixels = new byte[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);

        _currentGeneration = new Generation();
        for (var c = 0; c < NumCandidatesPerGeneration; ++c)
        {
            if (allCircles[c].Count != NumCirclesPerCandidate)
                throw new Exception(
                    $"Not the expected amount of circles in {c}th file, {allCircles[c].Count} instead of the expected {NumCirclesPerCandidate}.");
            _currentGeneration[c] = new Candidate
            {
                Circles = allCircles[c].ToArray(), ID = MaxID++,
                Fitness = 1.0f /
                          CircleEvaluator.EvaluateCircles(kGreyScaleStep, allCircles[c], image,
                              pixels, out var outPixels, out var numSegments),
                NumSegments = numSegments
            };
        }

        _currentGeneration.Candidates = _currentGeneration.Candidates.OrderByDescending(c => c.Fitness).ToArray();
    }

    public void GenerateNextGeneration(out float bestFitness)
    {
        var nextGeneration = new Generation { Candidates = new Candidate[NumCandidatesPerGeneration] };
        for (var elite = 0; elite < NumElitesKept; ++elite)
        {
            nextGeneration[elite] = _currentGeneration[elite];
            var currentElite = _currentGeneration[elite];
            if (currentElite.NumParents == 0)
                Console.WriteLine(
                    $"Elite {elite}: ID {currentElite.ID}, fitness {currentElite.Fitness}, segments {currentElite.NumSegments}");
            else
                Console.WriteLine(
                    $"Elite {elite}: ID [{currentElite.ID}], fitness {currentElite.Fitness}, parents {currentElite.NumParents}, segments {currentElite.NumSegments}, generation {(currentElite.ID - NumCandidatesPerGeneration) / (NumCandidatesPerGeneration - NumElitesKept) + 1}");
        }

        bestFitness = _currentGeneration[0].Fitness;
        Console.WriteLine($"Best ID: {_currentGeneration[0].ID}\n\tfitness {bestFitness}");

        // Normalize to enable picking parents.
        _currentGeneration.NormalizeFitness();

        for (var child = NumElitesKept; child < NumCandidatesPerGeneration; ++child)
        {
            var progress = (float)(child - NumElitesKept) / (NumCandidatesPerGeneration - NumElitesKept);
            nextGeneration.Candidates[child] =
                GenerateChildVoronoi((int)(NumParentsMin * (1.0 - progress) + (NumParentsMax + 1) * progress));
        }

        _currentGeneration = nextGeneration;
        _currentGeneration.Candidates = _currentGeneration.Candidates.OrderByDescending(c => c.Fitness).ToArray();
        // Console.WriteLine("Generation IDs:");
        // foreach (var candidate in _currentGeneration.Candidates)
        //     Console.Write($"{candidate.ID} ({candidate.NumParents})");
    }

    private Candidate GenerateChildRandomSelection(int numParents)
    {
        var parents = new List<int>
        {
            Capacity = numParents
        };

        while (parents.Count < numParents)
        {
            var nextParent = _currentGeneration.SelectParent();
            if (!parents.Contains(nextParent)) parents.Add(nextParent);
        }

        // Generate an ordered sequence of integers
        var keptCircleIndices = Enumerable.Range(0, numParents * NumCirclesPerCandidate).OrderBy(_ => random.Next())
            .Take(NumCirclesPerCandidate - NumCirclesRandomized);

        var selectedCircles = keptCircleIndices
            .Select(c =>
                _currentGeneration[parents[c / NumCirclesPerCandidate]].Circles[c % NumCirclesPerCandidate]
            ).ToArray();
        var selectedMutatedCircles = selectedCircles.Take(NumCirclesMutated)
            .Select(c =>
                c.Add(new Vector3(
                    (float)random.NextDouble() * MaximalMutation,
                    (float)random.NextDouble() * MaximalMutation,
                    (float)random.NextDouble() * MaximalMutation)));

        var randomCircles = Enumerable.Range(0, NumCirclesRandomized)
            .Select(_ => Circle.Random(kMinRadius, kMaxRadius));

        var allCircles = selectedCircles.Skip(NumCirclesMutated)
            .Concat(selectedMutatedCircles).Concat(randomCircles).ToArray();

        return new Candidate
        {
            Circles = allCircles,
            ID = MaxID++,
            Fitness = 1.0f /
                      CircleEvaluator.EvaluateCircles(_kGreyScaleStep, allCircles.ToList(), image,
                          pixels, out var outPixels, out var _),
            NumParents = numParents
        };
    }

    private Candidate GenerateChildVoronoi(int numParents)
    {
        // Console.WriteLine($"Num parents: {numParents}");
        var parents = new List<int>
        {
            Capacity = numParents
        };

        while (parents.Count < numParents)
        {
            var nextParent = _currentGeneration.SelectParent();
            if (!parents.Contains(nextParent)) parents.Add(nextParent);
        }

        var parentCenters = Enumerable.Range(0, numParents)
            .Select(_ => new Vector2((float)random.NextDouble(), (float)random.NextDouble())).ToList();

        var allCirclesByDistance = Enumerable.Range(0, numParents).SelectMany(p => _currentGeneration[parents[p]]
            .Circles.Select(c => c with { color = p + 1 }).Where(
                c =>
                {
                    var center = new Vector2(c.x, c.y);
                    var parentDist = Vector2.Distance(center, parentCenters[p]);
                    for (var pC = 0; pC < numParents; ++pC)
                        if (pC != p && Vector2.Distance(center, parentCenters[pC]) < parentDist)
                            return false;
                    return true;
                })).ToList();

        var numCirclesToRandomize =
            Math.Max(NumCirclesRandomized, NumCirclesPerCandidate - allCirclesByDistance.Count());


        // Generate an ordered sequence of integers
        var keptCircleIndices = Enumerable.Range(0, allCirclesByDistance.Count()).OrderBy(_ => random.Next())
            .Select(i => allCirclesByDistance[i])
            .Take(NumCirclesPerCandidate - numCirclesToRandomize).ToList();

        var selectedMutatedCircles = keptCircleIndices.Take(NumCirclesMutated)
            .Select(c =>
                c.Add(new Vector3(
                    (float)random.NextDouble() * MaximalMutation,
                    (float)random.NextDouble() * MaximalMutation,
                    (float)random.NextDouble() * MaximalMutation)));

        var randomCircles = Enumerable.Range(0, numCirclesToRandomize)
            .Select(_ => Circle.Random(kMinRadius, kMaxRadius));

        var allCircles = keptCircleIndices.Skip(NumCirclesMutated)
            .Concat(selectedMutatedCircles).Concat(randomCircles).ToArray();

        return new Candidate
        {
            Circles = allCircles,
            ID = MaxID++,
            Fitness = 1.0f /
                      CircleEvaluator.EvaluateCircles(_kGreyScaleStep, allCircles.ToList(), image,
                          pixels, out var outPixels, out var numSegments),
            NumParents = numParents,
            NumSegments = numSegments
        };
    }

    public class Candidate
    {
        public Circle[] Circles;
        public float Fitness;
        public int ID;
        public int NumParents, NumSegments;


        public class FitnessComparer : IComparer<Candidate>
        {
            public int Compare(Candidate x, Candidate y)
            {
                return x.Fitness.CompareTo(y.Fitness);
            }
        }
    }

    private class Generation
    {
        private bool _normalized;
        public Candidate[] Candidates = new Candidate[NumCandidatesPerGeneration];
        private float[] fitnessSumForSampling;


        public Candidate this[int key]
        {
            get => Candidates[key];
            set => Candidates[key] = value;
        }


        public void NormalizeFitness()
        {
            var fitnessSum = Candidates.Sum(c => c.Fitness);
            fitnessSumForSampling = new float[NumCandidatesPerGeneration];
            for (var c = 0; c < Candidates.Length; ++c)
                fitnessSumForSampling[c] =
                    Candidates[c].Fitness / fitnessSum + (c > 0 ? fitnessSumForSampling[c - 1] : 0);
            // Console.WriteLine($"Last weight was {Candidates.Last().Fitness}");
            _normalized = true;
        }


        public int SelectParent()
        {
            if (!_normalized) throw new Exception("Not normalized}");

            // Options:
            // x  Roulette Wheel Selection: direct fitness
            //    Rank Selection: order-based fitness
            //    Steady State Selection: keep a few good ones and throw rest away
            // x  Elitist Selection: keep best ones as-is
            //    Boltzmann Selection: increase pressure to converge
            var rnd = (float)random.NextDouble();
            var idx = Array.BinarySearch(fitnessSumForSampling, rnd);
            return idx < 0 ? -idx - 1 : idx;
        }
    }
}