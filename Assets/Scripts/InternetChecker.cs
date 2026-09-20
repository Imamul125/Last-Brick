using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class InternetChecker : MonoBehaviour
{
    [Tooltip("Drag your 'No Internet' UI Panel here")]
    public GameObject noInternetPopup;

    [Header("Events")]
    public UnityEvent onInternetLost;
    public UnityEvent onInternetRestored;

    private bool wasOffline = false;

    void Start()
    {
        // Use a Coroutine instead of InvokeRepeating for better optimization and to ignore Time.timeScale
        StartCoroutine(CheckInternetRoutine());
    }

    private IEnumerator CheckInternetRoutine()
    {
        while (true)
        {
            CheckInternet();
            // We MUST use Realtime here. If we use regular time, it will stop working when Time.timeScale = 0!
            yield return new WaitForSecondsRealtime(2f);
        }
    }

    void CheckInternet()
    {
        bool isCurrentlyOffline = (Application.internetReachability == NetworkReachability.NotReachable);

        if (isCurrentlyOffline)
        {
            if (noInternetPopup != null) noInternetPopup.SetActive(true);

            // If this is the FIRST moment we noticed it was offline, fire the event
            if (!wasOffline)
            {
                wasOffline = true;
                if (onInternetLost != null) onInternetLost.Invoke();
            }
        }
        else
        {
            if (noInternetPopup != null) noInternetPopup.SetActive(false);

            // If we were offline but just got connection back, fire the restored event
            if (wasOffline)
            {
                wasOffline = false;
                if (onInternetRestored != null) onInternetRestored.Invoke();
            }
        }
    }
}
