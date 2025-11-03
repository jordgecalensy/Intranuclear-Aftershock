using UnityEngine;
namespace Failsafe.Inventory {
public class SimplePickup : MonoBehaviour {
    public Camera cam; public float dist=3f; float hold=0f, threshold=0.35f;
    void Update(){
        if(Input.GetKeyDown(KeyCode.N)) hold=0f;
        if(Input.GetKey(KeyCode.N)) hold+=Time.deltaTime;
        if(Input.GetKeyUp(KeyCode.N)){
            if(Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, dist)){
                var wi = hit.collider.GetComponentInParent<WorldItem>();
                if(wi){ if(hold>=threshold) wi.TryPickupHold(); else wi.TryPickupTap(); }
            }
        }
    }
}}