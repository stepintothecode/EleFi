using Android.Gms.Tasks;
using Android.Graphics;
using Android.Media;
using Android.Runtime;
using EleFi.Application.Abstractions;
using EleFi.Domain.Receipts;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
using CancellationToken = System.Threading.CancellationToken;
using Task = System.Threading.Tasks.Task;

namespace EleFi.App.Services;

/// <summary>
/// Gets a receipt's photo from the share sheet, the camera or the gallery, makes it small and
/// upright, and reads it with ML Kit's bundled text recognition, all on the device (ADR-0018).
/// </summary>
/// <remarks>
/// The photo is scaled to at most 1600 px and saved as JPEG, about 150 KB, which is what is
/// kept; reading happens on that same image, so what was read is what the user can look at.
/// The pieces ML Kit returns are put back into printed rows by <see cref="TextRows"/>, which
/// is where the testable part lives.
/// </remarks>
public sealed class AndroidReceiptScanner : IReceiptScanner
{
    private const int PhotoEdge = 1600;
    private const int ThumbnailEdge = 240;

    /// <inheritdoc />
    public Task<ScannedReceipt?> TakeSharedAsync(CancellationToken cancellationToken = default)
    {
        var (waiting, image, text) = SharedReceiptIntake.Take();
        if (!waiting)
        {
            return Task.FromResult<ScannedReceipt?>(null);
        }

        if (image is not null)
        {
            return ReadAsync(image, cancellationToken);
        }

        // Text, or nothing readable: either way the receipt screen gets something to say about it.
        return Task.FromResult<ScannedReceipt?>(new ScannedReceipt(
            [.. (text ?? string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)], null, null));
    }

    /// <inheritdoc />
    public async Task<ScannedReceipt?> PickAsync(bool camera, CancellationToken cancellationToken = default)
    {
        var picked = camera
            ? await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(false)
            : await ChooseOneAsync().ConfigureAwait(false);
        if (picked is null)
        {
            return null;
        }

        await using var stream = await picked.OpenReadAsync().ConfigureAwait(false);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return await ReadAsync(copy.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    // The single-photo picker: a tap on the photo is the choice. The newer many-photo picker,
    // even limited to one, asks for a Done tap as well, and on some phones hands back null
    // rather than an empty list when it times out.
#pragma warning disable CS0618 // PickPhotoAsync is obsolete in favour of PickPhotosAsync, which is worse for one photo.
    private static Task<FileResult?> ChooseOneAsync() => MediaPicker.Default.PickPhotoAsync();
#pragma warning restore CS0618

    private static async Task<ScannedReceipt?> ReadAsync(byte[] original, CancellationToken cancellationToken)
    {
        // Decoding and scaling a 12 MP photo is real work; never on the UI thread.
        var prepared = await Task.Run(() => Prepare(original), cancellationToken).ConfigureAwait(false);
        if (prepared is not var (photo, thumbnail))
        {
            return null;
        }

        // Read from the photo as it will be kept, decoded afresh here, so no bitmap is shared
        // between threads: one that was handed over from the resize was already released by
        // the time text recognition reached it.
        using var bitmap = BitmapFactory.DecodeByteArray(photo, 0, photo.Length);
        var lines = bitmap is null ? [] : await RecogniseAsync(bitmap).ConfigureAwait(false);
        return new ScannedReceipt(lines, photo, thumbnail);
    }

    // The photo and its thumbnail as JPEG bytes; every bitmap made on the way is released here.
    private static (byte[] Photo, byte[] Thumbnail)? Prepare(byte[] original)
    {
        using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeByteArray(original, 0, original.Length, bounds);

        // Decode at a power-of-two reduction close to the target, so a large photo never sits
        // in memory at full size.
        var sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= PhotoEdge)
        {
            sample *= 2;
        }

        using var options = new BitmapFactory.Options { InSampleSize = sample };
        var decoded = BitmapFactory.DecodeByteArray(original, 0, original.Length, options);
        if (decoded is null)
        {
            return null;
        }

        var made = new List<Bitmap> { decoded };
        try
        {
            var upright = Upright(decoded, original);
            made.Add(upright);
            var photo = Scaled(upright, PhotoEdge);
            made.Add(photo);
            var small = Scaled(photo, ThumbnailEdge);
            made.Add(small);
            return (Jpeg(photo, 75), Jpeg(small, 70));
        }
        finally
        {
            // A scale or rotation that changes nothing returns the same bitmap, so each is
            // released once. Compared by reference, and listed before any is released: a Java
            // object's own hash asks Java, which a released one can no longer answer.
            foreach (var bitmap in made.Distinct(ReferenceEqualityComparer.Instance).Cast<Bitmap>().ToList())
            {
                bitmap.Recycle();
                bitmap.Dispose();
            }
        }
    }
    // A phone camera stores the photo as the sensor saw it and records the rotation; reading
    // text sideways finds nothing.
    private static Bitmap Upright(Bitmap bitmap, byte[] original)
    {
        using var stream = new MemoryStream(original);
        using var exif = new ExifInterface(stream);
        var orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, (int)Orientation.Normal);
        var degrees = orientation switch
        {
            (int)Orientation.Rotate90 => 90,
            (int)Orientation.Rotate180 => 180,
            (int)Orientation.Rotate270 => 270,
            _ => 0,
        };

        if (degrees == 0)
        {
            return bitmap;
        }

        using var matrix = new Matrix();
        matrix.PostRotate(degrees);
        return Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true)!;
    }

    private static Bitmap Scaled(Bitmap bitmap, int edge)
    {
        var longest = Math.Max(bitmap.Width, bitmap.Height);
        if (longest <= edge)
        {
            return bitmap;
        }

        var ratio = (double)edge / longest;
        return Bitmap.CreateScaledBitmap(bitmap, (int)(bitmap.Width * ratio), (int)(bitmap.Height * ratio), true)!;
    }

    private static byte[] Jpeg(Bitmap bitmap, int quality)
    {
        using var stream = new MemoryStream();
        bitmap.Compress(Bitmap.CompressFormat.Jpeg!, quality, stream);
        return stream.ToArray();
    }

    private static async Task<IReadOnlyList<string>> RecogniseAsync(Bitmap bitmap)
    {
        using var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
        using var image = InputImage.FromBitmap(bitmap, 0);
        using var listener = new Completion();
        recognizer.Process(image).AddOnSuccessListener(listener).AddOnFailureListener(listener);

        // JavaCast, not "as": the task hands back a plain Java object, and a C# cast of it to
        // the binding's Text type is always null, which read every receipt as blank.
        var result = (await listener.Task.ConfigureAwait(false))?.JavaCast<Text>();
        var pieces = result?.TextBlocks
            .SelectMany(block => block.Lines)
            .Select(line => line.BoundingBox is { } box
                ? new TextLine(line.Text, box.Left, box.Top, box.Right, box.Bottom)
                : new TextLine(line.Text, 0, 0, 0, 0))
            ?? [];

        return TextRows.Assemble(pieces);
    }

    // ML Kit answers through Google Play's task listeners; this turns that into an awaitable task.
    private sealed class Completion : Java.Lang.Object, IOnSuccessListener, IOnFailureListener
    {
        private readonly TaskCompletionSource<Java.Lang.Object?> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Java.Lang.Object?> Task => _done.Task;

        public void OnSuccess(Java.Lang.Object? result) => _done.TrySetResult(result);

        public void OnFailure(Java.Lang.Exception e) => _done.TrySetException(e);
    }
}
