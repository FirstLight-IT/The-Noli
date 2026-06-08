

public interface IInteractable
{
    void interact();
    void showIcon(bool visible);

    bool interacted(); // to unlock in journal
    int incrementCounter();


   
}
