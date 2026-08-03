using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class WireSnapController : MonoBehaviour
{
    [System.Serializable]
    public class TargetSnapPoint
    {
        [Tooltip("The target 3D transform point on the instrument (e.g., stud/screw center)")]
        public Transform snapTargetPoint;

        [Tooltip("The point on the wire itself that aligns with snapTargetPoint")]
        public Transform wirePointToAlign;

        [Tooltip("Highlight object/mesh to activate on page enter and hide when snapped")]
        public GameObject highlightObject;

        [Tooltip("Event triggered specifically when this point snaps")]
        public UnityEvent OnPointSnapped;
    }

    [System.Serializable]
    public class PageSnapConfig
    {
        [Tooltip("Page index in PageNavigationController (0-based)")]
        public int pageIndex;

        [Header("Snap Points Array for this Page")]
        [Tooltip("Array of snap points active on this specific page")]
        public TargetSnapPoint[] snapPoints;

        [Header("Page Events")]
        public UnityEvent OnPageAllPointsSnapped;
    }

    [Header("Wire Drag Target")]
    [SerializeField] private Transform wireTransform;
    [SerializeField] private Collider wireCollider;

    [Header("Camera & Input")]
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private LayerMask wireLayerMask = ~0;

    [Header("Snap Settings")]
    [Tooltip("Proximity distance in world space to trigger auto-snap")]
    [SerializeField] private float snapDistance = 0.15f;

    [Header("Highlight Settings")]
    [Tooltip("Common highlight material applied automatically to all active highlight objects")]
    [SerializeField] private Material globalHighlightMaterial;

    [Header("Page Configurations Array")]
    [SerializeField] private PageSnapConfig[] pageConfigs;

    [Header("Global Events")]
    public UnityEvent OnWireSnappedGlobal;

    private int activePageIndex = -1;
    private PageSnapConfig currentPageConfig = null;
    private int currentSnapPointIndex = 0;
    private bool isDragging = false;
    private bool isPageCompleted = false;

    // Global Z-Axis Position locking variables
    private float lockedX;
    private float lockedY;
    private float dragOffsetZ;
    private Plane dragPlane;

    private readonly HashSet<int> completedPages = new HashSet<int>();

    // Stores the exact snapped wire position for each completed page index
    private readonly Dictionary<int, Vector3> completedSnapPositions = new Dictionary<int, Vector3>();

    private void Awake()
    {
        if (wireTransform == null)
            wireTransform = transform;

        if (wireCollider == null)
            wireCollider = wireTransform.GetComponent<Collider>();

        if (raycastCamera == null)
            raycastCamera = Camera.main;
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
        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    private void Update()
    {
        if (currentPageConfig == null || isPageCompleted)
            return;

        HandleInput();
    }

    private void HandlePageChanged(int pageIndex)
    {
        activePageIndex = pageIndex;
        isDragging = false;

        HideAllHighlightsGlobal();

        currentPageConfig = GetPageConfigByIndex(pageIndex);

        if (currentPageConfig != null)
        {
            isPageCompleted = completedPages.Contains(pageIndex);

            if (isPageCompleted)
            {
                // If page was already completed, restore the wire's snapped position for this page
                if (completedSnapPositions.ContainsKey(pageIndex))
                {
                    wireTransform.position = completedSnapPositions[pageIndex];
                }
            }
            else
            {
                currentSnapPointIndex = 0;
                UpdateActiveHighlight();
            }
        }
    }

    private void HandleInput()
    {
        // Touch Input
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            var phase = touch.phase.ReadValue();
            Vector2 touchPos = touch.position.ReadValue();

            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                TryBeginDrag(touchPos);
            }
            else if ((phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary) && isDragging)
            {
                ContinueDrag(touchPos);
            }
            else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                EndDrag();
            }

            return;
        }

        // Mouse Input
        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryBeginDrag(mousePos);
            }
            else if (Mouse.current.leftButton.isPressed && isDragging)
            {
                ContinueDrag(mousePos);
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndDrag();
            }
        }
    }

    private void TryBeginDrag(Vector2 screenPosition)
    {
        if (raycastCamera == null || wireCollider == null)
            return;

        Ray ray = raycastCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, wireLayerMask) && hit.collider == wireCollider)
        {
            isDragging = true;

            // Lock global X and Y axes so wire only moves strictly along global Z
            lockedX = wireTransform.position.x;
            lockedY = wireTransform.position.y;

            // Use horizontal plane (Vector3.up) passing through wire position for accurate Z translation
            dragPlane = new Plane(Vector3.up, wireTransform.position);

            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                dragOffsetZ = wireTransform.position.z - hitPoint.z;
            }
        }
    }

    private void ContinueDrag(Vector2 screenPosition)
    {
        Ray ray = raycastCamera.ScreenPointToRay(screenPosition);

        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 worldHit = ray.GetPoint(enter);
            float targetZ = worldHit.z + dragOffsetZ;

            // Strictly update Z while keeping global X and Y locked
            wireTransform.position = new Vector3(lockedX, lockedY, targetZ);

            CheckProximityAndSnap();
        }
    }

    private void CheckProximityAndSnap()
    {
        if (currentPageConfig == null || currentPageConfig.snapPoints == null)
            return;

        if (currentSnapPointIndex < 0 || currentSnapPointIndex >= currentPageConfig.snapPoints.Length)
            return;

        TargetSnapPoint currentPoint = currentPageConfig.snapPoints[currentSnapPointIndex];

        if (currentPoint == null || currentPoint.snapTargetPoint == null)
            return;

        Transform alignPoint = (currentPoint.wirePointToAlign != null)
            ? currentPoint.wirePointToAlign
            : wireTransform;

        float distance = Vector3.Distance(alignPoint.position, currentPoint.snapTargetPoint.position);

        if (distance <= snapDistance)
        {
            ExecuteSnap(currentPoint);
        }
    }

    private void ExecuteSnap(TargetSnapPoint point)
    {
        isDragging = false;

        // Position alignment
        if (point.wirePointToAlign != null && point.snapTargetPoint != null)
        {
            Vector3 offset = wireTransform.position - point.wirePointToAlign.position;
            wireTransform.position = point.snapTargetPoint.position + offset;
        }
        else if (point.snapTargetPoint != null)
        {
            wireTransform.position = point.snapTargetPoint.position;
        }

        // Hide highlight
        if (point.highlightObject != null)
        {
            point.highlightObject.SetActive(false);
        }

        point.OnPointSnapped?.Invoke();

        currentSnapPointIndex++;

        if (currentSnapPointIndex < currentPageConfig.snapPoints.Length)
        {
            UpdateActiveHighlight();
        }
        else
        {
            isPageCompleted = true;
            completedPages.Add(activePageIndex);

            // Save the exact final snapped position for this page
            completedSnapPositions[activePageIndex] = wireTransform.position;

            currentPageConfig.OnPageAllPointsSnapped?.Invoke();
            OnWireSnappedGlobal?.Invoke();

            PageNavigationController.RequestNavigationUnlock();
        }
    }

    private void EndDrag()
    {
        isDragging = false;
    }

    private void UpdateActiveHighlight()
    {
        if (currentPageConfig == null || currentPageConfig.snapPoints == null)
            return;

        for (int i = 0; i < currentPageConfig.snapPoints.Length; i++)
        {
            var point = currentPageConfig.snapPoints[i];
            if (point != null && point.highlightObject != null)
            {
                bool shouldActivate = (i == currentSnapPointIndex && !isPageCompleted);
                point.highlightObject.SetActive(shouldActivate);

                if (shouldActivate && globalHighlightMaterial != null)
                {
                    ApplyHighlightMaterial(point.highlightObject);
                }
            }
        }
    }

    private void ApplyHighlightMaterial(GameObject obj)
    {
        if (obj == null || globalHighlightMaterial == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            if (rend != null)
            {
                rend.sharedMaterial = globalHighlightMaterial;
            }
        }
    }

    private void HideAllHighlightsGlobal()
    {
        if (pageConfigs == null) return;

        foreach (var page in pageConfigs)
        {
            if (page == null || page.snapPoints == null) continue;

            foreach (var point in page.snapPoints)
            {
                if (point != null && point.highlightObject != null)
                {
                    point.highlightObject.SetActive(false);
                }
            }
        }
    }

    private PageSnapConfig GetPageConfigByIndex(int index)
    {
        if (pageConfigs == null) return null;

        foreach (var page in pageConfigs)
        {
            if (page != null && page.pageIndex == index)
                return page;
        }

        return null;
    }
}