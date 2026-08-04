using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MultiImageBlinkEffect : MonoBehaviour
{
    [Header("Target Images (any number)")]
    [SerializeField] private List<Image> images = new List<Image>();

    [Header("Blink Settings")]
    [Tooltip("How fast the images fade in/out.")]
    [SerializeField] private float blinkSpeed = 2f;
    [Tooltip("Minimum alpha during blink.")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlpha = 0.2f;
    [Tooltip("Maximum alpha during blink.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 1f;

    [Header("Auto Start")]
    [SerializeField] private bool blinkOnStart = true;

    private bool isBlinking = false;
    private List<Color> baseColors = new List<Color>();

    private void Awake()
    {
        baseColors.Clear();

        foreach (var img in images)
        {
            baseColors.Add(img != null ? img.color : Color.white);
        }
    }

    private void Start()
    {
        if (blinkOnStart)
            StartBlink();
    }

    private void Update()
    {
        if (!isBlinking)
            return;

        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f);

        for (int i = 0; i < images.Count; i++)
        {
            if (images[i] == null)
                continue;

            Color c = baseColors[i];
            c.a = alpha;
            images[i].color = c;
        }
    }

    /// <summary>
    /// Call this to start blinking all images in the list.
    /// </summary>
    public void StartBlink()
    {
        isBlinking = true;
    }

    /// <summary>
    /// Call this to stop blinking and restore all images to their original alpha.
    /// </summary>
    public void StopBlink()
    {
        isBlinking = false;

        for (int i = 0; i < images.Count; i++)
        {
            if (images[i] != null)
                images[i].color = baseColors[i];
        }
    }

    /// <summary>
    /// Toggle blink on/off for all images in the list — handy for a single button hook.
    /// </summary>
    public void ToggleBlink()
    {
        if (isBlinking)
            StopBlink();
        else
            StartBlink();
    }

    /// <summary>
    /// Optional: add an image to the blink list at runtime.
    /// </summary>
    public void AddImage(Image img)
    {
        if (img == null || images.Contains(img))
            return;

        images.Add(img);
        baseColors.Add(img.color);
    }

    /// <summary>
    /// Optional: remove an image from the blink list at runtime.
    /// </summary>
    public void RemoveImage(Image img)
    {
        int index = images.IndexOf(img);
        if (index < 0)
            return;

        img.color = baseColors[index];
        images.RemoveAt(index);
        baseColors.RemoveAt(index);
    }
}