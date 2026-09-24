#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// Gives every iOS build a unique, always-increasing build number (CFBundleVersion)
/// based on the UTC build time, e.g. "20260924.1030".
/// App Store Connect silently discards uploads that reuse a build number, so
/// without this every Unity Cloud build shipped as build "0" and only the first one appeared.
/// </summary>
public class IOSBuildNumberPostProcessor
{
    [PostProcessBuild(102)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        // Components are compared as integers, so "20260924.1030" > "20260924.248" > "0".
        DateTime now = DateTime.UtcNow;
        string buildNumber = $"{now:yyyyMMdd}.{now.Hour * 100 + now.Minute}";

        string plistPath = Path.Combine(buildPath, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("CFBundleVersion", buildNumber);
        plist.WriteToFile(plistPath);

        Debug.Log($"[IOSBuildNumberPostProcessor] CFBundleVersion set to {buildNumber}");
    }
}
#endif
