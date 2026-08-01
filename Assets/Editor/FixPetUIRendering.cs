using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class FixPetUIRendering
{
    [MenuItem("Tools/Fix Pet UI")]
    public static void Fix()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        GameObject petAreaObj = GameObject.Find("PetSelectionArea");
        
        if (canvas == null || petAreaObj == null) {
            Debug.LogError("Could not find Canvas or PetSelectionArea");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Fix Pet UI");

        // We will create a dedicated "Pet Studio" far below the map
        GameObject petStudio = GameObject.Find("PetStudio");
        if (petStudio == null) {
            petStudio = new GameObject("PetStudio");
            petStudio.transform.position = new Vector3(0, -5000, 0);
        }

        Transform slotCat = petAreaObj.transform.Find("PetSlot_Cat");
        Transform slotDog = petAreaObj.transform.Find("PetSlot_Dog");
        Transform slotDog2 = petAreaObj.transform.Find("PetSlot_Dog_Locked2");

        if (slotCat != null) SetupPetSlot(slotCat, petStudio, "Cat");
        if (slotDog != null) SetupPetSlot(slotDog, petStudio, "Dog");
        // For the duplicated locked dog, we can reuse the same render texture as the first dog!
        if (slotDog2 != null) {
            RawImage ri = slotDog2.GetComponent<RawImage>();
            if (ri == null) ri = slotDog2.gameObject.AddComponent<RawImage>();
            if (slotDog != null) {
                RawImage dogRi = slotDog.GetComponent<RawImage>();
                if (dogRi != null) ri.texture = dogRi.texture;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Pet UI Fixed using RenderTextures!");
    }

    private static void SetupPetSlot(Transform slot, GameObject studio, string petName)
    {
        Transform petTrans = slot.Find(petName);
        GameObject petObj = null;

        if (petTrans != null) {
            petObj = petTrans.gameObject;
        } else {
            // Find in studio if already moved
            Transform studioPet = studio.transform.Find(petName);
            if (studioPet != null) petObj = studioPet.gameObject;
        }

        if (petObj == null) return;

        // Move pet to studio
        petObj.transform.SetParent(studio.transform);
        
        // Space them out in the studio
        float offset = petName == "Cat" ? 0 : 10;
        petObj.transform.localPosition = new Vector3(offset, 0, 0);
        petObj.transform.localRotation = Quaternion.Euler(0, 150, 0); // Nice 3/4 angle
        petObj.transform.localScale = Vector3.one; // Reset scale since it's world space now

        // Create Camera
        string camName = "PetCam_" + petName;
        Transform camTrans = studio.transform.Find(camName);
        Camera cam = null;
        if (camTrans == null) {
            GameObject camObj = new GameObject(camName);
            camObj.transform.SetParent(studio.transform);
            cam = camObj.AddComponent<Camera>();
        } else {
            cam = camTrans.GetComponent<Camera>();
        }

        // Position camera to look at pet
        cam.transform.localPosition = new Vector3(offset, 1f, 3f);
        cam.transform.localRotation = Quaternion.Euler(10, 180, 0); // Looking back at the pet
        
        // Make camera render beautifully with transparent background
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); // Transparent
        
        // Ensure the camera only renders things near it (the pet) to avoid rendering other things in the scene
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        
        // Create RenderTexture
        string rtPath = "Assets/Textures/RT_" + petName + ".renderTexture";
        RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (rt == null) {
            if (!AssetDatabase.IsValidFolder("Assets/Textures")) AssetDatabase.CreateFolder("Assets", "Textures");
            rt = new RenderTexture(512, 512, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm);
            AssetDatabase.CreateAsset(rt, rtPath);
        }
        
        cam.targetTexture = rt;

        // Add RawImage to UI Slot
        RawImage rawImage = slot.GetComponent<RawImage>();
        if (rawImage == null) {
            rawImage = slot.gameObject.AddComponent<RawImage>();
        }
        rawImage.texture = rt;

        // Remove old Image component if it exists so it doesn't conflict
        Image oldImg = slot.GetComponent<Image>();
        if (oldImg != null) Object.DestroyImmediate(oldImg);
    }
}
