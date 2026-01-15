using UnityEngine;
using UnityEngine.InputSystem;

public class InputDebugger : MonoBehaviour
{
    private InputSystem_Actions inputActions;

    private void Awake()
    {
        Debug.Log("=== INPUT DEBUGGER STARTED ===");

        inputActions = new InputSystem_Actions();

        // Alle verfügbaren Actions ausgeben
        Debug.Log($"Input Actions Asset: {inputActions.asset.name}");

        foreach (var actionMap in inputActions.asset.actionMaps)
        {
            Debug.Log($"Action Map: {actionMap.name}");
            foreach (var action in actionMap.actions)
            {
                Debug.Log($"  - Action: {action.name}");
                foreach (var binding in action.bindings)
                {
                    Debug.Log($"    > Binding: {binding.path} (effectivePath: {binding.effectivePath})");
                }
            }
        }
    }

    private void OnEnable()
    {
        inputActions.Enable();

        // Teste die Attack Action
        // Entferne den Vergleich mit null für Player, da PlayerActions eine Struktur ist und nie null sein kann
        if (inputActions.Player.Attack != null)
        {
            Debug.Log("Attack Action gefunden!");
            inputActions.Player.Attack.performed += ctx => Debug.Log("*** ATTACK PERFORMED! ***");
            inputActions.Player.Attack.started += ctx => Debug.Log("*** ATTACK STARTED! ***");
            inputActions.Player.Attack.canceled += ctx => Debug.Log("*** ATTACK CANCELED! ***");
        }
        else
        {
            Debug.LogError("Attack Action nicht gefunden!");
        }
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void Update()
    {
        // Teste direkte Keyboard/Mouse Inputs
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("DIREKT: Linke Maustaste gedrückt!");
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("DIREKT: Spacebar gedrückt!");
        }

        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            Debug.Log("DIREKT: Enter gedrückt!");
        }
    }
}
