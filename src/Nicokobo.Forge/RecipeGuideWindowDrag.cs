using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nicokobo.Forge;

internal static class RecipeGuideWindowDrag
{
    private const string HandleName = "NicoRecipeGuideWindowDrag";

    internal static bool ContainsPointer(GameObject? window)
    {
        if (window == null || !window.activeInHierarchy) return false;
        var panel = window.GetComponent<RectTransform>();
        if (panel == null) return false;
        var canvas = panel.GetComponentInParent(Il2CppType.Of<Canvas>())?.TryCast<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        var mouse = Input.mousePosition;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(panel,
            new Vector2(mouse.x, mouse.y), camera, out var point) && panel.rect.Contains(point);
    }

    internal static void KeepCursor(GameObject? window)
    {
        if (window == null || !window.activeInHierarchy) return;
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
    }

    internal static void Attach(GameObject? window)
    {
        if (window == null || window.transform.Find(HandleName) != null) return;
        var panel = window.GetComponent<RectTransform>();
        var parent = panel?.parent?.TryCast<RectTransform>();
        if (panel == null || parent == null) return;

        var handle = new GameObject(HandleName);
        var area = handle.AddComponent<RectTransform>();
        area.SetParent(panel, false);
        area.anchorMin = new Vector2(0, 1);
        area.anchorMax = Vector2.one;
        area.pivot = new Vector2(.5f, 1);
        area.sizeDelta = new Vector2(-96, 96);
        area.anchoredPosition = Vector2.zero;
        var image = handle.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        var events = handle.AddComponent<EventTrigger>();
        Vector2 startPointer = default, startPosition = default;
        bool dragging = false;

        void Listen(EventTriggerType type, Action<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(DelegateSupport.ConvertDelegate<
                UnityEngine.Events.UnityAction<BaseEventData>>(callback));
            events.triggers.Add(entry);
        }
        Listen(EventTriggerType.BeginDrag, data =>
        {
            var pointer = data.TryCast<PointerEventData>();
            dragging = pointer != null && pointer.button == PointerEventData.InputButton.Left &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    pointer.position, pointer.pressEventCamera, out startPointer);
            if (!dragging) return;
            startPosition = panel.anchoredPosition;
            panel.SetAsLastSibling();
        });
        Listen(EventTriggerType.Drag, data =>
        {
            var pointer = data.TryCast<PointerEventData>();
            if (!dragging || pointer == null || !window.activeInHierarchy ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    pointer.position, pointer.pressEventCamera, out var point)) return;
            panel.anchoredPosition = startPosition + point - startPointer;
            Clamp(panel);
        });
        Listen(EventTriggerType.EndDrag, _ => dragging = false);
    }

    internal static void Clamp(RectTransform panel)
    {
        var parent = panel.parent?.TryCast<RectTransform>();
        if (parent == null) return;
        // CalculateRelativeRectTransformBounds is an unstripping-failure stub in
        // this game's interop assembly. Transform the panel's own four corners
        // through the available native methods instead of including page content.
        var rect = panel.rect;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            var localCorner = new Vector3(
                (i & 1) == 0 ? rect.xMin : rect.xMax,
                (i & 2) == 0 ? rect.yMin : rect.yMax, 0);
            var corner = parent.InverseTransformPoint(panel.TransformPoint(localCorner));
            min.x = Math.Min(min.x, corner.x);
            min.y = Math.Min(min.y, corner.y);
            max.x = Math.Max(max.x, corner.x);
            max.y = Math.Max(max.y, corner.y);
        }
        var center = (min + max) * .5f;
        var extents = (max - min) * .5f;
        var frame = parent.rect;
        // Keep enough of the title visible to drag a moved window back.
        float x = Mathf.Clamp(center.x,
            frame.xMin - extents.x + 96, frame.xMax + extents.x - 96);
        float y = Mathf.Clamp(center.y,
            frame.yMin - extents.y + 96, frame.yMax - extents.y);
        panel.anchoredPosition += new Vector2(x - center.x, y - center.y);
    }
}
