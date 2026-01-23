using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug helper to inspect Input System actions, bindings,
/// and to verify whether inputs are actually being triggered.
/// </summary>
public class InputDebugger : MonoBehaviour
{
    // Generated Input Actions class from the Input System
    private InputSystem_Actions inputActions;

    private void Awake()
    {
        Debug.Log("=== INPUT DEBUGGER STARTED ===");

        // Create a new instance of the input actions
        inputActions = new InputSystem_Actions();

        // Log the name of the Input Actions asset
        Debug.Log($"Input Actions Asset: {inputActions.asset.name}");

        // Iterate through all action maps
        foreach (var actionMap in inputActions.asset.actionMaps)
        {
            Debug.Log($"Action Map: {actionMap.name}");

            // Iterate through all actions in the map
            foreach (var action in actionMap.actions)
            {
                Debug.Log($"  - Action: {action.name}");

                // Log all bindings for the action
                foreach (var binding in action.bindings)
                {
                    Debug.Log(
                        $"    > Binding: {binding.path} (effectivePath: {binding.effectivePath})"
                    );
                }
            }
        }
    }

    private void OnEnable()
    {
        // Enable all input actions
        inputActions.Enable();

        // Test the Attack action
        // Note: PlayerActions is a struct and can never be null,
        // but the action itself is checked for safety.
        if (inputActions.Player.Attack != null)
        {
            Debug.Log("Attack Action found!");

            // Log when the attack input starts
            inputActions.Player.Attack.started +=
                ctx => Debug.Log("*** ATTACK STARTED! ***");

            // Log when the attack input is performed
            inputActions.Player.Attack.performed +=
                ctx => Debug.Log("*** ATTACK PERFORMED! ***");

            // Log when the attack input is canceled
            inputActions.Player.Attack.canceled +=
                ctx => Debug.Log("*** ATTACK CANCELED! ***");
        }
        else
        {
            Debug.LogError("Attack Action not found!");
        }
    }

    private void OnDisable()
    {
        // Disable input actions when the object is disabled
        inputActions?.Disable();
    }

    private void Update()
    {
        // Directly test raw keyboard and mouse inputs
        // (bypassing the Input Actions system)

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("DIRECT: Left mouse button pressed!");
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("DIRECT: Spacebar pressed!");
        }

        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            Debug.Log("DIRECT: Enter key pressed!");
        }
    }
}
