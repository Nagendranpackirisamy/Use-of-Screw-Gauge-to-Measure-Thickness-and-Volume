using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))]
public class DraggableImageInitiator :
    MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public AdditiveHighlightDragManager manager;
    public Canvas canvas;

    [Header("Return Animation")]
    public float returnDuration = 0.25f;

    [Header("Scale Animation")]
    public float dragScale = 1.2f;
    public float scaleDuration = 0.15f;

    [Header("Drag Events")]
    public UnityEvent OnDragStart;
    public UnityEvent OnDragging;
    public UnityEvent OnDragEnd;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private Vector2 startAnchoredPos;
    private Vector3 startScale;
    private Vector2 dragOffset;

    Coroutine scaleRoutine;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        startScale = rectTransform.localScale;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        startAnchoredPos = rectTransform.anchoredPosition;
        canvasGroup.blocksRaycasts = false;

        StartScale(Vector3.one * dragScale);

        RectTransform parentRect = rectTransform.parent as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        dragOffset = localPoint - rectTransform.anchoredPosition;

        if (manager != null)
            manager.BeginDrag(rectTransform);

        // 🔔 Drag start event
        OnDragStart?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransform parentRect = rectTransform.parent as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        rectTransform.anchoredPosition = localPoint - dragOffset;

        // 🔔 Dragging event (called every frame while dragging)
        OnDragging?.Invoke();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        bool success = false;
        if (manager != null)
            success = manager.TryDrop(eventData.position);

        if (success)
        {
            gameObject.SetActive(false);
        }
        else
        {
            StartCoroutine(LerpBack());
            StartScale(startScale);
        }

        // 🔔 Drag end event
        OnDragEnd?.Invoke();
    }

    private IEnumerator LerpBack()
    {
        float t = 0f;
        Vector2 from = rectTransform.anchoredPosition;

        while (t < 1f)
        {
            t += Time.deltaTime / returnDuration;
            rectTransform.anchoredPosition =
                Vector2.Lerp(from, startAnchoredPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = startAnchoredPos;
    }

    void StartScale(Vector3 targetScale)
    {
        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        scaleRoutine = StartCoroutine(ScaleRoutine(targetScale));
    }

    IEnumerator ScaleRoutine(Vector3 target)
    {
        float t = 0f;
        Vector3 from = rectTransform.localScale;

        while (t < 1f)
        {
            t += Time.deltaTime / scaleDuration;
            rectTransform.localScale = Vector3.Lerp(from, target, t);
            yield return null;
        }

        rectTransform.localScale = target;
    }
}