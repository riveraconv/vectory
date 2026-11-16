namespace Vectory.Ship;

// Pintura elegida para una nave (spec docs/selector-pinturas.md, 3.1): el id de una pintura del catálogo de su casco 3D
// y si lleva marcas (librea en VECTOR, marcas en BLADE, marcas de flota en HAMMER). Las 40 posiciones de un catálogo
// son las 20 pinturas lisas y luego las 20 con marcas: HullPaints pasa de una a otra.
public readonly record struct PaintChoice(string PaintId, bool Marks);
