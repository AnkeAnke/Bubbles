const int kNumGreyScales = 5;
const int kGreyScaleStep = 255 / kNumGreyScales;

var imageFile = args[0];
var circlesFile = args[1];

CircleEvaluator.EvaluateCirclesForImage(imageFile, circlesFile, kGreyScaleStep);

