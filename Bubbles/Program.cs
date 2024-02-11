using System.Numerics;
using Bubbles;

const int kNumGreyScales = 5;
const int kGreyScaleStep = 255 / kNumGreyScales;

var imageFile = args[0];
var circlesDirectory = args[1];

PruneRatedCircles();
// PruneExample();

void PruneRatedCircles()
{
    var startingSet =
        CirclePruning.GenerateCirclesWithGoodRating(imageFile, kGreyScaleStep, 100000, out var image, out var pixels);
    var prunedCircles = CirclePruning.PruneRatedCircles(startingSet, out var intersectionPoints);

    // var bestestCircles =
    //     startingSet.OrderDescending(new CirclePruning.RatedCircle.FitnessComparer()).Take(100)
    //         .Select(c => c.Circle with { color = 1 });
    // CircleEvaluator.WriteSvg(bestestCircles,
    //     new List<Vector2>(),
    //     circlesDirectory + "bestestCircles.svg");

    // CircleEvaluator.WriteSvg(startingSet.Select(c => c.Circle), new List<Vector2>(),
    //     circlesDirectory + "ratedCircles.svg");
    CircleEvaluator.WriteSvg(prunedCircles, intersectionPoints, circlesDirectory + "ratedCirclesPruned.svg");

    CircleEvaluator.OutputCirclesAndError(prunedCircles, circlesDirectory, image, pixels, kGreyScaleStep);
}

void PruneExample()
{
    var testSet = CirclePruning.GenerateRandom();
    var prunedCircles = CirclePruning.PruneCircles(testSet, out var intersectionPoints);
    CircleEvaluator.WriteSvg(testSet, new List<Vector2>(), circlesDirectory + "testCircles.svg");
    CircleEvaluator.WriteSvg(prunedCircles, intersectionPoints, circlesDirectory + "prunedTestCircles.svg");
}

void GeneticAlg(bool generateInput, int maxNumGenerations = 10)
{
    if (generateInput) CircleEvaluator.GenerateBaseImages(imageFile, circlesDirectory, kGreyScaleStep, 50);

    var geneticAlg = new GeneticAlgorithm();
    geneticAlg.LoadFirstGeneration(imageFile, circlesDirectory, kGreyScaleStep);
    Console.WriteLine("Setup done");
    var bestFitness = 0.0f;
    var numGenerations = 0;

    while (bestFitness < 0.2 && numGenerations < maxNumGenerations)
    {
        geneticAlg.GenerateNextGeneration(out bestFitness);
        numGenerations++;
        Console.WriteLine($"Generation {numGenerations} done");
    }

    geneticAlg.OutputBestCandidate(circlesDirectory);
}