using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.emotes;

public static class EmoteUi
{
    public static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.08f, 0.96f);
    public static readonly Color HighlightColor = new Color(0.24f, 0.24f, 0.24f, 1f);
    public static readonly Color MutedTextColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    // Only a step lighter than the panel, so the edge reads without glaring.
    public static readonly Color BorderColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public const float RADIUS = 3f;

    // The child of the panel's visual tree that holds the game UI, so added elements inherit its theme.
    public static VisualElement DocumentRoot(VisualElement element)
    {
        VisualElement treeRoot = element.panel.visualTree;
        VisualElement current = element;
        while (current.parent != null && current.parent != treeRoot)
        {
            current = current.parent;
        }
        return current;
    }

    // Copies the font from the first text element under source that actually has one.
    // A TextField itself resolves to an empty font (it lives on the inner text element), and
    // assigning an empty definition overrides the theme and renders no text at all.
    public static void CopyFont(VisualElement source, VisualElement target)
    {
        TextElement withFont = source.Query<TextElement>().Where(HasFont).First();
        if (withFont == null && source is TextElement self && HasFont(self))
        {
            withFont = self;
        }
        if (withFont != null)
        {
            target.style.unityFontDefinition = withFont.resolvedStyle.unityFontDefinition;
        }
    }

    static bool HasFont(TextElement e)
    {
        FontDefinition font = e.resolvedStyle.unityFontDefinition;
        return font.fontAsset != null || font.font != null;
    }

    public static string SpriteTag(string atlasName, string emoteName)
    {
        return "<sprite=\"" + atlasName + "\" name=\"" + emoteName + "\">";
    }

    // Dark panel with a 1px light gray border, the look shared by every popup.
    public static void Box(VisualElement element)
    {
        element.style.backgroundColor = PanelColor;
        element.style.borderTopWidth = 1;
        element.style.borderBottomWidth = 1;
        element.style.borderLeftWidth = 1;
        element.style.borderRightWidth = 1;
        element.style.borderTopColor = BorderColor;
        element.style.borderBottomColor = BorderColor;
        element.style.borderLeftColor = BorderColor;
        element.style.borderRightColor = BorderColor;
        Rounded(element);
    }

    public static void Rounded(VisualElement element, float radius = RADIUS)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }

    public static void Padding(VisualElement element, float vertical, float horizontal)
    {
        element.style.paddingTop = vertical;
        element.style.paddingBottom = vertical;
        element.style.paddingLeft = horizontal;
        element.style.paddingRight = horizontal;
    }

    // Clicks inside the element must not move keyboard focus, or the game closes chat input.
    public static void KeepFocusOnClick(VisualElement element)
    {
        element.RegisterCallback<PointerDownEvent>(e => element.focusController?.IgnoreEvent(e), TrickleDown.TrickleDown);
    }

    // Small padlock drawn from boxes so no icon asset is needed.
    public static VisualElement LockIcon(Color color)
    {
        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
        icon.style.width = 10;
        icon.style.height = 12;
        icon.style.alignItems = Align.Center;

        var shackle = new VisualElement();
        shackle.style.width = 6;
        shackle.style.height = 5;
        shackle.style.borderTopWidth = 2;
        shackle.style.borderLeftWidth = 2;
        shackle.style.borderRightWidth = 2;
        shackle.style.borderTopColor = color;
        shackle.style.borderLeftColor = color;
        shackle.style.borderRightColor = color;

        var body = new VisualElement();
        body.style.width = 10;
        body.style.height = 7;
        body.style.backgroundColor = color;

        icon.Add(shackle);
        icon.Add(body);
        return icon;
    }
}

// A bordered box with an optional large emote preview above a line of text, and a caret
// pointing at its target, flipped when it has to sit below it.
public class EmoteTooltip : VisualElement
{
    const float CARET_WIDTH = 12f;
    const float CARET_HEIGHT = 6f;
    const float GAP = 2f;

    readonly VisualElement bubble;
    readonly Label preview;
    readonly Label text;
    readonly VisualElement caretDown;
    readonly VisualElement caretUp;

