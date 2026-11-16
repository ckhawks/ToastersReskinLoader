using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.emotes;

// Autocomplete popup and emote picker for the chat input.
//
// Both live on the document root (not inside the chat element) so they are never clipped,
// and every click inside them calls IgnoreEvent so keyboard focus stays in the chat text field;
// losing it would make the game close chat input.
//
// Keys reach us twice in a runtime panel: as KeyDownEvent and as a separate Navigation*Event
// from the input module, in either order. handledFrame makes one keypress act once.
public class EmoteInput : MonoBehaviour
{
    const int MAX_SUGGESTIONS = 8;
    const int PICKER_COLUMNS = 9;
    const float PICKER_CELL = 36f;
    const float PICKER_FONT = 28f;
    const float BUTTON_FONT = 18f;
    const float PICKER_MAX_HEIGHT = 300f;
    const float BUTTON_SIZE = 28f;
    const float GAP = 4f;

    static readonly FieldInfo chatField = AccessTools.Field(typeof(UIChat), "chat");
    static readonly FieldInfo textFieldField = AccessTools.Field(typeof(UIChat), "textField");

    static EmoteInput instance;

    EmoteAtlas atlas;
    // From EmoteFeed: the server set and the local player's personal set, refreshed when
    // either changes. usable maps each name you can send to the sprite it shows (server first).
    List<(string name, EmoteSlot slot)> serverEntries = new List<(string, EmoteSlot)>();
    List<(string name, EmoteSlot slot)> personalEntries = new List<(string, EmoteSlot)>();
    bool personalAllowed = true;
    readonly Dictionary<string, EmoteSlot> usable = new Dictionary<string, EmoteSlot>(System.StringComparer.OrdinalIgnoreCase);
    List<string> usableNames = new List<string>();
    System.Action<ICollection<string>> onSetsChanged;
    ScrollView pickerScroll;
    UIChat uiChat;
    VisualElement chat;
    TextField textField;
    VisualElement root;
    bool originalFocusRequiresMouse;
    int handledFrame = -10;

    VisualElement suggestBox;
    Label suggestHeader;
    readonly List<Label> suggestRows = new List<Label>();
    List<string> suggestions = new List<string>();
    int selected;

    Label pickerButton;
    VisualElement picker;
    Label pickerFooter;
    Label footerEmote;
    Label footerNote;
    const string OFF_HERE = "Off on this server";

    bool SuggestOpen => suggestBox != null && suggestBox.style.display == DisplayStyle.Flex;
    bool PickerOpen => picker != null && picker.style.display == DisplayStyle.Flex;

    public static EmoteInput Create(EmoteAtlas atlas)
    {
        var go = new GameObject("TRLEmotesInput");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<EmoteInput>();
        instance.atlas = atlas;
        instance.RefreshSets();
        EmoteInput self = instance;
        instance.onSetsChanged = ids =>
        {
            EmoteFeed feed = EmoteFeed.Instance;
            if (ids == null || (feed?.LocalSteamId != null && ids.Contains(feed.LocalSteamId)))
            {
                self.RefreshSets();
            }
        };
        EmoteFeed.SetsChanged += instance.onSetsChanged;

        // If the chat UI already exists, attach now; otherwise ChatInitializePatch attaches later.
        UIManager uiManager = MonoBehaviourSingleton<UIManager>.Instance;
        if (uiManager != null && uiManager.Chat != null && textFieldField.GetValue(uiManager.Chat) != null)
        {
            instance.Attach(uiManager.Chat);
        }
        return instance;
    }

    public static void OnChatInitialized(UIChat ui)
    {
        instance?.Attach(ui);
    }

    // True if this Escape press belongs to us (closing a popup), so the game must not also close chat.
    public static bool ConsumeEscape()
    {
        if (instance == null)
        {
            return false;
        }
        if (Time.frameCount - instance.handledFrame <= 1)
        {
            return true;
        }
        if (instance.SuggestOpen || instance.PickerOpen)
        {
            instance.HideSuggestions();
            instance.ClosePicker();
            instance.handledFrame = Time.frameCount;
            return true;
        }
        return false;
    }

