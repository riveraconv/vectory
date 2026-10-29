namespace Vectory.Menu;

// Una opción de un menú: identificador (lo que devuelve Confirm), texto que se ve y si se puede elegir.
// Activar una opción más adelante es cambiar Enabled en la lista y conectar su acción.
public readonly record struct MenuOption(string Id, string Label, bool Enabled);
