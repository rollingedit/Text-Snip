using Microsoft.ML.OnnxRuntime.Tensors;
using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class CtcDecoderTests
{
    [Fact]
    public void Decode_RemovesBlanksAndRepeatedClasses()
    {
        var tensor = new DenseTensor<float>([1, 5, 4]);
        SetBest(tensor, 0, 1, 0.9f);
        SetBest(tensor, 1, 1, 0.8f);
        SetBest(tensor, 2, 0, 0.7f);
        SetBest(tensor, 3, 2, 0.6f);
        SetBest(tensor, 4, 3, 0.5f);

        var decoded = CtcDecoder.Decode(tensor, ["A", "B", "C"]);

        Assert.Equal("ABC", decoded.Text);
        Assert.Equal((0.9f + 0.6f + 0.5f) / 3f, decoded.Confidence, precision: 5);
    }

    private static void SetBest(DenseTensor<float> tensor, int timeStep, int classIndex, float value)
    {
        for (var i = 0; i < tensor.Dimensions[2]; i++)
        {
            tensor[0, timeStep, i] = -1;
        }

        tensor[0, timeStep, classIndex] = value;
    }
}
