using UnityEngine;
using UnityEngine.Purchasing;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// In-app purchases for Google Play and the Apple App Store (Unity IAP v5 StoreController).
/// Products are all non-consumable: Remove Ads and pets. Product IDs must match in
/// Google Play Console and App Store Connect.
/// </summary>
public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    private StoreController store;
    private bool productsReady;

    [Header("Remove Ads Product")]
    public string removeAdsProductId = "com.lastbrick.removeads";
    [Tooltip("The actual button you click to pay")]
    public GameObject removeAdsBuyButton;
    public TextMeshProUGUI removeAdsPriceText;

    [Tooltip("Other UI elements to hide after purchase (like the Main Menu button that opens the popup)")]
    public GameObject[] objectsToHideOnPurchase;

    [Header("Consumable (Optional, e.g. Coins)")]
    public string coinsProductId = "com.lastbrick.coins100";

    private async void Awake()
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

        store = UnityIAPServices.StoreController();

        // Subscribe to every event BEFORE Connect: unfinished purchases from a previous session may arrive immediately.
        store.OnStoreConnected += OnStoreConnected;
        store.OnStoreDisconnected += failure => Debug.LogWarning($"[IAP] Store disconnected: {failure.message}");
        store.OnProductsFetched += OnProductsFetched;
        store.OnProductsFetchFailed += failure => Debug.LogWarning($"[IAP] Product fetch failed: {failure.FailureReason}");
        store.OnPurchasesFetched += OnPurchasesFetched;
        store.OnPurchasesFetchFailed += failure => Debug.LogWarning($"[IAP] Purchase fetch failed: {failure.message}");
        store.OnPurchasePending += OnPurchasePending;
        store.OnPurchaseConfirmed += OnPurchaseConfirmed;
        store.OnPurchaseFailed += failed => Debug.LogWarning($"[IAP] Purchase failed: {failed.FailureReason} - {failed.Details}");
        store.OnPurchaseDeferred += deferred => Debug.Log("[IAP] Purchase waiting for approval (Ask to Buy / pending payment).");

        try
        {
            await store.Connect();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAP] Connect failed: {e.Message}");
        }
    }

    private void Start()
    {
        // Initially hide or disable button if already purchased
        if (PlayerPrefs.GetInt("NoAdsPurchased", 0) == 1)
        {
            HideRemoveAdsUI();
        }

        if (removeAdsBuyButton != null)
        {
            removeAdsBuyButton.GetComponent<Button>().onClick.AddListener(BuyRemoveAds);
        }
    }

    private void OnStoreConnected()
    {
        var products = new List<ProductDefinition>
        {
            new ProductDefinition(removeAdsProductId, ProductType.NonConsumable)
        };

        // Pet products (the pet shop is set up by the time the store connects)
        if (PetSelectionManager.Instance != null)
        {
            foreach (var pet in PetSelectionManager.Instance.pets)
            {
                if (!string.IsNullOrEmpty(pet.iapProductID))
                {
                    products.Add(new ProductDefinition(pet.iapProductID, ProductType.NonConsumable));
                }
            }
        }

        store.FetchProducts(products);
    }

    private void OnProductsFetched(List<Product> products)
    {
        productsReady = true;
        Debug.Log($"[IAP] {products.Count} products ready.");

        // Update UI with localized price for Remove Ads
        if (removeAdsPriceText != null)
        {
            removeAdsPriceText.text = GetLocalizedPriceString(removeAdsProductId);
        }

        // Re-grant anything already owned (e.g. after reinstalling or on a new device)
        store.FetchPurchases();
    }

    private void OnPurchasesFetched(Orders orders)
    {
        foreach (var order in orders.ConfirmedOrders)
        {
            GrantProduct(GetProductId(order), false);
        }
    }

    private void OnPurchasePending(PendingOrder order)
    {
        // Grant first, then confirm. Granting is idempotent, so a re-delivered order is harmless.
        GrantProduct(GetProductId(order), true);
        store.ConfirmPurchase(order);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        switch (order)
        {
            case ConfirmedOrder confirmed:
                Debug.Log($"[IAP] Purchase confirmed: {GetProductId(confirmed)}");
                break;
            case FailedOrder failed:
                Debug.LogWarning($"[IAP] Confirmation failed: {failed.FailureReason} - {failed.Details}");
                break;
        }
    }

    private static string GetProductId(Order order)
    {
        return order.CartOrdered.Items().FirstOrDefault()?.Product?.definition.id;
    }

    private bool IsInitialized()
    {
        return store != null && productsReady;
    }

    public void BuyRemoveAds()
    {
        BuyProductID(removeAdsProductId);
    }

    public void BuyProductID(string productId)
    {
        if (!IsInitialized())
        {
            Debug.Log("[IAP] BuyProductID FAIL. Store not ready.");
            return;
        }

        Product product = store.GetProductById(productId);
        if (product != null && product.availableToPurchase)
        {
            Debug.Log($"[IAP] Purchasing '{productId}'");
            store.PurchaseProduct(product);
        }
        else
        {
            Debug.Log($"[IAP] BuyProductID FAIL. '{productId}' not found or not available for purchase.");
        }
    }

    public string GetLocalizedPriceString(string productId)
    {
        if (IsInitialized())
        {
            Product product = store.GetProductById(productId);
            if (product != null)
            {
                return product.metadata.localizedPriceString;
            }
        }
        return "$0.00"; // Fallback
    }

    /// <summary>
    /// Connect to a "Restore Purchases" button. Required by Apple for non-consumable purchases.
    /// Each restored purchase is delivered again through OnPurchasePending.
    /// </summary>
    public void RestorePurchases()
    {
        if (store == null)
        {
            Debug.Log("[IAP] RestorePurchases FAIL. Not initialized.");
            return;
        }

        store.RestoreTransactions((success, error) =>
        {
            if (success)
            {
                Debug.Log("[IAP] Restore finished.");
                store.FetchPurchases();
            }
            else
            {
                Debug.LogWarning($"[IAP] Restore failed: {error}");
            }
        });
    }

    private void GrantProduct(string productId, bool isNewPurchase)
    {
        if (string.IsNullOrEmpty(productId)) return;

        if (String.Equals(productId, removeAdsProductId, StringComparison.Ordinal))
        {
            Debug.Log("[IAP] Granting Remove Ads");
            PlayerPrefs.SetInt("NoAdsPurchased", 1);
            PlayerPrefs.Save();
            HideRemoveAdsUI();
            return;
        }

        // Check if it's a pet purchase
        if (PetSelectionManager.Instance != null)
        {
            for (int i = 0; i < PetSelectionManager.Instance.pets.Count; i++)
            {
                if (String.Equals(productId, PetSelectionManager.Instance.pets[i].iapProductID, StringComparison.Ordinal))
                {
                    Debug.Log("[IAP] Unlocking pet index: " + i);
                    PetSelectionManager.Instance.UnlockPet(i);

                    if (isNewPurchase && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayUnlockSound();
                    }
                    return;
                }
            }
        }

        Debug.Log($"[IAP] Unrecognized product: '{productId}'");
    }

    private void HideRemoveAdsUI()
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
}