    void Attach(UIChat ui)
    {
        Detach();
        uiChat = ui;
        chat = chatField.GetValue(ui) as VisualElement;
        textField = textFieldField.GetValue(ui) as TextField;
        if (chat == null || textField == null || chat.panel == null)
        {
            Plugin.LogError("[Emotes] Chat input not found; autocomplete and picker disabled.");
            uiChat = null;
            return;
        }
        root = EmoteUi.DocumentRoot(chat);

        textField.RegisterCallback<ChangeEvent<string>>(OnTextChanged);
        textField.RegisterCallback<KeyUpEvent>(OnKeyUp);
        chat.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        chat.RegisterCallback<NavigationSubmitEvent>(OnNavSubmit, TrickleDown.TrickleDown);
        chat.RegisterCallback<NavigationMoveEvent>(OnNavMove, TrickleDown.TrickleDown);
        chat.RegisterCallback<NavigationCancelEvent>(OnNavCancel, TrickleDown.TrickleDown);

        BuildSuggestBox();
        BuildPicker();

        // The picker needs a free cursor while typing.
        originalFocusRequiresMouse = ui.FocusRequiresMouse;
        ui.FocusRequiresMouse = true;
        Plugin.Log($"[Emotes] Chat input attached (FocusRequiresMouse was {originalFocusRequiresMouse}).");
    }

    void Detach()
    {
        if (uiChat == null)
        {
            return;
        }
        textField.UnregisterCallback<ChangeEvent<string>>(OnTextChanged);
        textField.UnregisterCallback<KeyUpEvent>(OnKeyUp);
        chat.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        chat.UnregisterCallback<NavigationSubmitEvent>(OnNavSubmit, TrickleDown.TrickleDown);
        chat.UnregisterCallback<NavigationMoveEvent>(OnNavMove, TrickleDown.TrickleDown);
        chat.UnregisterCallback<NavigationCancelEvent>(OnNavCancel, TrickleDown.TrickleDown);
        suggestBox?.RemoveFromHierarchy();
        pickerButton?.RemoveFromHierarchy();
        picker?.RemoveFromHierarchy();
        suggestRows.Clear();
        uiChat.FocusRequiresMouse = originalFocusRequiresMouse;
        uiChat = null;
    }

    public void Destroy()
    {
        EmoteFeed.SetsChanged -= onSetsChanged;
        Detach();
        instance = null;
        Object.Destroy(gameObject);
    }

    void Update()
    {
        if (uiChat == null)
        {
            return;
        }
        if (!uiChat.IsFocused || !EmoteRuntime.Enabled)
        {
            pickerButton.style.display = DisplayStyle.None;
        EmoteUi.Rounded(pickerButton);
            HideSuggestions();
            ClosePicker();
            return;
        }

        Rect field = root.WorldToLocal(textField.worldBound);
        bool anyEmotes = serverEntries.Count > 0 || personalEntries.Count > 0;
        pickerButton.style.display = anyEmotes ? DisplayStyle.Flex : DisplayStyle.None;
        pickerButton.style.left = field.xMax + GAP;
        pickerButton.style.top = field.center.y - BUTTON_SIZE / 2f;

        if (SuggestOpen)
        {
            suggestBox.style.left = field.xMin;
            suggestBox.style.width = field.width;
            suggestBox.style.top = field.yMin - Height(suggestBox) - GAP;
        }
        if (PickerOpen)
        {
            float width = Width(picker);
            picker.style.left = Mathf.Max(0f, field.xMax + GAP + BUTTON_SIZE - width);
            picker.style.top = Mathf.Max(0f, field.yMin - Height(picker) - GAP);
        }
    }

    static float Height(VisualElement e)
    {
        return float.IsNaN(e.layout.height) ? 0f : e.layout.height;
    }

