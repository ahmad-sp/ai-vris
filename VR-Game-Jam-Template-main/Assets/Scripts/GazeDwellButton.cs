using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GazeDwellButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    public float dwellTime = 2.0f; // seconds to activate

    private float timer;
    private bool gazing;
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void Update()
    {
        if (!gazing || !button.interactable)
            return;

        timer += Time.deltaTime;

        if (timer >= dwellTime)
        {
            button.onClick.Invoke();
            timer = 0f;          // reset so it doesn't spam
            gazing = false;     // require re-gaze
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        gazing = true;
        Debug.Log("Gazed at button: " + gameObject.name);
        timer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        gazing = false;
        timer = 0f;
    }
}
