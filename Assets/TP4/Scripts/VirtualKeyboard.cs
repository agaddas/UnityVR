using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TP4
{
    /// <summary>
    /// Exercice 4 : clavier virtuel AZERTY cliquable au laser (XR Ray / Near-Far Interactor).
    /// Les touches sont générées au démarrage ; elles écrivent dans l'InputField cible.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualKeyboard : MonoBehaviour
    {
        public TMP_InputField targetField;
        [Tooltip("Afficher le clavier seulement quand l'InputField est sélectionné")]
        public bool showOnSelect = false;

        [Header("Apparence")]
        public Sprite keySprite;
        public Vector2 keySize = new Vector2(52f, 52f);
        public float spacing = 6f;
        public float fontSize = 26f;
        public Color keyColor = new Color(0.93f, 0.94f, 0.97f);
        public Color specialKeyColor = new Color(0.72f, 0.80f, 0.95f);

        static readonly string[] k_Rows = { "1234567890", "azertyuiop", "qsdfghjklm", "wxcvbn,.-'" };

        readonly List<(TMP_Text label, string ch)> m_LetterKeys = new List<(TMP_Text, string)>();
        bool m_Shift;
        bool m_Built;

        void Awake()
        {
            Build();
            if (targetField != null)
            {
                targetField.shouldHideSoftKeyboard = true;   // pas de clavier système du Quest
                targetField.shouldHideMobileInput = true;
                targetField.onSelect.AddListener(_ => Show());
            }
            if (showOnSelect) gameObject.SetActive(false);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        void Build()
        {
            if (m_Built) return;
            m_Built = true;

            int rowCount = k_Rows.Length + 1;
            float totalH = rowCount * keySize.y + (rowCount - 1) * spacing;
            float y = totalH / 2f - keySize.y / 2f;

            foreach (string row in k_Rows)
            {
                float rowW = row.Length * keySize.x + (row.Length - 1) * spacing;
                float x = -rowW / 2f + keySize.x / 2f;
                foreach (char c in row)
                {
                    string ch = c.ToString();
                    TMP_Text label = CreateKey(ch, new Vector2(x, y), keySize, keyColor, () => Type(ch));
                    m_LetterKeys.Add((label, ch));
                    x += keySize.x + spacing;
                }
                y -= keySize.y + spacing;
            }

            // Dernière rangée : touches spéciales.
            var specials = new (string label, float width, System.Action action)[]
            {
                ("Maj", 1.8f, ToggleShift),
                ("Espace", 4.6f, () => Type(" ")),
                ("Effacer", 2.0f, Backspace),
                ("Vider", 1.6f, Clear),
                ("OK", 1.6f, Submit),
            };
            float total = 0f;
            foreach (var s in specials) total += s.width * keySize.x;
            total += (specials.Length - 1) * spacing;
            float sx = -total / 2f;
            foreach (var s in specials)
            {
                float w = s.width * keySize.x;
                var action = s.action;
                CreateKey(s.label, new Vector2(sx + w / 2f, y), new Vector2(w, keySize.y), specialKeyColor, () => action());
                sx += w + spacing;
            }
            RefreshLabels();
        }

        TMP_Text CreateKey(string text, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Key " + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            img.sprite = keySprite;
            img.type = keySprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.4f);
            colors.pressedColor = new Color(1f, 0.6f, 0.2f);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.layer = gameObject.layer;
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.1f, 0.1f, 0.15f);
            tmp.raycastTarget = false;
            return tmp;
        }

        void Type(string s)
        {
            if (targetField == null) return;
            if (m_Shift) s = s.ToUpperInvariant();
            if (targetField.characterLimit > 0 && targetField.text.Length >= targetField.characterLimit) return;
            targetField.text += s;
            targetField.caretPosition = targetField.text.Length;
            if (m_Shift) { m_Shift = false; RefreshLabels(); }
        }

        void Backspace()
        {
            if (targetField == null || targetField.text.Length == 0) return;
            targetField.text = targetField.text.Substring(0, targetField.text.Length - 1);
        }

        void Clear()
        {
            if (targetField != null) targetField.text = string.Empty;
        }

        void Submit()
        {
            if (targetField != null)
            {
                targetField.onSubmit.Invoke(targetField.text);
                targetField.onEndEdit.Invoke(targetField.text);
            }
            if (showOnSelect) Hide();
        }

        void ToggleShift()
        {
            m_Shift = !m_Shift;
            RefreshLabels();
        }

        void RefreshLabels()
        {
            foreach (var (label, ch) in m_LetterKeys)
                label.text = m_Shift ? ch.ToUpperInvariant() : ch;
        }
    }
}
