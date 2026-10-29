using System;

namespace Vectory.Records;

// Dónde se guarda el texto del archivo de récords
public interface IRecordStorage
{
	// Texto guardado, o null si no hay archivo
	string Read();

	// Guarda el texto (sin dejar un archivo a medias si se corta)
	void Write(string text);

	// Aparta el archivo actual como copia de seguridad (archivo corrupto)
	void MoveToBackup();
}

// Almacenamiento en memoria, para las pruebas
public sealed class MemoryRecordStorage : IRecordStorage
{
	public string Text { get; set; }
	public string Backup { get; private set; }

	public string Read() => Text;

	public void Write(string text) => Text = text;

	public void MoveToBackup()
	{
		Backup = Text;
		Text = null;
	}
}

// Carga y guarda el RecordBook. Nunca lanza excepciones por el archivo: si está corrupto o no se puede
// leer o escribir, deja un aviso en Warning y sigue (sin récords, o sin guardar).
public sealed class RecordStore
{
	private readonly IRecordStorage _storage;

	public RecordBook Book { get; private set; } = new();

	// Último problema con el archivo (null si todo fue bien)
	public string Warning { get; private set; }

	public RecordStore(IRecordStorage storage)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
	}

	public void Load()
	{
		Warning = null;
		string text;
		try
		{
			text = _storage.Read();
		}
		catch (Exception e)
		{
			Warning = $"records could not be read ({e.Message}); starting without records";
			Book = new RecordBook();
			return;
		}

		if (text == null)
		{
			Book = new RecordBook();
			return;
		}

		if (RecordBook.TryFromJson(text, out RecordBook book, out string error))
		{
			Book = book;
			return;
		}

		Book = new RecordBook();
		Warning = $"records file is not valid ({error}); it was renamed to .bak and records start empty";
		try
		{
			_storage.MoveToBackup();
		}
		catch (Exception e)
		{
			Warning += $" (backup failed: {e.Message})";
		}
	}

	public bool Save()
	{
		Warning = null;
		try
		{
			_storage.Write(Book.ToJson());
			return true;
		}
		catch (Exception e)
		{
			Warning = $"records could not be saved ({e.Message})";
			return false;
		}
	}
}
