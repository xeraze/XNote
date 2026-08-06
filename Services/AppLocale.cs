using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace XNote.Services;

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

public static class AppLocale
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
    public string Settings => AppLocale.T("Settings", "Настройки");
    public string Configuration => AppLocale.T("Configuration", "Конфигурация");
    public string Information => AppLocale.T("Information", "Информация");
    public string Language => AppLocale.T("Language", "Язык");
    public string LanguageRestartHint => AppLocale.T(
        "Restart the app to apply the language.",
        "Перезапустите приложение, чтобы применить язык.");
    public string About => AppLocale.T("About", "О приложении");
    public string AboutText => AppLocale.T(
        "XNote v0.7. Developed by xeraze.",
        "XNote v0.7. Разработано xeraze.");
    public string Hotkeys => AppLocale.T("Hotkeys", "Горячие клавиши");
    public string Bold => AppLocale.T("Bold", "Жирный");
    public string Italic => AppLocale.T("Italic", "Курсив");
    public string Underline => AppLocale.T("Underline", "Подчёркнутый");
    public string Undo => AppLocale.T("Undo", "Отменить");
    public string Redo => AppLocale.T("Redo", "Повторить");

    public string SearchPlaceholder => AppLocale.T("Search notes…", "Поиск заметок…");
    public string TipSettings => AppLocale.T("Settings", "Настройки");
    public string TipImportDirect => AppLocale.T("Import note as a .txt file", "Импорт заметки как .txt файла");
    public string TipNewNote => AppLocale.T("New note (Ctrl+N)", "Новая заметка (Ctrl+N)");
    public string RegularNote => AppLocale.T("Regular note", "Обычная заметка");
    public string TimedNote => AppLocale.T("Timed note", "Временная заметка");

    public string FilterAll => AppLocale.T("All", "Все");
    public string FilterNotes => AppLocale.T("Notes", "Заметки");
    public string FilterTasks => AppLocale.T("Tasks", "Задачи");
    public string FilterOpen => AppLocale.T("Open", "Открытые");
    public string FilterDone => AppLocale.T("Done", "Выполненные");

    public string Draft => AppLocale.T("Draft", "Черновик");
    public string Timed => AppLocale.T("Timed", "Временная");
    public string Save => AppLocale.T("Save", "Сохранить");
    public string TipSave => AppLocale.T("Save this note", "Сохранить заметку");
    public string Task => AppLocale.T("Task", "Задача");
    public string TipTask => AppLocale.T("Mark as a task", "Отметить как задачу");
    public string Done => AppLocale.T("Done", "Готово");
    public string TipDone => AppLocale.T("Mark task as done", "Отметить задачу выполненной");

    public string RemindMe => AppLocale.T("Remind me", "Напомнить");
    public string Date => AppLocale.T("Date", "Дата");
    public string Time => AppLocale.T("Time", "Время");
    public string Clear => AppLocale.T("Clear", "Очистить");
    public string Set => AppLocale.T("Set", "Установить");
    public string YearsPrefix => AppLocale.T("Years:", "Годы:");
    public string ExpiryDate => AppLocale.T("Expiry date", "Дата удаления");
    public string ExpiryTime => AppLocale.T("Expiry time", "Время удаления");
    public string CountdownAfterSave => AppLocale.T(
        "Countdown starts after save.",
        "Обратный отсчёт начнётся после сохранения.");
    public string TipAutoDelete => AppLocale.T("Auto-delete timer", "Таймер авто-удаления");

    public string Import => AppLocale.T("Import", "Импорт");
    public string Export => AppLocale.T("Export", "Экспорт");
    public string TipDelete => AppLocale.T("Delete", "Удалить");
    public string DeleteConfirm => AppLocale.T("Delete this note?", "Удалить эту заметку?");
    public string Delete => AppLocale.T("Delete", "Удалить");
    public string Cancel => AppLocale.T("Cancel", "Отмена");
    public string TagsPlaceholder => AppLocale.T(
        "Add tags, separated by commas…",
        "Теги через запятую…");

    public string NoNoteSelected => AppLocale.T("No note selected", "Заметка не выбрана");
    public string NoNoteHint => AppLocale.T(
        "Pick a note on the left, or create a new one",
        "Выберите заметку слева или создайте новую");
    public string CreateFirstNote => AppLocale.T("Create your first note", "Создать первую заметку");

    public string NoteDeleted => AppLocale.T("Note deleted", "Заметка удалена");
    public string NotesDeletedMany(int count) => AppLocale.T(
        $"{count} notes deleted",
        count switch
        {
            1 => "1 заметка удалена",
            >= 2 and <= 4 => $"{count} заметки удалены",
            _ => $"{count} заметок удалено",
        });
    public string UndoStackPreview(string lastTitle, int moreCount) => AppLocale.T(
        $"{lastTitle} +{moreCount} more",
        $"{lastTitle} и ещё {moreCount}");
    public string Dismiss => AppLocale.T("Dismiss", "Закрыть");
    public string OpenNote => AppLocale.T("Open Note", "Открыть");
    public string Reminder => AppLocale.T("Reminder", "Напоминание");

    public string Minimize => AppLocale.T("Minimize", "Свернуть");
    public string MaximizeRestore => AppLocale.T("Maximize", "Развернуть");
    public string Close => AppLocale.T("Close", "Закрыть");

    public string TrayNewNote => AppLocale.T("New Note", "Новая заметка");
    public string TrayExit => AppLocale.T("Exit", "Выход");

    public string Untitled => AppLocale.T("Untitled", "Без названия");
    public string NewNoteTitle => AppLocale.T("New note", "Новая заметка");
    public string NewTimedNoteTitle => AppLocale.T("New timed note", "Новая временная заметка");

    public string SetupTitle => AppLocale.T("Timed note", "Временная заметка");
    public string SetupSubtitle => AppLocale.T(
        "Pick auto-delete date to create timed note.",
        "Укажите дату авто-удаления для создания временной заметки.");
    public string SetupConfirmInfo => AppLocale.T(
        "The countdown starts only after you save the note. 30 seconds before auto-delete you will get a notification.",
        "Обратный отсчёт начнётся только после сохранения заметки. За 30 секунд до удаления вам придёт уведомление.");
    public string Confirm => AppLocale.T("Confirm", "Подтвердить");

    public string ReminderDueNow => AppLocale.T("Reminder is due now.", "Время напоминания наступило.");
    public string TimedRemoveIn30 => AppLocale.T(
        "Will be removed in 30 seconds.",
        "Будет удалена через 30 секунд.");

    public string NoReminder => AppLocale.T("No reminder", "Нет напоминания");
    public string NoTimer => AppLocale.T("No timer", "Нет таймера");
    public string Today => AppLocale.T("Today", "Сегодня");
    public string Tomorrow => AppLocale.T("Tomorrow", "Завтра");

    public string Created => AppLocale.T("Created", "Создано");
    public string Edited => AppLocale.T("Edited", "Изменено");
    public string SavedStatus => AppLocale.T("Saved ✓", "Сохранено ✓");
    public string SavingStatus => AppLocale.T("Saving", "Сохранение");

    public string LanguageEnglish => "English";
    public string LanguageRussian => "Русский";

    public string NotesOne => AppLocale.T("1 note", "1 заметка");
    public string NotesMany(int n) => AppLocale.T($"{n} notes", $"{n} заметок");
    public string NotesFiltered(int filtered, string totalStr) =>
        AppLocale.T($"{filtered} of {totalStr}", $"{filtered} из {totalStr}");
}