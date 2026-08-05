using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ObjectRotationUnlockController : MonoBehaviour
{
    [Header("Target Object")]
    [Tooltip("The object/image to appear, rotate, and hide (e.g., Rotation_IMG)")]
    [SerializeField] private GameObject targetObject;

    [Header("Rotation Settings")]
    [Tooltip("Target rotation angle around the Z-axis in degrees")]
    [SerializeField] private float targetAngleZ = 90f;

    [Tooltip("Time in seconds to complete the rotation")]
    [SerializeField] private float duration = 1.0f;

    [Tooltip("If true, resets rotation back to 0° before starting")]
    [SerializeField] private bool resetRotationOnStart = true;

    [Header("Events")]
    public UnityEvent OnRotationStarted;
    public UnityEvent OnRotationCompleted;

    private bool isRotating = false;

    private void Start()
    {
        // Ensure the object is hidden on start
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }

    /// <summary>
    /// Drag and drop this function into your UI Button's OnClick() list in the Inspector.
    /// </summary>
    public void StartRotationSequence()
    {
        if (isRotating || targetObject == null)
            return;

        StartCoroutine(RotateAndUnlockRoutine());
    }

    private IEnumerator RotateAndUnlockRoutine()
    {
        isRotating = true;

        // 1. Activate object
        targetObject.SetActive(true);

        Vector3 initialEuler = targetObject.transform.localEulerAngles;
        float startZ = resetRotationOnStart ? 0f : initialEuler.z;
        float endZ = startZ + targetAngleZ;

        if (resetRotationOnStart)
        {
            targetObject.transform.localRotation = Quaternion.Euler(initialEuler.x, initialEuler.y, 0f);
        }

        OnRotationStarted?.Invoke();

        // 2. Rotate around Z-axis over duration
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float currentZ = Mathf.Lerp(startZ, endZ, t);
            targetObject.transform.localRotation = Quaternion.Euler(initialEuler.x, initialEuler.y, currentZ);

            yield return null;
        }

        // Lock final angle
        targetObject.transform.localRotation = Quaternion.Euler(initialEuler.x, initialEuler.y, endZ);

        // 3. Hide object
        targetObject.SetActive(false);

        isRotating = false;

        OnRotationCompleted?.Invoke();

        // 4. Request navigation unlock
        PageNavigationController.RequestNavigationUnlock();
    }
}