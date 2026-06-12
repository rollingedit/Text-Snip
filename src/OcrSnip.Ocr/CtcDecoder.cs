using System.Text;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace OcrSnip.Ocr;

public static class CtcDecoder
{
    public static (string Text, float Confidence) Decode(Tensor<float> output, IReadOnlyList<string> characters)
    {
        var timeSteps = output.Dimensions[1];
        var classCount = output.Dimensions[2];
        var builder = new StringBuilder();
        var confidences = new List<float>();
        var previous = -1;

        for (var t = 0; t < timeSteps; t++)
        {
            var bestIndex = 0;
            var best = float.NegativeInfinity;
            for (var c = 0; c < classCount; c++)
            {
                var value = output[0, t, c];
                if (value > best)
                {
                    best = value;
                    bestIndex = c;
                }
            }

            if (bestIndex != 0 && bestIndex != previous)
            {
                var charIndex = bestIndex - 1;
                if ((uint)charIndex < (uint)characters.Count)
                {
                    builder.Append(characters[charIndex]);
                    confidences.Add(best);
                }
            }

            previous = bestIndex;
        }

        var confidence = confidences.Count == 0 ? 0 : confidences.Average();
        return (builder.ToString(), confidence);
    }
}
