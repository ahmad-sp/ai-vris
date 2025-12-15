using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(Button))]
public class ModernUIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private Vector3 originalScale;
    private Color originalColor;
    
    [Header("Animation Settings")]
    public float hoverScale = 1.05f;
    public float transitionTime = 0.1f;
    public Color hoverColorMultiplier = new Color(1.1f, 1.1f, 1.1f, 1f); // To brighten slightly
    
    private Image targetImage;

    private void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
        
        targetImage = button.targetGraphic as Image;
        if (targetImage != null)
        {
            originalColor = targetImage.color;
        }
    }

    private void OnEnable()
    {
        transform.localScale = originalScale;
        if (targetImage != null) targetImage.color = originalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable) return;
        StopAllCoroutines();
        StartCoroutine(AnimateScale(originalScale * hoverScale));
        if (targetImage != null)
            StartCoroutine(AnimateColor(originalColor * hoverColorMultiplier));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!button.interactable) return;
        StopAllCoroutines();
        StartCoroutine(AnimateScale(originalScale));
        if (targetImage != null)
            StartCoroutine(AnimateColor(originalColor));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable) return;
        StopAllCoroutines();
        StartCoroutine(AnimateScale(originalScale * 0.95f));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!button.interactable) return;
        StopAllCoroutines();
        StartCoroutine(AnimateScale(originalScale * hoverScale)); // Return to hover state
    }

    private IEnumerator AnimateScale(Vector3 target)
    {
        float elapsed = 0f;
        Vector3 start = transform.localScale;
        while (elapsed < transitionTime)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, target, elapsed / transitionTime);
            yield return null;
        }
        transform.localScale = target;
    }

    private IEnumerator AnimateColor(Color target)
    {
        if (targetImage == null) yield break;
        
        float elapsed = 0f;
        Color start = targetImage.color;
        while (elapsed < transitionTime)
        {
            elapsed += Time.deltaTime;
            targetImage.color = Color.Lerp(start, target, elapsed / transitionTime);
            yield return null;
        }
        targetImage.color = target;
    }
}
