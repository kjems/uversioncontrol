// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UVC;

public class RendererInspectorManager : Editor
{
    void OnEnable()
    {
        VCCommands.Instance.StatusCompleted += Repaint;
    }

    void OnDisable()
    {
        VCCommands.Instance.StatusCompleted -= Repaint;
    }

    private struct PrioritizedInspectorCallback
    {
        public PrioritizedInspectorCallback(System.Action<Object[]> inspectorcallback, int priority)
        {
            this.inspectorcallback = inspectorcallback;
            this.priority = priority;
        }
        public readonly System.Action<Object[]> inspectorcallback;
        public readonly int priority;
    }
    private static readonly List<PrioritizedInspectorCallback> prioritizedInspectors = new List<PrioritizedInspectorCallback>();

    /// <summary>
    /// Add an inspector callback to a list of inspectors for renderers
    /// </summary>
    /// <param name="inspectorcallback">Callback for inspector</param>
    /// <param name="priority">Priority of the added inspector where higher priority is called first</param>
    public static void AddInspector(System.Action<Object[]> inspectorcallback, int priority = 0)
    {
        prioritizedInspectors.Add(new PrioritizedInspectorCallback(inspectorcallback, priority));
        prioritizedInspectors.Sort((a, b) => b.priority - a.priority);
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        foreach (var prioritizedInspectorIt in prioritizedInspectors)
        {
            prioritizedInspectorIt.inspectorcallback(targets);
        }
    }
}

