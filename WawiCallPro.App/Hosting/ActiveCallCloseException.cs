namespace WawiCallPro.App.Hosting;

/// <summary>A user close or plugin switch must not implicitly end an existing phone call.</summary>
internal sealed class ActiveCallCloseException() : Exception("Noch ein aktiver Anruf: Bitte zuerst auflegen oder ablehnen.");
