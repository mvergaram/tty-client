using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TtyClient.TTY;

namespace TtyClient;

public partial class SettingsWindow : Window
{
    internal static readonly Regex DigitsOnly = new("^[0-9]+$");

    /// <summary>Parses a positive whole number of hops, rejecting zero, negatives, and overflow.</summary>
    internal static bool TryParsePositiveHops(string text, out int hops)
    {
        bool ok = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out hops) && hops > 0;
        if (!ok) hops = 0;
        return ok;
    }

    /// <summary>Formats the "~ X sec." label shown next to the hops field, or a placeholder when invalid.</summary>
    internal static string FormatSecondsLabel(string hopsText)
    {
        if (!TryParsePositiveHops(hopsText, out int hops))
            return "~ ? sec.";

        double seconds = GoertzelDetector.HopsToSeconds(hops);
        return $"~ {seconds.ToString("0.0", CultureInfo.InvariantCulture)} sec.";
    }

    public SettingsWindow()
    {
        InitializeComponent();

        HopsInput.Text = App.Settings.IdleHopsThreshold.ToString(CultureInfo.InvariantCulture);
        HopsInput.SelectAll();
        HopsInput.Focus();
        UpdateSecondsLabel();

        AiImprovedTextCheckBox.IsChecked = App.Settings.AiImprovedTextEnabled;
        AiUrlInput.Text = App.Settings.AiUrl;
        AiApiKeyInput.Text = App.Settings.AiApiKey;
        AiProviderInput.Text = App.Settings.AiProvider;
        AiModelInput.Text = App.Settings.AiModel;
        AiMaxOutputTokensInput.Text = App.Settings.AiMaxOutputTokens.ToString(CultureInfo.InvariantCulture);
        AiFieldsPanel.IsEnabled = App.Settings.AiImprovedTextEnabled;

        DataObject.AddPastingHandler(HopsInput, HopsInput_Pasting);
        DataObject.AddPastingHandler(AiMaxOutputTokensInput, HopsInput_Pasting);
    }

    private void AiImprovedTextCheckBox_CheckedChanged(object sender, RoutedEventArgs e) =>
        AiFieldsPanel.IsEnabled = AiImprovedTextCheckBox.IsChecked ?? false;

    private void HopsInput_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = !DigitsOnly.IsMatch(e.Text);
    }

    private void HopsInput_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text) ||
            !DigitsOnly.IsMatch((string)e.DataObject.GetData(DataFormats.Text)))
        {
            e.CancelCommand();
        }
    }

    private void HopsInput_TextChanged(object sender, TextChangedEventArgs e) => UpdateSecondsLabel();

    private void UpdateSecondsLabel() => SecondsLabel.Text = FormatSecondsLabel(HopsInput.Text);

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParsePositiveHops(HopsInput.Text, out var hops))
        {
            MessageBox.Show("Enter a positive whole number.", "Invalid value",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        int.TryParse(AiMaxOutputTokensInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var maxOutputTokens);

        App.Settings.Save(
            hops,
            AiImprovedTextCheckBox.IsChecked ?? false,
            AiUrlInput.Text,
            AiApiKeyInput.Text,
            AiProviderInput.Text,
            AiModelInput.Text,
            maxOutputTokens);
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private void SettingsInfoLink_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(((Hyperlink)sender).NavigateUri.ToString()) { UseShellExecute = true });
}
