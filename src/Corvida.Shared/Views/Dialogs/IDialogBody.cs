namespace Corvida.Views.Dialogs;

/// <summary>
/// A dialog body that can be shown two ways: as an overlay in the shell, where it draws its own
/// OK/Cancel row, or inside a <c>RiceDialogWindow</c>, which draws the buttons itself and calls
/// <see cref="Confirm"/> from its OK button.
/// </summary>
internal interface IDialogBody
{
    /// <summary>Caption of the confirming button.</summary>
    string ConfirmText { get; }

    /// <summary>Validates and completes the dialog with its result; does nothing while the input is invalid.</summary>
    void Confirm();

    /// <summary>Hides the body's own button row and outer margin, for a host that supplies both.</summary>
    void UseHostButtons();
}
