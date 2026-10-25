using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// Lives on a generated environment root and frees every runtime mesh/material/profile it created.
/// </summary>
public class GeneratedEnvironment : MonoBehaviour
{
    public readonly List<Object> owned = new List<Object>();
    public Material skybox;
    public VolumeProfile volume;

    private void OnDestroy()
    {
        foreach (Object o in owned)
        {
            if (o == null) continue;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
        owned.Clear();
    }
}
