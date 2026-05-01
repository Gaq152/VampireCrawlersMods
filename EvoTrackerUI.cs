using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Nosebleed.Pancake.GameConfig;

namespace EvoTrackerMod;

public class EvoTrackerUI : MonoBehaviour
{
    private bool _showPanel;
    private GUIStyle _bgStyle;
    private GUIStyle _ownedStyle;
    private GUIStyle _missingStyle;
    private GUIStyle _completeStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _recipeStyle;
    private GUIStyle _componentStyle;
    private GUIStyle _markerNeededStyle;
    private GUIStyle _markerOwnedStyle;
    private GUIStyle _markerEvolvedStyle;
    private GUIStyle _markerPinnedStyle;
    private GUIStyle _markBarFullStyle;
    private GUIStyle _markBarPartialStyle;
    private GUIStyle _buttonStyle;
    private bool _stylesInitialized;
    private Vector2 _buttonPos;
    private bool _draggingButton;
    private Vector2 _dragOffset;
    private int _scrollOffset;
    private float _scale;

    private Dictionary<string, int> _cachedGroupCounts = new();
    private float _lastCountRefresh;
    private const float CountRefreshInterval = 0.4f;

    private readonly HashSet<string> _markedGroupIds = new();

    private static GUIStyle MakeBoxStyle(Color bgColor)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, bgColor);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        var style = new GUIStyle();
        style.normal.background = tex;
        return style;
    }

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _scale = Mathf.Max(Screen.height / 1080f, 1f);

        _bgStyle = MakeBoxStyle(new Color(0.08f, 0.08f, 0.12f, 0.93f));
        _ownedStyle = MakeBoxStyle(new Color(0.12f, 0.45f, 0.12f, 0.9f));
        _missingStyle = MakeBoxStyle(new Color(0.45f, 0.12f, 0.12f, 0.7f));
        _completeStyle = MakeBoxStyle(new Color(0.5f, 0.42f, 0.05f, 0.9f));
        _markBarFullStyle = MakeBoxStyle(new Color(1f, 0.8f, 0.2f, 0.9f));
        _markBarPartialStyle = MakeBoxStyle(new Color(1f, 0.8f, 0.2f, 0.45f));

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)(16 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _headerStyle.normal.textColor = new Color(1f, 0.9f, 0.5f);

        _recipeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)(14 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        _recipeStyle.normal.textColor = Color.white;

        _componentStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)(13 * _scale),
            alignment = TextAnchor.MiddleLeft
        };

        var neededBg = new Texture2D(1, 1);
        neededBg.SetPixel(0, 0, new Color(0.6f, 0.15f, 0.05f, 0.92f));
        neededBg.Apply();
        neededBg.hideFlags = HideFlags.HideAndDontSave;
        _markerNeededStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = (int)(13 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _markerNeededStyle.normal.textColor = new Color(1f, 0.95f, 0.3f);
        _markerNeededStyle.normal.background = neededBg;

        var ownedBg = new Texture2D(1, 1);
        ownedBg.SetPixel(0, 0, new Color(0.1f, 0.35f, 0.1f, 0.85f));
        ownedBg.Apply();
        ownedBg.hideFlags = HideFlags.HideAndDontSave;
        _markerOwnedStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = (int)(13 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _markerOwnedStyle.normal.textColor = new Color(0.7f, 1f, 0.7f);
        _markerOwnedStyle.normal.background = ownedBg;

        var evolvedBg = new Texture2D(1, 1);
        evolvedBg.SetPixel(0, 0, new Color(0.5f, 0.42f, 0.05f, 0.9f));
        evolvedBg.Apply();
        evolvedBg.hideFlags = HideFlags.HideAndDontSave;
        _markerEvolvedStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = (int)(13 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _markerEvolvedStyle.normal.textColor = new Color(1f, 0.85f, 0f);
        _markerEvolvedStyle.normal.background = evolvedBg;

        var pinnedBg = new Texture2D(1, 1);
        pinnedBg.SetPixel(0, 0, new Color(0.55f, 0.1f, 0.5f, 0.95f));
        pinnedBg.Apply();
        pinnedBg.hideFlags = HideFlags.HideAndDontSave;
        _markerPinnedStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = (int)(15 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _markerPinnedStyle.normal.textColor = Color.white;
        _markerPinnedStyle.normal.background = pinnedBg;

        var btnBg = new Texture2D(1, 1);
        btnBg.SetPixel(0, 0, new Color(0.15f, 0.12f, 0.3f, 0.9f));
        btnBg.Apply();
        btnBg.hideFlags = HideFlags.HideAndDontSave;
        _buttonStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = (int)(14 * _scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _buttonStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);
        _buttonStyle.normal.background = btnBg;

        _buttonPos = new Vector2(10, 10);
    }

    private void OnGUI()
    {
        var plugin = EvoTrackerPlugin.Instance;
        if (plugin == null) return;
        if (plugin.CachedPlayer == null) return;

        InitStyles();

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F8)
        {
            _showPanel = !_showPanel;
            _scrollOffset = 0;
            Event.current.Use();
        }

        float now;
        try { now = Time.unscaledTime; } catch { now = Time.realtimeSinceStartup; }
        if (now - _lastCountRefresh > CountRefreshInterval)
        {
            _cachedGroupCounts = plugin.GetPlayerOwnedGroupCounts();
            _lastCountRefresh = now;
        }
        var ownedCounts = _cachedGroupCounts;

        DrawToggleButton(plugin, ownedCounts);

        if (_showPanel)
            DrawFullPanel(plugin, ownedCounts);

        if (plugin.IsChooseCardModalOpen && plugin.CurrentChoices.Count > 0)
            DrawChoiceMarkers(plugin, ownedCounts);
    }

    private void DrawToggleButton(EvoTrackerPlugin plugin, Dictionary<string, int> ownedCounts)
    {
        float btnW = 110 * _scale;
        float btnH = 30 * _scale;
        var btnRect = new Rect(_buttonPos.x, _buttonPos.y, btnW, btnH);

        int evolved = plugin.AllRecipes.Count(r => plugin.IsRecipeEvolved(r));
        int inProgress = plugin.AllRecipes.Count(r =>
            !plugin.IsRecipeEvolved(r) && r.IsPartiallyOwned(ownedCounts));

        string btnText = plugin.AllRecipes.Count == 0
            ? "进化 ..."
            : $"进化 {evolved}/{plugin.AllRecipes.Count}";

        GUI.Box(btnRect, btnText, _buttonStyle);

        var e = Event.current;
        if (e.type == EventType.MouseDown && btnRect.Contains(e.mousePosition))
        {
            if (e.button == 0)
            {
                _showPanel = !_showPanel;
                _scrollOffset = 0;
                e.Use();
            }
            else if (e.button == 1)
            {
                _draggingButton = true;
                _dragOffset = e.mousePosition - _buttonPos;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && _draggingButton)
        {
            _buttonPos = e.mousePosition - _dragOffset;
            _buttonPos.x = Mathf.Clamp(_buttonPos.x, 0, Screen.width - btnW);
            _buttonPos.y = Mathf.Clamp(_buttonPos.y, 0, Screen.height - btnH);
            e.Use();
        }
        else if (e.type == EventType.MouseUp && _draggingButton)
        {
            _draggingButton = false;
            e.Use();
        }
    }

    private void DrawFullPanel(EvoTrackerPlugin plugin, Dictionary<string, int> ownedCounts)
    {
        float lineH = 22 * _scale;
        float padding = 8 * _scale;
        float panelW = 320 * _scale;
        float btnW = 110 * _scale;
        float btnH = 30 * _scale;
        float gap = 4 * _scale;
        float markBarW = 6 * _scale;

        var recipes = plugin.AllRecipes;
        if (recipes.Count == 0) return;

        float spaceBelow = Screen.height - (_buttonPos.y + btnH) - gap - 10;
        float spaceAbove = _buttonPos.y - gap - 10;
        bool expandDown = spaceBelow >= spaceAbove;
        float maxAvailableH = Mathf.Max(spaceBelow, spaceAbove);
        float maxPanelH = Mathf.Min(Screen.height * 0.75f, Mathf.Max(maxAvailableH, lineH * 4));

        int totalLines = 1;
        foreach (var r in recipes)
            totalLines += 1 + r.RequiredGroups.Count;

        int maxVisible = Mathf.Max(1, (int)((maxPanelH - lineH - padding * 3) / lineH));
        if (_scrollOffset < 0) _scrollOffset = 0;
        if (_scrollOffset > Mathf.Max(0, totalLines - maxVisible))
            _scrollOffset = Mathf.Max(0, totalLines - maxVisible);

        float contentH = Mathf.Min(totalLines, maxVisible) * lineH + padding * 2;
        float panelH = lineH + padding + contentH;
        panelH = Mathf.Min(panelH, maxPanelH);

        float btnCenterX = _buttonPos.x + btnW / 2f;
        float panelX = btnCenterX - panelW / 2f;
        panelX = Mathf.Clamp(panelX, 10, Screen.width - panelW - 10);

        float panelY;
        if (expandDown)
            panelY = _buttonPos.y + btnH + gap;
        else
            panelY = _buttonPos.y - gap - panelH;
        panelY = Mathf.Clamp(panelY, 10, Screen.height - panelH - 10);

        var panelRect = new Rect(panelX, panelY, panelW, panelH);

        bool isHovering = panelRect.Contains(Event.current.mousePosition);
        var savedColor = GUI.color;
        if (!isHovering)
            GUI.color = new Color(1f, 1f, 1f, 0.4f);

        GUI.Box(panelRect, "", _bgStyle);

        if (Event.current.type == EventType.ScrollWheel &&
            panelRect.Contains(Event.current.mousePosition))
        {
            _scrollOffset += Event.current.delta.y > 0 ? 3 : -3;
            Event.current.Use();
        }

        float cx = panelX + padding;
        float cy = panelY + padding;

        int complete = recipes.Count(r => plugin.IsRecipeEvolved(r));
        int inProg = recipes.Count(r => !plugin.IsRecipeEvolved(r) && r.IsPartiallyOwned(ownedCounts));
        GUI.Label(new Rect(cx, cy, panelW - padding * 2, lineH),
            $"进化配方 ({complete} 已完成, {inProg} 进行中)", _headerStyle);
        cy += lineH + padding;

        float clipBottom = panelY + panelH - padding;
        int lineIdx = 0;

        for (int ri = 0; ri < recipes.Count; ri++)
        {
            var recipe = recipes[ri];
            int owned = recipe.GetOwnedCount(ownedCounts);
            int total = recipe.RequiredGroups.Count;
            bool isComplete = plugin.IsRecipeEvolved(recipe);
            int recipeLines = 1 + total;

            if (lineIdx + recipeLines <= _scrollOffset)
            {
                lineIdx += recipeLines;
                continue;
            }

            int markedCount = recipe.RequiredGroups.Count(g =>
            {
                try { return g != null && _markedGroupIds.Contains(g.AssetId); }
                catch { return false; }
            });
            bool recipeFullyMarked = markedCount == total && total > 0;

            if (lineIdx >= _scrollOffset && cy < clipBottom)
            {
                var rowRect = new Rect(panelX, cy, panelW, lineH);
                var recipeBg = isComplete ? _completeStyle : _bgStyle;
                GUI.Box(rowRect, "", recipeBg);

                _recipeStyle.normal.textColor = isComplete
                    ? new Color(1f, 0.85f, 0f) : Color.white;

                string icon = isComplete ? "★" : $"({owned}/{total})";
                string status = isComplete ? " [完成]" : "";
                GUI.Label(new Rect(cx, cy, panelW - padding * 2 - markBarW, lineH),
                    $"{icon} {recipe.GetName()}{status}", _recipeStyle);

                if (markedCount > 0)
                {
                    var barStyle = recipeFullyMarked ? _markBarFullStyle : _markBarPartialStyle;
                    GUI.Box(new Rect(panelX + panelW - markBarW, cy, markBarW, lineH), "", barStyle);
                }

                if (Event.current.type == EventType.MouseDown &&
                    Event.current.button == 1 &&
                    rowRect.Contains(Event.current.mousePosition))
                {
                    if (recipeFullyMarked)
                    {
                        foreach (var g in recipe.RequiredGroups)
                        {
                            try { if (g != null) _markedGroupIds.Remove(g.AssetId); }
                            catch { }
                        }
                    }
                    else
                    {
                        foreach (var g in recipe.RequiredGroups)
                        {
                            try { if (g != null) _markedGroupIds.Add(g.AssetId); }
                            catch { }
                        }
                    }
                    Event.current.Use();
                }

                cy += lineH;
            }
            lineIdx++;

            foreach (var group in recipe.RequiredGroups)
            {
                if (group == null) { lineIdx++; continue; }
                if (lineIdx < _scrollOffset) { lineIdx++; continue; }
                if (cy >= clipBottom) { lineIdx++; continue; }

                try
                {
                    string groupId = group.AssetId;
                    int count = ownedCounts.TryGetValue(groupId, out var c) ? c : 0;
                    bool isOwned = count > 0;
                    string displayName = recipe.GetGroupDisplayName(group);
                    bool isGroupMarked = _markedGroupIds.Contains(groupId);

                    float rowX = panelX + padding * 2;
                    float rowW = panelW - padding * 2;
                    var lineBg = isOwned ? _ownedStyle : _missingStyle;
                    GUI.Box(new Rect(rowX, cy, rowW, lineH - 1), "", lineBg);

                    string mark = isOwned ? "✓" : "✗";
                    _componentStyle.normal.textColor = isOwned
                        ? new Color(0.6f, 1f, 0.6f)
                        : new Color(1f, 0.5f, 0.5f);
                    GUI.Label(new Rect(panelX + padding * 3, cy,
                        panelW - padding * 4 - markBarW, lineH - 1),
                        $" {mark} {displayName}{(count > 1 ? $" x{count}" : "")}", _componentStyle);

                    if (isGroupMarked)
                    {
                        GUI.Box(new Rect(panelX + panelW - markBarW, cy,
                            markBarW, lineH - 1), "", _markBarFullStyle);
                    }

                    var clickRect = new Rect(rowX, cy, rowW, lineH);
                    if (Event.current.type == EventType.MouseDown &&
                        Event.current.button == 1 &&
                        clickRect.Contains(Event.current.mousePosition))
                    {
                        if (isGroupMarked)
                            _markedGroupIds.Remove(groupId);
                        else
                            _markedGroupIds.Add(groupId);
                        Event.current.Use();
                    }

                    cy += lineH;
                }
                catch { cy += lineH; }
                lineIdx++;
            }
        }

        GUI.color = savedColor;
    }

    private void DrawChoiceMarkers(EvoTrackerPlugin plugin, Dictionary<string, int> ownedCounts)
    {
        float markerH = 22 * _scale;
        var cam = Camera.main;

        foreach (var choice in plugin.CurrentChoices)
        {
            if (choice == null || choice.Config == null) continue;
            if (choice.Config.cardGroup == null) continue;

            string groupId;
            try { groupId = choice.Config.cardGroup.AssetId; }
            catch { continue; }

            bool isChoiceOwned = ownedCounts.TryGetValue(groupId, out var cc) && cc > 0;
            bool isPinned = _markedGroupIds.Contains(groupId);

            var matchingRecipes = plugin.AllRecipes.Where(r =>
                r.RequiredGroups.Any(g =>
                {
                    try { return g != null && g.AssetId == groupId; }
                    catch { return false; }
                })
            ).ToList();

            if (matchingRecipes.Count == 0) continue;

            float screenX = Screen.width / 2f;
            float screenY = Screen.height * 0.2f;

            if (choice.ViewTransform != null && cam != null)
            {
                try
                {
                    var worldPos = choice.ViewTransform.position;
                    var sp = cam.WorldToScreenPoint(worldPos);
                    screenX = sp.x;
                    screenY = Screen.height - sp.y - 100 * _scale;
                }
                catch { }
            }

            float markerY = screenY;
            foreach (var recipe in matchingRecipes)
            {
                bool isEvolved = plugin.IsRecipeEvolved(recipe);
                int owned = recipe.GetOwnedCount(ownedCounts);
                int total = recipe.RequiredGroups.Count;

                int choiceCount = cc;
                string statusTag;
                GUIStyle baseStyle;
                if (isEvolved)
                {
                    statusTag = $"★ 已进化(x{choiceCount})";
                    baseStyle = _markerEvolvedStyle;
                }
                else if (isChoiceOwned)
                {
                    statusTag = $"✓ 已有(x{choiceCount})";
                    baseStyle = _markerOwnedStyle;
                }
                else
                {
                    statusTag = "★ 需要";
                    baseStyle = _markerNeededStyle;
                }

                string label;
                GUIStyle style;
                if (isPinned)
                {
                    label = $"[!] {statusTag} → {recipe.GetName()} ({owned}/{total})";
                    style = _markerPinnedStyle;
                }
                else
                {
                    label = $"{statusTag} → {recipe.GetName()} ({owned}/{total})";
                    style = baseStyle;
                }

                float labelW = style.CalcSize(new GUIContent(label)).x + 16;
                float x = screenX - labelW / 2f;
                x = Mathf.Clamp(x, 0, Screen.width - labelW);
                float y = Mathf.Clamp(markerY, 0, Screen.height - markerH);

                GUI.Box(new Rect(x, y, labelW, markerH), label, style);
                markerY += markerH + 2;
            }
        }
    }
}
