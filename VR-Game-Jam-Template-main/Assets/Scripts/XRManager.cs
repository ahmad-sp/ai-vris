using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;
using Google.XR.Cardboard;

public class XRManager : MonoBehaviour
{
    private bool xrStarted = false;

    IEnumerator Start()
    {
        yield return StartXR();
    }

    IEnumerator StartXR()
    {
        if (XRGeneralSettings.Instance == null ||
            XRGeneralSettings.Instance.Manager == null)
        {
            Debug.LogError("XRGeneralSettings missing");
            yield break;
        }

        var manager = XRGeneralSettings.Instance.Manager;

        yield return manager.InitializeLoader();

        if (manager.activeLoader == null)
        {
            Debug.LogError("Cardboard XR loader failed");
            yield break;
        }

        manager.StartSubsystems();

        // Start in mono view
        UnityEngine.XR.XRSettings.enabled = false;

        xrStarted = true;
        Debug.Log("Cardboard XR ready in this scene");
    }

    void Update()
    {
        // Required for Cardboard XR
        Api.UpdateScreenParams();

        // Handle Cardboard buttons (ONLY in this scene)
        if (UnityEngine.XR.XRSettings.enabled)
        {
            if (Api.IsCloseButtonPressed)
                ExitVR();

            if (Api.IsGearButtonPressed)
                Api.ScanDeviceParams();
        }
    }

    // ===== UI BUTTONS =====

    public void EnterVR()
    {
        if (!xrStarted) return;

        UnityEngine.XR.XRSettings.enabled = true;
    }

    public void ExitVR()
    {
        UnityEngine.XR.XRSettings.enabled = false;
    }

    void OnDestroy()
    {
        // Make sure VR is OFF when leaving the scene
        UnityEngine.XR.XRSettings.enabled = false;
    }
}
