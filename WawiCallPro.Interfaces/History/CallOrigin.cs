using System.Text;

namespace WawiCallPro.Interfaces.History;

/// <summary>Historical origin of a call. Names are captured at creation; the installation ID survives a PC rename.</summary>
public sealed record CallOrigin
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public CallOrigin(Guid workstationId, string machineName, string windowsUserName)
    {
        if (workstationId == Guid.Empty) throw new ArgumentException("A workstation identity is required.", nameof(workstationId));
        ValidateName(machineName, nameof(machineName));
        ValidateName(windowsUserName, nameof(windowsUserName));
        WorkstationId = workstationId;
        MachineName = machineName;
        WindowsUserName = windowsUserName;
    }

    public Guid WorkstationId { get; }
    public string MachineName { get; }
    public string WindowsUserName { get; }

    private static void ValidateName(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl))
            throw new ArgumentException("An origin name must contain 1–256 characters without controls.", parameter);
        try { _ = Utf8.GetByteCount(value); }
        catch (EncoderFallbackException) { throw new ArgumentException("An origin name must contain valid Unicode.", parameter); }
    }
}
