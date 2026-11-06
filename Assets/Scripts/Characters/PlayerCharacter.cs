using System;
using UnityEngine;

[DefaultExecutionOrder(-300)]
public class PlayerCharacter : MonoBehaviour
{
    public static PlayerCharacter Instance { get; private set; }
    public static event Action<NPCInfoSO> OnCharacterChanged;

    [SerializeField] private NPCInfoSO currentCharacter;
    [SerializeField] private Animator characterAnimator;

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
        {
            Debug.LogError("Player Character needs a character data asset.", this);
            return;
        }

        ApplyCharacterAppearance(currentCharacter);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetCharacter(NPCInfoSO character)
    {
        if (character == null)
            return;

        bool characterChanged = character != currentCharacter;
        currentCharacter = character;
        ApplyCharacterAppearance(currentCharacter);

        if (characterChanged)
            OnCharacterChanged?.Invoke(currentCharacter);
    }

    private void ApplyCharacterAppearance(NPCInfoSO character)
    {
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>(true);

        if (characterAnimator == null || character.PlayerAnimatorController == null)
            return;

        characterAnimator.runtimeAnimatorController = character.PlayerAnimatorController;
    }
}
