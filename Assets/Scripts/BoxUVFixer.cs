using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class BoxUVFixer : MonoBehaviour
{
    private Vector3 lastScale;

    void Start()
    {
        FixUVs();
        
#if !UNITY_EDITOR
        // On mobile/build, we only need to fix UVs once on startup.
        // After that, we can destroy this script so it costs 0 CPU!
        Destroy(this);
#endif
    }

#if UNITY_EDITOR
    void Update()
    {
        // Only run this scale check loop in the Editor while you are designing!
        if (transform.localScale != lastScale)
        {
            FixUVs();
        }
    }
#endif

    [ContextMenu("Fix UVs")]
    public void FixUVs()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Mesh mesh = mf.sharedMesh;
        // Don't permanently modify the default Unity Cube asset!
        if (mesh.name == "Cube")
        {
            mesh = Instantiate(mf.sharedMesh);
            mesh.name = "Cube_FixedUV";
            mf.sharedMesh = mesh;
        }

        Vector2[] uvs = mesh.uv;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector3 s = transform.localScale;

        for (int i = 0; i < uvs.Length; i++)
        {
            Vector3 n = normals[i];
            Vector3 vPos = vertices[i];
            
            float u = 0f;
            float v = 0f;
            
            // Generate UVs based on absolute world scale of the face
            if (Mathf.Abs(n.x) > 0.5f) 
            { 
                u = vPos.z * s.z; 
                v = vPos.y * s.y; 
            }
            else if (Mathf.Abs(n.y) > 0.5f) 
            { 
                u = vPos.x * s.x; 
                v = vPos.z * s.z; 
            }
            else if (Mathf.Abs(n.z) > 0.5f) 
            { 
                u = vPos.x * s.x; 
                v = vPos.y * s.y; 
            }
            
            uvs[i] = new Vector2(u, v);
        }
        
        mesh.uv = uvs;
        lastScale = s;
    }
}