    static float Width(VisualElement e)
    {
        return float.IsNaN(e.layout.width) ? 0f : e.layout.width;
    }

    // ---- autocomplete ----

    void BuildSuggestBox()
    {
        suggestBox = Panel("TRLEmotesSuggest");
        suggestHeader = new Label { pickingMode = PickingMode.Ignore };
        suggestHeader.style.fontSize = 12;
        suggestHeader.style.color = EmoteUi.MutedTextColor;
        suggestHeader.style.marginBottom = 2;
        EmoteUi.Padding(suggestHeader, 2, 6);
        suggestBox.Add(suggestHeader);

        for (int i = 0; i < MAX_SUGGESTIONS; i++)
        {
            int index = i;
            var row = new Label { focusable = false };
            row.style.fontSize = 16;
            row.style.color = Color.white;
            EmoteUi.Padding(row, 3, 6);
            EmoteUi.Rounded(row);
                row.RegisterCallback<PointerEnterEvent>(_ => Select(index));
            row.RegisterCallback<PointerDownEvent>(_ => Accept(index));
            suggestRows.Add(row);
            suggestBox.Add(row);
        }
        root.Add(suggestBox);
    }

    void OnTextChanged(ChangeEvent<string> _)
    {
        textField.schedule.Execute(UpdateSuggestions);
    }

