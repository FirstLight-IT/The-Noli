using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ComposureHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private KeepingComposureMinigame minigame;

    public void OnPointerDown(PointerEventData eventData)
    {
        minigame?.SetPointerHeld(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        minigame?.SetPointerHeld(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        minigame?.SetPointerHeld(false);
    }

    private void OnDisable()
    {
        minigame?.SetPointerHeld(false);
    }
}
