using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class NumpadManager : MonoBehaviour
{
    [Header("Input Fields")]
    public TMP_InputField[] inputFields;

    [Header("Buttons")]
    public Button autoFillButton;

    [Header("Feedback Images")]
    public Image[] feedbackImages;

    [Header("Audio")]

    public AudioSource audioSource;

    public AudioClip correctAudio;
    public AudioClip wrongAudio;

    public Sprite correctSprite;
    public Sprite wrongSprite;

    [Header("Correct Answers")]
    public float[] correctAnswers =
    {
        0.64f,
        -0.02f,
        0.66f
    };


    [Header("Explanation")]
    public GameObject explanationPanel;
    public Button continueButton;
    [Header("Settings")]
    public int maxAttempts = 3;
    public float tolerance = 0.001f;

    [Header("Events")]
    public UnityEvent OnAllAnswersCorrect;

    private int activeFieldIndex = 0;
    private int attempts = 0;


    // True if a field has been auto-filled and locked
    private bool[] lockedFields;

    private TMP_InputField CurrentField
    {
        get { return inputFields[activeFieldIndex]; }
    }

    private void Start()
    {
        lockedFields = new bool[inputFields.Length];

        autoFillButton.gameObject.SetActive(false);

        for (int i = 0; i < inputFields.Length; i++)
        {
            inputFields[i].readOnly = true;


        }
        foreach (Image img in feedbackImages)
        {
            img.gameObject.SetActive(false);
        }
        if (explanationPanel != null)
            explanationPanel.SetActive(false);

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(CloseExplanationPanel);
        }
    }

    //-------------------------------------------------
    // Select Field
    //-------------------------------------------------

    public void SelectField(int index)
    {
        if (index < 0 || index >= inputFields.Length)
            return;

        if (lockedFields[index])
            return;

        activeFieldIndex = index;
    }

    //-------------------------------------------------
    // Number Button
    //-------------------------------------------------
    public void CloseExplanationPanel()
    {
        if (explanationPanel != null)
            explanationPanel.SetActive(false);
    }
    public void PressNumber(string number)
    {
        if (activeFieldIndex < feedbackImages.Length)
        {
            feedbackImages[activeFieldIndex].gameObject.SetActive(false);
        }
        if (lockedFields[activeFieldIndex])
            return;

        string text = CurrentField.text;

        int maxLength;

        if (text.StartsWith("-"))
            maxLength = text.Contains(".") ? 5 : 3;
        else
            maxLength = text.Contains(".") ? 4 : 3;

        if (text.Length >= maxLength)
            return;

        CurrentField.text += number;
    }

    //-------------------------------------------------
    // Decimal Button
    //-------------------------------------------------

    public void PressDot()
    {
        if (activeFieldIndex < feedbackImages.Length)
        {
            feedbackImages[activeFieldIndex].gameObject.SetActive(false);
        }
        if (lockedFields[activeFieldIndex])
            return;

        string text = CurrentField.text;

        if (text.Contains("."))
            return;

        if (text == "")
        {
            CurrentField.text = "0.";
        }
        else if (text == "-")
        {
            CurrentField.text = "-0.";
        }
        else
        {
            CurrentField.text += ".";
        }
    }

    //-------------------------------------------------
    // Minus Button
    //-------------------------------------------------

    public void PressMinus()
    {
        if (activeFieldIndex < feedbackImages.Length)
        {
            feedbackImages[activeFieldIndex].gameObject.SetActive(false);
        }
        if (lockedFields[activeFieldIndex])
            return;

        if (CurrentField.text.StartsWith("-"))
            return;

        CurrentField.text = "-" + CurrentField.text;
    }

    //-------------------------------------------------
    // Backspace
    //-------------------------------------------------

    public void Backspace()
    {
        if (activeFieldIndex < feedbackImages.Length)
        {
            feedbackImages[activeFieldIndex].gameObject.SetActive(false);
        }
        if (lockedFields[activeFieldIndex])
            return;

        if (CurrentField.text.Length == 0)
            return;

        CurrentField.text =
            CurrentField.text.Remove(CurrentField.text.Length - 1);
    }

    //-------------------------------------------------
    // Clear Current Field
    //-------------------------------------------------

    public void ClearCurrentField()
    {
        if (lockedFields[activeFieldIndex])
            return;

        CurrentField.text = "";
    }

    //-------------------------------------------------
    // Check Answers
    //-------------------------------------------------

    public void CheckAnswers()
    {
        // Ignore if current field is already locked
        if (lockedFields[activeFieldIndex])
            return;

        // Empty or invalid value
        if (!float.TryParse(CurrentField.text, out float value))
        {
            WrongAnswer();
            return;
        }
        else
        {
            // Correct answer
            if (Mathf.Abs(value - correctAnswers[activeFieldIndex]) <= tolerance)
            {

                if (audioSource != null && correctAudio != null)
                {
                    audioSource.PlayOneShot(correctAudio);
                }
                Debug.Log("Correct");

                lockedFields[activeFieldIndex] = true;
                feedbackImages[activeFieldIndex].gameObject.SetActive(false);
                feedbackImages[activeFieldIndex].sprite = correctSprite;
                feedbackImages[activeFieldIndex].gameObject.SetActive(true);
                CurrentField.readOnly = true;

                attempts = 0;
                autoFillButton.gameObject.SetActive(false);

                MoveNextField();

                return;
            }
            else
            {
                WrongAnswer();
                return;
            }
        }


    }

    void WrongAnswer()
    {
        if (audioSource != null && wrongAudio != null)
        {
            audioSource.PlayOneShot(wrongAudio);
        }
        Debug.Log("Wrong");

        feedbackImages[activeFieldIndex].sprite = wrongSprite;
        feedbackImages[activeFieldIndex].gameObject.SetActive(true);

        CurrentField.text = "";

        attempts++;

        Debug.Log("Attempts : " + attempts);

        if (attempts >= maxAttempts)
        {
            attempts = 0;
            autoFillButton.gameObject.SetActive(true);
        }
    }

    //-------------------------------------------------
    // Auto Fill
    //-------------------------------------------------

    public void AutoFill()
    {
        if (audioSource != null && correctAudio != null)
        {
            audioSource.PlayOneShot(correctAudio);
        }
        CurrentField.text = correctAnswers[activeFieldIndex].ToString("0.00");

        feedbackImages[activeFieldIndex].sprite = correctSprite;
        feedbackImages[activeFieldIndex].gameObject.SetActive(true);

        lockedFields[activeFieldIndex] = true;
        CurrentField.readOnly = true;

        autoFillButton.gameObject.SetActive(false);

        attempts = 0;

        MoveNextField();

        if (activeFieldIndex < inputFields.Length)
        {
            inputFields[activeFieldIndex].Select();
            inputFields[activeFieldIndex].ActivateInputField();
        }
    }

    //-------------------------------------------------
    // Optional Reset
    //-------------------------------------------------

    public void ResetAll()
    {
        attempts = 0;


        autoFillButton.gameObject.SetActive(false);
        for (int i = 0; i < feedbackImages.Length; i++)
        {
            feedbackImages[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < inputFields.Length; i++)
        {
            inputFields[i].text = "";
            inputFields[i].readOnly = false;
            lockedFields[i] = false;
        }

        activeFieldIndex = 0;
    }

    void MoveNextField()
    {
        for (int i = activeFieldIndex + 1; i < inputFields.Length; i++)
        {
            if (!lockedFields[i])
            {
                activeFieldIndex = i;

                inputFields[i].Select();
                inputFields[i].ActivateInputField();

                return;
            }
        }

        // All fields completed
        Debug.Log("All Fields Completed!");

        if (explanationPanel != null)
        {
            explanationPanel.SetActive(true);
        }

        OnAllAnswersCorrect?.Invoke();
    }
}