    void OnKeyUp(KeyUpEvent e)
    {
        if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.Home || e.keyCode == KeyCode.End)
        {
            textField.schedule.Execute(UpdateSuggestions);
        }
    }

    void UpdateSuggestions()
    {
        if (uiChat == null || !uiChat.IsFocused || !EmoteRuntime.Enabled
            || !EmoteSearch.TryFindQuery(textField.value, textField.cursorIndex, out _, out string query))
        {
            HideSuggestions();
            return;
        }

        List<string> found = EmoteSearch.Rank(usableNames, query, MAX_SUGGESTIONS);
        if (found.Count == 0)
        {
            HideSuggestions();
            return;
        }

        string previous = selected < suggestions.Count ? suggestions[selected] : null;
        suggestions = found;
        selected = previous == null ? 0 : Mathf.Max(0, suggestions.IndexOf(previous));

        suggestHeader.text = "EMOTES MATCHING :" + query;
        for (int i = 0; i < suggestRows.Count; i++)
        {
            Label row = suggestRows[i];
            if (i < suggestions.Count)
            {
                row.text = atlas.Tag(usable[suggestions[i]]) + "  :" + suggestions[i] + ":";
                row.style.display = DisplayStyle.Flex;
            }
            else
            {
                row.style.display = DisplayStyle.None;
            }
        }
        suggestBox.style.display = DisplayStyle.Flex;
        suggestBox.BringToFront();
        Select(selected);
    }


    void HideSuggestions()
    {
        if (suggestBox != null)
        {
            suggestBox.style.display = DisplayStyle.None;
        }
    }

    void Select(int index)
    {
        selected = index;
        for (int i = 0; i < suggestRows.Count; i++)
        {
            suggestRows[i].style.backgroundColor = i == selected ? EmoteUi.HighlightColor : Color.clear;
        }
    }

    void MoveSelection(int delta)
    {
        int count = suggestions.Count;
        Select(((selected + delta) % count + count) % count);
    }

    void Accept(int index)
    {
        if (index < 0 || index >= suggestions.Count)
        {
            return;
        }
        string text = textField.value;
        int caret = textField.cursorIndex;
        if (!EmoteSearch.TryFindQuery(text, caret, out int colon, out _))
        {
            HideSuggestions();
            return;
        }
        string result = EmoteSearch.Complete(text, colon, caret, suggestions[index], out int newCaret);
        HideSuggestions();
        SetText(result, newCaret);
    }

    void SetText(string text, int caret)
    {
        textField.value = text;
        int clamped = Mathf.Min(caret, textField.value.Length);
        // Move the caret now so the suggestion refresh queued by the value change sees it,
        // and again next tick in case setting the value reset it.
        textField.SelectRange(clamped, clamped);
        textField.schedule.Execute(() => textField.SelectRange(clamped, clamped));
    }

    // ---- keys ----

    void OnKeyDown(KeyDownEvent e)
    {
        bool actionKey = e.keyCode == KeyCode.UpArrow
            || e.keyCode == KeyCode.DownArrow
            || e.keyCode == KeyCode.Tab
            || e.keyCode == KeyCode.Return
            || e.keyCode == KeyCode.KeypadEnter
            || e.keyCode == KeyCode.Escape;
        if (!actionKey)
        {
            return;
        }
        if (handledFrame == Time.frameCount)
        {
            Swallow(e);
            return;
        }

        if (SuggestOpen)
        {
            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                    MoveSelection(-1);
                    break;
                case KeyCode.DownArrow:
                    MoveSelection(1);
                    break;
                case KeyCode.Escape:
                    HideSuggestions();
                    break;
                default:
                    Accept(selected);
                    break;
            }
        }
        else if (PickerOpen && e.keyCode == KeyCode.Escape)
        {
            ClosePicker();
        }
        else
        {
            return;
        }
        handledFrame = Time.frameCount;
        Swallow(e);
    }

    void OnNavSubmit(NavigationSubmitEvent e)
    {
        if (handledFrame == Time.frameCount)
        {
            Swallow(e);
            return;
        }
        if (!SuggestOpen)
        {
            return;
        }
        Accept(selected);
        handledFrame = Time.frameCount;
        Swallow(e);
    }

    void OnNavMove(NavigationMoveEvent e)
    {
        if (handledFrame == Time.frameCount)
        {
            Swallow(e);
            return;
        }
        if (!SuggestOpen)
        {
            return;
        }
        switch (e.direction)
        {
            case NavigationMoveEvent.Direction.Up:
                MoveSelection(-1);
                break;
            case NavigationMoveEvent.Direction.Down:
                MoveSelection(1);
                break;
            case NavigationMoveEvent.Direction.Next:
            case NavigationMoveEvent.Direction.Previous:
                Accept(selected);
                break;
            default:
                return;
        }
        handledFrame = Time.frameCount;
        Swallow(e);
    }

    void OnNavCancel(NavigationCancelEvent e)
    {
        if (handledFrame == Time.frameCount)
        {
            Swallow(e);
            return;
        }
        if (!SuggestOpen && !PickerOpen)
        {
            return;
        }
        HideSuggestions();
        ClosePicker();
        handledFrame = Time.frameCount;
        Swallow(e);
    }

    void Swallow(EventBase e)
    {
        e.StopImmediatePropagation();
        textField.focusController?.IgnoreEvent(e);
    }

    // ---- picker ----

    void BuildPicker()
    {
        pickerButton = new Label { name = "TRLEmotesPickerButton", focusable = false };
        pickerButton.style.position = Position.Absolute;
        pickerButton.style.width = BUTTON_SIZE;
        pickerButton.style.height = BUTTON_SIZE;
        pickerButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        EmoteUi.CopyFont(chat, pickerButton);
        pickerButton.style.display = DisplayStyle.None;
        ShuffleButtonIcon();
        pickerButton.RegisterCallback<PointerEnterEvent>(_ =>
        {
            pickerButton.style.backgroundColor = EmoteUi.HighlightColor;
            ShuffleButtonIcon();
        });
        pickerButton.RegisterCallback<PointerLeaveEvent>(_ => pickerButton.style.backgroundColor = Color.clear);
        pickerButton.RegisterCallback<PointerDownEvent>(_ => TogglePicker());
        EmoteUi.KeepFocusOnClick(pickerButton);
        root.Add(pickerButton);

        picker = Panel("TRLEmotesPicker");
        picker.style.width = PICKER_COLUMNS * PICKER_CELL + 16f;

        pickerScroll = new ScrollView(ScrollViewMode.Vertical);
        pickerScroll.style.maxHeight = PICKER_MAX_HEIGHT;
        pickerScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        picker.Add(pickerScroll);
        RebuildPickerSections();

        var divider = new VisualElement { pickingMode = PickingMode.Ignore };
        divider.style.height = 1;
        divider.style.marginTop = 4;
        divider.style.marginBottom = 4;
        divider.style.backgroundColor = EmoteUi.HighlightColor;
        picker.Add(divider);

        var footer = new VisualElement { pickingMode = PickingMode.Ignore };
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.alignItems = Align.Center;
        footerEmote = new Label { pickingMode = PickingMode.Ignore };
        footerEmote.style.width = PICKER_CELL;
        footerEmote.style.unityTextAlign = TextAnchor.MiddleCenter;
        EmoteUi.Padding(footerEmote, 0, 0);
        pickerFooter = new Label { pickingMode = PickingMode.Ignore };
        pickerFooter.style.fontSize = 14;
        pickerFooter.style.color = Color.white;
        footerNote = Note(OFF_HERE);
        footerNote.style.marginLeft = 8;
        footer.Add(footerEmote);
        footer.Add(pickerFooter);
        footer.Add(footerNote);
        picker.Add(footer);

        root.Add(picker);
    }

    // Reads the current sets from EmoteFeed and rebuilds whatever UI shows them.
    void RefreshSets()
    {
        EmoteFeed feed = EmoteFeed.Instance;
        var byName = System.StringComparer.OrdinalIgnoreCase;
        serverEntries = feed == null ? new List<(string, EmoteSlot)>() : feed.ServerEmotes().OrderBy(e => e.name, byName).ToList();
        personalEntries = feed == null ? new List<(string, EmoteSlot)>() : feed.LocalPersonalEmotes().OrderBy(e => e.name, byName).ToList();
        personalAllowed = feed?.AllowPersonal ?? true;

        usable.Clear();
        foreach (var entry in serverEntries)
        {
            usable[entry.name] = entry.slot;
        }
        if (personalAllowed)
        {
            foreach (var entry in personalEntries)
            {
                if (!usable.ContainsKey(entry.name))
                {
                    usable[entry.name] = entry.slot;
                }
            }
        }
        usableNames = usable.Keys.OrderBy(n => n, byName).ToList();

        if (pickerScroll != null)
        {
            RebuildPickerSections();
            ShuffleButtonIcon();
        }
    }

    void RebuildPickerSections()
    {
        pickerScroll.Clear();
        AddPickerSection(pickerScroll, "SERVER", serverEntries, locked: false);
        AddPickerSection(pickerScroll, "PERSONAL", personalEntries, locked: !personalAllowed);
    }

    void AddPickerSection(VisualElement parent, string title, List<(string name, EmoteSlot slot)> entries, bool locked)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var header = new VisualElement { pickingMode = PickingMode.Ignore };
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginTop = parent.childCount > 0 ? 6 : 0;
        EmoteUi.Padding(header, 2, 4);
        var label = new Label(title) { pickingMode = PickingMode.Ignore };
        label.style.fontSize = 12;
        label.style.color = EmoteUi.MutedTextColor;
        EmoteUi.Padding(label, 0, 2);
        if (locked)
        {
            header.Add(EmoteUi.LockIcon(EmoteUi.MutedTextColor));
        }
        header.Add(label);
        if (locked)
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            header.Add(spacer);
            header.Add(Note(OFF_HERE));
        }
        parent.Add(header);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        if (locked)
        {
            var grayscale = new FilterFunction(FilterFunctionType.Grayscale);
            grayscale.AddParameter(new FilterParameter(1f));
            grid.style.filter = new StyleList<FilterFunction>(new List<FilterFunction> { grayscale });
            grid.style.opacity = 0.55f;
        }
        foreach (var entry in entries)
        {
            grid.Add(PickerCell(entry.name, entry.slot, locked));
        }
        parent.Add(grid);
    }

    VisualElement PickerCell(string name, EmoteSlot slot, bool locked)
    {
        var cell = new Label(atlas.Tag(slot)) { focusable = false };
        cell.style.width = PICKER_CELL;
        cell.style.height = PICKER_CELL;
        cell.style.fontSize = FitFontSize(slot, PICKER_FONT);
        cell.style.unityTextAlign = TextAnchor.MiddleCenter;
        EmoteUi.Padding(cell, 0, 0);
        EmoteUi.Rounded(cell);
        cell.RegisterCallback<PointerEnterEvent>(_ =>
        {
            if (!locked)
            {
                cell.style.backgroundColor = EmoteUi.HighlightColor;
            }
            ShowInFooter(name, slot, locked);
        });
        cell.RegisterCallback<PointerLeaveEvent>(_ => cell.style.backgroundColor = Color.clear);
        if (locked)
        {
            VisualElement badge = EmoteUi.LockIcon(Color.white);
            badge.style.position = Position.Absolute;
            badge.style.right = 2;
            badge.style.bottom = 2;
            badge.style.scale = new Scale(new Vector2(0.8f, 0.8f));
            cell.Add(badge);
        }
        else
        {
            // Shift-click keeps the picker open to add several, like Discord.
            cell.RegisterCallback<PointerDownEvent>(e => InsertFromPicker(name, e.shiftKey));
        }
        return cell;
    }

    // The footer always shows an emote: the last hovered one, or the first usable one on open.
    void ShowInFooter(string name, EmoteSlot slot, bool locked)
    {
        footerEmote.text = atlas.Tag(slot);
        footerEmote.style.fontSize = FitFontSize(slot, PICKER_FONT);
        pickerFooter.text = ":" + name + ":";
        footerNote.style.display = locked ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // Sprites scale with font size and keep their aspect, so a 2:1 emote at the normal size is
    // twice as wide as a cell. Shrink wide ones until they fit the square.
    static float FitFontSize(EmoteSlot slot, float size)
    {
        if (slot == null || slot.Rect.height == 0)
        {
            return size;
        }
        float aspect = (float)slot.Rect.width / slot.Rect.height;
        return aspect > 1f ? size / aspect : size;
    }

    void ShuffleButtonIcon()
    {
        if (usableNames.Count > 0)
        {
            EmoteSlot slot = usable[usableNames[Random.Range(0, usableNames.Count)]];
            pickerButton.text = atlas.Tag(slot);
            pickerButton.style.fontSize = FitFontSize(slot, BUTTON_FONT);
        }
    }

    void TogglePicker()
    {
        if (PickerOpen)
        {
            ClosePicker();
            return;
        }
        HideSuggestions();
        if (usableNames.Count > 0)
        {
            ShowInFooter(usableNames[0], usable[usableNames[0]], locked: false);
        }
        picker.style.display = DisplayStyle.Flex;
        picker.BringToFront();
    }

    void ClosePicker()
    {
        if (picker != null)
        {
            picker.style.display = DisplayStyle.None;
        }
    }

    void InsertFromPicker(string name, bool keepOpen)
    {
        string result = EmoteSearch.Insert(textField.value, textField.cursorIndex, name, out int caret);
        SetText(result, caret);
        if (!keepOpen)
        {
            ClosePicker();
        }
    }

    static Label Note(string text)
    {
        var note = new Label(text) { pickingMode = PickingMode.Ignore };
        note.style.fontSize = 12;
        note.style.color = EmoteUi.MutedTextColor;
        note.style.unityFontStyleAndWeight = FontStyle.Italic;
        return note;
    }

    VisualElement Panel(string name)
    {
        var panel = new VisualElement { name = name };
        panel.style.position = Position.Absolute;
        panel.style.flexDirection = FlexDirection.Column;
        EmoteUi.Box(panel);
        EmoteUi.CopyFont(chat, panel);
        panel.style.display = DisplayStyle.None;
        EmoteUi.Padding(panel, 6, 6);
        EmoteUi.KeepFocusOnClick(panel);
        return panel;
    }
}
