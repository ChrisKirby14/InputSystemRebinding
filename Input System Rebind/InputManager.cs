using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;
using TMPro;

    public class InputManager : MonoBehaviour
    {

        public static PlayerInputActions playerInputActions; // your generated input system class

        public static event Action rebindComplete;
        public static event Action rebindCanceled;
        public static event Action<InputAction, int> rebindStarted;

        private void Awake() 
        {
            if(playerInputActions == null)
                playerInputActions = new PlayerInputActions();
        }

        public static void StartRebind(string actionName, int bindingIndex, TMP_Text statusText, TMP_Text rebindOverlayText, GameObject rebindOverlay, bool excludeMouse)
        {
            InputAction action = playerInputActions.asset.FindAction(actionName);
            if(action == null || action.bindings.Count <= bindingIndex)
            {
                Debug.Log("Couldn't find action or binding");
                return;
            }

            if(action.bindings[bindingIndex].isComposite)
            {
                var firstPartIndex = bindingIndex + 1;
                if(firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isPartOfComposite)
                    DoRebind(action, firstPartIndex, statusText, rebindOverlayText, rebindOverlay, excludeMouse, true);
            }
            else
            {
                DoRebind(action, bindingIndex, statusText, rebindOverlayText, rebindOverlay, excludeMouse, false);
            }
        }

        private static void DoRebind(InputAction actionToRebind, int bindingIndex, TMP_Text statusText, TMP_Text rebindOverlayText,  GameObject rebindOverlay, bool excludeMouse, bool allCompositeParts)
        {
            if(actionToRebind == null || bindingIndex < 0)
                return;
           
            actionToRebind.Disable();

            var rebind = actionToRebind.PerformInteractiveRebinding(bindingIndex);

            rebind.OnComplete(operation =>
            {
                actionToRebind.Enable();
                rebindOverlay?.SetActive(false);
                operation.Dispose();

            if (CheckDuplicateBindings(actionToRebind, bindingIndex, allCompositeParts))
            {
                actionToRebind.RemoveBindingOverride(bindingIndex);
                operation.Dispose();
                DoRebind(actionToRebind, bindingIndex, statusText, rebindOverlayText, rebindOverlay, excludeMouse, allCompositeParts);
                return;
            }

                if(allCompositeParts)
                {
                    var nextBindingsIndex = bindingIndex + 1;
                    if(nextBindingsIndex < actionToRebind.bindings.Count && actionToRebind.bindings[nextBindingsIndex].isPartOfComposite)
                        DoRebind(actionToRebind, nextBindingsIndex,statusText, rebindOverlayText, rebindOverlay, excludeMouse, true);
                }

                SaveBindingOverride(actionToRebind);
                rebindComplete?.Invoke();
            });

            rebind.OnCancel(operation =>
            {
                actionToRebind.Enable();
                rebindOverlay?.SetActive(false);
                operation.Dispose();

                rebindCanceled?.Invoke();
            });

            rebind.WithCancelingThrough("<Keyboard>/escape");

            if (excludeMouse)
                rebind.WithControlsExcluding("Mouse");
                rebind.WithControlsExcluding("<Gamepad>/leftstick");
                rebind.WithControlsExcluding("<Gamepad>/rightstick");
                
            var partName = default(string);
            if (actionToRebind.bindings[bindingIndex].isPartOfComposite)
                partName = $"Binding '{actionToRebind.bindings[bindingIndex].name}'.";

            rebindOverlay?.SetActive(true);
            if (rebindOverlayText != null)
            {
                var text = !string.IsNullOrEmpty(actionToRebind.expectedControlType)
                    ? $"{partName} Waiting for input..."
                    : $"{partName} Waiting for input...";
                rebindOverlayText.text = text;
            }

            rebindStarted?.Invoke(actionToRebind, bindingIndex);
            rebind.Start(); //actually starts the rebinding
        }

        // Only checks for duplicates within the same action map.
        private static bool CheckDuplicateBindings(InputAction actionToRebind, int bindingIndex, bool allCompositeParts = false)
        {
            InputBinding newBinding = actionToRebind.bindings[bindingIndex];
            foreach ( InputBinding binding in actionToRebind.actionMap.bindings)
            {
                if (binding.action == newBinding.action)
                {
                    continue;
                }
                if (binding.effectivePath == newBinding.effectivePath)
                {
                    Debug.Log("Duplicate binding found" + newBinding.effectivePath);
                    return true;
                }
            
            }
            //Check for duplicate composite bindings
            if (allCompositeParts)
            {
                for (int i = 0; i < bindingIndex; ++i)
                {
                    if (actionToRebind.bindings[i].effectivePath == newBinding.effectivePath)
                    {
                        Debug.Log("Duplicate binding found" + newBinding.effectivePath);
                        return true;
                    }
                }
            }
            return false;
        }

        public static string GetBindingName(String actionName, int bindingIndex)
        {
            if ( playerInputActions == null)
                playerInputActions = new PlayerInputActions();

                InputAction action = playerInputActions.asset.FindAction(actionName);
                return action.GetBindingDisplayString(bindingIndex);
        }

        private static void SaveBindingOverride(InputAction action)
        {
            for ( int i = 0; i < action.bindings.Count; i ++)
            {
                PlayerPrefs.SetString( action.actionMap + action.name + i, action.bindings[i].overridePath);
            }
        }

        public static void LoadBindingOverride(String actionName)
        {
            if ( playerInputActions == null)
                playerInputActions = new PlayerInputActions();

            InputAction action = playerInputActions.asset.FindAction(actionName);

            for ( int i = 0; i < action.bindings.Count; i++)
            {
                if ( !string.IsNullOrEmpty(PlayerPrefs.GetString(action.actionMap + action.name + i)))
                    action.ApplyBindingOverride(i, PlayerPrefs.GetString(action.actionMap + action.name + i));
            }
        }

        public static void ResetBinding(string actionName, int bindingIndex)
        {
            InputAction action = playerInputActions.asset.FindAction(actionName);

            if ( action == null || action.bindings.Count <= bindingIndex)
            {
                Debug.Log("Could not find action or binding");
                return;
            }

            if (action.bindings[bindingIndex].isComposite)
            {
                for (int i = bindingIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; i++)
                    action.RemoveBindingOverride(i);
            }
            else
                action.RemoveBindingOverride(bindingIndex);

            SaveBindingOverride(action);
        }
    }