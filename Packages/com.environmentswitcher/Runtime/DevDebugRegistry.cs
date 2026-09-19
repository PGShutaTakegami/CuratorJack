using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EnvironmentSwitcher
{
    /// <summary>
    /// ゲーム側拡張が DEBUG パネルに UI を足すときのコンテキスト。
    /// </summary>
    public sealed class DevDebugMenuContext
    {
        private readonly Font _font;

        internal DevDebugMenuContext(Transform content, Font font)
        {
            Content = content;
            _font = font;
        }

        /// <summary>パネル内のスクロール Content。</summary>
        public Transform Content { get; }

        public void AddSectionHeader(string text)
        {
            AddLabel(text, 18, FontStyle.Bold);
        }

        public void AddLabel(string text, int fontSize = 16, FontStyle style = FontStyle.Normal)
        {
            GameObject labelGo = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(Content, false);

            Text label = labelGo.GetComponent<Text>();
            label.text = text;
            label.font = _font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;

            LayoutElement layout = labelGo.AddComponent<LayoutElement>();
            layout.minHeight = fontSize + 10;
            layout.preferredHeight = fontSize + 10;
        }

        public Button AddButton(string name, string label, Color color, UnityAction onClick, float height = 44f)
        {
            GameObject buttonGo = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonGo.transform.SetParent(Content, false);

            Image image = buttonGo.GetComponent<Image>();
            image.color = color;

            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            LayoutElement layout = buttonGo.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);

            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text text = labelGo.GetComponent<Text>();
            text.text = label;
            text.font = _font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return button;
        }

        public Transform AddHorizontalRow(string name, float height = 44f)
        {
            GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(Content, false);

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = height;
            rowLayout.preferredHeight = height;
            return row.transform;
        }

        /// <summary>横並び行の中にボタンを追加する。</summary>
        public Button AddButtonToRow(
            Transform row,
            string name,
            string label,
            Color color,
            UnityAction onClick,
            float height = 40f)
        {
            GameObject buttonGo = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonGo.transform.SetParent(row, false);

            Image image = buttonGo.GetComponent<Image>();
            image.color = color;

            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            LayoutElement layout = buttonGo.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);

            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text text = labelGo.GetComponent<Text>();
            text.text = label;
            text.font = _font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return button;
        }
    }

    /// <summary>拡張セクションのビルダー。</summary>
    public delegate void DevDebugSectionBuilder(DevDebugMenuContext context);

    /// <summary>
    /// ゲーム固有の DEBUG 項目を登録する入口。
    /// パッケージ本体は標準機能のみ。中身は各ゲームが Register する。
    /// </summary>
    public static class DevDebugRegistry
    {
        private sealed class SectionEntry
        {
            public string Id;
            public string Title;
            public DevDebugSectionBuilder Builder;
            public int Order;
        }

        private static readonly List<SectionEntry> Sections = new List<SectionEntry>();

        /// <summary>セクション構成が変わったとき（遅延登録の再構築用）。</summary>
        public static event System.Action SectionsChanged;

        /// <summary>
        /// DEBUG パネルにセクションを追加する。同じ id なら上書き。
        /// </summary>
        public static void RegisterSection(
            string id,
            string title,
            DevDebugSectionBuilder builder,
            int order = 0)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id が空です。", nameof(id));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (int i = 0; i < Sections.Count; i++)
            {
                if (Sections[i].Id == id)
                {
                    Sections[i].Title = title ?? id;
                    Sections[i].Builder = builder;
                    Sections[i].Order = order;
                    SectionsChanged?.Invoke();
                    return;
                }
            }

            Sections.Add(new SectionEntry
            {
                Id = id,
                Title = string.IsNullOrEmpty(title) ? id : title,
                Builder = builder,
                Order = order
            });
            SectionsChanged?.Invoke();
        }

        public static void UnregisterSection(string id)
        {
            bool removed = false;
            for (int i = Sections.Count - 1; i >= 0; i--)
            {
                if (Sections[i].Id == id)
                {
                    Sections.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                SectionsChanged?.Invoke();
            }
        }

        public static void Clear()
        {
            if (Sections.Count == 0)
            {
                return;
            }

            Sections.Clear();
            SectionsChanged?.Invoke();
        }

        internal static void BuildAll(DevDebugMenuContext context)
        {
            if (Sections.Count == 0)
            {
                return;
            }

            SectionEntry[] ordered = Sections.ToArray();
            Array.Sort(ordered, (a, b) =>
            {
                int cmp = a.Order.CompareTo(b.Order);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.Id, b.Id);
            });

            for (int i = 0; i < ordered.Length; i++)
            {
                SectionEntry entry = ordered[i];
                context.AddSectionHeader(entry.Title);
                try
                {
                    entry.Builder(context);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    context.AddLabel($"[Error] {entry.Id}: {e.Message}", 14, FontStyle.Italic);
                }
            }
        }
    }
}
