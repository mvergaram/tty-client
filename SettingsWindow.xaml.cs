using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using TtyClient.TTY;

namespace TtyClient;

public partial class SettingsWindow : Window
{
    private static readonly Regex DigitsOnly = new("^[0-9]+$");

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
        AiFieldsPanel.IsEnabled = App.Settings.AiImprovedTextEnabled;

        DataObject.AddPastingHandler(HopsInput, HopsInput_Pasting);
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

    private void UpdateSecondsLabel()
    {
        if (int.TryParse(HopsInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var hops) && hops > 0)
        {
            var seconds = GoertzelDetector.HopsToSeconds(hops);
            SecondsLabel.Text = $"~ {seconds.ToString("0.0", CultureInfo.InvariantCulture)} sec.";
        }
        else
        {
            SecondsLabel.Text = "~ ? sec.";
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HopsInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var hops) || hops <= 0)
        {
            MessageBox.Show("Enter a positive whole number.", "Invalid value",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        App.Settings.Save(
            hops,
            AiImprovedTextCheckBox.IsChecked ?? false,
            AiUrlInput.Text,
            AiApiKeyInput.Text,
            AiProviderInput.Text,
            AiModelInput.Text);
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
}
