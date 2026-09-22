using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;

public class PodfilePostProcessor
{
    // Run after EDM4U generates the Podfile (usually around 40-50)
    [PostProcessBuild(60)] 
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        string podfilePath = Path.Combine(path, "Podfile");
        if (File.Exists(podfilePath))
        {
            string podfileContent = File.ReadAllText(podfilePath);
            
            // Append the post_install hook to forcefully disable signing on all Pod targets
            string postInstallHook = @"
post_install do |installer|
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['CODE_SIGNING_ALLOWED'] = 'NO'
    end
  end
end";
            
            if (!podfileContent.Contains("post_install"))
            {
                File.AppendAllText(podfilePath, postInstallHook);
                Debug.Log("Injected post_install hook into Podfile to disable CocoaPods signing.");
            }
        }
    }
}
