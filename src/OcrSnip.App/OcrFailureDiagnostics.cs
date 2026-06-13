using System.Text;

namespace OcrSnip.App;

public static class OcrFailureDiagnostics
{
    public static string Format(Exception exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine("OCR failed.");
        builder.AppendLine();
        builder.AppendLine("This usually means a required model or native runtime dependency is missing on this machine.");
        builder.AppendLine();
        AppendException(builder, exception, "Error");
        return builder.ToString();
    }

    private static void AppendException(StringBuilder builder, Exception exception, string label)
    {
        builder.AppendLine($"{label}: {exception.GetType().Name}");
        if (!string.IsNullOrWhiteSpace(exception.Message))
        {
            builder.AppendLine(exception.Message);
        }

        if (exception.InnerException is not null)
        {
            builder.AppendLine();
            AppendException(builder, exception.InnerException, "Inner error");
        }
    }
}
