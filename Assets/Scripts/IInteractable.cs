

public interface IInteractable
{
    void interact();
    void showIcon(bool visible);
    void setInteracted();          // to unlock in journal

    bool canInteract();
    int incrementCounter();

}
