using Microsoft.ML.OnnxRuntime;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: OcrSnip.Tools.OnnxInspect <model.onnx>");
    return 2;
}

var modelPath = Path.GetFullPath(args[0]);
if (!File.Exists(modelPath))
{
    Console.Error.WriteLine($"Model not found: {modelPath}");
    return 2;
}

using var session = new InferenceSession(modelPath);
Console.WriteLine($"Model: {modelPath}");
Console.WriteLine("Inputs:");
foreach (var item in session.InputMetadata)
{
    Console.WriteLine($"  {item.Key}");
    Console.WriteLine($"    Type: {item.Value.ElementDataType}");
    Console.WriteLine($"    Shape: {string.Join(" x ", item.Value.Dimensions.Select(d => d.ToString()))}");
}

Console.WriteLine("Outputs:");
foreach (var item in session.OutputMetadata)
{
    Console.WriteLine($"  {item.Key}");
    Console.WriteLine($"    Type: {item.Value.ElementDataType}");
    Console.WriteLine($"    Shape: {string.Join(" x ", item.Value.Dimensions.Select(d => d.ToString()))}");
}

return 0;
