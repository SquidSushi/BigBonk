using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : CharacterInputReader
{
    InputAction moveAction;
    InputAction sprintAction;
    [SerializeField] private bool holdToSprint;
    public bool SprintToggledOn { get; private set; }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        sprintAction = InputSystem.actions.FindAction("Sprint");
        if (moveAction == null)
        {
            Debug.LogError("du hund");
            this.enabled = false;
        }
    }

    // Update is called once per frame
    public override void Update()
    {
        this.MovementInput = moveAction.ReadValue<Vector2>();
        SprintToggledOn = sprintAction.IsPressed();
    }
}
