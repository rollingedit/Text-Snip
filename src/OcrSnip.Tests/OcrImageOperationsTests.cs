using OcrSnip.Ocr;
using OpenCvSharp;

namespace OcrSnip.Tests;

public sealed class OcrImageOperationsTests
{
    [Fact]
    public void CreateDetectorTensor_UsesExpectedShape()
    {
        using var image = new Mat(32, 64, MatType.CV_8UC3, new Scalar(0, 0, 255));

        var tensor = OcrImageOperations.CreateDetectorTensor(image);

        Assert.Equal([1, 3, 32, 64], tensor.Dimensions.ToArray());
    }

    [Fact]
    public void CreateRecognizerTensor_UsesExpectedPaddedShape()
    {
        using var image = new Mat(24, 80, MatType.CV_8UC3, new Scalar(255, 255, 255));

        var tensor = OcrImageOperations.CreateRecognizerTensor(image);

        Assert.Equal([1, 3, 48, 320], tensor.Dimensions.ToArray());
    }

    [Fact]
    public void PerspectiveCrop_RectifiesQuadrilateral()
    {
        using var image = new Mat(80, 160, MatType.CV_8UC3, Scalar.All(255));
        var box = new OcrQuadrilateral(
            new OcrPoint(20, 20),
            new OcrPoint(120, 25),
            new OcrPoint(116, 55),
            new OcrPoint(18, 50));

        using var crop = OcrImageOperations.PerspectiveCrop(image, box);

        Assert.True(crop.Width >= 95);
        Assert.True(crop.Height >= 25);
    }
}
