namespace WawiCallPro.Interfaces.Office;

/// <summary>Bounds provider responses and local BLOBs without truncating attachments.</summary>
public static class MailLimits
{
    public const int AttachmentBytes = 32 * 1024 * 1024;
    public const int AttachmentResponseBytes = 48 * 1024 * 1024;
    public const int ProtectedAttachmentBytes = 40 * 1024 * 1024;
}
