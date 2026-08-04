using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ScrewGaugeController : MonoBehaviour
{
    [System.Serializable]
    public class PageSetting
    {
        [Tooltip("Page Index (0 Based)")]
        public int pageIndex;

        [Range(0f, 1f)]
        [Tooltip("Required Slider Value")]
        public float requiredValue;
    }

    [Header("UI")]
    [SerializeField] private Slider slider;

    [SerializeField] private GameObject correctImage;

    [Header("Per Page Settings")]
    [SerializeField] private List<PageSetting> pageSettings = new();

    [Header("Screw")]
    [SerializeField] private Transform screw;
    [SerializeField] private Transform screwStartPoint;
    [SerializeField] private Transform screwEndPoint;

    [Header("Buttons")]
    [SerializeField] private Button sliderIncreaseButton;
    [SerializeField] private Button sliderDecreaseButton;

    [Header("Thimble")]
    [SerializeField] private Transform thimble;
    [SerializeField] private Transform thimbleStartPoint;
    [SerializeField] private Transform thimbleEndPoint;

    [SerializeField] private float rotations = 1f;

    // Runtime Data
    private Dictionary<int, float> sliderValues = new();
    private HashSet<int> completedPages = new();

    private void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        if (correctImage != null)
            correctImage.SetActive(false);

        slider.onValueChanged.AddListener(OnSliderChanged);

        if (sliderIncreaseButton != null)
        {
            sliderIncreaseButton.onClick.AddListener(() =>
            {
                slider.value += slider.wholeNumbers ? 1f : 0.01f;
            });
        }

        if (sliderDecreaseButton != null)
        {
            sliderDecreaseButton.onClick.AddListener(() =>
            {
                slider.value -= slider.wholeNumbers ? 1f : 0.01f;
            });
        }
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }

    private void Start()
    {
        OnPageChanged(PageNavigationController.CurrentIndex);
    }

    private void OnDestroy()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderChanged);

        if (sliderIncreaseButton != null)
            sliderIncreaseButton.onClick.RemoveAllListeners();

        if (sliderDecreaseButton != null)
            sliderDecreaseButton.onClick.RemoveAllListeners();
    }

private void OnPageChanged(int page)
{
    float value = 0f;

    if (sliderValues.TryGetValue(page, out float savedValue))
        value = savedValue;

    slider.SetValueWithoutNotify(value);

    UpdateGauge(value);

    bool hasSliderTask = TryGetRequiredValue(page, out _);

    // Enable slider only if this page has a required value
    slider.interactable = hasSliderTask && !completedPages.Contains(page);
    sliderIncreaseButton.interactable = hasSliderTask && !completedPages.Contains(page);
    sliderDecreaseButton.interactable = hasSliderTask && !completedPages.Contains(page);

    // Show correct image only for completed slider pages
    if (correctImage != null)
        correctImage.SetActive(hasSliderTask && completedPages.Contains(page));

    // If this page has no slider task, unlock navigation immediately
    if (!hasSliderTask)
    {
        PageNavigationController.RequestNavigationUnlock();
    }
}

    private void OnSliderChanged(float value)
    {
        int currentPage = PageNavigationController.CurrentIndex;

        sliderValues[currentPage] = value;

        UpdateGauge(value);

        CheckPageCompletion(currentPage);
    }

    private void CheckPageCompletion(int page)
    {
        if (completedPages.Contains(page))
            return;

        if (!TryGetRequiredValue(page, out float requiredValue))
        {
            PageNavigationController.RequestNavigationUnlock();
            return;
        }

        if (Mathf.Abs(slider.value - requiredValue) <= 0.01f)
        {
            completedPages.Add(page);

            slider.interactable = false;
            sliderIncreaseButton.interactable = false;
            sliderDecreaseButton.interactable = false;

            if (correctImage != null)
                correctImage.SetActive(true);

            PageNavigationController.RequestNavigationUnlock();

            Debug.Log($"Page {page} Completed");
        }
    }

    private bool TryGetRequiredValue(int page, out float value)
    {
        foreach (PageSetting setting in pageSettings)
        {
            if (setting.pageIndex == page)
            {
                value = setting.requiredValue;
                return true;
            }
        }

        value = 0f;
        return false;
    }

    private void UpdateGauge(float value)
    {
        MoveScrew(value);
        RotateThimble(value);
        MoveThimble(value);
    }

    private void MoveScrew(float value)
    {
        if (screw == null || screwStartPoint == null || screwEndPoint == null)
            return;

        screw.position = Vector3.Lerp(
            screwStartPoint.position,
            screwEndPoint.position,
            value);
    }

    private void RotateThimble(float value)
    {
        if (thimble == null)
            return;

        float angle = value * rotations * 360f;

        // Always rotate around the X axis
        thimble.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }

    private void MoveThimble(float value)
    {
        if( thimble == null || thimbleStartPoint == null || thimbleEndPoint == null)
            return;

        thimble.position = Vector3.Lerp(
            thimbleStartPoint.position,
            thimbleEndPoint.position,
            value);
    }


    public bool IsPageCompleted(int page)
    {
        return completedPages.Contains(page);
    }

    public float GetPageValue(int page)
    {
        if (sliderValues.TryGetValue(page, out float value))
            return value;

        return 0f;
    }

    public void ResetCurrentPage()
    {
        int page = PageNavigationController.CurrentIndex;

        sliderValues[page] = 0f;
        completedPages.Remove(page);

        slider.interactable = true;

        slider.SetValueWithoutNotify(0f);
        UpdateGauge(0f);

        if (correctImage != null)
            correctImage.SetActive(false);
    }

    public void ResetAllPages()
    {
        sliderValues.Clear();
        completedPages.Clear();

        slider.interactable = true;

        slider.SetValueWithoutNotify(0f);
        UpdateGauge(0f);

        if (correctImage != null)
            correctImage.SetActive(false);
    }
}