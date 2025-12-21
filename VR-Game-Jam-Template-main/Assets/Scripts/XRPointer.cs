using UnityEngine;

public class XRPointer : MonoBehaviour
{
    private GameObject prevhit;
    public XRManager manager;
    // Update is called once per frame
    void Update()
    {
        if(manager.isStereo == false)
            return;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit))
        {
            if(hit.collider.gameObject.GetComponent<GazeDwellButton>() != null && hit.collider.gameObject != prevhit)
            {
                hit.collider.gameObject.GetComponent<GazeDwellButton>().OnPointerEnter();
                prevhit = hit.collider.gameObject;
            }
            
            if(hit.collider.gameObject != prevhit && prevhit != null)
            {
                if(prevhit.GetComponent<GazeDwellButton>() != null)
                {
                    prevhit.GetComponent<GazeDwellButton>().OnPointerExit();
                    prevhit = null;
                }
            }
        }
    }
}
