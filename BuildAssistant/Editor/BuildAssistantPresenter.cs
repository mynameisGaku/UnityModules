using System;
using System.Globalization;
using System.Linq;

namespace BuildAssistant.Editor
{
    /// <summary>Owns the explicit Preview, confirmation, Build, history, and export state used by the editor window.</summary>
    internal sealed class BuildAssistantPresenter
    {
        private readonly Func<string, BuildAssistantPlan> preview;
        private readonly Func<BuildAssistantPlan, BuildAssistantBuildResult> build;
        private readonly Func<BuildAssistantHistory> loadHistory;
        private readonly Func<BuildAssistantHistoryEntry, string, BuildAssistantError> exportJson;
        private int selectedHistoryIndex = -1;

        internal BuildAssistantPresenter(Func<string, BuildAssistantPlan> preview = null, Func<BuildAssistantPlan, BuildAssistantBuildResult> build = null, Func<BuildAssistantHistory> loadHistory = null, Func<BuildAssistantHistoryEntry, string, BuildAssistantError> exportJson = null)
        {
            this.preview = preview ?? BuildAssistantService.Preview;
            this.build = build ?? BuildAssistantService.Build;
            this.loadHistory = loadHistory ?? BuildAssistantService.LoadHistory;
            this.exportJson = exportJson ?? BuildAssistantService.ExportJson;
            History = EmptyHistory();
        }

        internal string OutputRoot { get; private set; } = string.Empty;
        internal BuildAssistantPlan Plan { get; private set; }
        internal BuildAssistantBuildResult Result { get; private set; }
        internal BuildAssistantHistory History { get; private set; }
        internal bool ConfirmationAccepted { get; private set; }
        internal string Message { get; private set; } = string.Empty;
        internal string ExportMessage { get; private set; } = string.Empty;
        internal BuildAssistantError LastExportError { get; private set; } = BuildAssistantError.None;
        internal bool CanBuild => Plan != null && Plan.IsReady && ConfirmationAccepted;
        internal int SelectedHistoryIndex => selectedHistoryIndex;
        internal BuildAssistantHistoryEntry SelectedHistoryEntry => selectedHistoryIndex >= 0 && selectedHistoryIndex < History.Entries.Count ? History.Entries[selectedHistoryIndex] : null;
        internal BuildAssistantHistoryEntry ExportEntry => SelectedHistoryEntry ?? Result?.Entry;

        /// <summary>Updates the output root and invalidates any preview that captured a different value.</summary>
        internal void SetOutputRoot(string value)
        {
            var normalized = value ?? string.Empty;
            if (StringComparer.Ordinal.Equals(OutputRoot, normalized))
                return;

            OutputRoot = normalized;
            InvalidatePlan();
        }

        /// <summary>Creates a fresh plan for the current output root and requires a new confirmation.</summary>
        internal void Preview()
        {
            ConfirmationAccepted = false;
            Result = null;
            if (selectedHistoryIndex < 0 && History.Entries.Count > 0)
                selectedHistoryIndex = 0;
            ExportMessage = string.Empty;
            LastExportError = BuildAssistantError.None;
            try
            {
                Plan = preview(OutputRoot);
                if (Plan == null)
                {
                    Message = "Preview returned no plan.";
                    return;
                }

                Message = Plan.IsReady
                    ? "Preview is ready. Review and confirm the captured inputs."
                    : FormatError(Plan.Error, Plan.Message);
            }
            catch (Exception exception)
            {
                Plan = null;
                Message = "Preview failed: " + exception.Message;
            }
        }

        /// <summary>Accepts confirmation only while a ready plan is visible.</summary>
        internal void SetConfirmation(bool value)
        {
            ConfirmationAccepted = value && Plan != null && Plan.IsReady;
        }

        /// <summary>Consumes one confirmed plan, executes it once, and refreshes detached history.</summary>
        internal void Build()
        {
            if (!CanBuild)
            {
                Message = "Preview and confirm a ready plan before building.";
                return;
            }

            var consumedPlan = Plan;
            Plan = null;
            ConfirmationAccepted = false;
            try
            {
                Result = build(consumedPlan);
                Message = FormatResult(Result);
            }
            catch (Exception exception)
            {
                Result = new BuildAssistantBuildResult(false, false, BuildAssistantError.BuildInvocationFailed, exception.Message, null);
                Message = FormatResult(Result);
            }

            RefreshHistory(Result?.Entry?.RunId);
        }

