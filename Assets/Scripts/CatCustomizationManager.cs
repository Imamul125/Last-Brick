using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class CatOutfit
{
    public string outfitID;
    public string displayName;
    public int price;
    public GameObject prefab;
    public bool isUnlocked;
}

public class CatCustomizationManager : MonoBehaviour
{
    public static CatCustomizationManager Instance { get; private set; }

    [Header("Outfits")]
    public List<CatOutfit> outfits = new List<CatOutfit>();
    private string activeOutfitID = "";

    [Header("UI Reference")]
    private GameObject wardrobePanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);

        LoadOutfits();
    }

    private void Start()
    {
        // Listen to level start to apply outfits
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.levels.ForEach(l => l.onLevelStart.AddListener(ApplyOutfitToAllCats));
        }
    }

    private void LoadOutfits()
    {
        activeOutfitID = PlayerPrefs.GetString("ActiveCatOutfit", "");

        foreach (var outfit in outfits)
        {
            if (outfit.price == 0 || PlayerPrefs.GetInt("OutfitUnlocked_" + outfit.outfitID, 0) == 1)
            {
                outfit.isUnlocked = true;
            }
            else
            {
                outfit.isUnlocked = false;
            }
        }
    }

    public void BuyOrEquipOutfit(string id)
    {
        CatOutfit outfit = outfits.Find(o => o.outfitID == id);
        if (outfit == null) return;

        if (outfit.isUnlocked)
        {
            activeOutfitID = outfit.outfitID;
            PlayerPrefs.SetString("ActiveCatOutfit", activeOutfitID);
            PlayerPrefs.Save();
            Debug.Log("Equipped outfit: " + outfit.displayName);
        }
        else
        {
            if (UIManager.Instance.currentCoins >= outfit.price)
            {
                UIManager.Instance.AddCoin(-outfit.price);
                outfit.isUnlocked = true;
                PlayerPrefs.SetInt("OutfitUnlocked_" + outfit.outfitID, 1);
                
                // Auto equip on buy
                activeOutfitID = outfit.outfitID;
                PlayerPrefs.SetString("ActiveCatOutfit", activeOutfitID);
                PlayerPrefs.Save();
                Debug.Log("Bought and Equipped outfit: " + outfit.displayName);
            }
            else
            {
                Debug.Log("Not enough coins to buy outfit!");
                if (HapticManager.Instance != null) HapticManager.Instance.VibrateError();
            }
        }
    }

    public void ApplyOutfitToAllCats()
    {
        if (string.IsNullOrEmpty(activeOutfitID)) return;

        CatOutfit currentOutfit = outfits.Find(o => o.outfitID == activeOutfitID);
        if (currentOutfit == null || currentOutfit.prefab == null) return;

        CatController[] catsInScene = FindObjectsByType<CatController>(FindObjectsSortMode.None);
        foreach (var cat in catsInScene)
        {
            // Find head bone or just attach to the root for now
            Transform headTransform = cat.transform.Find("Armature/Hips/Spine/Neck/Head"); 
            if (headTransform == null) headTransform = cat.transform; // fallback to root

            GameObject hat = Instantiate(currentOutfit.prefab, headTransform);
            hat.transform.localPosition = Vector3.zero;
            // Depending on the model, we might need a specific offset/rotation, 
            // but usually the prefab should have its own offset built-in
        }
    }

    // Temporary UI generation for testing without a full UI pass
    public void OpenWardrobeUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        if (wardrobePanel != null) Destroy(wardrobePanel);

        wardrobePanel = new GameObject("WardrobePanel");
        wardrobePanel.transform.SetParent(canvas.transform, false);
        
        RectTransform rt = wardrobePanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.1f);
        rt.anchorMax = new Vector2(0.9f, 0.9f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = wardrobePanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(wardrobePanel.transform, false);
        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        titleTxt.text = "CAT WARDROBE";
        titleTxt.fontSize = 60;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.fontStyle = FontStyles.Bold;
        titleObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 400);

        // Close Button
        GameObject closeBtnObj = new GameObject("CloseBtn");
        closeBtnObj.transform.SetParent(wardrobePanel.transform, false);
        Image cbBg = closeBtnObj.AddComponent<Image>();
        cbBg.color = Color.red;
        Button cBtn = closeBtnObj.AddComponent<Button>();
        cBtn.onClick.AddListener(() => Destroy(wardrobePanel));
        RectTransform cbRt = closeBtnObj.GetComponent<RectTransform>();
        cbRt.anchoredPosition = new Vector2(0, -500);
        cbRt.sizeDelta = new Vector2(300, 100);
        
        GameObject ctObj = new GameObject("Text");
        ctObj.transform.SetParent(closeBtnObj.transform, false);
        TextMeshProUGUI ctTxt = ctObj.AddComponent<TextMeshProUGUI>();
        ctTxt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        ctTxt.text = "CLOSE";
        ctTxt.fontSize = 40;
        ctTxt.alignment = TextAlignmentOptions.Center;
        ctTxt.color = Color.white;
        ctObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 100);

        // Items
        float yOffset = 200;
        foreach (var outfit in outfits)
        {
            CreateOutfitEntry(outfit, wardrobePanel.transform, yOffset);
            yOffset -= 150;
        }
    }

    private void CreateOutfitEntry(CatOutfit outfit, Transform parent, float yPos)
    {
        GameObject entry = new GameObject("Entry_" + outfit.outfitID);
        entry.transform.SetParent(parent, false);
        RectTransform rt = entry.AddComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(600, 100);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(entry.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        txt.text = outfit.displayName;
        txt.fontSize = 40;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, 0);

        GameObject btnObj = new GameObject("Btn");
        btnObj.transform.SetParent(entry.transform, false);
        Image bBg = btnObj.AddComponent<Image>();
        bBg.color = outfit.isUnlocked ? Color.green : Color.yellow;
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => {
            BuyOrEquipOutfit(outfit.outfitID);
            // Refresh UI
            Destroy(wardrobePanel);
            OpenWardrobeUI();
        });
        RectTransform bRt = btnObj.GetComponent<RectTransform>();
        bRt.anchoredPosition = new Vector2(150, 0);
        bRt.sizeDelta = new Vector2(250, 80);

        GameObject btObj = new GameObject("Text");
        btObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btTxt = btObj.AddComponent<TextMeshProUGUI>();
        btTxt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        
        if (outfit.outfitID == activeOutfitID) btTxt.text = "EQUIPPED";
        else if (outfit.isUnlocked) btTxt.text = "EQUIP";
        else btTxt.text = "BUY (" + outfit.price + ")";
        
        btTxt.fontSize = 35;
        btTxt.alignment = TextAlignmentOptions.Center;
        btTxt.color = Color.black;
        btObj.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 80);
    }
}
