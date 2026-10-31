namespace YiZhiMao.Dc.Core
{
    internal static class Compatibility
    {
        public static void NotNull(object? value, string parameterName)
        {
            if (value is null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        public static void NotNullOrWhiteSpace(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
            }
        }

        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static void MoveFileReplacingExisting(string sourcePath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(sourcePath, destinationPath, null, ignoreMetadataErrors: true);
                return;
            }

            File.Move(sourcePath, destinationPath);
        }

        public static string GetRelativePath(string relativeTo, string path)
        {
            var basePath = Path.GetFullPath(relativeTo);
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                basePath += Path.DirectorySeparatorChar;
            }

            var baseUri = new Uri(basePath, UriKind.Absolute);
            var pathUri = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }

    internal sealed class RequiredMemberAttribute : Attribute
    {
    }

    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }

        public bool IsOptional { get; init; }
    }
}
#endif
