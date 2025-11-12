using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using UnityEngine.Android;
using TMPro;

public class MobileBluetoothTest : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI heartRateText;
    public Button scanButton;
    public Button connectButton;
    public Button disconnectButton;

    [Header("Polar H10 Settings")]
    private string HR_SERVICE = "0000180D-0000-1000-8000-00805F9B34FB";
    private string HR_MEASUREMENT = "00002A37-0000-1000-8000-00805F9B34FB";

    private string deviceAddress = "";
    private string deviceName = "";
    private bool isScanning = false;
    private bool isConnected = false;
    private int currentHeartRate = 0;

    // Updated permissions array with location permissions
    private readonly string[] requiredPermissions = new string[]
    {
        "android.permission.BLUETOOTH_SCAN",
        "android.permission.BLUETOOTH_CONNECT",
        "android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_COARSE_LOCATION"
    };

    void Start()
    {
        // Setup UI buttons
        scanButton.onClick.AddListener(OnScanButtonPressed);
        connectButton.onClick.AddListener(OnConnectButtonPressed);
        disconnectButton.onClick.AddListener(OnDisconnectButtonPressed);

        // Initial button states
        scanButton.interactable = false;
        connectButton.interactable = false;
        disconnectButton.interactable = false;

        statusText.text = "Checking permissions...";
        heartRateText.text = "HR: -- BPM";

        // Check and request permissions
        CheckAndRequestPermissions();
    }

    void CheckAndRequestPermissions()
    {
#if UNITY_ANDROID
        // Check if all permissions are granted
        bool allGranted = true;
        foreach (string permission in requiredPermissions)
        {
            if (!Permission.HasUserAuthorizedPermission(permission))
            {
                allGranted = false;
                break;
            }
        }

        if (allGranted)
        {
            statusText.text = "Permissions granted. Initializing Bluetooth...";
            StartCoroutine(InitializeBluetoothAfterDelay(0.5f));
        }
        else
        {
            // Request all missing permissions
            RequestPermissions();
        }
#else
        // For non-Android platforms
        StartCoroutine(InitializeBluetoothAfterDelay(0.5f));
#endif
    }

    void RequestPermissions()
    {
#if UNITY_ANDROID
        statusText.text = "Requesting permissions...";
        // Use Unity 6 recommended approach - request all at once
        Permission.RequestUserPermissions(requiredPermissions);
        // Check results after a short delay
        StartCoroutine(CheckPermissionsAfterRequest());
#endif
    }

    IEnumerator CheckPermissionsAfterRequest()
    {
        // Wait for permission dialog to complete
        yield return new WaitForSeconds(1f);

#if UNITY_ANDROID
        bool allGranted = true;
        string deniedPermission = "";

        foreach (string permission in requiredPermissions)
        {
            if (!Permission.HasUserAuthorizedPermission(permission))
            {
                allGranted = false;
                deniedPermission = permission;
                break;
            }
        }

        if (allGranted)
        {
            statusText.text = "All permissions granted! Initializing...";
            StartCoroutine(InitializeBluetoothAfterDelay(0.5f));
        }
        else
        {
            // Check if we should show rationale
            if (Permission.ShouldShowRequestPermissionRationale(deniedPermission))
            {
                statusText.text = "Bluetooth & Location permissions required. Please grant them.";
                // Give user option to retry
                Invoke("RequestPermissions", 2f);
            }
            else
            {
                statusText.text = "Permissions denied. Enable them in Settings to use this app.";
            }
        }
#endif
    }

    IEnumerator InitializeBluetoothAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        InitializeBluetooth();
    }

    void InitializeBluetooth()
    {
        statusText.text = "Initializing Bluetooth...";

        BluetoothLEHardwareInterface.Initialize(true, false, () =>
        {
            statusText.text = "Bluetooth Ready! Tap 'Scan' to find Polar H10 devices.";
            scanButton.interactable = true;
            Debug.Log("BLE Initialized Successfully");
        },
        (error) =>
        {
            statusText.text = "Bluetooth Error: " + error;
            Debug.LogError("BLE Initialization Error: " + error);
        });
    }

    void OnScanButtonPressed()
    {
        // Double-check permissions before scanning
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
        {
            statusText.text = "Bluetooth scan permission required";
            RequestPermissions();
            return;
        }
#endif

        if (!isScanning)
        {
            StartScan();
        }
    }

    void StartScan()
    {
        statusText.text = "Scanning for Polar H10 devices...";
        isScanning = true;
        scanButton.interactable = false;
        deviceAddress = "";
        deviceName = "";

        BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(
            new string[] { HR_SERVICE },
            (address, name) =>
            {
                Debug.Log($"Found device: {name} ({address})");

                // Check if device name contains "Polar H10"
                if (!string.IsNullOrEmpty(name) && name.Contains("Polar H10"))
                {
                    statusText.text = $"Found: {name}";
                    deviceAddress = address;
                    deviceName = name;

                    BluetoothLEHardwareInterface.StopScan();
                    isScanning = false;
                    connectButton.interactable = true;
                    scanButton.interactable = true;

                    Debug.Log($"Polar H10 device found: {name} at {address}");
                }
            },
            null,
            false,
            false);

        // Scan timeout after 15 seconds
        Invoke("StopScanTimeout", 15f);
    }

    void StopScanTimeout()
    {
        if (isScanning)
        {
            BluetoothLEHardwareInterface.StopScan();
            isScanning = false;

            if (string.IsNullOrEmpty(deviceAddress))
            {
                statusText.text = "Scan timeout. No Polar H10 found. Try again.";
            }

            scanButton.interactable = true;
            Debug.Log("Scan timeout");
        }
    }

    void OnConnectButtonPressed()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
        {
            statusText.text = "Bluetooth connect permission required";
            RequestPermissions();
            return;
        }
