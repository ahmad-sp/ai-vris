using UnityEngine;

public class MobileVRManager : MonoBehaviour
{
    [Header("Camera References")]
    public Camera leftCamera;
    public Camera rightCamera;
    public Camera monoCamera;

    [Header("Head Transform")]
    public Transform head;

    [Header("Settings")]
    public float ipd = 0.064f;          // Inter-Pupillary Distance (64 mm)
    public bool useGyro = true;
    public float gyroSmooth = 10f;

    Quaternion targetRotation = Quaternion.identity;

    void Start()
    {
        // Default to mono mode when scene starts
        SetVR(false);

        if (SystemInfo.supportsGyroscope)
            Input.gyro.enabled = true;
    }

    void Update()
    {
        if (!useGyro || !SystemInfo.supportsGyroscope) return;

        // Convert gyro attitude to Unity's coordinate system
        Quaternion deviceRotation = new Quaternion(
            Input.gyro.attitude.x,
            Input.gyro.attitude.y,
            -Input.gyro.attitude.z,
            -Input.gyro.attitude.w
        );

        targetRotation = deviceRotation;

        // Smooth head rotation
        head.localRotation =
            Quaternion.Slerp(head.localRotation, targetRotation, Time.deltaTime * gyroSmooth);
    }

    public void SetVR(bool enable)
    {
        if (enable)
        {
            // Enable split-screen VR
            leftCamera.enabled = true;
            rightCamera.enabled = true;
            monoCamera.enabled = false;

            // Set camera positions for stereoscopic vision
            leftCamera.transform.localPosition = new Vector3(-ipd * 0.5f, 0, 0);
            rightCamera.transform.localPosition = new Vector3(ipd * 0.5f, 0, 0);

            // Left eye = left half
            leftCamera.rect = new Rect(0f, 0f, 0.5f, 1f);

            // Right eye = right half
            rightCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);

            Debug.Log("VR MODE ENABLED");
        }
        else
        {
            // Disable stereo cameras
            leftCamera.enabled = false;
            rightCamera.enabled = false;

            // Enable mono camera
            monoCamera.enabled = true;
            monoCamera.rect = new Rect(0f, 0f, 1f, 1f);

            Debug.Log("VR MODE DISABLED (MONO)");
        }
    }
}
