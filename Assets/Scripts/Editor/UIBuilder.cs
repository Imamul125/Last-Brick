using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class UIBuilder
{
    [MenuItem("Tools/Build UI Layout")]
    public static void BuildUI()
    {
        // 1. Convert Textures to Sprites
        ConvertTextureToSprite("Assets/UI/ui_panel.png");
        ConvertTextureToSprite("Assets/UI/ui_coins.png");

        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ui_panel.png");
        Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ui_coins.png");
        
        // Load default TMP font
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // 2. Setup Canvas
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null) canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        if (canvasObj.GetComponent<GraphicRaycaster>() == null) canvasObj.AddComponent<GraphicRaycaster>();

        // Ensure EventSystem
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Clean up old UI to rebuild
        foreach (Transform child in canvasObj.transform)
        {
            Object.DestroyImmediate(child.gameObject);
        }

        // --- TOP BAR ---
        GameObject topBar = new GameObject("TopBar");
        topBar.transform.SetParent(canvasObj.transform, false);
        RectTransform topRect = topBar.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0, 1);
        topRect.anchorMax = new Vector2(1, 1);
        topRect.offsetMin = new Vector2(0, -250);
        topRect.offsetMax = new Vector2(0, 0);

        // Level Text
        GameObject levelObj = new GameObject("LevelText");
        levelObj.transform.SetParent(topBar.transform, false);
        TextMeshProUGUI lvlText = levelObj.AddComponent<TextMeshProUGUI>();
        lvlText.font = font;
        lvlText.text = "LEVEL <color=#FFB700>1</color>";
        lvlText.fontSize = 80;
        lvlText.alignment = TextAlignmentOptions.Center;
        lvlText.fontStyle = FontStyles.Bold;
        RectTransform lvlRect = levelObj.GetComponent<RectTransform>();
        lvlRect.anchorMin = new Vector2(0.5f, 0.5f);
        lvlRect.anchorMax = new Vector2(0.5f, 0.5f);
        lvlRect.sizeDelta = new Vector2(400, 100);
        lvlRect.anchoredPosition = new Vector2(0, 50);

        // Moves Text
        GameObject movesObj = new GameObject("MovesText");
        movesObj.transform.SetParent(topBar.transform, false);
        Image movesBg = movesObj.AddComponent<Image>();
        movesBg.sprite = panelSprite;
        movesBg.color = new Color(0, 0, 0, 0.7f);
        RectTransform movesRect = movesObj.GetComponent<RectTransform>();
        movesRect.anchorMin = new Vector2(0.5f, 0.5f);
        movesRect.anchorMax = new Vector2(0.5f, 0.5f);
        movesRect.sizeDelta = new Vector2(300, 80);
        movesRect.anchoredPosition = new Vector2(0, -50);

        GameObject mTextObj = new GameObject("Text");
        mTextObj.transform.SetParent(movesObj.transform, false);
        TextMeshProUGUI mText = mTextObj.AddComponent<TextMeshProUGUI>();
        mText.font = font;
        mText.text = "MOVES: <color=#FFB700>0</color>";
        mText.fontSize = 45;
        mText.alignment = TextAlignmentOptions.Center;
        mText.fontStyle = FontStyles.Bold;
        mTextObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 80);

        // Coins Display
        GameObject coinDisplay = new GameObject("CoinDisplay");
        coinDisplay.transform.SetParent(topBar.transform, false);
        Image coinBg = coinDisplay.AddComponent<Image>();
        coinBg.sprite = panelSprite;
        coinBg.color = new Color(0, 0, 0, 0.7f);
        RectTransform coinRect = coinDisplay.GetComponent<RectTransform>();
        coinRect.anchorMin = new Vector2(1, 0.5f);
        coinRect.anchorMax = new Vector2(1, 0.5f);
        coinRect.sizeDelta = new Vector2(250, 80);
        coinRect.anchoredPosition = new Vector2(-150, 50);

        GameObject coinIcon = new GameObject("Icon");
        coinIcon.transform.SetParent(coinDisplay.transform, false);
        Image cIcon = coinIcon.AddComponent<Image>();
        cIcon.sprite = coinSprite;
        RectTransform cIconRect = coinIcon.GetComponent<RectTransform>();
        cIconRect.anchorMin = new Vector2(0, 0.5f);
        cIconRect.anchorMax = new Vector2(0, 0.5f);
        cIconRect.sizeDelta = new Vector2(80, 80);
        cIconRect.anchoredPosition = new Vector2(20, 0);

        GameObject coinTextObj = new GameObject("Text");
        coinTextObj.transform.SetParent(coinDisplay.transform, false);
        TextMeshProUGUI cText = coinTextObj.AddComponent<TextMeshProUGUI>();
        cText.font = font;
        cText.text = "0";
        cText.fontSize = 45;
        cText.alignment = TextAlignmentOptions.MidlineLeft;
        cText.fontStyle = FontStyles.Bold;
        RectTransform ctRect = coinTextObj.GetComponent<RectTransform>();
        ctRect.sizeDelta = new Vector2(150, 80);
        ctRect.anchoredPosition = new Vector2(50, 0);


        // --- LEFT SIDE: OBJECTIVE ---
        GameObject objPanel = new GameObject("ObjectivePanel");
        objPanel.transform.SetParent(canvasObj.transform, false);
        Image oBg = objPanel.AddComponent<Image>();
        oBg.sprite = panelSprite;
        oBg.color = new Color(0, 0, 0, 0.7f);
        RectTransform oRect = objPanel.GetComponent<RectTransform>();
        oRect.anchorMin = new Vector2(0, 0.6f);
        oRect.anchorMax = new Vector2(0, 0.6f);
        oRect.sizeDelta = new Vector2(280, 350);
        oRect.anchoredPosition = new Vector2(180, 0);

        GameObject oTitle = new GameObject("Title");
        oTitle.transform.SetParent(objPanel.transform, false);
        TextMeshProUGUI otText = oTitle.AddComponent<TextMeshProUGUI>();
        otText.font = font;
        otText.text = "<color=#FFB700>OBJECTIVE</color>";
        otText.fontSize = 35;
        otText.alignment = TextAlignmentOptions.Center;
        otText.fontStyle = FontStyles.Bold;
        oTitle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 130);

        GameObject oDesc = new GameObject("Desc");
        oDesc.transform.SetParent(objPanel.transform, false);
        TextMeshProUGUI odText = oDesc.AddComponent<TextMeshProUGUI>();
        odText.font = font;
        odText.text = "Remove bricks without collapsing the tower";
        odText.fontSize = 28;
        odText.alignment = TextAlignmentOptions.Center;
        odText.enableWordWrapping = true;
        RectTransform odRect = oDesc.GetComponent<RectTransform>();
        odRect.sizeDelta = new Vector2(240, 150);
        odRect.anchoredPosition = new Vector2(0, 30);

        GameObject oProg = new GameObject("Progress");
        oProg.transform.SetParent(objPanel.transform, false);
        TextMeshProUGUI opText = oProg.AddComponent<TextMeshProUGUI>();
        opText.font = font;
        opText.text = "<color=#FFB700>0</color>/15";
        opText.fontSize = 50;
        opText.alignment = TextAlignmentOptions.Center;
        opText.fontStyle = FontStyles.Bold;
        oProg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -110);


        // --- RIGHT SIDE: REWARD ---
        GameObject rewPanel = new GameObject("RewardPanel");
        rewPanel.transform.SetParent(canvasObj.transform, false);
        Image rBg = rewPanel.AddComponent<Image>();
        rBg.sprite = panelSprite;
        rBg.color = new Color(0, 0, 0, 0.7f);
        RectTransform rRect = rewPanel.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(1, 0.6f);
        rRect.anchorMax = new Vector2(1, 0.6f);
        rRect.sizeDelta = new Vector2(280, 250);
        rRect.anchoredPosition = new Vector2(-180, 50);

        GameObject rTitle = new GameObject("Title");
        rTitle.transform.SetParent(rewPanel.transform, false);
        TextMeshProUGUI rtText = rTitle.AddComponent<TextMeshProUGUI>();
        rtText.font = font;
        rtText.text = "<color=#FFB700>NEXT REWARD</color>";
        rtText.fontSize = 30;
        rtText.alignment = TextAlignmentOptions.Center;
        rtText.fontStyle = FontStyles.Bold;
        rTitle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 90);

        GameObject rIcon = new GameObject("Icon");
        rIcon.transform.SetParent(rewPanel.transform, false);
        Image riImg = rIcon.AddComponent<Image>();
        riImg.sprite = coinSprite;
        RectTransform riRect = rIcon.GetComponent<RectTransform>();
        riRect.sizeDelta = new Vector2(100, 100);
        riRect.anchoredPosition = new Vector2(0, -10);

        GameObject rAmnt = new GameObject("Amount");
        rAmnt.transform.SetParent(rewPanel.transform, false);
        TextMeshProUGUI raText = rAmnt.AddComponent<TextMeshProUGUI>();
        raText.font = font;
        raText.text = "50";
        raText.fontSize = 45;
        raText.alignment = TextAlignmentOptions.Center;
        raText.fontStyle = FontStyles.Bold;
        rAmnt.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -80);

        // --- BOTTOM BANNER ---
        GameObject botBanner = new GameObject("BottomBanner");
        botBanner.transform.SetParent(canvasObj.transform, false);
        Image bBg = botBanner.AddComponent<Image>();
        bBg.sprite = panelSprite;
        bBg.color = new Color(0, 0, 0, 0.7f);
        RectTransform bRect = botBanner.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0);
        bRect.anchorMax = new Vector2(0.5f, 0);
        bRect.sizeDelta = new Vector2(700, 100);
        bRect.anchoredPosition = new Vector2(0, 150);

        GameObject bTextObj = new GameObject("Text");
        bTextObj.transform.SetParent(botBanner.transform, false);
        TextMeshProUGUI btText = bTextObj.AddComponent<TextMeshProUGUI>();
        btText.font = font;
        btText.text = "TAP A BRICK TO REMOVE";
        btText.fontSize = 40;
        btText.alignment = TextAlignmentOptions.Center;
        btText.fontStyle = FontStyles.Bold;
        bTextObj.GetComponent<RectTransform>().sizeDelta = new Vector2(700, 100);

        // Connect to UIManager
        UIManager uiMgr = Object.FindObjectOfType<UIManager>();
        if (uiMgr != null)
        {
            uiMgr.levelText = lvlText;
            uiMgr.movesText = mText;
            uiMgr.coinsText = cText;
            uiMgr.objectiveProgressText = opText;
            EditorUtility.SetDirty(uiMgr);
        }

        Debug.Log("UI Successfully Built!");
    }

    private static void ConvertTextureToSprite(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
