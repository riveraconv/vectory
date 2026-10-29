using System.IO;
using Godot;

namespace Vectory.Records;

// Récords en user://records.json (en Windows, la carpeta de datos de usuario de Godot).
// Escritura segura: se escribe a un .tmp y luego se reemplaza el archivo bueno.
// Un archivo corrupto se aparta como records.json.bak (sobrescribiendo una .bak anterior).
public sealed class FileRecordStorage : IRecordStorage
{
	public const string DefaultPath = "user://records.json";

	private readonly string _path;

	public FileRecordStorage(string godotPath = DefaultPath)
	{
		_path = ProjectSettings.GlobalizePath(godotPath);
	}

	public string Read()
	{
		return File.Exists(_path) ? File.ReadAllText(_path) : null;
	}

	public void Write(string text)
	{
		string directory = Path.GetDirectoryName(_path);
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		string temp = _path + ".tmp";
		File.WriteAllText(temp, text);
		File.Move(temp, _path, overwrite: true);
	}

	public void MoveToBackup()
	{
		if (File.Exists(_path))
			File.Move(_path, _path + ".bak", overwrite: true);
	}
}
