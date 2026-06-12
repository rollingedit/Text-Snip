using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class ModelPathsTests
{
    [Fact]
    public void FromAppBaseDirectory_UsesBundledModelLayout()
    {
        var paths = ModelPaths.FromAppBaseDirectory(@"C:\Program Files\OcrSnip");

        Assert.EndsWith(@"models\ppocrv6-small-det\inference.onnx", paths.DetectorOnnx);
        Assert.EndsWith(@"models\ppocrv6-small-det\inference.yml", paths.DetectorConfig);
        Assert.EndsWith(@"models\ppocrv6-small-rec\inference.onnx", paths.RecognizerOnnx);
        Assert.EndsWith(@"models\ppocrv6-small-rec\inference.yml", paths.RecognizerConfig);
        Assert.Equal(4, paths.AllFiles.Count);
    }
}
