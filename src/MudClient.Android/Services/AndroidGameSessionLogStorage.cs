using Android.Content;
using Android.Provider;
using MudClient.App.Services;

namespace MudClient.Android.Services;

/// <summary>Writes logs through Android's persisted Storage Access Framework folder URI.</summary>
public sealed class AndroidGameSessionLogStorage(Context context) : IGameSessionLogStorage
{
    private readonly Context _context = context.ApplicationContext
        ?? throw new ArgumentException("Brak kontekstu aplikacji Android.", nameof(context));

    public bool SupportsFolder(string folderIdentifier) =>
        System.Uri.TryCreate(folderIdentifier, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, ContentResolver.SchemeContent, StringComparison.OrdinalIgnoreCase);

    public static void PersistFolderPermission(string folderIdentifier)
    {
        var context = global::Android.App.Application.Context;
        var uri = global::Android.Net.Uri.Parse(folderIdentifier)
            ?? throw new InvalidOperationException("Android zwrócił nieprawidłowy URI folderu.");
        var resolver = context.ContentResolver
            ?? throw new InvalidOperationException("Android nie udostępnił magazynu dokumentów.");
        resolver.TakePersistableUriPermission(
            uri,
            ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
    }

    public Task<Stream> CreateFileAsync(
        string folderIdentifier,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var treeUri = global::Android.Net.Uri.Parse(folderIdentifier)
                ?? throw new InvalidOperationException("Zapisany URI folderu jest nieprawidłowy.");
            var resolver = _context.ContentResolver
                ?? throw new InvalidOperationException("Android nie udostępnił magazynu dokumentów.");
            var documentId = DocumentsContract.GetTreeDocumentId(treeUri)
                ?? throw new InvalidOperationException("Nie udało się odczytać identyfikatora folderu.");
            var parentUri = DocumentsContract.BuildDocumentUriUsingTree(treeUri, documentId)
                ?? throw new InvalidOperationException("Nie udało się otworzyć wybranego folderu.");
            var fileUri = DocumentsContract.CreateDocument(
                    resolver,
                    parentUri,
                    "text/plain",
                    fileName)
                ?? throw new IOException("Android nie utworzył pliku zapisu sesji.");
            Stream stream = resolver.OpenOutputStream(fileUri, "w")
                ?? throw new IOException("Android nie otworzył pliku zapisu sesji.");
            return Task.FromResult(stream);
        }
        catch (Java.Lang.Exception exception)
        {
            throw new IOException("Android odmówił dostępu do wybranego folderu.", exception);
        }
    }
}
