using UnityEngine;

/// <summary>
/// Camera-delta parallax (no material instancing — B13-safe). factor 0 =
/// pinned to world, 1 = pinned to camera. Layers tile horizontally x3 so
/// the seam never shows inside the confiner bounds.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    public float factor = 0.25f;

    Transform cam;
    Vector3 camStart;
    Vector3 selfStart;

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
            camStart = cam.position;
        selfStart = transform.position;
    }

    void LateUpdate()
    {
        if (cam == null)
            return;
        Vector3 delta = cam.position - camStart;
        transform.position = new Vector3(
            selfStart.x + delta.x * factor,
            selfStart.y + delta.y * factor * 0.5f,
            selfStart.z);
    }
}
