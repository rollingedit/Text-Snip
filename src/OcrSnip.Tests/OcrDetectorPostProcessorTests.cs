using Microsoft.ML.OnnxRuntime.Tensors;
using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class OcrDetectorPostProcessorTests
{
    [Fact]
    public void GetBoxes_FindsSyntheticProbabilityIsland()
    {
        var tensor = new DenseTensor<float>([1, 1, 64, 64]);
        for (var y = 20; y < 36; y++)
        {
            for (var x = 14; x < 46; x++)
            {
                tensor[0, 0, y, x] = 0.95f;
            }
        }

        var boxes = OcrDetectorPostProcessor.GetBoxes(tensor, 640, 320);

        var box = Assert.Single(boxes);
        Assert.True(box.TopLeft.X < 160);
        Assert.True(box.TopLeft.Y < 110);
        Assert.True(box.BottomRight.X > 430);
        Assert.True(box.BottomRight.Y > 175);
    }

    [Fact]
    public void GetBoxes_RejectsLowScoreIsland()
    {
        var tensor = new DenseTensor<float>([1, 1, 32, 32]);
        for (var y = 8; y < 20; y++)
        {
            for (var x = 8; x < 20; x++)
            {
                tensor[0, 0, y, x] = 0.25f;
            }
        }

        var boxes = OcrDetectorPostProcessor.GetBoxes(tensor, 320, 320, mapThreshold: 0.20f, boxThreshold: 0.45f);

        Assert.Empty(boxes);
    }

    [Fact]
    public void GetBoxes_ScoresContourMaskNotEntireBoundingNoise()
    {
        using var probability = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_32FC1, OpenCvSharp.Scalar.All(0.1));
        using var mask = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(0));
        var points = new[]
        {
            new OpenCvSharp.Point(20, 10),
            new OpenCvSharp.Point(44, 32),
            new OpenCvSharp.Point(20, 54),
            new OpenCvSharp.Point(8, 32)
        };
        OpenCvSharp.Cv2.FillPoly(probability, [points], OpenCvSharp.Scalar.All(0.95));
        OpenCvSharp.Cv2.FillPoly(mask, [points], OpenCvSharp.Scalar.All(255));

        var boxes = OcrDetectorPostProcessor.GetBoxes(probability, mask, 640, 640, boxThreshold: 0.80f);

        Assert.Single(boxes);
    }
}
