using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Events;

public class AdditiveHighlightDragManager : MonoBehaviour
{
    public Camera mainCamera;

    [Header("UI Images (Order Matters)")]
    public List<RectTransform> uiImages;

    [Header("Target Objects (Hierarchy with Multiple Mesh Renderers)")]
    [Tooltip("The 3D target objects. Can contain multiple child MeshRenderers.")]
    public List<GameObject> targetObjects;

    [Header("Additive Highlight Material")]
    [Tooltip("Material appended to all MeshRenderers on the active target object while dragging.")]
    public Material highlightMaterial;

    [Header("UI Parent To Disable When Snapping Is Complete")]
    [Tooltip("Specific UI parent GameObject (e.g. Panel/Group) to turn off when snapping completes (Not the whole Canvas).")]
    public GameObject targetParentObject;

    [Header("Placement Events")]
    [Tooltip("Triggered every time a single object is successfully placed.")]
    public UnityEvent OnObjectPlaced;

    [Tooltip("Triggered when ALL target objects have been successfully placed.")]
    public UnityEvent OnAllObjectsPlaced;

    private int currentDragIndex = -1;

    // Dictionary tracking all MeshRenderers on the active target and their original materials
    private readonly Dictionary<Renderer, Material[]> activeHighlightedRenderers = new Dictionary<Renderer, Material[]>();
    private readonly HashSet<int> placedIndices = new HashSet<int>();

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Hide all target objects initially
        foreach (var obj in targetObjects)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }

    public void BeginDrag(RectTransform ui)
    {
        currentDragIndex = uiImages.IndexOf(ui);

        if (currentDragIndex < 0)
        {
            Debug.LogError("[AdditiveHighlightDragManager] UI not found in uiImages list.", this);
            return;
        }

        // Clean up any existing active highlights
        RemoveActiveHighlights();

        for (int i = 0; i < targetObjects.Count; i++)
        {
            if (targetObjects[i] == null) continue;

            // Do not modify already placed objects
            if (placedIndices.Contains(i)) continue;

            bool isCurrentTarget = (i == currentDragIndex);

            if (isCurrentTarget)
            {
                // 1. Unhide the target object while dragging
                targetObjects[i].SetActive(true);

                // 2. Find ALL MeshRenderers in child hierarchy and apply additive highlight
                MeshRenderer[] childMeshRenderers = targetObjects[i].GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer meshRend in childMeshRenderers)
                {
                    ApplyAdditiveHighlight(meshRend);
                }

                // Also check for SkinnedMeshRenderers in case of rigged models
                SkinnedMeshRenderer[] childSkinnedRenderers = targetObjects[i].GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (SkinnedMeshRenderer skinnedRend in childSkinnedRenderers)
                {
                    ApplyAdditiveHighlight(skinnedRend);
                }
            }
            else
            {
                // Keep other unplaced target objects hidden
                targetObjects[i].SetActive(false);
            }
        }
    }

    public bool TryDrop(Vector2 screenPosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            int hitIndex = GetTargetIndexFromHit(hit.collider);

            if (hitIndex == currentDragIndex && hitIndex != -1)
            {
                // 1. Strip highlight materials from all MeshRenderers
                RemoveActiveHighlights();

                // 2. Mark as successfully placed (Stays unhidden forever)
                placedIndices.Add(hitIndex);
                targetObjects[hitIndex].SetActive(true);

                Debug.Log("Target Object Placed Permanently: " + targetObjects[hitIndex].name, targetObjects[hitIndex]);
                OnObjectPlaced?.Invoke();

                currentDragIndex = -1;

                // 3. Check if all items are completed
                CheckSnappingCompletion();

                return true;
            }
        }

        // Failed drop - remove highlight materials from all child MeshRenderers and hide the object
        RemoveActiveHighlights();

        if (currentDragIndex >= 0 && currentDragIndex < targetObjects.Count && targetObjects[currentDragIndex] != null)
        {
            if (!placedIndices.Contains(currentDragIndex))
            {
                targetObjects[currentDragIndex].SetActive(false);
            }
        }

        currentDragIndex = -1;
        return false;
    }

    private int GetTargetIndexFromHit(Collider hitCollider)
    {
        if (hitCollider == null) return -1;

        for (int i = 0; i < targetObjects.Count; i++)
        {
            if (targetObjects[i] == null) continue;

            // Checks if hit collider belongs to target object or any child mesh
            if (targetObjects[i] == hitCollider.gameObject || hitCollider.transform.IsChildOf(targetObjects[i].transform))
            {
                return i;
            }
        }

        return -1;
    }

    private void CheckSnappingCompletion()
    {
        if (placedIndices.Count >= targetObjects.Count)
        {
            // ✅ All objects placed
            OnAllObjectsPlaced?.Invoke();

            // Disable only the specific parent UI object (Panel/Group) instead of the whole Canvas
            if (targetParentObject != null)
                targetParentObject.SetActive(false);
        }
    }

    // =========================================================================
    // MULTI MESH RENDERER ADDITIVE HIGHLIGHT LOGIC
    // =========================================================================

    private void ApplyAdditiveHighlight(Renderer rend)
    {
        if (rend == null || highlightMaterial == null) return;

        Material[] currentMats = rend.sharedMaterials;

        // Save original materials for clean removal later
        if (!activeHighlightedRenderers.ContainsKey(rend))
        {
            activeHighlightedRenderers.Add(rend, currentMats);
        }

        // Avoid adding duplicate highlight slots
        foreach (Material mat in currentMats)
        {
            if (mat == highlightMaterial) return;
        }

        // Build new array containing base sub-mesh materials + 1 highlight slot
        Material[] newMats = new Material[currentMats.Length + 1];
        for (int i = 0; i < currentMats.Length; i++)
        {
            newMats[i] = currentMats[i];
        }
        newMats[newMats.Length - 1] = highlightMaterial;

        rend.sharedMaterials = newMats;
    }

    private void RemoveActiveHighlights()
    {
        foreach (KeyValuePair<Renderer, Material[]> kvp in activeHighlightedRenderers)
        {
            if (kvp.Key != null)
            {
                // Restore exact original material array for each child mesh
                kvp.Key.sharedMaterials = kvp.Value;
            }
        }

        activeHighlightedRenderers.Clear();
    }
}