using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class PetMenuSetupScript
{
    [MenuItem("Tools/Setup Pet Menu")]
    public static void SetupPetMenu()
    {
        // 1. Find MainMenu_Cat, Cat, and Dog
        GameObject mainMenuCat = GameObject.Find("MainMenu_Cat");
        if (mainMenuCat == null) {
            // Might be inactive, let's find it by iterating root objects or looking at Canvas
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null) {
                Transform[] trs = canvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in trs) {
                    if (t.name == "MainMenu_Cat") {
                        mainMenuCat = t.gameObject;
                        break;
                    }
                }
            }
        }

        GameObject catObj = null;
        GameObject dogObj = null;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.hideFlags != HideFlags.None) continue;
            if (go.scene.name == null) continue; // Skip prefabs in project

            if (go.name == "Cat" && go.transform.parent == null) catObj = go;
            if (go.name == "Dog" && go.transform.parent == null) dogObj = go;
        }

        if (mainMenuCat == null || catObj == null || dogObj == null)
        {
            Debug.LogError("Could not find MainMenu_Cat, Cat, or Dog in the scene.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(mainMenuCat, "Setup Pet Menu");
        Undo.RegisterFullObjectHierarchyUndo(catObj, "Setup Pet Menu");
        Undo.RegisterFullObjectHierarchyUndo(dogObj, "Setup Pet Menu");

        // 2. Create PetSelectionArea inside MainMenu_Cat
        GameObject petAreaObj = new GameObject("PetSelectionArea");
        petAreaObj.transform.SetParent(mainMenuCat.transform, false);
        RectTransform petAreaRT = petAreaObj.AddComponent<RectTransform>();
        
        // Anchor it to the center-ish of the menu
        petAreaRT.anchorMin = new Vector2(0, 0.3f);
        petAreaRT.anchorMax = new Vector2(1, 0.7f);
        petAreaRT.offsetMin = Vector2.zero;
        petAreaRT.offsetMax = Vector2.zero;

        HorizontalLayoutGroup hlg = petAreaObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 100f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // 3. Create Slot 1 (Cat)
        GameObject slotCat = new GameObject("PetSlot_Cat");
        slotCat.transform.SetParent(petAreaObj.transform, false);
        RectTransform slotCatRT = slotCat.AddComponent<RectTransform>();
        slotCatRT.sizeDelta = new Vector2(300, 300);
        
        // Parent Cat to Slot 1
        catObj.transform.SetParent(slotCat.transform, false);
        catObj.transform.localPosition = new Vector3(0, -100, -100); // adjust slightly so it looks grounded
        catObj.transform.localRotation = Quaternion.Euler(0, 180, 0); // Face forward
        catObj.transform.localScale = new Vector3(150, 150, 150); // Scale up for UI
        catObj.SetActive(true);

        // 4. Create Slot 2 (Dog)
        GameObject slotDog = new GameObject("PetSlot_Dog");
        slotDog.transform.SetParent(petAreaObj.transform, false);
        RectTransform slotDogRT = slotDog.AddComponent<RectTransform>();
        slotDogRT.sizeDelta = new Vector2(300, 300);

        // Parent Dog to Slot 2
        dogObj.transform.SetParent(slotDog.transform, false);
        dogObj.transform.localPosition = new Vector3(0, -100, -100);
        dogObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        dogObj.transform.localScale = new Vector3(150, 150, 150);
        dogObj.SetActive(true);

        // 5. Add Lock Overlay to Dog
        GameObject lockOverlay = new GameObject("LockOverlay");
        lockOverlay.transform.SetParent(slotDog.transform, false);
        RectTransform lockRT = lockOverlay.AddComponent<RectTransform>();
        lockRT.anchorMin = Vector2.zero;
        lockRT.anchorMax = Vector2.one;
        lockRT.offsetMin = Vector2.zero;
        lockRT.offsetMax = Vector2.zero;

        Image lockImg = lockOverlay.AddComponent<Image>();
        lockImg.color = new Color(0, 0, 0, 0.7f); // Semi-transparent black

        GameObject lockTextObj = new GameObject("LockText");
        lockTextObj.transform.SetParent(lockOverlay.transform, false);
        RectTransform lockTextRT = lockTextObj.AddComponent<RectTransform>();
        lockTextRT.anchoredPosition = Vector2.zero;
        UnityEngine.UI.Text text = lockTextObj.AddComponent<UnityEngine.UI.Text>();
        text.text = "LOCKED";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 48;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        // 6. Duplicate Dog slot for a 3rd pet
        GameObject slotDog2 = Object.Instantiate(slotDog, petAreaObj.transform);
        slotDog2.name = "PetSlot_Dog_Locked2";
        slotDog2.transform.SetAsLastSibling();

        // 7. Ensure MainMenu_Cat is visible so user can see it
        mainMenuCat.SetActive(true);

        // Notify Unity
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Pet Menu Setup Complete!");
    }
}
