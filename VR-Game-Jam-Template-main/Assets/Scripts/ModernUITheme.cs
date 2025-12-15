using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModernUITheme : MonoBehaviour
{
    [Header("Color Palette")]
    public Color primaryColor = new Color(0.2f, 0.4f, 0.8f); // Soft Blue
    public Color secondaryColor = new Color(0.15f, 0.15f, 0.2f); // Dark Grey/Blue
    public Color accentColor = new Color(0.0f, 0.8f, 0.6f); // Teal/Cyan
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.12f); // Very Dark Grey
    public Color textColor = Color.white;
    public Color subTextColor = new Color(0.7f, 0.7f, 0.7f);

    [Header("Settings")]
    public TMP_FontAsset mainFont; // Optional: Assign a font asset
    public float cornerRadius = 10f; // For sprites if supported (sliced)

    [ContextMenu("Apply Modern Theme")]
    public void ApplyTheme()
    {
        ApplyToHierarchy(transform);
    }

    private void ApplyToHierarchy(Transform root)
    {
        // Recursively apply to all children
        foreach (Transform child in root)
        {
            ApplyStyle(child.gameObject);
            ApplyToHierarchy(child);
        }
    }

    private void ApplyStyle(GameObject obj)
    {
        // 1. Buttons
        Button btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            Image btnImg = obj.GetComponent<Image>();
            if (btnImg != null)
            {
                // Determine if it's a primary or secondary action? 
                // For now, default to Primary. Maybe check name.
                if (obj.name.ToLower().Contains("cancel") || obj.name.ToLower().Contains("back"))
                    btnImg.color = secondaryColor;
                else
                    btnImg.color = primaryColor;
            }

            // Add Modern interaction if missing
            if (obj.GetComponent<ModernUIButton>() == null)
            {
                obj.AddComponent<ModernUIButton>();
            }

            // Adjust text inside button
            TextMeshProUGUI btnText = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.color = textColor;
                if (mainFont != null) btnText.font = mainFont;
            }
        }

        // 2. Texts (that are not inside buttons, handled above)
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt != null && btn == null)
        {
            if (obj.name.ToLower().Contains("title") || obj.name.ToLower().Contains("header"))
            {
                txt.color = accentColor;
                txt.fontSizeMax = 36;
            }
            else if (obj.name.ToLower().Contains("sub") || obj.name.ToLower().Contains("desc"))
            {
                txt.color = subTextColor;
            }
            else
            {
                txt.color = textColor;
            }
            
            if (mainFont != null) txt.font = mainFont;
        }

        // 3. Panels (Images that are not buttons)
        Image img = obj.GetComponent<Image>();
        if (img != null && btn == null && obj.name.ToLower().Contains("panel"))
        {
            img.color = backgroundColor;
        }
        
        // 4. Input Fields
        TMP_InputField input = obj.GetComponent<TMP_InputField>();
        if (input != null)
        {
            Image bg = input.GetComponent<Image>();
            if (bg != null) bg.color = secondaryColor;
            
            if (input.textComponent != null) input.textComponent.color = textColor;
            if (input.placeholder != null && input.placeholder is TextMeshProUGUI placeholder)
                placeholder.color = subTextColor;
        }
        
        // 5. Spinners
        if (obj.name.ToLower().Contains("spinner") || obj.name.ToLower().Contains("load"))
        {
             // Ensure it has rotation script
             if (obj.GetComponent<rotatespinner>() == null)
                 obj.AddComponent<rotatespinner>();
             
             // Color it accent
             if (img != null) img.color = accentColor;
        }
    }
}
