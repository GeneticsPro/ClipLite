using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClipLite
{
    /// <summary>
    /// UI strings. The English text is itself the lookup key, so anything missing from the
    /// table simply falls through untranslated. Long-lived UI registers its texts with
    /// <see cref="Bind"/> so the language can be switched without restarting.
    /// </summary>
    internal static class Loc
    {
        public const string Auto = "auto";
        public static readonly string[] Languages = { Auto, "en", "uk" };

        static string lang = "en";
        static readonly List<Action> binders = new List<Action>();

        /// <summary>Resolved language, always "en" or "uk".</summary>
        public static string Language { get { return lang; } }

        public static CultureInfo Culture
        {
            get { return CultureInfo.GetCultureInfo(lang == "uk" ? "uk-UA" : "en-US"); }
        }

        public static void Init(Settings s) { Use(s.Get("Language", Auto)); }

        /// <summary>Applies a stored language code ("auto" | "en" | "uk").</summary>
        public static void Use(string code)
        {
            if (string.IsNullOrEmpty(code) || code == Auto)
                code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            lang = code == "uk" ? "uk" : "en";
        }

        public static string LanguageName(string code)
        {
            switch (code)
            {
                case "en": return "English";
                case "uk": return "Українська";
                default: return T("System default");
            }
        }

        public static string T(string en)
        {
            string v;
            return lang == "uk" && uk.TryGetValue(en, out v) ? v : en;
        }

        public static string T(string en, params object[] args)
        {
            return string.Format(Culture, T(en), args);
        }

        /// <summary>Applies a piece of UI text now and again whenever the language changes.</summary>
        public static void Bind(Action apply) { binders.Add(apply); apply(); }

        public static void Retranslate() { foreach (var a in binders) a(); }

        static readonly Dictionary<string, string> uk = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ----- tree / list -----
            { "My Clips", "Мої кліпи" },
            { "InBox", "Вхідні" },
            { "Trash Can", "Кошик" },
            { "Search Results", "Пошук" },
            { "Search Results ({0})", "Пошук ({0})" },
            { "Safe", "Збережені" },
            { "Title", "Назва" },
            { "Date", "Дата" },
            { "Search clips…  (Ctrl+F)", "Пошук кліпів…  (Ctrl+F)" },
            { "(empty)", "(порожньо)" },

            // ----- editor toolbar -----
            { "Paste into previous window (Enter)", "Вставити в попереднє вікно (Enter)" },
            { "Copy to clipboard (Ctrl+C)", "Копіювати в буфер (Ctrl+C)" },
            { "Save edits (Ctrl+S)", "Зберегти зміни (Ctrl+S)" },
            { "Word wrap", "Перенесення рядків" },
            { "Delete (Del)", "Видалити (Del)" },
            { "Change case", "Змінити регістр" },
            { "Options… (Ctrl+,)", "Параметри… (Ctrl+,)" },
            { "Text", "Текст" },
            { "Binary", "Двійковий" },
            { "(no HTML or RTF format in this clip)", "(у цьому кліпі немає HTML або RTF)" },

            // ----- change case -----
            { "lower case", "нижній регістр" },
            { "UPPER CASE", "ВЕРХНІЙ РЕГІСТР" },
            { "Mixed Case", "Кожне Слово З Великої" },
            { "Sentence case.", "Як у реченні." },
            { "iNVERT cASE", "іНВЕРТУВАТИ рЕГІСТР" },

            // ----- status bar -----
            { "Paste Ctrl+V", "Вставити Ctrl+V" },
            { "Paste Shift+Ins", "Вставити Shift+Ins" },
            { "Copy only", "Лише копіювати" },
            { "{0} clips", "кліпів: {0}" },
            { "{0} Bytes", "{0} Б" },
            { "{0} Chars", "{0} симв." },
            { "{0} Words", "{0} слів" },

            // ----- context menus -----
            { "Paste", "Вставити" },
            { "Copy to clipboard", "Копіювати в буфер" },
            { "Rename…", "Перейменувати…" },
            { "Move to InBox", "Перемістити у Вхідні" },
            { "Move to Safe", "Перемістити у Збережені" },
            { "Move to Trash", "Перемістити в Кошик" },
            { "Delete permanently", "Видалити назавжди" },
            { "Empty Trash", "Очистити кошик" },

            // ----- editor context menu -----
            { "Undo", "Скасувати дію" },
            { "Cut", "Вирізати" },
            { "Copy", "Копіювати" },
            { "Delete", "Видалити" },
            { "Select All", "Виділити все" },

            // ----- tray -----
            { "Show ClipLite", "Показати ClipLite" },
            { "Pause capture", "Призупинити захоплення" },
            { "Run at Windows startup", "Запускати разом із Windows" },
            { "Put the selected clip on the clipboard", "Вибраний кліп одразу класти в буфер обміну" },
            { "Open data folder", "Відкрити папку даних" },
            { "Options…", "Параметри…" },
            { "Edit settings.ini (restart to apply)", "Редагувати settings.ini (потрібен перезапуск)" },
            { "Exit", "Вихід" },
            { "ClipLite (paused)", "ClipLite (призупинено)" },
            { "Running in the tray. Press {0} to open.", "Працює в треї. Натисніть {0}, щоб відкрити." },
            { "Hotkey {0} is taken by another app. Change it in Options.", "Гарячу клавішу {0} зайняла інша програма. Змініть її в параметрах." },

            // ----- dialogs -----
            { "Rename clip", "Перейменувати кліп" },
            { "Delete this clip permanently?", "Видалити цей кліп назавжди?" },
            { "Delete {0} clips permanently?", "Видалити назавжди кліпів: {0}?" },
            { "Permanently delete {0} clips from Trash?", "Видалити з кошика назавжди кліпів: {0}?" },
            { "OK", "OK" },
            { "Cancel", "Скасувати" },
            { "Browse…", "Огляд…" },
            { "▶  Test", "▶  Тест" },
            { "Off", "Вимк." },
            { "Default:", "Стандартний:" },
            { "Custom:", "Власний:" },
            { "WAV sounds (*.wav)|*.wav", "Звуки WAV (*.wav)|*.wav" },

            // ----- options -----
            { "ClipLite — Options", "ClipLite — Параметри" },
            { "General", "Загальні" },
            { "Language:", "Мова:" },
            { "Global hotkey:", "Глобальна клавіша:" },
            { "Paste mode:", "Режим вставки:" },
            { "InBox limit:", "Ліміт Вхідних:" },
            { "Trash limit:", "Ліміт Кошика:" },
            { "System default", "Як у системі" },
            { "Sounds", "Звуки" },
            { "Hotkey {0} could not be registered — another application is using it.", "Не вдалося зареєструвати {0} — її використовує інша програма." },

            // ----- sound events / presets -----
            { "New Data Captured From Clipboard", "Нові дані захоплено з буфера" },
            { "Clipboard Erased By Another Application", "Буфер очищено іншою програмою" },
            { "Clipboard Data Ignored/Rejected", "Дані буфера проігноровано/відхилено" },
            { "Capture", "Захоплення" },
            { "Erase", "Очищення" },
            { "Ignore", "Ігнорування" },
            { "Append", "Додавання" },
        };
    }
}
