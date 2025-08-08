using UnityEngine;
using UnityEngine.InputSystem;

namespace Interaction
{
    public class ControllerInputHandler : MonoBehaviour
    {
        private PlayerInput playerInput;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            playerInput.actions.Enable();
        }

        private void OnDisable()
        {
            playerInput.actions.Disable();
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                HandleInteraction();
            }
        }

        private void HandleInteraction()
        {
            // Implement interaction logic here
            Debug.Log("Interaction triggered");
        }
    }
}