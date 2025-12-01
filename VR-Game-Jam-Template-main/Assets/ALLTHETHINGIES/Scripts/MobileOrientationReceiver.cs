// MobileOrientationReceiver.cs
using UnityEngine;
using System;

public class MobileOrientationReceiver : MonoBehaviour
{
    public Transform head; // assign your Head transform in Inspector
    public float smoothSpeed = 8f;

    void OnEnable()
    {
        WebSocketServerUnity.OnMessageReceived += HandleMessage;
    }

    void OnDisable()
    {
        WebSocketServerUnity.OnMessageReceived -= HandleMessage;
    }

    Quaternion targetRot = Quaternion.identity;

    void HandleMessage(string json)
    {
        try
        {
            // Parse JSON. We expect fields: alpha (z), beta (x), gamma (y) in degrees
            var data = JsonUtility.FromJson<OrientationData>(json);
            // Convert DeviceOrientation (alpha, beta, gamma) to Unity quaternion
            // NOTE: device orientation axes vary by browser & screen orientation.
            // This mapping works for typical portrait orientation; adjust if needed.
            // We'll convert to radians and make a quaternion:
            float alpha = data.alpha; // z (compass)
            float beta = data.beta;   // x (front-back)
            float gamma = data.gamma; // y (left-right)

            // A quick conversion: rotate by ( -beta, -alpha, gamma )
            // You may need to tweak signs and axis order depending on device orientation.
            targetRot = Quaternion.Euler(-beta, -alpha, gamma);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed parse orientation JSON: " + ex.Message + " raw: " + json);
        }
    }

    void Update()
    {
        if (head == null) return;
        head.localRotation = Quaternion.Slerp(head.localRotation, targetRot, Time.deltaTime * smoothSpeed);
    }

    [Serializable]
    public class OrientationData
    {
        public float alpha;
        public float beta;
        public float gamma;
        public long timestamp;
    }
}
