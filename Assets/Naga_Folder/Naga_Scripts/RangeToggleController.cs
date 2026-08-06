using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RangeToggleController : MonoBehaviour
{
    [System.Serializable]
    public class PageRangeConfig
    {
        [Header("Page Range (Inclusive)")]
        [Tooltip("Starting page index (e.g. 0)")]
        public int startPageIndex;

        [Tooltip("Ending page index (e.g. 3)")]
        public int endPageIndex;

        [Header("Range Specific Object (NumPad)")]
        [Tooltip("The object for this page range (e.g. NumPad) that swaps visibility with common object")]
        public GameObject rangeSpecificObject;

        [Header("Button Text Settings")]
        [Tooltip("Button text displayed when the common object is visible")]
        public string textWhenCommonVisible = "Show Image";

        [Tooltip("Button text displayed when the range-specific object is visible")]
        public string textWhenSpecificVisible = "Show Numpad";
    }

    [System.Serializable]
    public class CalculatorRangeConfig
    {
        [Tooltip("Starting page index for calculator")]
        public int startPageIndex;

        [Tooltip("Ending page index for calculator")]
        public int endPageIndex;
    }

    [Header("Common Reference")]
    [Tooltip("The common object that hides/unhides across all page ranges")]
    [SerializeField] private GameObject commonObject;

    [Header("UI Controls")]
    [Tooltip("Toggle button for swapping Common Object and Range Specific Object")]
    [SerializeField] private Button toggleButton;

    [Tooltip("Dedicated NumPad Toggle Button")]
    [SerializeField] private Button numpadButton;

    [Tooltip("Optional TMP_Text reference on the toggle button")]
    [SerializeField] private TMP_Text buttonTmpText;

    [Tooltip("Optional standard UI Text reference on the toggle button")]
    [SerializeField] private Text buttonStandardText;

    [Header("Calculator Settings")]
    [SerializeField] private GameObject calculatorObject;
    [SerializeField] private Button calculatorButton;

    [Tooltip("Multiple page ranges where the calculator is available")]
    [SerializeField] private List<CalculatorRangeConfig> calculatorPageRanges = new List<CalculatorRangeConfig>();

    [Header("NumPad Page Ranges Configuration")]
    [SerializeField] private List<PageRangeConfig> pageRanges = new List<PageRangeConfig>();

    // Runtime state tracking
    private Dictionary<int, bool> rangeStates = new Dictionary<int, bool>();
    private int currentRangeIndex = -1;
    private bool isCalculatorActive = false;

    private void Awake()
    {
        if (toggleButton != null)
        {
            if (buttonTmpText == null)
                buttonTmpText = toggleButton.GetComponentInChildren<TMP_Text>();

            if (buttonStandardText == null)
                buttonStandardText = toggleButton.GetComponentInChildren<Text>();
        }
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
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveAllListeners();
            toggleButton.onClick.AddListener(OnToggleButtonClicked);
        }

        if (numpadButton != null)
        {
            numpadButton.onClick.RemoveAllListeners();
            numpadButton.onClick.AddListener(OnNumPadButtonClicked);
        }

        if (calculatorButton != null)
        {
            calculatorButton.onClick.RemoveAllListeners();
            calculatorButton.onClick.AddListener(OnCalculatorButtonClicked);
        }

        HideAllRangeSpecificObjectsInitial();

        if (calculatorObject != null)
            calculatorObject.SetActive(false);

        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void HandlePageChanged(int pageIndex)
    {
        currentRangeIndex = GetRangeIndexForPage(pageIndex);
        bool isCalculatorPage = IsPageInCalculatorRanges(pageIndex);

        // Update Calculator Button state on page change
        if (calculatorButton != null)
            calculatorButton.interactable = isCalculatorPage;

        if (!isCalculatorPage)
        {
            isCalculatorActive = false;
            if (calculatorObject != null)
                calculatorObject.SetActive(false);
        }

        // Update Toggle & NumPad Buttons state on page change
        if (currentRangeIndex != -1)
        {
            if (toggleButton != null) toggleButton.interactable = true;
            if (numpadButton != null) numpadButton.interactable = true;

            bool isToggled = rangeStates.ContainsKey(currentRangeIndex) && rangeStates[currentRangeIndex];
            ApplyState(currentRangeIndex, isToggled);
        }
        else
        {
            if (toggleButton != null) toggleButton.interactable = false;
            if (numpadButton != null) numpadButton.interactable = false;

            if (commonObject != null)
                commonObject.SetActive(false);

            HideAllRangeObjectsExcept(-1);
        }
    }

    /// <summary>
    /// Toggle Button: Swaps commonObject vs active rangeSpecificObject
    /// </summary>
    public void OnToggleButtonClicked()
    {
        if (currentRangeIndex == -1)
            return;

        // Close calculator to prevent overlap
        CloseCalculator();

        bool currentState = rangeStates.ContainsKey(currentRangeIndex) && rangeStates[currentRangeIndex];
        bool newState = !currentState;

        rangeStates[currentRangeIndex] = newState;
        ApplyState(currentRangeIndex, newState);
    }

    /// <summary>
    /// Dedicated NumPad Button: Explicitly toggles the active range-specific NumPad object
    /// </summary>
    public void OnNumPadButtonClicked()
    {
        if (currentRangeIndex == -1)
            return;

        // Close calculator to prevent overlap
        CloseCalculator();

        PageRangeConfig config = pageRanges[currentRangeIndex];
        if (config == null || config.rangeSpecificObject == null)
            return;

        bool isCurrentlyActive = config.rangeSpecificObject.activeSelf;
        bool newState = !isCurrentlyActive;

        rangeStates[currentRangeIndex] = newState;
        ApplyState(currentRangeIndex, newState);
    }

    /// <summary>
    /// Dedicated Calculator Button: Toggles Calculator object and hides NumPad/Range objects
    /// </summary>
    public void OnCalculatorButtonClicked()
    {
        int currentPage = PageNavigationController.CurrentIndex;
        if (!IsPageInCalculatorRanges(currentPage) || calculatorObject == null)
            return;

        isCalculatorActive = !isCalculatorActive;
        calculatorObject.SetActive(isCalculatorActive);

        // If Calculator opens, automatically close NumPad/Range-Specific objects to prevent overlap
        if (isCalculatorActive && currentRangeIndex != -1)
        {
            rangeStates[currentRangeIndex] = false;
            PageRangeConfig config = pageRanges[currentRangeIndex];

            if (config != null && config.rangeSpecificObject != null)
            {
                config.rangeSpecificObject.SetActive(false);
            }

            if (commonObject != null)
                commonObject.SetActive(true);

            UpdateButtonText(config.textWhenCommonVisible);
        }
    }

    private void ApplyState(int activeRangeIdx, bool toggled)
    {
        HideAllRangeObjectsExcept(activeRangeIdx);

        PageRangeConfig config = pageRanges[activeRangeIdx];

        if (toggled)
        {
            // Show range-specific NumPad, hide common object
            if (commonObject != null) commonObject.SetActive(false);
            if (config.rangeSpecificObject != null) config.rangeSpecificObject.SetActive(true);

            UpdateButtonText(config.textWhenSpecificVisible);
        }
        else
        {
            // Show common object, hide range-specific NumPad
            if (commonObject != null) commonObject.SetActive(true);
            if (config.rangeSpecificObject != null) config.rangeSpecificObject.SetActive(false);

            UpdateButtonText(config.textWhenCommonVisible);
        }
    }

    private void CloseCalculator()
    {
        if (calculatorObject != null && calculatorObject.activeSelf)
        {
            isCalculatorActive = false;
            calculatorObject.SetActive(false);
        }
    }

    private void HideAllRangeSpecificObjectsInitial()
    {
        for (int i = 0; i < pageRanges.Count; i++)
        {
            if (pageRanges[i] != null && pageRanges[i].rangeSpecificObject != null)
            {
                pageRanges[i].rangeSpecificObject.SetActive(false);
            }
        }
    }

    private void HideAllRangeObjectsExcept(int activeIndex)
    {
        for (int i = 0; i < pageRanges.Count; i++)
        {
            if (i != activeIndex && pageRanges[i] != null && pageRanges[i].rangeSpecificObject != null)
            {
                pageRanges[i].rangeSpecificObject.SetActive(false);
            }
        }
    }

    private void UpdateButtonText(string text)
    {
        if (buttonTmpText != null)
            buttonTmpText.text = text;

        if (buttonStandardText != null)
            buttonStandardText.text = text;
    }

    private int GetRangeIndexForPage(int pageIndex)
    {
        for (int i = 0; i < pageRanges.Count; i++)
        {
            if (pageRanges[i] != null && pageIndex >= pageRanges[i].startPageIndex && pageIndex <= pageRanges[i].endPageIndex)
            {
                return i;
            }
        }
        return -1;
    }

    private bool IsPageInCalculatorRanges(int pageIndex)
    {
        if (calculatorPageRanges == null) return false;

        foreach (var range in calculatorPageRanges)
        {
            if (range != null && pageIndex >= range.startPageIndex && pageIndex <= range.endPageIndex)
            {
                return true;
            }
        }
        return false;
    }
}