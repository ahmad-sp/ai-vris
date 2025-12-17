using UnityEngine;

public class IPManager : MonoBehaviour
{
    public string backendBaseUrl = "http://192.168.1.76:8000";
    public string baseUrl = "/api/";
    public string sessionsEndpoint = "/api/interview/sessions/";
    public string reportBaseEndpoint = "/api/reports";

    public void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
      baseUrl = backendBaseUrl + baseUrl;
      sessionsEndpoint = backendBaseUrl + sessionsEndpoint;
      reportBaseEndpoint = backendBaseUrl + reportBaseEndpoint;
    }
}
