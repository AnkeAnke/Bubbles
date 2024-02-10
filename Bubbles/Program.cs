﻿const int kNumGreyScales = 5;
const int kGreyScaleStep = 255 / kNumGreyScales;

var imageFile = args[0];
var circlesFile = args[1];

// CircleEvaluator.EvaluateCirclesForImage(imageFile, circlesFile, kGreyScaleStep);

var geneticAlg = new GeneticAlgorithm();
geneticAlg.LoadFirstGeneration(imageFile, circlesFile, kGreyScaleStep);
Console.WriteLine("Setup done");
var bestFitness = 0.0f;
var numGenerations = 0;

while (bestFitness < 0.2 && numGenerations < 10)
{
    geneticAlg.GenerateNextGeneration(out bestFitness);
    numGenerations++;
    Console.WriteLine($"Generation {numGenerations} done");
}

// var bestCandidate = geneticAlg.GetBestCandidate();
// CircleEvaluator.WriteCSV(bestCandidate.Circles, circlesFile + "bestCircles.csv");
geneticAlg.OutputBestCandidate(circlesFile);