using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class SingleFieldDialerController : MonoBehaviour
{
    [Header("Input")]
    public TMP_InputField[] answerFields;
    [Tooltip("Which answer field is currently active (0-based)")]
    public int activeFieldIndex = 0;

    [Header("Correct Answer")]
    public float[] correctAnswers;

    [Header("Image Slots & Feedback Sprites")]
    [Tooltip("4 Image slots corresponding to each answer field")]
    public Image[] answerImageSlots = new Image[4];
    public Sprite correctSprite;
    public Sprite wrongSprite;

    private Image ActiveImageSlot => (activeFieldIndex >= 0 && activeFieldIndex < answerImageSlots.Length) ? answerImageSlots[activeFieldIndex] : null;

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

    private int wrongAttempts = 0;
    private bool solved = false;
    private bool isValidating = false;

    private PageNavigationController slideController;

    private TMP_InputField ActiveField => (activeFieldIndex >= 0 && activeFieldIndex < answerFields.Length) ? answerFields[activeFieldIndex] : null;
    private float ActiveAnswer => (activeFieldIndex >= 0 && activeFieldIndex < correctAnswers.Length) ? correctAnswers[activeFieldIndex] : 0f;

    void Start()
    {
        slideController = FindFirstObjectByType<PageNavigationController>();

        if (answerFields.Length != correctAnswers.Length)
        {
            Debug.LogError("Answer Fields and Correct Answers arrays must be the same size.");
            enabled = false;
            return;
        }

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

        if (ActiveImageSlot != null)
        {
            ActiveImageSlot.sprite = wrongSprite;
            ActiveImageSlot.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(0.7f);

        if (ActiveImageSlot != null)
        {
            ActiveImageSlot.gameObject.SetActive(false);
        }

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
        if (solved || isValidating || ActiveField == null) return;

        ActiveField.text += digit;
    }

    public void OnDecimalPressed()
    {
        if (solved || isValidating || ActiveField == null) return;

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
        if (solved || isValidating || ActiveField == null) return;

        if (ActiveField.text.Length > 0)
        {
            ActiveField.text = ActiveField.text.Substring(0, ActiveField.text.Length - 1);
        }
    }

    public void OnValidatePressed()
    {
        if (solved || isValidating || ActiveField == null) return;

        // Block validation if field is empty or non-numeric
        if (string.IsNullOrEmpty(ActiveField.text) || !float.TryParse(ActiveField.text, out float value))
            return;

        if (Mathf.Abs(value - ActiveAnswer) > tolerance)
        {
            wrongAttempts++;
            OnWrongAnswer?.Invoke();

            if (autoFillButton != null && wrongAttempts >= maxWrongAttempts)
                autoFillButton.gameObject.SetActive(true);

            StartCoroutine(ShowWrongIconRoutine());
            return;
        }

        // Lock validation during transition to prevent double-clicks from validating the next field simultaneously
        StartCoroutine(ValidateAndAdvanceRoutine());
    }

    private IEnumerator ValidateAndAdvanceRoutine()
    {
        isValidating = true;

        // 1. Mark current field icon as correct
        if (ActiveImageSlot != null)
        {
            ActiveImageSlot.sprite = correctSprite;
            ActiveImageSlot.gameObject.SetActive(true);
        }

        // 2. Lock current field completely
        if (ActiveField != null)
        {
            ActiveField.interactable = false;
        }

        OnCorrectAnswer?.Invoke();

        // 3. Reset attempts for the next individual field
        wrongAttempts = 0;
        if (autoFillButton != null)
            autoFillButton.gameObject.SetActive(false);

        // Wait one frame
        yield return null;

        // Unlock the page
        slideController?.EnableNavigationButtons();
        PageNavigationController.RequestNavigationUnlock();

        // Stop validating
        isValidating = false;
    }

    public void AutoFill()
    {
        if (solved || isValidating || ActiveField == null) return;

        ActiveField.text = ActiveAnswer.ToString();
        OnValidatePressed();
    }

    private void ActivateOnlyCurrentField()
    {
        for (int i = 0; i < answerFields.Length; i++)
        {
            if (answerFields[i] != null)
            {
                // Strict isolation: ONLY activeFieldIndex is interactable, all others disabled
                answerFields[i].interactable = (i == activeFieldIndex);
            }
        }

        if (ActiveField != null)
        {
            ActiveField.Select();
            ActiveField.ActivateInputField();
        }
    }

    void FinishPuzzle()
    {
        solved = true;

        if (validateButton) validateButton.interactable = false;
        if (autoFillButton) autoFillButton.gameObject.SetActive(false);

        // Unlock page navigation upon completing all elements
        slideController?.EnableNavigationButtons();
        PageNavigationController.RequestNavigationUnlock();

        OnAllAnswersVerified?.Invoke();
    }

    public void ResetAll()
    {
        solved = false;
        isValidating = false;
        activeFieldIndex = 0;
        wrongAttempts = 0;

        if (validateButton) validateButton.interactable = true;
        if (autoFillButton) autoFillButton.gameObject.SetActive(false);

        // Reset image slots
        for (int i = 0; i < answerImageSlots.Length; i++)
        {
            if (answerImageSlots[i] != null)
            {
                answerImageSlots[i].gameObject.SetActive(false);
            }
        }

        // Clear all fields and disable all except index 0
        for (int i = 0; i < answerFields.Length; i++)
        {
            if (answerFields[i] != null)
            {
                answerFields[i].text = "";
                answerFields[i].interactable = (i == 0);
            }
        }

        ActivateOnlyCurrentField();
    }
   
    public void MoveToNextField()
{
    if (activeFieldIndex < answerFields.Length - 1)
    {
        activeFieldIndex++;
        ActivateOnlyCurrentField();
    }
    else
    {
        FinishPuzzle();
    }
}

}