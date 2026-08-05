using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// All-in-one Drag & Snap manager for UI (Canvas / RectTransform) elements.
///
/// - draggables[i] can ONLY snap correctly onto snapTargets[i] (index-paired).
/// - Drop is checked using the target's RectTransform bounds (UI bounds check),
///   not distance.
/// - Wrong drop (empty space or wrong target) -> snaps back to its original position.
/// - Correct drop -> snaps to target position and locks permanently (no further
///   dragging, cannot be reset).
/// - OnAllSnapped event fires once, the moment every assigned draggable has been
///   correctly snapped. Hook your Page Flow Manager's "unlock next button"
///   function to this event in the Inspector or via code.
///
/// SETUP:
/// 1. Put this script on any GameObject (e.g. an empty "ScreenToWorldSnapManager" under your Canvas).
/// 2. Assign draggables[] and snapTargets[] in the Inspector, index-matched
///    (draggables[0] belongs to snapTargets[0], etc).
/// 3. Each draggable needs a CanvasGroup component (auto-added at runtime if missing)
///    so it can ignore raycasts while dragging.
/// 4. Each snap target needs a RectTransform with a size (it's used as the drop bounds).
/// </summary>
public class ScreenToWorldSnapManager : MonoBehaviour
{
    [Header("Index-paired lists: draggables[i] snaps correctly onto snapTargets[i]")]
    public List<RectTransform> draggables = new List<RectTransform>();
    public List<RectTransform> snapTargets = new List<RectTransform>();

    [Header("Optional: 'already placed' visuals (index-paired with draggables)")]
    [Tooltip("If assigned, placedImages[i] is a pre-positioned GameObject sitting correctly on the target's own canvas (e.g. World Space). On correct snap, draggables[i] is hidden and placedImages[i] is activated instead   avoids any cross-canvas coordinate mismatch. Leave empty to fall back to reparenting the draggable itself.")]
    public List<GameObject> placedImages = new List<GameObject>();

    [Header("Optional: parent Canvas (auto-found if left empty)")]
    public Canvas canvas;

    [Header("Snap animation")]
    [Tooltip("Seconds to animate into place on snap / return. Set 0 for instant.")]
    public float snapAnimDuration = 0.15f;

    // Fires once, the first time ALL assigned draggables are correctly snapped.
    // (Plain C# event   not shown in Inspector, [Header]/[Tooltip] aren't valid here.)
    public event Action OnAllSnapped;

    [Header("Events")]
    [Tooltip("Optional UnityEvent alternative so you can wire this in the Inspector instead of code.")]
    public UnityEngine.Events.UnityEvent OnAllSnappedUnityEvent;

    // Per-draggable runtime state
    private class DragState
    {
        public RectTransform rect;
        public RectTransform correctTarget;
        public Vector2 originalAnchoredPos;
        public Transform originalParent;
        public int originalSiblingIndex;
        public bool isLocked; // permanently snapped, never resets
        public CanvasGroup canvasGroup;
        public DragHandler handler;
        public GameObject placedImage; // optional pre-placed "already snapped" visual

        // Offset (in the dragged item's parent-local space) between the pointer's
        // local point and the item's anchoredPosition at the moment the drag started.
        // Keeping this constant for the whole drag is what makes it track the
        // finger/cursor precisely instead of snapping its pivot to the pointer.
        public Vector2 grabOffset;
    }

