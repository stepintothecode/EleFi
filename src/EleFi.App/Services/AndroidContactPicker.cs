using Android.Content;
using EleFi.Application.Abstractions;

namespace EleFi.App.Services;

/// <summary>
/// The phone's Contacts, through the system picker: one contact and one number at a time.
/// </summary>
/// <remarks>
/// No contacts permission. The picker grants read access to the single row the user chose,
/// and only its name, number and lookup key are read from it. The rest of the address book is
/// never seen, never copied, and never leaves the phone.
/// </remarks>
public sealed class AndroidContactPicker : IContactPicker
{
    // Contract column names, stable since API 5: ContactsContract's DISPLAY_NAME,
    // CommonDataKinds.Phone.NUMBER and LOOKUP_KEY.
    private const string DisplayName = "display_name";
    private const string Number = "data1";
    private const string LookupKey = "lookup";

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public async Task<PickedContact?> PickAsync(CancellationToken cancellationToken = default)
    {
        var row = await MainActivity.PickContactAsync().ConfigureAwait(false);
        if (row is null || Platform.AppContext.ContentResolver is not { } resolver)
        {
            return null;
        }

        using var cursor = resolver.Query(row, [DisplayName, Number, LookupKey], null, null, null);
        if (cursor is null || !cursor.MoveToFirst())
        {
            return null;
        }

        var name = Read(cursor, DisplayName);
        return string.IsNullOrWhiteSpace(name)
            ? null
            : new PickedContact(name, Read(cursor, Number), Read(cursor, LookupKey));
    }

    /// <inheritdoc />
    public void Open(string lookupKey)
    {
        try
        {
            var uri = Android.Net.Uri.Parse("content://com.android.contacts/contacts/lookup/" + Android.Net.Uri.Encode(lookupKey));
            using var intent = new Intent(Intent.ActionView, uri);
            intent.AddFlags(ActivityFlags.NewTask);
            Platform.AppContext.StartActivity(intent);
        }
        catch (ActivityNotFoundException)
        {
            // No Contacts app to open it in. The details copied here are still here.
        }
    }

    private static string? Read(Android.Database.ICursor cursor, string column)
    {
        var index = cursor.GetColumnIndex(column);
        return index < 0 || cursor.IsNull(index) ? null : cursor.GetString(index);
    }
}
