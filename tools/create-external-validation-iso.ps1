param(
    [string]$OutputPath = "artifacts/publish/OcrSnip-ExternalValidationKit.iso",
    [string]$StagingRoot = "artifacts/external-validation-kit",
    [string]$VolumeName = "OCRSNIP_VALID"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$staging = Join-Path $repoRoot $StagingRoot
$output = Join-Path $repoRoot $OutputPath

if (!("OcrSnipIsoStreamWriter" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class OcrSnipIsoStreamWriter
{
    public static void WriteToFile(object streamObject, string path)
    {
        var input = (IStream)streamObject;
        using (var output = File.Open(path, FileMode.CreateNew, FileAccess.Write))
        {
            var buffer = new byte[65536];
            var bytesReadPointer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                while (true)
                {
                    Marshal.WriteInt32(bytesReadPointer, 0);
                    input.Read(buffer, buffer.Length, bytesReadPointer);
                    var bytesRead = Marshal.ReadInt32(bytesReadPointer);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    output.Write(buffer, 0, bytesRead);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(bytesReadPointer);
            }
        }
    }
}
"@
}

if (!(Test-Path (Join-Path $staging "tools/run-external-validation-kit.ps1")) -or
    !(Test-Path (Join-Path $staging "OcrSnip/OcrSnip.App.exe"))) {
    & (Join-Path $PSScriptRoot "create-external-validation-kit.ps1") -StagingRoot $StagingRoot
}

if ($VolumeName.Length -gt 16) {
    throw "ISO volume name must be 16 characters or fewer for broad Windows compatibility."
}

New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Force
}

$image = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
$image.FileSystemsToCreate = 7
$image.VolumeName = $VolumeName
$image.Root.AddTree($staging, $false)
$result = $image.CreateResultImage()
[OcrSnipIsoStreamWriter]::WriteToFile($result.ImageStream, $output)

Write-Host "External validation ISO written to $output"
