using System.IO;

namespace OcrSnip.Ocr;

public sealed record ModelPaths(
    string DetectorOnnx,
    string DetectorConfig,
    string RecognizerOnnx,
    string RecognizerConfig)
{
    public static ModelPaths FromAppBaseDirectory(string baseDirectory)
    {
        var models = Path.Combine(baseDirectory, "models");
        return new ModelPaths(
            Path.Combine(models, "ppocrv6-small-det", "inference.onnx"),
            Path.Combine(models, "ppocrv6-small-det", "inference.yml"),
            Path.Combine(models, "ppocrv6-small-rec", "inference.onnx"),
            Path.Combine(models, "ppocrv6-small-rec", "inference.yml"));
    }

    public IReadOnlyList<string> AllFiles => [DetectorOnnx, DetectorConfig, RecognizerOnnx, RecognizerConfig];
}
