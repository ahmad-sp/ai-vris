using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Google.XR.Cardboard;

public class XRManager : MonoBehaviour
{
    XRDisplaySubsystem display;
    public bool isStereo = true;

    IEnumerator Start()
    {
        var mgr = XRGeneralSettings.Instance.Manager;

        yield return mgr.InitializeLoader();
        mgr.StartSubsystems();

        // Get display
        List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        display = displays[0];

        // Start in MONO
        if(!isStereo) display.Stop();
    }

    void Update()
    {
        // Required for Cardboard
        Api.UpdateScreenParams();
    if (Api.IsCloseButtonPressed)
    {
        // YOU decide what "close" means
        SwitchToMono();
    }

    if (Api.IsGearButtonPressed)
    {
        // Opens Cardboard QR / lens config
        Api.ScanDeviceParams();
    }
    }

    public void SwitchToStereo()
    {
        isStereo = true;
        if (!display.running)
            display.Start();
    }

    public void SwitchToMono()
    {
        isStereo = false;
        if (display.running)
            display.Stop();
    }
}
