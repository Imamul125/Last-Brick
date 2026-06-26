using UnityEngine;

public class PushNotificationManager : MonoBehaviour
{
    public static PushNotificationManager Instance { get; private set; }

    void Awake()
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

    void Start()
    {
        Debug.Log("[PushNotificationManager] Firebase Notifications not configured yet. Skipping initialization.");
    }

    public void ScheduleDailyReminder()
    {
        Debug.Log("[PushNotificationManager] ScheduleDailyReminder not implemented yet.");
    }
}
