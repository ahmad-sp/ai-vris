using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Google.XR.Cardboard;

public class XRManager : MonoBehaviour
{
    private bool xrStarted = false;
    private XRDisplaySubsystem xrDisplaySubsystem;

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

        // Get the XRDisplaySubsystem
        List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        if (displays.Count > 0)
            xrDisplaySubsystem = displays[0];

        // Start in mono view
        if (xrDisplaySubsystem != null)
            xrDisplaySubsystem.Stop();

        xrStarted = true;
        Debug.Log("Cardboard XR ready in this scene");
        // Handle Cardboard buttons (ONLY in this scene)
        if (xrDisplaySubsystem != null && xrDisplaySubsystem.running)
        {
            if (Api.IsCloseButtonPressed)
                ExitVR();

            if (Api.IsGearButtonPressed)
                Api.ScanDeviceParams();
        }
    }

    public void EnterVR()
    {
        if (!xrStarted || xrDisplaySubsystem == null) return;

        xrDisplaySubsystem.Start();
    }

    public void ExitVR()
    {
        if (xrDisplaySubsystem != null)
            xrDisplaySubsystem.Stop();
    }

    void OnDestroy()
    {
        // Make sure VR is OFF when leaving the scene
        if (xrDisplaySubsystem != null)
            xrDisplaySubsystem.Stop();
    }
}
