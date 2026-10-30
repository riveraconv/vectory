using System.IO;
using Godot;
using Vectory.Records;

namespace Vectory.Ghost;

// Fantasmas en user://ghosts/<IdCircuito>.json, uno por circuito. Cada archivo usa FileRecordStorage
// (escritura vía .tmp y .bak si está corrupto); la carpeta se crea al guardar el primero.
public sealed class FileGhostStorage : IGhostStorage
{
	public const string DefaultDirectory = "user://ghosts";

	private readonly string _godotDirectory;

	public FileGhostStorage(string godotDirectory = DefaultDirectory)
	{
		_godotDirectory = godotDirectory;
	}

	public IRecordStorage For(string circuitId)
	{
		return new FileRecordStorage($"{_godotDirectory}/{FileName(circuitId)}");
	}

	// Borra todos los archivos de la carpeta de fantasmas (también .bak y .tmp)
	public void DeleteAll()
	{
		string directory = ProjectSettings.GlobalizePath(_godotDirectory);
		if (!Directory.Exists(directory))
			return;

		foreach (string file in Directory.GetFiles(directory))
			File.Delete(file);
	}

	// El Id del circuito como nombre de archivo (sin caracteres que no admita el sistema)
	private static string FileName(string circuitId)
	{
		foreach (char c in Path.GetInvalidFileNameChars())
			circuitId = circuitId.Replace(c, '_');
		return circuitId + ".json";
	}
}
