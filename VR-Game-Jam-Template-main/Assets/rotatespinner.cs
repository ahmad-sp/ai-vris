using UnityEngine;

public class rotatespinner : MonoBehaviour
{
    [Tooltip("Speed of rotation in degrees per second")]
    public float speed = 200f;

    [Tooltip("Axis to rotate around")]
    public Vector3 axis = Vector3.forward;

    void Update()
    {
        // Rotate around the specified axis
        transform.Rotate(axis, -speed * Time.deltaTime);
    }
}
