using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace XNote.Utils;

public class AppSettings
{
    public string Language { get; set; } = "en";
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string FilePath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XNote");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}

public enum AppLanguage
{
    English,
    Russian,
}

public static class Strings
{
    public static AppLanguage Current { get; private set; } = AppLanguage.English;

    public static string CurrentCode => Current == AppLanguage.Russian ? "ru" : "en";

    public static void ApplyFromSettings()
    {
        var settings = SettingsStore.Load();
        Apply(Parse(settings.Language));
    }

    public static AppLanguage Parse(string? code) =>
        string.Equals(code, "ru", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Russian
            : AppLanguage.English;

    public static void Apply(AppLanguage language)
    {
        Current = language;
        var culture = language == AppLanguage.Russian
            ? new CultureInfo("ru-RU")
            : new CultureInfo("en-US");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static string T(string en, string ru) =>
        Current == AppLanguage.Russian ? ru : en;
}

public static class Ui
{
    public static UiStrings Strings { get; } = new();
}

public class UiStrings
{
    public string Settings => Strings.T("Settings", "Настройки");
    public string Configuration => Strings.T("Configuration", "Конфигурация");
    public string Information => Strings.T("Information", "Информация");
    public string Language => Strings.T("Language", "Язык");
    public string LanguageRestartHint => Strings.T(
        "Restart the app to apply the language.",
        "Перезапустите приложение, чтобы применить язык.");
    public string About => Strings.T("About", "О приложении");
    public string AboutText => Strings.T(
        "XNote v0.9. Developed by xeraze.",
        "XNote v0.9. Разработано xeraze.");
    public string Hotkeys => Strings.T("Hotkeys", "Горячие клавиши");
    public string Bold => Strings.T("Bold", "Жирный");
    public string Italic => Strings.T("Italic", "Курсив");
    public string Underline => Strings.T("Underline", "Подчёркнутый");
    public string Undo => Strings.T("Undo", "Отменить");
    public string Redo => Strings.T("Redo", "Повторить");

    public string SearchPlaceholder => Strings.T("Search notes…", "Поиск заметок…");
    public string TipSettings => Strings.T("Settings", "Настройки");
    public string TipImportDirect => Strings.T("Import note as a .txt file", "Импорт заметки как .txt файла");
    public string TipNewNote => Strings.T("New note (Ctrl+N)", "Новая заметка (Ctrl+N)");
    public string RegularNote => Strings.T("Regular note", "Обычная заметка");
    public string TimedNote => Strings.T("Timed note", "Временная заметка");

    public string FilterAll => Strings.T("All", "Все");
    public string FilterNotes => Strings.T("Notes", "Заметки");
    public string FilterTasks => Strings.T("Tasks", "Задачи");
    public string FilterOpen => Strings.T("Open", "Открытые");
    public string FilterDone => Strings.T("Done", "Выполненные");

    public string Draft => Strings.T("Draft", "Черновик");
    public string Timed => Strings.T("Timed", "Временная");
    public string Save => Strings.T("Save", "Сохранить");
    public string TipSave => Strings.T("Save this note", "Сохранить заметку");
    public string TipImage => Strings.T("Insert image", "Вставить фото");
    public string TipVideo => Strings.T("Insert video", "Вставить видео");
    public string InsertVideoFile => Strings.T("Video file…", "Видеофайл…");
    public string InsertVideoLink => Strings.T("Add link", "Добавить ссылку");
    public string VideoUrlPlaceholder => Strings.T(
        "Paste a YouTube or video link…",
        "Вставьте ссылку YouTube или видео…");
    public string VideoCopyFailed => Strings.T(
        "Couldn't copy the video file.",
        "Не удалось скопировать видеофайл.");
    public string VideoPlayerError => Strings.T(
        "Couldn't open the player.",
        "Не удалось открыть плеер.");
    public string VideoFallbackTitle => Strings.T(
        "This link can't be played here.",
        "Эта ссылка не может быть проиграна здесь.");
    public string OpenInBrowser => Strings.T(
        "Open in browser",
        "Открыть в браузере");
    public string WatchOnYouTube => Strings.T(
        "Watch on YouTube",
        "Смотреть на YouTube");
    public string VideoLoading => Strings.T(
        "Loading video…",
        "Загрузка видео…");
    public string Task => Strings.T("Task", "Задача");
    public string TipTask => Strings.T("Mark as a task", "Отметить как задачу");
    public string Done => Strings.T("Done", "Готово");
    public string TipDone => Strings.T("Mark task as done", "Отметить задачу выполненной");

    public string RemindMe => Strings.T("Remind me", "Напомнить");
    public string Date => Strings.T("Date", "Дата");
    public string Time => Strings.T("Time", "Время");
    public string Clear => Strings.T("Clear", "Очистить");
    public string Set => Strings.T("Set", "Установить");
    public string YearsPrefix => Strings.T("Years:", "Годы:");
    public string ExpiryDate => Strings.T("Expiry date", "Дата удаления");
    public string ExpiryTime => Strings.T("Expiry time", "Время удаления");
    public string CountdownAfterSave => Strings.T(
        "Countdown starts after save.",
        "Обратный отсчёт начнётся после сохранения.");
    public string TipAutoDelete => Strings.T("Auto-delete timer", "Таймер авто-удаления");

    public string Import => Strings.T("Import", "Импорт");
    public string Export => Strings.T("Export", "Экспорт");
    public string TipDelete => Strings.T("Delete", "Удалить");
    public string DeleteConfirm => Strings.T("Delete this note?", "Удалить эту заметку?");
    public string Delete => Strings.T("Delete", "Удалить");
    public string Cancel => Strings.T("Cancel", "Отмена");
    public string TagsPlaceholder => Strings.T(
        "Add tags via Enter…",
        "Теги через Enter…");

    public string NoNoteSelected => Strings.T("No note selected", "Заметка не выбрана");

    public string TipEmoji => Strings.T("Emoji", "Эмодзи");
    public string TipGif => Strings.T("GIF", "GIF");
    public string EmojiTabNative => Strings.T("Basic", "Основные");
    public string EmojiTabSymbols => Strings.T("Symbols", "Символы");
    public string EmojiTabCustom => Strings.T("Custom", "Кастом");
    public string EmojiSearchPlaceholder => Strings.T("Search emoji…", "Поиск эмодзи…");
    public string GifSearchPlaceholder => Strings.T("Search GIFs…", "Поиск GIF…");
    public string NoInternetConnection => Strings.T(
        "No internet connection", "Нет подключения к интернету");
    public string GifNothingFound => Strings.T("Nothing found", "Ничего не найдено");
    public string CustomEmojiEmpty => Strings.T(
        "No custom emoji yet", "Кастомных эмодзи пока нет");
    public string CustomSymbolsEmpty => Strings.T(
        "No custom symbols yet", "Кастомных символов пока нет");
    public string NoNoteHint => Strings.T(
        "Pick a note on the left, or create a new one",
        "Выберите заметку слева или создайте новую");
    public string CreateFirstNote => Strings.T("Create your first note", "Создать первую заметку");

    public string NoteDeleted => Strings.T("Note deleted", "Заметка удалена");
    public string NotesDeletedMany(int count) => Strings.T(
        $"{count} notes deleted",
        count switch
        {
            1 => "1 заметка удалена",
            >= 2 and <= 4 => $"{count} заметки удалены",
            _ => $"{count} заметок удалено",
        });
    public string UndoStackPreview(string lastTitle, int moreCount) => Strings.T(
        $"{lastTitle} +{moreCount} more",
        $"{lastTitle} и ещё {moreCount}");
    public string Dismiss => Strings.T("Dismiss", "Закрыть");
    public string OpenNote => Strings.T("Open Note", "Открыть");
    public string Reminder => Strings.T("Reminder", "Напоминание");

    public string Minimize => Strings.T("Minimize", "Свернуть");
    public string MaximizeRestore => Strings.T("Maximize", "Развернуть");
    public string Close => Strings.T("Close", "Закрыть");

    public string TrayNewNote => Strings.T("New Note", "Новая заметка");
    public string TrayExit => Strings.T("Exit", "Выход");

    public string Untitled => Strings.T("Untitled", "Без названия");
    public string NewNoteTitle => Strings.T("New note", "Новая заметка");
    public string NewTimedNoteTitle => Strings.T("New timed note", "Новая временная заметка");

    public string SetupTitle => Strings.T("Timed note", "Временная заметка");
    public string SetupSubtitle => Strings.T(
        "Pick auto-delete date to create timed note.",
        "Укажите дату авто-удаления для создания временной заметки.");
    public string SetupConfirmInfo => Strings.T(
        "The countdown starts only after you save the note. 30 seconds before auto-delete you will get a notification.",
        "Обратный отсчёт начнётся только после сохранения заметки. За 30 секунд до удаления вам придёт уведомление.");
    public string Confirm => Strings.T("Confirm", "Подтвердить");

    public string ReminderDueNow => Strings.T("Reminder is due now.", "Время напоминания наступило.");
    public string TimedRemoveIn30 => Strings.T(
        "Will be removed in 30 seconds.",
        "Будет удалена через 30 секунд.");

    public string NoReminder => Strings.T("No reminder", "Нет напоминания");
    public string NoTimer => Strings.T("No timer", "Нет таймера");
    public string Today => Strings.T("Today", "Сегодня");
    public string Tomorrow => Strings.T("Tomorrow", "Завтра");

    public string Created => Strings.T("Created", "Создано");
    public string Edited => Strings.T("Edited", "Изменено");
    public string SavedStatus => Strings.T("Saved ✓", "Сохранено ✓");
    public string SavingStatus => Strings.T("Saving", "Сохранение");

    public string LanguageEnglish => "English";
    public string LanguageRussian => "Русский";

    public string NotesOne => Strings.T("1 note", "1 заметка");
    public string NotesMany(int n) => Strings.T($"{n} notes", $"{n} заметок");
    public string NotesFiltered(int filtered, string totalStr) =>
        Strings.T($"{filtered} of {totalStr}", $"{filtered} из {totalStr}");

    public string CustomElementTitle => Strings.T("Custom element", "Кастомный элемент");
    public string DrawSymbolMode => Strings.T("Symbol (1 color)", "Символ (1 цвет)");
    public string DrawEmojiMode => Strings.T("Emoji (colors)", "Эмодзи (цвета)");
    public string DrawEraser => Strings.T("Eraser", "Ластик");
    public string DrawFill => Strings.T("Fill", "Заливка");
    public string DrawPipette => Strings.T("Pipette", "Пипетка");
    public string DrawPhoto => Strings.T("Photo", "Фото");
    public string DrawChooseBackground => Strings.T(
        "Choose a background image",
        "Выберите фоновое изображение");
    public string DrawCustomButton => Strings.T("+ Draw", "+ Нарисовать");
    public string DrawFileButton => Strings.T("+ File", "+ Файл");
    public string CustomEmojiCategory => Strings.T("Emoji", "Эмодзи");
    public string CustomSymbolsCategory => Strings.T("Symbols", "Символы");
    public string CustomFormatsHint => Strings.T(
        "Formats: PNG, JPG, JPEG, WEBP, BMP · resized to 24px",
        "Форматы: PNG, JPG, JPEG, WEBP, BMP · уменьшаются до 24px");
    public string ChooseImageTitle => Strings.T("Choose an image", "Выберите изображение");
    public string CustomFormatError => Strings.T(
        "Format not supported. Allowed: PNG, JPG, JPEG, WEBP, BMP.",
        "Формат не поддерживается. Разрешены: PNG, JPG, JPEG, WEBP, BMP.");
    public string CustomLoadError => Strings.T(
        "Could not load the image.",
        "Не удалось загрузить изображение.");
}