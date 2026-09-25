using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Sets the iOS build number (CFBundleVersion) to the Unity Cloud Build number (#28, #29, ...),
/// so every upload to App Store Connect is unique and increasing.
/// Set "Pre-export method name" in the Unity Cloud build configuration to: CloudBuildNumber.PreExport
/// </summary>
public class CloudBuildNumber : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

#if UNITY_CLOUD_BUILD
    public static void PreExport(UnityEngine.CloudBuild.BuildManifestObject manifest)
    {
        string buildNumber = manifest.GetValue<string>("buildNumber");
        if (string.IsNullOrEmpty(buildNumber)) return;

        PlayerSettings.iOS.buildNumber = buildNumber;
        Debug.Log($"[CloudBuildNumber] iOS build number set to {buildNumber}");
    }
#endif

    public void OnPreprocessBuild(BuildReport report)
    {
#if UNITY_CLOUD_BUILD
        // App Store Connect silently discards uploads that reuse a build number, so fail loudly instead.
        if (report.summary.platform == BuildTarget.iOS && PlayerSettings.iOS.buildNumber == "0")
        {
            throw new BuildFailedException(
                "iOS build number is still 0. In Unity Cloud, set the build configuration's " +
                "'Pre-export method name' to CloudBuildNumber.PreExport");
        }
#endif
    }
}
