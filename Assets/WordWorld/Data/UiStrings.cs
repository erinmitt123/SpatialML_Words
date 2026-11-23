using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWorld.Data
{
    /// <summary>Localization table (Assets/Data/ui_strings.json): every UI string, in en and zh.</summary>
    [AddComponentMenu("Word World/UI Strings")]
    public sealed class UiStrings : MonoBehaviour
    {
        [SerializeField] private TextAsset m_table;

        [Serializable]
        private sealed class Row
        {
            public string key;
            public string en;
            public string zh;
        }

        [Serializable]
        private sealed class Table
        {
            public Row[] strings;
        }

        private readonly Dictionary<string, Row> m_rows = new Dictionary<string, Row>();

        private void Awake()
        {
            if (m_table == null)
            {
                Debug.LogError("[WordWorld.UiStrings] no table assigned");
                return;
            }
            Table table = JsonUtility.FromJson<Table>(m_table.text);
            if (table?.strings == null) return;
            foreach (Row row in table.strings)
                if (!string.IsNullOrEmpty(row.key)) m_rows[row.key] = row;
        }

        /// <summary>The string in "en" or "zh"; falls back to English, then to the key so gaps are visible.</summary>
        public string Get(string key, string language)
        {
            if (!m_rows.TryGetValue(key, out Row row)) return key;
            string value = language == "zh" ? row.zh : row.en;
            return string.IsNullOrEmpty(value) ? (string.IsNullOrEmpty(row.en) ? key : row.en) : value;
        }
    }
}
