using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class ScrewGaugeRotateController : MonoBehaviour
{
    [System.Serializable]
    public class PageRotationConfig
    {
        [Tooltip("Page Index in PageNavigationController (0-based)")]
        public int pageIndex;

        [Header("Target Movement & Rotation")]
        [Tooltip("Total number of full 360-degree rotations to perform")]
        public int numberOfRotations = 4;

        [Tooltip("Distance the thimble moves per full rotation (pitch distance)")]
        public float distancePerRotation = 0.5f;

        [Tooltip("Axis of linear movement (e.g., Vector3.right, Vector3.forward, Vector3.left)")]
        public Vector3 moveDirection = Vector3.left;

        [Tooltip("Axis of rotation (e.g., Vector3.right)")]
        public Vector3 rotationAxis = Vector3.right;

        [Header("Duration & Speed")]
        [Tooltip("Duration in seconds for ONE full 360-degree rotation")]
        public float timePerRotation = 1.0f;

        [Header("Sequential Highlights")]
        [Tooltip("Image/Mesh highlights to enable one by one after each rotation completes")]
        public GameObject[] highlightObjects;

        [Header("Page Events")]
        public UnityEvent OnPageRotationCompleted;
    }

    [Header("Screw Gauge References")]
    [SerializeField] private Transform thimbleTransform;
    [SerializeField] private GameObject curvedArrowFX;
    [SerializeField] private TMP_Text counterText;

    [Header("UI Trigger Button")]
    [SerializeField] private Button rotateButton;

    [Header("Page Configurations Array")]
    [SerializeField] private PageRotationConfig[] pageConfigs;

    [Header("Global Events")]
    public UnityEvent OnRotationStarted;
    public UnityEvent OnAllRotationsCompletedGlobal;

    private int activePageIndex = -1;
    private PageRotationConfig currentConfig = null;
    private bool isRotating = false;

    // Persistence tracking across page navigation
    private readonly HashSet<int> completedPages = new HashSet<int>();
    private readonly Dictionary<int, Vector3> completedPositions = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, Quaternion> completedRotations = new Dictionary<int, Quaternion>();

    private void Awake()
    {
        if (thimbleTransform == null)
            thimbleTransform = transform;
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Start()
    {
        if (rotateButton != null)
        {
            rotateButton.onClick.RemoveAllListeners();
            rotateButton.onClick.AddListener(OnRotateButtonClicked);
        }

        if (counterText != null)
            counterText.gameObject.SetActive(false);

        if (curvedArrowFX != null)
            curvedArrowFX.SetActive(false);

        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void HandlePageChanged(int pageIndex)
    {
        activePageIndex = pageIndex;
        isRotating = false;

        currentConfig = GetConfigByPageIndex(pageIndex);

        // Hide temporary FX and Counter text
        if (curvedArrowFX != null) curvedArrowFX.SetActive(false);
        if (counterText != null) counterText.gameObject.SetActive(false);

        if (currentConfig != null)
        {
            bool isAlreadyCompleted = completedPages.Contains(pageIndex);

            if (rotateButton != null)
                rotateButton.interactable = !isAlreadyCompleted;

            // Ensure highlights are maintained if page was previously finished
            ResetHighlightsForConfig(currentConfig, isAlreadyCompleted);

            // Restore exact final position and rotation to prevent PersistentAssetController from resetting it
            if (isAlreadyCompleted && completedPositions.ContainsKey(pageIndex))
            {
                StartCoroutine(ForceRestoreTransformNextFrame(pageIndex));
            }
        }
        else
        {
            if (rotateButton != null)
                rotateButton.interactable = false;
        }
    }

    /// <summary>
    /// Forces final position/rotation after other controllers (like PersistentAssetController) update on page change.
    /// </summary>
    private IEnumerator ForceRestoreTransformNextFrame(int pageIndex)
    {
        yield return null; // Wait 1 frame for external page scripts to execute

        if (completedPositions.TryGetValue(pageIndex, out Vector3 targetPos))
        {
            thimbleTransform.position = targetPos;
        }

        if (completedRotations.TryGetValue(pageIndex, out Quaternion targetRot))
        {
            thimbleTransform.rotation = targetRot;
        }
    }

    public void OnRotateButtonClicked()
    {
        if (isRotating || currentConfig == null || completedPages.Contains(activePageIndex))
            return;

        StartCoroutine(AnimateRotationRoutine(currentConfig));
    }

    private IEnumerator AnimateRotationRoutine(PageRotationConfig config)
    {
        isRotating = true;

        if (rotateButton != null)
            rotateButton.interactable = false;

        // Show FX & Counter
        if (curvedArrowFX != null)
            curvedArrowFX.SetActive(true);

        if (counterText != null)
        {
            counterText.text = "0";
            counterText.gameObject.SetActive(true);
        }

        OnRotationStarted?.Invoke();

        Vector3 startPosition = thimbleTransform.position;
        Quaternion startRotation = thimbleTransform.rotation;

        int totalRotations = config.numberOfRotations;
        float totalDistance = totalRotations * config.distancePerRotation;
        float totalTime = totalRotations * config.timePerRotation;

        float elapsed = 0f;
        int currentCompletedRotations = 0;

        while (elapsed < totalTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / totalTime);

            // 1. Translate thimble along movement direction
            thimbleTransform.position = startPosition + (config.moveDirection.normalized * (totalDistance * progress));

            // 2. Rotate thimble continuously
            float currentAngle = progress * (totalRotations * 360f);
            thimbleTransform.rotation = startRotation * Quaternion.AngleAxis(currentAngle, config.rotationAxis);

            // 3. Track discrete rotation numbers (1, 2, 3...)
            int completedNow = Mathf.FloorToInt(progress * totalRotations);
            if (completedNow > currentCompletedRotations && completedNow <= totalRotations)
            {
                currentCompletedRotations = completedNow;

                // Update text counter
                if (counterText != null)
                    counterText.text = currentCompletedRotations.ToString();

                // Enable corresponding sequential highlight ring
                int highlightIndex = currentCompletedRotations - 1;
                if (config.highlightObjects != null && highlightIndex >= 0 && highlightIndex < config.highlightObjects.Length)
                {
                    if (config.highlightObjects[highlightIndex] != null)
                        config.highlightObjects[highlightIndex].SetActive(true);
                }
            }

            yield return null;
        }

        // Lock final target position and rotation precisely
        Vector3 finalPos = startPosition + (config.moveDirection.normalized * totalDistance);
        Quaternion finalRot = startRotation * Quaternion.AngleAxis(totalRotations * 360f, config.rotationAxis);

        thimbleTransform.position = finalPos;
        thimbleTransform.rotation = finalRot;

        if (counterText != null)
            counterText.text = totalRotations.ToString();

        // Hide curved arrow FX
        if (curvedArrowFX != null)
            curvedArrowFX.SetActive(false);

        // Record page completion and save final transform state for persistence on back navigation
        completedPages.Add(activePageIndex);
        completedPositions[activePageIndex] = finalPos;
        completedRotations[activePageIndex] = finalRot;

        isRotating = false;

        config.OnPageRotationCompleted?.Invoke();
        OnAllRotationsCompletedGlobal?.Invoke();

        // Unlock navigation in PageNavigationController
        PageNavigationController.RequestNavigationUnlock();
    }

    private void ResetHighlightsForConfig(PageRotationConfig config, bool enableAll)
    {
        if (config == null || config.highlightObjects == null) return;

        foreach (GameObject obj in config.highlightObjects)
        {
            if (obj != null)
                obj.SetActive(enableAll);
        }
    }

    private PageRotationConfig GetConfigByPageIndex(int index)
    {
        if (pageConfigs == null) return null;

        foreach (var config in pageConfigs)
        {
            if (config != null && config.pageIndex == index)
                return config;
        }

        return null;
    }
}