#endif

        if (string.IsNullOrEmpty(deviceAddress))
        {
            statusText.text = "No device to connect. Scan first.";
            return;
        }

        ConnectToDevice();
    }

    void ConnectToDevice()
    {
        statusText.text = $"Connecting to {deviceName}...";
        connectButton.interactable = false;
        scanButton.interactable = false;

        BluetoothLEHardwareInterface.ConnectToPeripheral(
            deviceAddress,
            (address) =>
            {
                statusText.text = "Connected! Discovering services...";
                Debug.Log($"Connected to {address}");
            },
            (address, serviceUUID) =>
            {
                Debug.Log($"Service discovered: {serviceUUID}");
            },
            (address, serviceUUID, characteristicUUID) =>
            {
                Debug.Log($"Characteristic discovered: {characteristicUUID}");

                if (IsEqual(characteristicUUID, HR_MEASUREMENT))
                {
                    Debug.Log("Heart Rate characteristic found, subscribing...");
                    SubscribeToHeartRate();
                }
            },
            (address) =>
            {
                statusText.text = "Disconnected from device";
                heartRateText.text = "HR: -- BPM";
                isConnected = false;
                disconnectButton.interactable = false;
                scanButton.interactable = true;
                currentHeartRate = 0;
                Debug.Log($"Disconnected from {address}");
            });
    }

    void SubscribeToHeartRate()
    {
        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(
            deviceAddress,
            HR_SERVICE,
            HR_MEASUREMENT,
            (notifyAddress, notifyCharacteristic) =>
            {
                statusText.text = $"Receiving heart rate from {deviceName}...";
                isConnected = true;
                disconnectButton.interactable = true;
                Debug.Log("Successfully subscribed to heart rate notifications");
            },
            (address, characteristicUUID, data) =>
            {
                currentHeartRate = ParseHeartRate(data);
                heartRateText.text = $"HR: {currentHeartRate} BPM";
                Debug.Log($"Heart Rate: {currentHeartRate} BPM");

                CheckHeartRateThresholds(currentHeartRate);
            });
    }

    int ParseHeartRate(byte[] data)
    {
        if (data == null || data.Length < 2)
        {
            Debug.LogWarning("Invalid heart rate data received");
            return 0;
        }

        byte flags = data[0];
        bool is16bit = (flags & 0x01) != 0;
        int heartRate;

        if (is16bit)
        {
            heartRate = BitConverter.ToUInt16(data, 1);
        }
        else
        {
            heartRate = data[1];
        }

        return heartRate;
    }

    void CheckHeartRateThresholds(int hr)
    {
        // Trigger different events based on heart rate zones
        if (hr > 160)
        {
            Debug.Log("TRIGGER: Heart rate VERY HIGH! (>160)");
            // Add your voice-over trigger here
        }
        else if (hr > 140)
        {
            Debug.Log("TRIGGER: Heart rate HIGH (>140)");
            // Add your voice-over trigger here
        }
        else if (hr > 120)
        {
            Debug.Log("TRIGGER: Heart rate is increasing (>120)");
            // Add your voice-over trigger here
        }
        else if (hr > 100)
        {
            Debug.Log("TRIGGER: Heart rate elevated (>100)");
        }
        else if (hr < 50 && hr > 0)
        {
            Debug.Log("TRIGGER: Heart rate is low (<50)");
        }
    }

    void OnDisconnectButtonPressed()
    {
        Disconnect();
    }

    void Disconnect()
    {
        if (string.IsNullOrEmpty(deviceAddress))
            return;

        statusText.text = "Disconnecting...";

        BluetoothLEHardwareInterface.DisconnectPeripheral(deviceAddress, (address) =>
        {
            statusText.text = "Disconnected. Tap 'Scan' to reconnect.";
            isConnected = false;
            disconnectButton.interactable = false;
            scanButton.interactable = true;
            heartRateText.text = "HR: -- BPM";
            currentHeartRate = 0;
            Debug.Log("Manually disconnected from device");
        });
    }

    bool IsEqual(string uuid1, string uuid2)
    {
        if (string.IsNullOrEmpty(uuid1) || string.IsNullOrEmpty(uuid2))
            return false;

        string uuid1Upper = uuid1.ToUpper();
        string uuid2Upper = uuid2.ToUpper();

        if (uuid1Upper == uuid2Upper)
            return true;

        if (uuid1Upper.Contains(uuid2Upper) || uuid2Upper.Contains(uuid1Upper))
            return true;

        return false;
    }

    void OnDestroy()
    {
        if (isScanning)
        {
            BluetoothLEHardwareInterface.StopScan();
        }

        if (isConnected && !string.IsNullOrEmpty(deviceAddress))
        {
            BluetoothLEHardwareInterface.DisconnectPeripheral(deviceAddress, null);
        }

        BluetoothLEHardwareInterface.DeInitialize(() =>
        {
            Debug.Log("Bluetooth deinitialized");
        });
    }

    // Public methods for other scripts to access heart rate data
    public int GetCurrentHeartRate()
    {
        return currentHeartRate;
    }

    public bool IsConnectedToDevice()
    {
        return isConnected;
    }

    public string GetConnectedDeviceName()
    {
        return deviceName;
    }
}
          