    private readonly Dictionary<RectTransform, DragState> _states = new Dictionary<RectTransform, DragState>();
    private int _correctSnapCount;
    private bool _allSnappedFired;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        SetupAll();
    }

    private void SetupAll()
    {
        if (draggables.Count != snapTargets.Count)
        {
            Debug.LogError($"[ScreenToWorldSnapManager] draggables ({draggables.Count}) and snapTargets ({snapTargets.Count}) counts must match (index-paired). Fix the lists in the Inspector.");
        }

        int count = Mathf.Min(draggables.Count, snapTargets.Count);
        for (int i = 0; i < count; i++)
        {
            RectTransform d = draggables[i];
            RectTransform t = snapTargets[i];
            if (d == null || t == null)
            {
                Debug.LogWarning($"[ScreenToWorldSnapManager] Null entry at index {i}, skipping.");
                continue;
            }

            var state = new DragState
            {
                rect = d,
                correctTarget = t,
                originalAnchoredPos = d.anchoredPosition,
                originalParent = d.parent,
                originalSiblingIndex = d.GetSiblingIndex(),
                isLocked = false
            };

            var cg = d.GetComponent<CanvasGroup>();
            if (cg == null) cg = d.gameObject.AddComponent<CanvasGroup>();
            state.canvasGroup = cg;

            var handler = d.GetComponent<DragHandler>();
            if (handler == null) handler = d.gameObject.AddComponent<DragHandler>();
            handler.Init(this, d);
            state.handler = handler;

            // Optional pre-placed "already snapped" visual, index-paired.
            if (i < placedImages.Count && placedImages[i] != null)
            {
                state.placedImage = placedImages[i];
                state.placedImage.SetActive(false); // hidden until correctly snapped
            }

            _states[d] = state;
        }
    }

    /// <summary>Called by DragHandler when a drag ends.</summary>
    internal void HandleDrop(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return; // safety: locked items shouldn't be draggable at all

        RectTransform hitTarget = FindTargetUnderPointer(draggedRect, eventData);

        if (hitTarget != null && hitTarget == state.correctTarget)
        {
            SnapCorrect(state);
        }
        else
        {
            SnapBack(state);
        }
    }

    /// <summary>
    /// Resolves the correct camera for a SPECIFIC RectTransform's own canvas.
    /// Needed because draggables and snapTargets can live on DIFFERENT canvases
    /// with different render modes (e.g. draggable on Screen Space - Overlay,
    /// snap target on World Space)   each rect must be measured with its OWN
    /// canvas's camera, not the dragged item's camera reused for everything.
    /// </summary>
    private Camera ResolveCameraFor(RectTransform rect, PointerEventData eventData)
    {
        Canvas rectCanvas = rect.GetComponentInParent<Canvas>();
        if (rectCanvas == null) return ResolveCamera(eventData);

        if (rectCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null; // Overlay always uses null camera

        if (rectCanvas.renderMode == RenderMode.WorldSpace)
            return rectCanvas.worldCamera != null ? rectCanvas.worldCamera : Camera.main;

        // Screen Space - Camera
        return rectCanvas.worldCamera != null ? rectCanvas.worldCamera : ResolveCamera(eventData);
    }

    /// <summary>
    /// Returns the correct camera for screen&lt;-&gt;UI space conversions given the current
    /// event, for the MANAGER's own canvas context (used for drag-follow math, where
    /// dragged item and its parent are always on the same canvas).
    /// </summary>
    private Camera ResolveCamera(PointerEventData eventData)
    {
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null; // Overlay mode always uses a null camera by design

        if (eventData != null && eventData.pressEventCamera != null)
            return eventData.pressEventCamera;

        return canvas != null ? canvas.worldCamera : null;
    }

    /// <summary>
    /// Checks every snap target's RectTransform screen-space bounds against the
    /// pointer's current screen position, returns the one it was dropped on (if any).
    /// Each target is measured with ITS OWN canvas's camera (see ResolveCameraFor),
    /// so this works correctly even when targets live on a different canvas/render
    /// mode than the draggable (e.g. World Space snap targets).
    /// </summary>
    private RectTransform FindTargetUnderPointer(RectTransform draggedRect, PointerEventData eventData)
    {
        for (int i = 0; i < snapTargets.Count; i++)
        {
            RectTransform target = snapTargets[i];
            if (target == null) continue;

            Camera targetCam = ResolveCameraFor(target, eventData);
            if (RectTransformUtility.RectangleContainsScreenPoint(target, eventData.position, targetCam))
            {
                return target;
            }
        }
        return null;
    }

    /// <summary>
    /// Snap onto the correct target. Two modes:
    ///
    /// A) placedImage assigned (recommended for cross-canvas setups, e.g. draggable
    ///    on Screen Space - Overlay + target on World Space): the draggable is simply
    ///    HIDDEN and the pre-positioned placedImage is activated instead. No coordinate
    ///    conversion needed at all   placedImage already sits exactly where it should
    ///    because you positioned it by hand on the target's own canvas.
    ///
    /// B) No placedImage assigned: falls back to reparenting the draggable itself into
    ///    the target (SetParent + zero anchoredPosition). Works fine when draggable and
    ///    target share the same canvas / render mode.
    /// </summary>
    private void SnapCorrect(DragState state)
    {
        state.isLocked = true;
        state.canvasGroup.blocksRaycasts = false; // no longer needs to block/receive drag raycasts
        state.handler.enabled = false; // permanently disable further dragging

        if (state.placedImage != null)
        {
            state.rect.gameObject.SetActive(false);
            state.placedImage.SetActive(true);
        }
        else
        {
            // Fallback: reparent the draggable itself onto the target.
            // worldPositionStays = true avoids a visual jump across differing canvas scales.
            state.rect.SetParent(state.correctTarget, true);
            state.rect.SetAsLastSibling();
            state.rect.localRotation = Quaternion.identity;
            state.rect.localScale = Vector3.one;

            StopAndAnimate(state.rect, Vector2.zero);
        }

        _correctSnapCount++;
        CheckAllSnapped();
    }

    /// <summary>
    /// Wrong drop: restore original parent, sibling order, and anchored position exactly.
    /// </summary>
    private void SnapBack(DragState state)
    {
        if (state.rect.parent != state.originalParent)
        {
            state.rect.SetParent(state.originalParent, false);
            state.rect.SetSiblingIndex(state.originalSiblingIndex);
        }
        StopAndAnimate(state.rect, state.originalAnchoredPos);
    }

    private void StopAndAnimate(RectTransform rect, Vector2 targetAnchoredPos)
    {
        var handler = rect.GetComponent<DragHandler>();
        if (handler != null)
        {
            handler.StopAllCoroutines();
            handler.StartCoroutine(handler.AnimateTo(rect, targetAnchoredPos, snapAnimDuration));
        }
        else
        {
            rect.anchoredPosition = targetAnchoredPos;
        }
    }

    private void CheckAllSnapped()
    {
        if (_allSnappedFired) return;
        if (_correctSnapCount >= _states.Count && _states.Count > 0)
        {
            _allSnappedFired = true;
            OnAllSnapped?.Invoke();
            OnAllSnappedUnityEvent?.Invoke();
        }
    }

    // ---- Public helpers ----

    /// <summary>True once every assigned draggable has been correctly snapped.</summary>
    public bool AllSnapped => _allSnappedFired;

    /// <summary>How many draggables are correctly snapped right now.</summary>
    public int CorrectSnapCount => _correctSnapCount;

    public int TotalDraggables => _states.Count;

    /// <summary>
    /// Begin drag: called by DragHandler. Brings element to front and lets raycasts pass through it
    /// so drop-target detection under the pointer works.
    /// </summary>
    internal void HandleBeginDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        state.canvasGroup.blocksRaycasts = false;
        draggedRect.SetAsLastSibling(); // render above everything while dragging

        // Compute where in the parent's local space the pointer currently is,
        // then store the offset from that point to the item's current anchoredPosition.
        // Applying this same offset every frame keeps the exact grab point glued to
        // the pointer instead of snapping the item's pivot to the pointer.
        Camera cam = ResolveCamera(eventData);
        RectTransform parentRect = draggedRect.parent as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, cam, out Vector2 pointerLocal))
        {
            state.grabOffset = draggedRect.anchoredPosition - pointerLocal;
        }
        else
        {
            state.grabOffset = Vector2.zero;
        }
    }

    internal void HandleDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        Camera cam = ResolveCamera(eventData);
        RectTransform parentRect = draggedRect.parent as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, cam, out Vector2 pointerLocal))
        {
            draggedRect.anchoredPosition = pointerLocal + state.grabOffset;
        }
    }

    internal void HandleEndDrag(RectTransform draggedRect, PointerEventData eventData)
    {
        if (!_states.TryGetValue(draggedRect, out DragState state)) return;
        if (state.isLocked) return;

        state.canvasGroup.blocksRaycasts = true;
        HandleDrop(draggedRect, eventData);
    }
}

/// <summary>
/// Lightweight per-element drag handler. Auto-attached by ScreenToWorldSnapManager.
/// Forwards all pointer events to the manager, which owns all the drag/snap logic.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ScreenToWorldSnapManager _manager;
    private RectTransform _rect;

    public void Init(ScreenToWorldSnapManager manager, RectTransform rect)
    {
        _manager = manager;
        _rect = rect;
    }

    public void OnBeginDrag(PointerEventData eventData) => _manager.HandleBeginDrag(_rect, eventData);
    public void OnDrag(PointerEventData eventData) => _manager.HandleDrag(_rect, eventData);
    public void OnEndDrag(PointerEventData eventData) => _manager.HandleEndDrag(_rect, eventData);

    public System.Collections.IEnumerator AnimateTo(RectTransform rect, Vector2 target, float duration)
    {
        if (duration <= 0f)
        {
            rect.anchoredPosition = target;
            yield break;
        }

        Vector2 start = rect.anchoredPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
            rect.anchoredPosition = Vector2.Lerp(start, target, k);
            yield return null;
        }
        rect.anchoredPosition = target;
    }
}