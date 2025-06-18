using Microsoft.Extensions.FileProviders;

namespace Ztatic;

internal static class StaticWebAssetsFinder
{
    public static List<ContentToCopy> FindDefaultStaticAssets(string outputFolderPath, IFileProvider fileProvider)
    {
        List<ContentToCopy> assets = [];
        
        FindDefaultStaticAssets(fileProvider, outputFolderPath, "", assets);
        assets.AddRange(ProcessFiles(assets));
     
        return assets.DistinctBy(x => (x.SourcePath, x.TargetPath)).ToList();
    }
    
    private static void FindDefaultStaticAssets(IFileProvider fileProvider, string outputFolderPath, string subPath, List<ContentToCopy> assets)
    {
        var contents = fileProvider.GetDirectoryContents(subPath);

        foreach (var item in contents)
        {
            var fullPath = $"{subPath}{item.Name}";

            if (item.IsDirectory)
                FindDefaultStaticAssets(fileProvider, outputFolderPath, $"{fullPath}/", assets);
            else if (item.PhysicalPath is not null)
                assets.Add(new ContentToCopy(item.PhysicalPath, Path.Combine(outputFolderPath, fullPath)));
        }
    }

    private static readonly string[] CompressionExtensions = [".gz", ".br"];

    private static List<ContentToCopy> ProcessFiles(List<ContentToCopy> inputFiles)
    {
        // Group by base TargetPath (without compression extension)
        var grouped = inputFiles.GroupBy(f => RemoveCompressionExtension(f.TargetPath));
        var result = new List<ContentToCopy>();

        foreach (var group in grouped)
        {
            var uncompressed = group.FirstOrDefault(f => !IsCompressed(f.TargetPath));
            var compressedFiles = group.Where(f => IsCompressed(f.TargetPath)).ToList();

            // Skip if missing pairs.
            if (uncompressed is null || compressedFiles.Count is 0)
                continue;

            foreach (var compressed in compressedFiles)
            {
                string fingerprint = ExtractFingerprint(compressed.SourcePath);

                // 1. Original uncompressed.
                result.Add(new ContentToCopy(uncompressed.SourcePath, uncompressed.TargetPath));

                // 2. Original compressed.
                result.Add(new ContentToCopy(compressed.SourcePath, compressed.TargetPath));

                // 3. Fingerprinted uncompressed.
                string uncompressedFingerprintTarget = AddFingerprint(uncompressed.TargetPath, fingerprint);
                result.Add(new ContentToCopy(uncompressed.SourcePath, uncompressedFingerprintTarget));

                // 4. Fingerprinted compressed.
                string compressedFingerprintTarget = AddFingerprint(compressed.TargetPath, fingerprint);
                result.Add(new ContentToCopy(compressed.SourcePath, compressedFingerprintTarget));
            }
        }

        return result;
    }

    private static string ExtractFingerprint(string compressedSourcePath)
    {
        var filename = Path.GetFileNameWithoutExtension(compressedSourcePath); // Remove .gz/.br
        var parts = filename.Split('-');
        return parts switch
        {
            [_, "{0}", _, ..] => parts[2],
            [_, _, ..] => parts[1],
            _ => ""
        };
    }

    private static string AddFingerprint(string targetPath, string fingerprint)
    {
        // Handle compression extensions (.gz, .br)
        string compressionExtension = "";
        foreach (var compExt in CompressionExtensions)
        {
            if (targetPath.EndsWith(compExt, StringComparison.OrdinalIgnoreCase))
            {
                compressionExtension = compExt;
                targetPath = targetPath[..^compExt.Length]; // Remove compression extension temporarily
                break;
            }
        }

        // Check for special case: .styles.css
        const string stylesSuffix = ".styles.css";
        if (targetPath.EndsWith(stylesSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var prefix = targetPath[..^stylesSuffix.Length];
            return $"{prefix}.{fingerprint}{stylesSuffix}{compressionExtension}";
        }

        // Normal case.
        var mainExtension = Path.GetExtension(targetPath);
        var fileNameWithoutExtension = targetPath[..^mainExtension.Length];

        return $"{fileNameWithoutExtension}.{fingerprint}{mainExtension}{compressionExtension}";
    }

    private static string RemoveCompressionExtension(string targetPath)
    {
        foreach (var extension in CompressionExtensions)
        {
            if (targetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return targetPath[..^extension.Length];
        }
        
        return targetPath;
    }

    private static bool IsCompressed(string targetPath)
    {
        return CompressionExtensions.Any(x => targetPath.EndsWith(x, StringComparison.OrdinalIgnoreCase));
    }
}