    public EmoteTooltip(VisualElement fontSource)
    {
        name = "TRLEmotesTooltip";
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.alignItems = Align.Center;
        style.display = DisplayStyle.None;

        caretUp = Caret(pointsUp: true);
        bubble = new VisualElement { pickingMode = PickingMode.Ignore };
        bubble.style.alignItems = Align.Center;
        EmoteUi.Box(bubble);
        EmoteUi.Padding(bubble, 6, 8);
        EmoteUi.CopyFont(fontSource, bubble);

        preview = new Label { pickingMode = PickingMode.Ignore };
        preview.style.unityTextAlign = TextAnchor.MiddleCenter;
        preview.style.marginBottom = 2;
        EmoteUi.Padding(preview, 0, 0);
        text = new Label { pickingMode = PickingMode.Ignore, enableRichText = false };
        text.style.color = Color.white;
        text.style.fontSize = 14;
        EmoteUi.Padding(text, 0, 0);
        bubble.Add(preview);
        bubble.Add(text);
        caretDown = Caret(pointsUp: false);

        Add(caretUp);
        Add(bubble);
        Add(caretDown);
    }

    // targetRect is in this tooltip's parent space. previewTag, when given, is a sprite tag
    // shown above the text at previewFontSize.
    public void ShowAt(string label, Rect targetRect, string previewTag = null, float previewFontSize = 0f)
    {
        text.text = label;
        if (previewTag != null)
        {
            preview.text = previewTag;
            preview.style.fontSize = previewFontSize;
            preview.style.display = DisplayStyle.Flex;
        }
        else
        {
            preview.style.display = DisplayStyle.None;
        }
        style.display = DisplayStyle.Flex;
        BringToFront();

        Vector2 size = new Vector2(
            float.IsNaN(layout.width) ? 0f : layout.width,
            float.IsNaN(layout.height) ? 0f : layout.height);
        float parentWidth = parent.layout.width;
        float x = Mathf.Clamp(targetRect.center.x - size.x / 2f, 0f, Mathf.Max(0f, parentWidth - size.x));
        float y = targetRect.yMin - size.y - GAP;
        bool below = y < 0f;
        if (below)
        {
            y = targetRect.yMax + GAP;
        }

        caretUp.style.display = below ? DisplayStyle.Flex : DisplayStyle.None;
        caretDown.style.display = below ? DisplayStyle.None : DisplayStyle.Flex;
        // Keep the caret on the target even when the bubble is pushed in from a screen edge.
        float caretOffset = targetRect.center.x - (x + size.x / 2f);
        caretUp.style.translate = new Translate(caretOffset, 0);
        caretDown.style.translate = new Translate(caretOffset, 0);

        style.left = x;
        style.top = y;
    }

    public void Hide()
    {
        style.display = DisplayStyle.None;
    }

    // Overlaps the bubble by 1px so its fill covers the border where they join.
    static VisualElement Caret(bool pointsUp)
    {
        var caret = new VisualElement { pickingMode = PickingMode.Ignore };
        caret.style.width = CARET_WIDTH;
        caret.style.height = CARET_HEIGHT;
        if (pointsUp)
        {
            caret.style.marginBottom = -1;
        }
        else
        {
            caret.style.marginTop = -1;
        }
        caret.generateVisualContent += mgc =>
        {
            Painter2D p = mgc.painter2D;
            float w = CARET_WIDTH;
            float h = CARET_HEIGHT;
            float baseY = pointsUp ? h : 0f;
            float tipY = pointsUp ? 0f : h;

            p.fillColor = EmoteUi.PanelColor;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, baseY));
            p.LineTo(new Vector2(w / 2f, tipY));
            p.LineTo(new Vector2(w, baseY));
            p.ClosePath();
            p.Fill();

            p.strokeColor = EmoteUi.BorderColor;
            p.lineWidth = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, baseY));
            p.LineTo(new Vector2(w / 2f, tipY));
            p.LineTo(new Vector2(w, baseY));
            p.Stroke();
        };
        return caret;
    }
}
