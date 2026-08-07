using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class IAPManager : MonoBehaviour, IDetailedStoreListener
{
    public static IAPManager Instance { get; private set; }

    private IStoreController storeController;
    private IExtensionProvider storeExtensionProvider;

    [Header("Remove Ads Product")]
    public string removeAdsProductId = "com.lastbrick.removeads";
    [Tooltip("The actual button you click to pay")]
    public GameObject removeAdsBuyButton;
    public TextMeshProUGUI removeAdsPriceText;
    
    [Tooltip("Other UI elements to hide after purchase (like the Main Menu button that opens the popup)")]
    public GameObject[] objectsToHideOnPurchase;

    [Header("Consumable (Optional, e.g. Coins)")]
    public string coinsProductId = "com.lastbrick.coins100";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (storeController == null)
        {
            InitializePurchasing();
        }

        // Initially hide or disable button if already purchased
        if (PlayerPrefs.GetInt("NoAdsPurchased", 0) == 1)
        {
            if (removeAdsBuyButton != null)
            {
                removeAdsBuyButton.SetActive(false);
            }
            
            if (objectsToHideOnPurchase != null)
            {
                foreach (var obj in objectsToHideOnPurchase)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }
        }
        
        if (removeAdsBuyButton != null)
        {
            removeAdsBuyButton.GetComponent<Button>().onClick.AddListener(BuyRemoveAds);
        }
    }

    public void InitializePurchasing()
    {
        if (IsInitialized()) return;

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        builder.AddProduct(removeAdsProductId, ProductType.NonConsumable);
        
        // You can add more products here if needed
        // builder.AddProduct(coinsProductId, ProductType.Consumable);

        // Also add Pet Products if they are statically known, or add them dynamically if needed.
        // It's better to add them here if you know their IDs.
        if (PetSelectionManager.Instance != null)
        {
            foreach (var pet in PetSelectionManager.Instance.pets)
            {
                if (!string.IsNullOrEmpty(pet.iapProductID))
                {
                    builder.AddProduct(pet.iapProductID, ProductType.NonConsumable);
                }
            }
        }

        UnityPurchasing.Initialize(this, builder);
    }

    private bool IsInitialized()
    {
        return storeController != null && storeExtensionProvider != null;
    }

    public void BuyRemoveAds()
    {
        BuyProductID(removeAdsProductId);
    }

    public void BuyProductID(string productId)
    {
        if (IsInitialized())
        {
            Product product = storeController.products.WithID(productId);
            if (product != null && product.availableToPurchase)
            {
                Debug.Log($"Purchasing product asynchronously: '{product.definition.id}'");
                storeController.InitiatePurchase(product);
            }
            else
            {
                Debug.Log("BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase.");
            }
        }
        else
        {
            Debug.Log("BuyProductID FAIL. Not initialized.");
        }
    }

    public string GetLocalizedPriceString(string productId)
    {
        if (IsInitialized())
        {
            Product product = storeController.products.WithID(productId);
            if (product != null)
            {
                return product.metadata.localizedPriceString;
            }
        }
        return "$0.00"; // Fallback
    }

    public void RestorePurchases()
    {
        if (!IsInitialized())
        {
            Debug.Log("RestorePurchases FAIL. Not initialized.");
            return;
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer || 
            Application.platform == RuntimePlatform.OSXPlayer)
        {
            Debug.Log("RestorePurchases started ...");
            var apple = storeExtensionProvider.GetExtension<IAppleExtensions>();
            apple.RestoreTransactions((result, error) => {
                Debug.Log("RestorePurchases continuing: " + result + ". If no further messages, no purchases available to restore.");
            });
        }
        else
        {
            Debug.Log("RestorePurchases is only handled implicitly on Android (Google Play).");
        }
    }

    // --- IDetailedStoreListener Methods ---

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log("OnInitialized: PASS");
        storeController = controller;
        storeExtensionProvider = extensions;

        // Update UI with localized price for Remove Ads
        if (removeAdsPriceText != null)
        {
            Product product = storeController.products.WithID(removeAdsProductId);
            if (product != null)
            {
                removeAdsPriceText.text = product.metadata.localizedPriceString;
            }
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.Log($"OnInitializeFailed InitializationFailureReason:{error}");
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.Log($"OnInitializeFailed InitializationFailureReason:{error} message:{message}");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string productId = args.purchasedProduct.definition.id;

        if (String.Equals(productId, removeAdsProductId, StringComparison.Ordinal))
        {
            Debug.Log("ProcessPurchase: PASS. Product: " + args.purchasedProduct.definition.id);
            PlayerPrefs.SetInt("NoAdsPurchased", 1);
            PlayerPrefs.Save();
            
            if (removeAdsBuyButton != null)
            {
                removeAdsBuyButton.SetActive(false);
            }

            if (objectsToHideOnPurchase != null)
            {
                foreach (var obj in objectsToHideOnPurchase)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }
        }
        else
        {
            // Check if it's a pet purchase
            if (PetSelectionManager.Instance != null)
            {
                for (int i = 0; i < PetSelectionManager.Instance.pets.Count; i++)
                {
                    if (String.Equals(productId, PetSelectionManager.Instance.pets[i].iapProductID, StringComparison.Ordinal))
                    {
                        Debug.Log("ProcessPurchase: PASS. Unlocking pet index: " + i);
                        PetSelectionManager.Instance.UnlockPet(i);
                        
                        // Optionally play sound
                        if (AudioManager.Instance != null)
                        {
                            AudioManager.Instance.PlayUnlockSound();
                        }
                        
                        return PurchaseProcessingResult.Complete;
                    }
                }
            }

            Debug.Log(string.Format("ProcessPurchase: FAIL. Unrecognized product: '{0}'", args.purchasedProduct.definition.id));
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.Log($"OnPurchaseFailed: FAIL. Product: '{product.definition.storeSpecificId}', PurchaseFailureReason: {failureReason}");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
         Debug.Log($"OnPurchaseFailed: FAIL. Product: '{product.definition.storeSpecificId}', Description: {failureDescription.message}");
    }
}
