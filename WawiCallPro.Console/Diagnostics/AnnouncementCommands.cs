using System.Text;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp.Presentation;

namespace WawiCallPro.ConsoleApp.Diagnostics;

internal static class AnnouncementCommands
{
    internal static async Task<ConsoleTable> ExecuteAsync(ConsoleCommand command, IAnnouncementSpeech speech,
        IAudioFileConversion audio, CancellationToken token)
    {
        switch (command.Name)
        {
            case "--announcement-voices":
                return new("Installierte Windows-Stimmen", "Stimme", "Sprache und ID",
                    speech.GetVoices().Select(voice => new ConsoleRow(voice.DisplayName, $"{voice.Language}\n{voice.Id}")).ToArray());
            case "--announcement-preview":
                await audio.PreviewAsync(command.File!, token);
                return new("Ansage vorgehört", "Merkmal", "Wert", [new("Datei", command.File!)]);
            case "--announcement-speech":
                using (var reader = new StreamReader(command.AnnouncementTextFile!, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false))
                {
                    const int maximumCharacters = 4000;
                    var buffer = new char[maximumCharacters + 2];
                    int count;
                    try { count = await reader.ReadBlockAsync(buffer.AsMemory(), token); }
                    catch (DecoderFallbackException)
                    {
                        throw new DiagnosticException("Die Ansagetextdatei muss als UTF-8 gespeichert sein.");
                    }
                    var offset = count > 0 && buffer[0] == '\uFEFF' ? 1 : 0;
                    if (count - offset > maximumCharacters)
                        throw new DiagnosticException("Der Ansagetext darf höchstens 4000 Zeichen enthalten.");
                    var voice = await speech.WriteWaveAsync(new string(buffer, offset, count - offset),
                        command.AnnouncementVoiceId, command.File!, token);
                    return new("Ansage erzeugt", "Merkmal", "Wert",
                        [new("Datei", command.File!), new("Stimme", voice.DisplayName), new("Stimm-ID", voice.Id)]);
                }
            default:
                throw new InvalidOperationException();
        }
    }
}
