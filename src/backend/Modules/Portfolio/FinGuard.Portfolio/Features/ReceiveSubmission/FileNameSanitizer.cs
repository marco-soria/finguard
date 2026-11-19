namespace FinGuard.Portfolio.Features.ReceiveSubmission;

/// <summary>Makes a client file name safe to store and display. The result is never used as a path (D-91).</summary>
internal static class FileNameSanitizer
{
    public static string Sanitize(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        // Browsers on Windows may send full paths; Path.GetFileName only splits on '\' on Windows, so split on both.
        var name = fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..];
        name = new string([.. name.Where(c => !char.IsControl(c))]).Trim();
        return name.Length <= Persistence.Submission.FileNameMaxLength
            ? name
            : name[..Persistence.Submission.FileNameMaxLength];
    }
}
