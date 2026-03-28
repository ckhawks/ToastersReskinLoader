using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Object = System.Object;

namespace ToasterReskinLoader;

/// <summary>
/// Debug-only chat command handler. Type "/hierarchy" in chat to dump
/// the full scene GameObject tree to a file for inspection.
/// </summary>
public static class PatchClientChat
{
    [HarmonyPatch(typeof(ChatManagerController), "Event_OnChatSubmitMessage")]
    class PatchChatManagerControllerEventOnChatSubmitMessage
    {
        [HarmonyPrefix]
        static bool Prefix(ChatManagerController __instance, Dictionary<string, object> message)
        {
            try
            {
                string content = (string)message["content"];
                Plugin.Log($"Patch: ChatManagerController.Event_OnChatSubmitMessage (Prefix) was called.");
                string[] messageParts = content.Split(' ');

                if (messageParts[0].Equals($"/hierarchy"))
                {
                    PrintHierarchyToFile("hierarchy.txt");
                    return false;
                }
            }
            catch (Exception e)
            {
                Plugin.LogError($"Error in PatchClientChat: {e.Message}");
            }

            return true;
        }
    }

    private static void PrintHierarchyToFile(string filePath)
    {
        Debug.Log($"Writing hierarchy of the current scene to file: {filePath}");

        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            Object[] allObjects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject));

            foreach (var obj in allObjects)
            {
                GameObject gameObject = obj as GameObject;
                if (gameObject == null || gameObject.transform == null)
                {
                    continue;
                }

                if (gameObject.transform.parent == null)
                {
                    WriteGameObjectHierarchyToFile(gameObject, 0, writer);
                }
            }
        }

        Debug.Log("Hierarchy successfully written to file.");
    }

    private static void WriteGameObjectHierarchyToFile(GameObject obj, int depth, StreamWriter writer)
    {
        if (obj == null || obj.transform == null)
        {
            return;
        }

        if (obj.name.Contains("Spectator") && !obj.name.Contains("Manager") && !obj.name.Contains("Camera") &&
            !obj.name.Contains("Controller"))
        {
            return;
        }

        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            if (parent.name.Contains("Spectator") && !parent.name.Contains("Manager") &&
                !parent.name.Contains("Controller") && !parent.name.Contains("Camera"))
            {
                return;
            }

            parent = parent.parent;
        }

        Component[] components = obj.GetComponents<Component>();
        string componentTypes = string.Join(", ", components.Select(c => c.GetType().Name));

        writer.WriteLine($"{new string('-', depth * 2)}{obj.name} " +
                         $"[Active: {obj.activeSelf}, Layer: {obj.layer}" +
                         $"Position: {obj.transform.position}, Components: {componentTypes}]");

        foreach (var childObj in obj.transform)
        {
            if (childObj is Transform childTransform && childTransform.gameObject != null)
            {
                WriteGameObjectHierarchyToFile(childTransform.gameObject, depth + 1, writer);
            }
        }
    }
}
