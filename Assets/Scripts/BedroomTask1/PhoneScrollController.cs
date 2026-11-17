using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PhoneScrollController : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private InputActionReference scrollAction;
    [SerializeField] private float thumbstickSpeed;
    [SerializeField] private float mouseScrollSpeed;

    private XRGrabInteractable phoneGrab;

    private void Awake()
    {
        phoneGrab = GetComponent<XRGrabInteractable>();
    }

    private void Update()
    {
        if (!phoneGrab.isSelected || scrollRect == null)
            return;

        float scrollAmount = 0f;

        // VR controller scrolling
        if (scrollAction != null && scrollAction.action != null)
        {
            float thumbstick =
                scrollAction.action.ReadValue<Vector2>().y;

            if (Mathf.Abs(thumbstick) > 0.15f)
                scrollAmount += thumbstick *
                                thumbstickSpeed * Time.deltaTime;
        }

        // Mouse wheel scrolling for Unity testing
        if (Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;

            if (Mathf.Abs(wheel) > 0.01f)
                scrollAmount += wheel * mouseScrollSpeed;
        }

        if (Mathf.Abs(scrollAmount) > 0f)
        {
            scrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(
                    scrollRect.verticalNormalizedPosition + scrollAmount
                );
        }
    }
}