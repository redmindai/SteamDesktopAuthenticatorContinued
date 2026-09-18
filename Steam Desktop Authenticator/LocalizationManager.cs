using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    /// <summary>
    /// A deliberately small translation layer: one flat JSON table per language, embedded in
    /// the assembly, looked up by control name. Anything without a key keeps whatever the
    /// designer set, so untranslated strings degrade to English instead of disappearing.
    /// </summary>
    public static class LocalizationManager
    {
        public const string DefaultLanguage = "en";

        /// <summary>Languages offered in Settings. Names are written in the language itself.</summary>
        public static readonly LanguageOption[] AvailableLanguages =
        {
            new LanguageOption("en", "English"),
            new LanguageOption("uk", "Українська")
        };

        private static Dictionary<string, string> current = new Dictionary<string, string>();
        private static Dictionary<string, string> fallback = new Dictionary<string, string>();

        public static string CurrentLanguage { get; private set; } = DefaultLanguage;

        /// <summary>
        /// Loads a language table plus the English one behind it. An unknown or broken
        /// language falls back to English rather than leaving the UI blank.
        /// </summary>
        public static void Load(string languageCode)
        {
            fallback = LoadTable(DefaultLanguage);

            if (string.IsNullOrWhiteSpace(languageCode) || languageCode == DefaultLanguage)
            {
                current = fallback;
                CurrentLanguage = DefaultLanguage;
                return;
            }

            Dictionary<string, string> table = LoadTable(languageCode);
            if (table.Count == 0)
            {
                current = fallback;
                CurrentLanguage = DefaultLanguage;
                return;
            }

            current = table;
            CurrentLanguage = languageCode;
        }

        /// <summary>Translation for a key, the English string if untranslated, else null.</summary>
        public static string Find(string key)
        {
            string value;
            if (current.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;
            if (fallback.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;
            return null;
        }

        /// <summary>Translation for a key, or the key itself when there is none.</summary>
        public static string T(string key)
        {
            return Find(key) ?? key;
        }

        /// <summary>Translation for a key, or <paramref name="englishText"/> when there is none.</summary>
        public static string T(string key, string englishText)
        {
            return Find(key) ?? englishText;
        }

        /// <summary>
        /// Walks a form and replaces the text of every control that has a key, using
        /// "FormName.ControlName". The form's own title is "FormName.$Title". Controls
        /// without a key are left alone, which is what keeps runtime text such as the
        /// current code or status line from being overwritten.
        /// </summary>
        public static void ApplyTo(Form form)
        {
            if (form == null || string.IsNullOrEmpty(form.Name)) return;

            string title = Find(form.Name + ".$Title");
            if (title != null) form.Text = title;

            ApplyToControls(form.Name, form.Controls);
        }

        /// <summary>
        /// Context menus hang off a form's components rather than its control tree, so they
        /// have to be handed over explicitly.
        /// </summary>
        public static void ApplyTo(Form form, params ToolStrip[] detachedMenus)
        {
            ApplyTo(form);

            if (form == null || detachedMenus == null) return;
            foreach (ToolStrip menu in detachedMenus)
                ApplyToItems(form.Name, menu?.Items);
        }

        private static void ApplyToControls(string prefix, Control.ControlCollection controls)
        {
            if (controls == null) return;

            foreach (Control control in controls)
            {
                if (!string.IsNullOrEmpty(control.Name))
                {
                    string text = Find(prefix + "." + control.Name);
                    if (text != null) control.Text = text;
                }

                ToolStrip strip = control as ToolStrip;
                if (strip != null) ApplyToItems(prefix, strip.Items);

                ApplyToControls(prefix, control.Controls);

                SplitContainer split = control as SplitContainer;
                if (split != null)
                {
                    ApplyToControls(prefix, split.Panel1.Controls);
                    ApplyToControls(prefix, split.Panel2.Controls);
                }
            }
        }

        private static void ApplyToItems(string prefix, ToolStripItemCollection items)
        {
            if (items == null) return;

            foreach (ToolStripItem item in items)
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    string text = Find(prefix + "." + item.Name);
                    if (text != null) item.Text = text;
                }

                ToolStripDropDownItem dropDown = item as ToolStripDropDownItem;
                if (dropDown != null) ApplyToItems(prefix, dropDown.DropDownItems);
            }
        }

        private static Dictionary<string, string> LoadTable(string languageCode)
        {
            var empty = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(languageCode)) return empty;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string suffix = "lang." + languageCode + ".json";
                string name = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                if (name == null) return empty;

                using (Stream stream = assembly.GetManifestResourceStream(name))
                using (StreamReader reader = new StreamReader(stream))
                {
                    var table = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
                    return table ?? empty;
                }
            }
            catch (Exception)
            {
                // A missing or malformed table must never stop the program from starting.
                return empty;
            }
        }

        public class LanguageOption
        {
            public string Code { get; private set; }
            public string DisplayName { get; private set; }

            public LanguageOption(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }

            public override string ToString() { return DisplayName; }
        }
    }
}
