namespace Vectory.Core;

// Formato del número de versión que se muestra en pantalla (v0.1.0).
// El número sale de application/config/version del proyecto; si falta, v0.0.0.
public static class VersionFormat
{
	public const string Fallback = "0.0.0";

	public static string Format(string version)
	{
		string v = string.IsNullOrWhiteSpace(version) ? Fallback : version.Trim();
		return "v" + v;
	}
}
