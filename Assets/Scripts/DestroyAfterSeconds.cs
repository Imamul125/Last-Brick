using UnityEngine;

public class DestroyAfterSeconds : MonoBehaviour
{
    public float delay = 2.0f;
    void Start()
    {
        Destroy(gameObject, delay);
    }
}
