using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class DropdownMainButton : MonoBehaviour
{
    public enum SpawnSide { Left, Right }

    [Header("Data")]
    [SerializeField] private DropdownData data;

    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private RectTransform popupPrefab;
    [SerializeField] private Transform popupParent;

    [Header("Output")]
    [SerializeField] private TMP_Text outputText;

    [Header("Feedback Prefabs")]
    [SerializeField] private GameObject correctPrefab;
    [SerializeField] private GameObject wrongPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private SpawnSide spawnSide = SpawnSide.Right;
    [SerializeField] private float extraOffset = 10f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctClip;
    [SerializeField] private AudioClip wrongClip;

    [Header("Camera")]
    private GlobalCameraController cameraController;
    [SerializeField] private Transform cameraResetPoint;

    [Header("Optional Behaviour")]
    [SerializeField] private bool disableButtonOnCorrect = true;

    [Header("Popup Animation")]
    [SerializeField] private DropdownPopup.Direction direction;
    [SerializeField] private float animDuration = 0.25f;
    [SerializeField] private float distance = 100f;

    private RectTransform rectTransform;
    private RectTransform currentPopup;
    private DropdownPopup popupScript;
    private GameObject currentFeedback;

    private bool hasSelected = true;

    // Tracks whether this dropdown has been answered correctly
    private bool isAnsweredCorrectly = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (button != null)
            button.onClick.AddListener(OnMainClick);

        cameraController = FindFirstObjectByType<GlobalCameraController>();
    }

    private void OnMainClick()
    {
        if (currentPopup != null && !hasSelected)
            return;

        if (currentPopup != null)
        {
            currentPopup.gameObject.SetActive(true);

            popupScript.ApplyAnimationSettings(direction, animDuration, distance);
            popupScript.PlayAnimation();

            hasSelected = false;
            return;
        }

        currentPopup = Instantiate(popupPrefab, popupParent);
        currentPopup.position = transform.position;

        popupScript = currentPopup.GetComponent<DropdownPopup>();

        popupScript.ApplyAnimationSettings(direction, animDuration, distance);
        popupScript.Initialize(data, this);

        hasSelected = false;
    }

    /// <summary>
    /// Returns whether this dropdown is currently correct.
    /// </summary>
    public bool IsCorrect()
    {
        return isAnsweredCorrectly;
    }

    /// <summary>
    /// Checks whether every DropdownMainButton in the scene is correct.
    /// </summary>
    private bool AreAllDropdownsCorrect()
    {
        DropdownMainButton[] dropdowns = FindObjectsOfType<DropdownMainButton>(true);

        foreach (DropdownMainButton dropdown in dropdowns)
        {
            if (!dropdown.IsCorrect())
                return false;
        }

        return true;
    }

    public void OnOptionSelected(int index, bool isCorrect, string text)
    {
        hasSelected = true;

        // Save current state
        isAnsweredCorrectly = isCorrect;

        // Update output text
        if (outputText != null)
            outputText.text = text;

        // Remove previous feedback
        if (currentFeedback != null)
            Destroy(currentFeedback);

        // Select feedback prefab
        GameObject prefab = isCorrect ? correctPrefab : wrongPrefab;

        if (prefab != null)
        {
            currentFeedback = Instantiate(prefab, transform);

            RectTransform feedbackRect = currentFeedback.GetComponent<RectTransform>();

            float halfWidth = rectTransform.rect.width * 0.5f;
            float directionMultiplier = (spawnSide == SpawnSide.Right) ? 1f : -1f;
            float xPos = (halfWidth * directionMultiplier) + (extraOffset * directionMultiplier);

            feedbackRect.anchoredPosition = new Vector2(xPos, 0f);
            feedbackRect.localScale = Vector3.one;
        }

        // Play audio
        if (audioSource != null)
        {
            AudioClip clip = isCorrect ? correctClip : wrongClip;

            if (clip != null)
                audioSource.PlayOneShot(clip);
        }


        if (isCorrect)
        {
            //Camera Position Restart
            cameraController.MoveTo(cameraResetPoint);

            // Unlock navigation ONLY if every dropdown is correct
            if (AreAllDropdownsCorrect())
            {
                Debug.Log("All dropdowns are correct. Navigation unlocked.");
                PageNavigationController.RequestNavigationUnlock();
            }

            // Disable this dropdown after correct answer
            if (disableButtonOnCorrect)
            {
            //    Image img = GetComponent<Image>();
            //    if (img != null)
            //        Destroy(img);

                if (button != null)
                    button.interactable = false;
            
            }

            // Destroy popup
            if (currentPopup != null)
            {
                Destroy(currentPopup.gameObject);
                currentPopup = null;
                popupScript = null;
            }
        }
        else
        {
            // Hide popup if wrong
            if (currentPopup != null)
                currentPopup.gameObject.SetActive(false);
        }
    }
}