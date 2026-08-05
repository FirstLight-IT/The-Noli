using System;
using UnityEngine;

public class PlayerCharacter : MonoBehaviour
{
    public static PlayerCharacter Instance { get; private set; }
    public static event Action<NPCInfoSO> OnCharacterChanged;

    [SerializeField] private NPCInfoSO currentCharacter;

    public NPCInfoSO CurrentCharacter => currentCharacter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one Player Character can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (currentCharacter == null)
            Debug.LogError("Player Character needs a character data asset.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetCharacter(NPCInfoSO character)
    {
        if (character == null || character == currentCharacter)
            return;

        currentCharacter = character;
        OnCharacterChanged?.Invoke(currentCharacter);
    }
}
