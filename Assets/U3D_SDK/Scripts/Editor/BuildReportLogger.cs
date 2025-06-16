using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;
using System.Text;

public class BuildReportLogger
{
    [MenuItem("Build/WebGL with Report")]
    public static void BuildWebGLWithReport()
    {
        BuildWebGL();
    }

    public static void BuildWebGL()
    {
        Debug.Log("=== Starting Unity WebGL Build with Detailed Report ===");

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "build/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.DetailedBuildReport // This enables detailed asset reporting
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        LogBuildReport(report);
    }

    public static void LogBuildReport(BuildReport report)
    {
        Debug.Log("=== BUILD REPORT START ===");

        // Build summary
        Debug.Log($"Build Result: {report.summary.result}");
        Debug.Log($"Build Size: {FormatBytes(report.summary.totalSize)}");
        Debug.Log($"Build Time: {report.summary.totalTime.TotalSeconds:F2} seconds");
        Debug.Log($"Platform: {report.summary.platform}");
        Debug.Log($"Output Path: {report.summary.outputPath}");

        // File size breakdown
        Debug.Log("\n=== LARGEST FILES ===");
        var files = report.GetFiles()
            .OrderByDescending(f => f.size)
            .Take(20); // Top 20 largest files

        foreach (var file in files)
        {
            Debug.Log($"{FormatBytes(file.size)} - {file.path}");
        }

        // Asset breakdown by type
        Debug.Log("\n=== ASSETS BY TYPE ===");
        var assetsByType = report.GetFiles()
            .GroupBy(f => System.IO.Path.GetExtension(f.path).ToLower())
            .OrderByDescending(g => g.Sum(f => (long)f.size))
            .Take(15);

        foreach (var group in assetsByType)
        {
            var totalSize = (ulong)group.Sum(f => (long)f.size);
            var count = group.Count();
            var extension = string.IsNullOrEmpty(group.Key) ? "[no extension]" : group.Key;
            Debug.Log($"{FormatBytes(totalSize)} ({count} files) - {extension}");
        }

        // Build steps timing
        Debug.Log("\n=== BUILD STEPS TIMING ===");
        foreach (var step in report.steps)
        {
            Debug.Log($"{step.duration.TotalSeconds:F2}s - {step.name}");

            // Log any errors or warnings in this step
            foreach (var message in step.messages)
            {
                if (message.type == LogType.Error || message.type == LogType.Warning)
                {
                    Debug.Log($"  {message.type}: {message.content}");
                }
            }
        }

        // Stripping info (if available)
        if (report.strippingInfo != null)
        {
            Debug.Log("\n=== CODE STRIPPING INFO ===");
            foreach (var module in report.strippingInfo.includedModules.Take(10))
            {
                Debug.Log($"Included Module: {module}");
            }
        }

        Debug.Log("=== BUILD REPORT END ===");
    }

    private static string FormatBytes(ulong bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}