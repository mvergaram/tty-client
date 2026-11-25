using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TtyClient.TTY;

namespace TtyClient.Views;

/// <summary>
/// Shared live-input filtering for any <see cref="TextBox"/> whose content will be sent as
/// Baudot/TTY text: normalizes typed or pasted text via <see cref="BaudotEncoder.TryNormalize"/>
/// (strip accents, upper-case, drop characters with no ITA-2 representation) so what's shown
/// always matches exactly what will be transmitted. Used by both
/// <see cref="CallConversationView"/>'s "send" box and <see cref="AudioFilesView"/>'s
/// "text to convert" box, so the two can never drift apart.
/// </summary>
internal static class BaudotTextBoxInput
{
    public static void FilterPreviewTextInput(TextBox textBox, TextCompositionEventArgs e)
    {
        if (!BaudotEncoder.TryNormalize(e.Text, out string normalized))
        {
            e.Handled = true; // nothing in e.Text survives normalization (e.g. "@", "%")
            return;
        }

        if (normalized != e.Text)
        {
            InsertNormalizedText(textBox, normalized);
            e.Handled = true;
        }
    }

    public static void FilterPasting(TextBox textBox, DataObjectPastingEventArgs e)
    {
        e.CancelCommand(); // always apply our own normalized insert instead of the raw paste

        if (!e.DataObject.GetDataPresent(DataFormats.Text))
            return;

        string pasted = (string)e.DataObject.GetData(DataFormats.Text);
        if (BaudotEncoder.TryNormalize(pasted, out string normalized))
            InsertNormalizedText(textBox, normalized);
    }

    private static void InsertNormalizedText(TextBox textBox, string text)
    {
        int start = textBox.SelectionStart;
        int selectionLength = textBox.SelectionLength;

        int maxLength = textBox.MaxLength;
        if (maxLength > 0)
        {
            int roomLeft = maxLength - (textBox.Text.Length - selectionLength);
            if (text.Length > roomLeft)
                text = roomLeft > 0 ? text[..roomLeft] : string.Empty;
        }

        textBox.Text = textBox.Text.Remove(start, selectionLength).Insert(start, text);
        textBox.CaretIndex = start + text.Length;
    }
}
