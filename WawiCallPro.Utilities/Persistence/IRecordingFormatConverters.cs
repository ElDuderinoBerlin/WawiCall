using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Resolves the converter for a registered recording artifact format.</summary>
public interface IRecordingFormatConverters
{
    IMp3Converter GetConverter(RecordingArtifactFormat format);
}
