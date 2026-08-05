using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class SingleFieldDialerController : MonoBehaviour
{
    [System.Serializable]
    public class PageField
    {
        [Header("References")]
        public TMP_InputField inputField;
        public Image feedbackImage;
        [Header("Audio")]



        [Header("Settings")]
        public float correctAnswer;
        public int pageIndex;

        [HideInInspector]
        public bool solved;
    }

    [Header("Page Fields")]
    public PageField[] pageFields;
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip wrongSound; 

    [Header("Feedback Sprites")]
    public Sprite correctSprite;
    public Sprite wrongSprite;

    [Header("Buttons")]
    public Button validateButton;
    public Button autoFillButton;

    [Header("Settings")]
    public int maxWrongAttempts = 3;
    public float tolerance = 0.001f;

    [Header("Events")]
    public UnityEvent OnCorrectAnswer;
    public UnityEvent OnWrongAnswer;
    public UnityEvent OnAllAnswersVerified;

    private PageNavigationController slideController;

    private int wrongAttempts;
    private bool solved;
    private bool isValidating;

    // Returns the field assigned to the current page
    private PageField CurrentField
    {
        get
        {
            foreach (PageField field in pageFields)
            {
                if (field.pageIndex == PageNavigationController.CurrentIndex)
                    return field;
            }

            return null;
        }
    }

    private TMP_InputField ActiveField
    {
        get
        {
            return CurrentField != null ? CurrentField.inputField : null;
        }
    }

    private float ActiveAnswer
    {
        get
        {
            return CurrentField != null ? CurrentField.correctAnswer : 0f;
        }
    }

    private Image ActiveImage
    {
        get
        {
            return CurrentField != null ? CurrentField.feedbackImage : null;
        }
    }

    private void Start()
    {
        slideController = FindFirstObjectByType<PageNavigationController>();

        if (validateButton != null)
        {
            validateButton.onClick.RemoveAllListeners();
            validateButton.onClick.AddListener(OnValidatePressed);
        }

        if (autoFillButton != null)
        {
            autoFillButton.onClick.RemoveAllListeners();
            autoFillButton.onClick.AddListener(AutoFill);
        }

        ResetAll();
    }

    IEnumerator ShowWrongIconRoutine()
    {
        isValidating = true;

        if (ActiveImage != null)
        {
            ActiveImage.sprite = wrongSprite;
            ActiveImage.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(0.7f);

        if (ActiveImage != null)
            ActiveImage.gameObject.SetActive(false);

        if (ActiveField != null)
        {
            ActiveField.text = "";
            ActiveField.Select();
            ActiveField.ActivateInputField();
        }

        isValidating = false;
    }
    public void OnDigitPressed(string digit)
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (!ActiveField.interactable)
            return;

        // Maximum 4 characters
        int maxLength = ActiveAnswer.ToString().Contains(".") ? 4 : 3;

        if (ActiveField.text.Length >= maxLength)
            return;

        ActiveField.text += digit;
    }

    public void OnDecimalPressed()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (!ActiveField.interactable)
            return;

        int maxLength = ActiveAnswer.ToString().Contains(".") ? 4 : 3;

        if (ActiveField.text.Length >= maxLength)
            return;

        if (!ActiveField.text.Contains("."))
        {
            if (ActiveField.text == "")
                ActiveField.text = "0.";
            else
                ActiveField.text += ".";
        }
    }

    public void OnBackspacePressed()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (!ActiveField.interactable)
            return;

        if (ActiveField.text.Length > 0)
        {
            ActiveField.text =
                ActiveField.text.Substring(0, ActiveField.text.Length - 1);
        }
    }

    public void OnValidatePressed()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        if (string.IsNullOrEmpty(ActiveField.text))
            return;

        if (!float.TryParse(ActiveField.text, out float value))
            return;

        if (Mathf.Abs(value - ActiveAnswer) > tolerance)
        {
            wrongAttempts++;

            if (audioSource != null && wrongSound != null)
            {
                audioSource.PlayOneShot(wrongSound);
            }

            OnWrongAnswer?.Invoke();

            if (wrongAttempts >= maxWrongAttempts && autoFillButton != null)
                autoFillButton.gameObject.SetActive(true);

            StartCoroutine(ShowWrongIconRoutine());
            return;
        }

        StartCoroutine(ValidateAndAdvanceRoutine());
    }

    private IEnumerator ValidateAndAdvanceRoutine()
    {
        isValidating = true;

        if (ActiveImage != null)
        {
            ActiveImage.sprite = correctSprite;
            ActiveImage.gameObject.SetActive(true);
        }

        if (audioSource != null && correctSound != null)
        {
            audioSource.PlayOneShot(correctSound);
        }

        // Lock this field permanently
        if (CurrentField != null)
        {
            CurrentField.solved = true;
            CurrentField.inputField.interactable = false;
        }

        OnCorrectAnswer?.Invoke();

        wrongAttempts = 0;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        yield return null;

        slideController?.EnableNavigationButtons();
        PageNavigationController.RequestNavigationUnlock();

        isValidating = false;

        MoveToNextField();
    }

    public void AutoFill()
    {
        if (solved || isValidating || ActiveField == null)
            return;

        ActiveField.text = ActiveAnswer.ToString();
        OnValidatePressed();
    }
    private void ActivateOnlyCurrentField()
    {
        // Disable all fields first
        foreach (PageField field in pageFields)
        {
            if (field.inputField != null)
                field.inputField.interactable = false;
        }

        // Enable only the field belonging to the current page
        if (CurrentField != null &&
            CurrentField.inputField != null &&
            !CurrentField.solved)
        {
            CurrentField.inputField.interactable = true;
            CurrentField.inputField.Select();
            CurrentField.inputField.ActivateInputField();
        }
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += OnPageChanged;

        // Handles the case where this object is enabled
        // after the page has already changed.
        ActivateOnlyCurrentField();
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= OnPageChanged;
    }

    private void OnPageChanged(int pageIndex)
    {
        ActivateOnlyCurrentField();
    }

    private void FinishPuzzle()
    {
        solved = true;

        if (validateButton != null)
            validateButton.interactable = false;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        slideController?.EnableNavigationButtons();
        PageNavigationController.RequestNavigationUnlock();

        OnAllAnswersVerified?.Invoke();
    }

    public void ResetAll()
    {
        solved = false;
        isValidating = false;
        wrongAttempts = 0;

        if (validateButton != null)
            validateButton.interactable = true;

        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        foreach (PageField field in pageFields)
        {
            field.solved = false;

            if (field.inputField != null)
            {
                field.inputField.text = "";
                field.inputField.interactable = false;
            }

            if (field.feedbackImage != null)
            {
                field.feedbackImage.gameObject.SetActive(false);
            }
        }

        ActivateOnlyCurrentField();
    }

    public void MoveToNextField()
    {
        bool allSolved = true;

        foreach (PageField field in pageFields)
        {
            if (!field.solved)
            {
                allSolved = false;
                break;
            }
        }

        if (allSolved)
        {
            FinishPuzzle();
        }
    }

}