using UnityEngine;
using TMPro;

/// <summary>
/// Attach this script to a UI element with a TextMeshProUGUI or TextMeshPro component.
/// It will automatically set the text to the current app version from Player Settings.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class VersionDisplay : MonoBehaviour
{
    [Tooltip("Optional prefix before the version number, e.g., 'v' or 'Version '")]
    public string prefix = "v";

    private void Start()
    {
        TMP_Text versionText = GetComponent<TMP_Text>();
        if (versionText != null)
        {
            // Application.version pulls directly from the Version field in Player Settings 
            // and works in both the Editor and actual Builds!
            versionText.text = prefix + Application.version;
        }
    }
}
