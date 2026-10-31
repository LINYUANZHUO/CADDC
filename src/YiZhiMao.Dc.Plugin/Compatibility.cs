using System;
using System.IO;

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

namespace YiZhiMao.Dc.Plugin
{
    internal static class Compatibility
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static double Clamp(double value, double minimum, double maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;

        public static bool Contains(string value, string text, StringComparison comparison) =>
            value.IndexOf(text, comparison) >= 0;

        public static void MoveFileReplacingExisting(string sourcePath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(sourcePath, destinationPath, null, ignoreMetadataErrors: true);
                return;
            }

            File.Move(sourcePath, destinationPath);
        }
    }
}
