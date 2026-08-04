using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class RotateImage : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;
    [SerializeField] private float rotationSpeed = 100f;
    private Image imageToRotate;

    void Start()
    {
        StartCoroutine(RotateRoutine());
        imageToRotate = GetComponent<Image>();
    }

    private IEnumerator RotateRoutine()
    {
        while (true)
        {
            imageToRotate.transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);
            yield return null;
        }
    }
}