        /// <summary>Reloads bounded history while preserving the current selection when possible.</summary>
        internal void RefreshHistory()
        {
            RefreshHistory(SelectedHistoryEntry?.RunId ?? Result?.Entry?.RunId);
        }

        /// <summary>Selects a visible history entry by newest-first index.</summary>
        internal void SetHistoryIndex(int value)
        {
            selectedHistoryIndex = value >= 0 && value < History.Entries.Count ? value : -1;
            ExportMessage = string.Empty;
            LastExportError = BuildAssistantError.None;
        }

        /// <summary>Exports the selected detached entry to a new JSON file.</summary>
        internal void Export(string absolutePath)
        {
            var entry = ExportEntry;
            if (entry == null)
            {
                LastExportError = BuildAssistantError.InvalidOutputRoot;
                ExportMessage = "Select a result or history entry before exporting.";
                return;
            }

            try
            {
                LastExportError = exportJson(entry, absolutePath);
                ExportMessage = LastExportError == BuildAssistantError.None
                    ? "JSON export completed."
                    : "JSON export failed: " + LastExportError + ". Existing files are never overwritten.";
            }
            catch (Exception exception)
            {
                LastExportError = BuildAssistantError.HistoryWriteFailed;
                ExportMessage = "JSON export failed: " + exception.Message;
            }
        }

        /// <summary>Formats a byte count with a stable binary unit for editor labels.</summary>
        internal static string FormatBytes(ulong bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            var value = (double)bytes;
            var unit = 0;
            while (value >= 1024d && unit < units.Length - 1)
            {
                value /= 1024d;
                unit++;
            }

            return unit == 0
                ? bytes.ToString(CultureInfo.InvariantCulture) + " " + units[unit]
                : value.ToString("0.00", CultureInfo.InvariantCulture) + " " + units[unit];
        }

        /// <summary>Formats a signed size delta without overflowing at Int64.MinValue.</summary>
        internal static string FormatDelta(long bytes)
        {
            var magnitude = bytes < 0 ? (ulong)(-(bytes + 1)) + 1UL : (ulong)bytes;
            return (bytes > 0 ? "+" : bytes < 0 ? "-" : string.Empty) + FormatBytes(magnitude);
        }

        /// <summary>Creates one compact newest-first history selector label.</summary>
        internal static string FormatHistoryLabel(BuildAssistantHistoryEntry entry)
        {
            if (entry == null)
                return "No history entry";
            return entry.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "  " + entry.Status + "  " + entry.RunId;
        }

        private void RefreshHistory(string preferredRunId)
        {
            try
            {
                History = loadHistory() ?? EmptyHistory();
            }
            catch (Exception exception)
            {
                History = new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, exception.Message);
            }

            selectedHistoryIndex = string.IsNullOrEmpty(preferredRunId)
                ? History.Entries.Count > 0 ? 0 : -1
                : History.Entries.Select((entry, index) => new { entry, index }).Where(item => StringComparer.Ordinal.Equals(item.entry.RunId, preferredRunId)).Select(item => item.index).DefaultIfEmpty(-1).First();
        }

        private void InvalidatePlan()
        {
            Plan = null;
            Result = null;
            ConfirmationAccepted = false;
            if (selectedHistoryIndex < 0 && History.Entries.Count > 0)
                selectedHistoryIndex = 0;
            Message = string.Empty;
            ExportMessage = string.Empty;
            LastExportError = BuildAssistantError.None;
        }

        private static string FormatResult(BuildAssistantBuildResult result)
        {
            if (result == null)
                return "Build returned no result.";
            if (result.BuildSucceeded && result.HistoryPersisted)
            {
                var summary = "Build completed and history was saved.";
                if (result.Error == BuildAssistantError.None && string.IsNullOrEmpty(result.Message))
                    return summary;
                var detail = result.Error == BuildAssistantError.None ? result.Message : FormatError(result.Error, result.Message);
                return summary + " Warning: " + detail;
            }
            if (result.BuildSucceeded)
                return "Build completed, but history was not saved: " + FormatError(result.Error, result.Message);
            return "Build failed: " + FormatError(result.Error, result.Message);
        }

        private static string FormatError(BuildAssistantError error, string message)
        {
            return string.IsNullOrEmpty(message) ? error.ToString() : error + ": " + message;
        }

        private static BuildAssistantHistory EmptyHistory()
        {
            return new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, string.Empty);
        }
    }
}
