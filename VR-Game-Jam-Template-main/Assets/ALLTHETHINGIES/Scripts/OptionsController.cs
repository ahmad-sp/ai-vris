using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using System.Collections;

public class OptionsController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject optionsPanel;

    [Header("Volume Sliders (0..1)")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider voiceSlider;

    [Header("Graphics / UI")]
    public TMP_Dropdown qualityDropdown; // Low / Medium / High
    public Slider brightnessSlider;      // 0..1
    public Slider uiScaleSlider;         // 0.75 .. 1.5
    public Slider subtitleSizeSlider;    // e.g. 12 .. 36

    [Header("Optional references (assign if available)")]
    public AudioMixer audioMixer; // optional: expects exposed params "Master", "Music", "SFX", "Voice"
    public Canvas rootCanvas;     // to apply scaleFactor for UI scale
    public TextMeshProUGUI subtitlePreviewText; // preview to apply subtitle size
    public Image uiBrightnessOverlay; // Optional full-screen UI Image (white) set to multiply/blend; used to simulate brightness
    public float panelAnimationDuration = 0.15f;

    // PlayerPrefs keys
    const string KEY_MASTER = "opt_master";
    const string KEY_MUSIC = "opt_music";
    const string KEY_SFX = "opt_sfx";
    const string KEY_VOICE = "opt_voice";
    const string KEY_QUALITY = "opt_quality";
    const string KEY_BRIGHT = "opt_brightness";
    const string KEY_UI_SCALE = "opt_ui_scale";
    const string KEY_SUB_SIZE = "opt_sub_size";

    void Start()
    {
        // hook up listeners (safe even if the user wired in inspector)
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (voiceSlider != null) voiceSlider.onValueChanged.AddListener(OnVoiceChanged);

        if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        if (brightnessSlider != null) brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        if (uiScaleSlider != null) uiScaleSlider.onValueChanged.AddListener(OnUiScaleChanged);
        if (subtitleSizeSlider != null) subtitleSizeSlider.onValueChanged.AddListener(OnSubtitleSizeChanged);

        // Populate quality options if empty
        if (qualityDropdown != null && qualityDropdown.options.Count == 0)
        {
            qualityDropdown.options.Add(new TMP_Dropdown.OptionData("Low"));
            qualityDropdown.options.Add(new TMP_Dropdown.OptionData("Medium"));
            qualityDropdown.options.Add(new TMP_Dropdown.OptionData("High"));
        }

        // Ensure panel is closed at start
        if (optionsPanel != null) optionsPanel.SetActive(false);

        LoadAllSettings();
    }

    #region Open/Close
    public void ToggleOptionsPanel()
    {
        if (optionsPanel == null)
        {
            Debug.LogWarning("[OptionsController] optionsPanel not assigned.");
            return;
        }

        bool open = !optionsPanel.activeSelf;
        if (open)
        {
            optionsPanel.SetActive(true);
            StartCoroutine(AnimatePanelScale(optionsPanel, true));
            LoadAllSettings(); // refresh preview when opening
        }
        else
        {
            StartCoroutine(AnimatePanelScale(optionsPanel, false));
        }
    }

    IEnumerator AnimatePanelScale(GameObject panel, bool open)
    {
        float t = 0f;
        float duration = panelAnimationDuration;
        Vector3 start = open ? new Vector3(0.9f, 0.9f, 0.9f) : Vector3.one;
        Vector3 end = open ? Vector3.one : new Vector3(0.9f, 0.9f, 0.9f);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        float startAlpha = open ? 0f : 1f;
        float endAlpha = open ? 1f : 0f;

        panel.transform.localScale = start;
        cg.alpha = startAlpha;

        if (open) panel.SetActive(true);

        while (t < duration)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / duration);
            panel.transform.localScale = Vector3.Lerp(start, end, f);
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, f);
            yield return null;
        }

        panel.transform.localScale = end;
        cg.alpha = endAlpha;

        if (!open)
            panel.SetActive(false);
    }
    #endregion

    #region Volume handlers
    void OnMasterChanged(float v)
    {
        // v in 0..1
        ApplyMasterVolume(v);
        PlayerPrefs.SetFloat(KEY_MASTER, v);
    }

    void OnMusicChanged(float v)
    {
        ApplyMusicVolume(v);
        PlayerPrefs.SetFloat(KEY_MUSIC, v);
    }

    void OnSfxChanged(float v)
    {
        ApplySfxVolume(v);
        PlayerPrefs.SetFloat(KEY_SFX, v);
    }

    void OnVoiceChanged(float v)
    {
        ApplyVoiceVolume(v);
        PlayerPrefs.SetFloat(KEY_VOICE, v);
    }

    void ApplyMasterVolume(float v)
    {
        if (audioMixer != null)
        {
            audioMixer.SetFloat("Master", Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f);
        }
        else
        {
            // fallback: use global AudioListener volume for master
            AudioListener.volume = Mathf.Clamp01(v);
        }
    }

    void ApplyMusicVolume(float v)
    {
        if (audioMixer != null)
            audioMixer.SetFloat("Music", Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f);
        else
            Debug.Log("[OptionsController] AudioMixer not assigned; Music volume requires mixer or per-source control.");
    }

    void ApplySfxVolume(float v)
    {
        if (audioMixer != null)
            audioMixer.SetFloat("SFX", Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f);
        else
            Debug.Log("[OptionsController] AudioMixer not assigned; SFX volume requires mixer or per-source control.");
    }

    void ApplyVoiceVolume(float v)
    {
        if (audioMixer != null)
            audioMixer.SetFloat("Voice", Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f);
        else
            Debug.Log("[OptionsController] AudioMixer not assigned; Voice volume requires mixer or per-source control.");
    }
    #endregion

    #region Graphics/UI handlers
    void OnQualityChanged(int idx)
    {
        QualitySettings.SetQualityLevel(idx, true);
        PlayerPrefs.SetInt(KEY_QUALITY, idx);
    }

    void OnBrightnessChanged(float v)
    {
        ApplyBrightness(v);
        PlayerPrefs.SetFloat(KEY_BRIGHT, v);
    }

    void OnUiScaleChanged(float v)
    {
        ApplyUiScale(v);
        PlayerPrefs.SetFloat(KEY_UI_SCALE, v);
    }

    void OnSubtitleSizeChanged(float v)
    {
        ApplySubtitleSize(v);
        PlayerPrefs.SetFloat(KEY_SUB_SIZE, v);
    }

    void ApplyBrightness(float v)
    {
        // If user assigned a uiBrightnessOverlay image, use it as a simple screen tint to simulate brightness:
        if (uiBrightnessOverlay != null)
        {
            // Assume overlay is white with multiply / alpha blending; use alpha inverse to simulate brightness
            float alpha = Mathf.Clamp01(1f - v); // v=1 => alpha 0 (bright), v=0 => alpha 1 (dark)
            Color c = uiBrightnessOverlay.color;
            c.a = alpha * 0.7f; // scale down max effect
            uiBrightnessOverlay.color = c;
        }
        else
        {
            // fallback: adjust ambient intensity (may affect lighting)
            try
            {
                RenderSettings.ambientIntensity = Mathf.Lerp(0.5f, 1.2f, v);
            }
            catch { /* ignore in case RenderSettings not appropriate */ }
        }
    }

    void ApplyUiScale(float v)
    {
        if (rootCanvas != null)
        {
            // For scaleFactor on Canvas
            rootCanvas.scaleFactor = v;
        }
        else
        {
            Debug.Log("[OptionsController] rootCanvas not assigned; UI scale will not be applied.");
        }
    }

    void ApplySubtitleSize(float v)
    {
        if (subtitlePreviewText != null)
        {
            subtitlePreviewText.fontSize = v;
        }
        else
        {
            Debug.Log("[OptionsController] subtitlePreviewText not assigned; assign a TextMeshProUGUI to preview subtitle size.");
        }
    }
    #endregion

    #region Save / Load
    public void LoadAllSettings()
    {
        float master = PlayerPrefs.GetFloat(KEY_MASTER, 0.8f);
        float music = PlayerPrefs.GetFloat(KEY_MUSIC, 0.8f);
        float sfx = PlayerPrefs.GetFloat(KEY_SFX, 0.8f);
        float voice = PlayerPrefs.GetFloat(KEY_VOICE, 0.9f);
        int quality = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
        float bright = PlayerPrefs.GetFloat(KEY_BRIGHT, 1f);
        float uiScale = PlayerPrefs.GetFloat(KEY_UI_SCALE, 1f);
        float subSize = PlayerPrefs.GetFloat(KEY_SUB_SIZE, 18f);

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(music);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfx);
        if (voiceSlider != null) voiceSlider.SetValueWithoutNotify(voice);

        if (qualityDropdown != null)
        {
            qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(quality, 0, qualityDropdown.options.Count - 1));
            QualitySettings.SetQualityLevel(quality, true);
        }

        if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(bright);
        if (uiScaleSlider != null) uiScaleSlider.SetValueWithoutNotify(uiScale);
        if (subtitleSizeSlider != null) subtitleSizeSlider.SetValueWithoutNotify(subSize);

        // Apply to live systems
        ApplyMasterVolume(master);
        ApplyMusicVolume(music);
        ApplySfxVolume(sfx);
        ApplyVoiceVolume(voice);
        ApplyBrightness(bright);
        ApplyUiScale(uiScale);
        ApplySubtitleSize(subSize);
    }

    public void ResetToDefaults()
    {
        PlayerPrefs.SetFloat(KEY_MASTER, 0.8f);
        PlayerPrefs.SetFloat(KEY_MUSIC, 0.8f);
        PlayerPrefs.SetFloat(KEY_SFX, 0.8f);
        PlayerPrefs.SetFloat(KEY_VOICE, 0.9f);
        PlayerPrefs.SetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
        PlayerPrefs.SetFloat(KEY_BRIGHT, 1f);
        PlayerPrefs.SetFloat(KEY_UI_SCALE, 1f);
        PlayerPrefs.SetFloat(KEY_SUB_SIZE, 18f);
        PlayerPrefs.Save();
        LoadAllSettings();
    }
    #endregion

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }
}
