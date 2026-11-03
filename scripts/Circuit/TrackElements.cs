using Godot;
using System.Collections.Generic;
using Vectory.Core;
using Vectory.UI;
using NVec2 = System.Numerics.Vector2;
using NVec3 = System.Numerics.Vector3;

namespace Vectory.Circuit;

// Elementos del circuito (spec docs/elementos-circuito.md): meta, checkpoints con su panel de tiempo, semáforo,
// marcas de parrilla, túneles, avisos de curva (chevrons) y señales de cresta. Nada se pinta en la pista salvo las
// marcas de parrilla, que se apagan en la salida. Hijo de Track: lo crea y lo monta Track.Build con sus parámetros.
//
// Todo son mallas de líneas agrupadas, generadas una vez sobre el marco de la pista (siguen pendientes y peraltes).
// Las partes animadas llevan su propio material (parámetros de shader), sin regenerar geometría. Las animaciones
// salen de clases puras (GateFlash, TunnelPulse, ChevronWave, Heartbeat, StartLightsState, GridMarksFade,
// TimedDisplay) y avanzan en _Process, así la pausa las congela. No tiene lógica de carrera: solo escucha.
public partial class TrackElements : Node3D
{
	// Líneas con brillo, mezcla con blanco y desvanecido; el color sale de los vértices.
	// La "opacidad" multiplica el color (sin ALPHA, así sigue en la pasada opaca y con glow).
	private const string GlowShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform float brightness = 1.0;
uniform float white_mix = 0.0;
uniform vec3 white_color = vec3(1.0);
uniform float fade = 1.0;

void fragment() {
	if (fade <= 0.002)
		discard;
	ALBEDO = mix(COLOR.rgb, white_color, white_mix) * brightness * fade;
}
";

	// Semáforo: UV.x = número de luz, UV.y = 0 octógono exterior / 1 octógono interior (solo con la luz encendida)
	private const string LightsShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform float lights_on = 0.0;
uniform float go_flash = 0.0;
uniform vec3 lamp_color = vec3(1.0, 0.6, 0.2);
uniform vec3 lit_color = vec3(1.0, 0.7, 0.4);
uniform vec3 go_color = vec3(0.2, 0.9, 1.0);
uniform float off_brightness = 0.5;
uniform float lit_brightness = 1.3;
uniform float go_brightness = 2.8;

void fragment() {
	bool lit = UV.x + 0.5 < lights_on;
	vec3 c;
	if (lit)
		c = lit_color * lit_brightness;
	else if (UV.y > 0.5) {
		if (go_flash <= 0.01)
			discard;
		c = vec3(0.0);
	}
	else
		c = lamp_color * off_brightness;
	ALBEDO = mix(c, go_color * go_brightness, go_flash);
}
";

	// Túnel: UV.x = m desde la entrada, UV.y = largo del túnel; UV2.y = 1 en las líneas que pulsan (anillos),
	// UV2.x = 1 en el segundo trazo de cada anillo, que solo se ve con el pulso (más grosor)
	private const string TunnelShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform float pulse_time = 0.0;
uniform float pulse_speed = 120.0;
uniform int pulse_count = 2;
uniform float pulse_width = 7.0;
uniform vec3 pulse_color = vec3(1.0);

varying float glow;

void vertex() {
	float len = max(UV.y, 0.001);
	float best = 1e9;
	for (int k = 0; k < pulse_count; k++) {
		float p = mod(pulse_time * pulse_speed + float(k) * len / float(pulse_count), len);
		best = min(best, abs(UV.x - p));
	}
	float x = best / pulse_width;
	glow = UV2.y * exp(-x * x);
}

void fragment() {
	vec3 c = mix(COLOR.rgb, pulse_color, 0.75 * glow) * (1.0 + 1.8 * glow);
	if (UV2.x > 0.5) {
		if (glow < 0.02)
			discard;
		c *= glow;
	}
	ALBEDO = c;
}
";

	// Chevrons: UV.x = m desde el inicio del aviso. Misma fórmula que ChevronWave.
	private const string ChevronShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform float wave_time = 0.0;
uniform float frequency = 2.2;
uniform float cycle_length = 45.0;
uniform float base_brightness = 0.55;
uniform float peak_brightness = 2.15;
uniform float peak_white = 0.4;
uniform vec3 white_color = vec3(1.0);

void fragment() {
	float phase = fract(wave_time * frequency - UV.x / cycle_length);
	float x = (phase - 0.1) / 0.09;
	float g = exp(-x * x);
	ALBEDO = mix(COLOR.rgb, white_color, peak_white * g) * mix(base_brightness, peak_brightness, g);
}
";

	// ---- Valores fijos de la spec (los ajustables están en el Inspector del Track) ----
	private const float FinishPeakBrightness = 3.5f;
	private const float FinishPeakWhite = 0.7f;
	private const float CheckpointPeakBrightness = 1.6f;
	private const float CheckpointPeakWhite = 0.4f;
	private const float GateLinkBrightness = 0.7f;       // uniones y X de meta y checkpoints

	private const float PanelHeight = 2.2f;              // panel del checkpoint
	private const float PanelInset = 1.2f;               // por lado, desde los pilares
	private const float PanelTextHeight = 1.0f;          // m de alto de las mayúsculas
	private const float PanelFadeIn = 0.1f;
	private const float PanelFadeOut = 0.3f;
	private const float PanelTextBrightness = 0.85f;     // brillo contenido: el glow no emborrona los números

	private const float LightsBelowTop = 2.1f;           // centro del semáforo por debajo del travesaño (12 → 9,9 m)
	private const float LightsPanelWidth = 10.8f;
	private const float LightsPanelHeight = 2.3f;
	private const float LightsSpacing = 2.1f;
	private const float LampRadius = 0.8f;
	private static readonly float[] LampRings = { 0.72f, 0.5f, 0.3f, 0.12f };
	private const float LightsPanelBrightness = 0.55f;
	private const float LightsTieX = 4f;

	private const float GridHeight = 0.05f;              // marcos de parrilla, apenas por encima de la superficie
	private const float GridBrightness = 0.85f;
	private const float GridNumberHeight = 1.4f;
	private const float GridNumberGap = 1f;              // detrás del marco (legible desde detrás de la nave)

	private const float TunnelMouthDepth = 1.2f;         // segundo anillo de cada boca, hacia fuera
	private const float TunnelThickGap = 0.15f;          // segundo trazo de anillos y bocas, sobre el eje
	private const float TunnelRailBrightness = 0.75f;
	private const float TunnelLatticeBrightness = 0.45f;

	private const float ChevronPost = 0.4f;
	private const float ChevronCenter = 1.6f;            // sobre el borde superior de la barrera
	private const float ChevronCycleLength = 45f;
	private const float ThickGap = 0.05f;                // segundo trazo de chevrons y flechas

	private const float BeaconPost = 2.2f;
	private const float BeaconHalfWidth = 1.5f;
	private const float BeaconHalfHeight = 1.1f;
	private const float BeaconInward = 0.6f;
	private const float BeaconFrameBrightness = 0.75f;
	private const float BeaconBeatFrequency = 0.6f;
	private const float CrestBarrierBeatFrequency = 0.8f;

	private const int LabelFontSize = 96;
	private const float FontCapHeight = 0.7f;            // alto de las mayúsculas respecto al tamaño de la fuente

	private Track _track;
	private TrackFrame _frame;
	private float _time;
	private Shader _glowShader;
	private Font _font;

	private GateFlash _finishFlash;
	private ShaderMaterial _finishMaterial;
	private readonly List<CheckpointGate> _checkpoints = new();

	private ShaderMaterial _lightsMaterial;
	private StartSequence _startSequence;

	private Node3D _grid;
	private ShaderMaterial _gridMaterial;
	private readonly List<(Label3D Label, Color Color)> _gridNumbers = new();

	private ShaderMaterial _tunnelMaterial;
	private ShaderMaterial _chevronMaterial;
	private ShaderMaterial _beaconMaterial;

	private sealed class CheckpointGate
	{
		public GateFlash Flash;
		public ShaderMaterial Material;
		public Label3D TimeLabel;
		public Label3D DeltaLabel;
		public Color DeltaColor;
		public float PanelElapsed = -1f;   // < 0: panel vacío
	}

	// Genera todos los elementos del circuito ya construido (marco, checkpoints y datos validados del Track)
	public void Build(Track track)
	{
		_track = track;
		_frame = track.Frame;
		_glowShader = new Shader { Code = GlowShaderCode };
		_font = new FontVariation { BaseFont = MenuFonts.Mono ?? ThemeDB.FallbackFont, VariationEmbolden = 0.7f };

		var fixedLines = new LineBatch();
		BuildFinish();
		BuildCheckpoints();
		BuildStartLights(fixedLines);
		BuildTunnels();
		BuildChevrons();
		BuildCrests(fixedLines);
		AddMesh("FixedLines", fixedLines, GlowMaterial());
	}

	// ---------------------------------------------------------------
	// Lo que recibe de la carrera
	// ---------------------------------------------------------------

	// Una nave cruza la meta: destello del pórtico (vuelve a empezar si ya estaba destellando)
	public void FlashFinish() => _finishFlash?.Trigger();

	// Una nave cruza el checkpoint "index" (orden de carrera)
	public void FlashCheckpoint(int index)
	{
		if (index >= 0 && index < _checkpoints.Count)
			_checkpoints[index].Flash.Trigger();
	}

	// Tiempo de sector de la nave que acaba de cruzar el checkpoint "index" (sustituye al que hubiera)
	public void ShowCheckpointTime(int index, SectorTimeText text)
	{
		if (index < 0 || index >= _checkpoints.Count)
			return;

		CheckpointGate gate = _checkpoints[index];
		gate.TimeLabel.Text = $"{text.Label}   {text.Time}";
		gate.DeltaLabel.Text = text.Delta ?? "";
		gate.DeltaColor = Dim(text.IsBetter ? Palette.SectorBetter : Palette.SectorWorse, PanelTextBrightness);
		gate.PanelElapsed = 0f;
		UpdatePanel(gate);
	}

	// Secuencia de salida de RACE para el semáforo y el apagado de la parrilla (null en TIME TRIAL)
	public void SetStartSequence(StartSequence sequence) => _startSequence = sequence;

	// RACE: marcos de parrilla con su número; el puesto del jugador en blanco. En TIME TRIAL no se llama.
	public void ShowGrid(int playerPosition, float noseDistance)
	{
		if (_grid != null)
			return;

		_grid = new Node3D { Name = "Grid" };
		AddChild(_grid);

		var lines = new LineBatch();
		float hx = _track.GridFrameWidth * 0.5f;
		float hz = _track.GridFrameLength * 0.5f;
		float pixelSize = PixelSizeFor(GridNumberHeight);

		for (int position = 1; position <= _track.GridSlots; position++)
		{
			_track.GetGridSlotCenter(position, noseDistance, out float s, out float lateral);
			Color color = Dim(position == playerPosition ? Palette.GridMarkPlayer : Palette.GridMark, GridBrightness);

			FramePoint back = _frame.At(s - hz);
			FramePoint front = _frame.At(s + hz);
			Vector3 bl = P(back, lateral - hx, GridHeight);
			Vector3 br = P(back, lateral + hx, GridHeight);
			Vector3 fl = P(front, lateral - hx, GridHeight);
			Vector3 fr = P(front, lateral + hx, GridHeight);
			lines.Add(bl, br, color);
			lines.Add(br, fr, color);
			lines.Add(fr, fl, color);
			lines.Add(fl, bl, color);

			// Número plano sobre el suelo, detrás del marco, leído desde detrás de la nave
			FramePoint at = _frame.At(s - hz - GridNumberGap);
			var label = MakeLabel(pixelSize, HorizontalAlignment.Center);
			label.Text = position.ToString();
			label.Modulate = color;
			label.Visible = true;
			label.Transform = new Transform3D(new Basis(G(at.Right), G(at.Tangent), G(at.Normal)), P(at, lateral, GridHeight + 0.01f));
			_grid.AddChild(label);
			_gridNumbers.Add((label, color));
		}

		_gridMaterial = GlowMaterial();
		var mesh = new MeshInstance3D { Name = "GridFrames", Mesh = lines.ToMesh(_gridMaterial) };
		_grid.AddChild(mesh);
		UpdateGrid();
	}

	// ---------------------------------------------------------------
	// Animación
	// ---------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (_track == null)
			return;

		float dt = (float)delta;
		_time += dt;

		_finishFlash.Update(dt);
		SetGlow(_finishMaterial, _finishFlash.Brightness, _finishFlash.WhiteMix);

		foreach (CheckpointGate gate in _checkpoints)
		{
			gate.Flash.Update(dt);
			SetGlow(gate.Material, gate.Flash.Brightness, gate.Flash.WhiteMix);
			if (gate.PanelElapsed >= 0f)
			{
				gate.PanelElapsed += dt;
				UpdatePanel(gate);
			}
		}

		StartLightsState lights = StartLightsState.From(_startSequence, _track.StartLightsFlashDuration);
		_lightsMaterial.SetShaderParameter("lights_on", (float)lights.LightsOn);
		_lightsMaterial.SetShaderParameter("go_flash", lights.GoFlash);

		UpdateGrid();

		_tunnelMaterial?.SetShaderParameter("pulse_time", _time);
		_chevronMaterial?.SetShaderParameter("wave_time", _time);
		_beaconMaterial?.SetShaderParameter("brightness", Heartbeat.Brightness(_time, BeaconBeatFrequency, 1f, 1.5f));
		if (_track.CrestWarnings.Count > 0)
			_track.SetCrestBarrierBrightness(Heartbeat.Brightness(_time, CrestBarrierBeatFrequency, 0.9f, 1.5f));
	}

	private void UpdatePanel(CheckpointGate gate)
	{
		float opacity = TimedDisplay.Opacity(gate.PanelElapsed, _track.CheckpointPanelTime, PanelFadeIn, PanelFadeOut);
		bool visible = opacity > 0f;
		gate.TimeLabel.Visible = visible;
		gate.DeltaLabel.Visible = visible && gate.DeltaLabel.Text != "";
		gate.TimeLabel.Modulate = new Color(Dim(Palette.CheckpointText, PanelTextBrightness), opacity);
		gate.DeltaLabel.Modulate = new Color(gate.DeltaColor, opacity);
		if (!visible && gate.PanelElapsed >= _track.CheckpointPanelTime)
			gate.PanelElapsed = -1f;
	}

	// Las marcas se apagan desde el GO y ya no vuelven
	private void UpdateGrid()
	{
		if (_grid == null || !_grid.Visible)
			return;

		float opacity = GridMarksFade.Opacity(GridMarksFade.TimeSinceGo(_startSequence), _track.GridFadeTime);
		_gridMaterial.SetShaderParameter("fade", opacity);
		foreach (var (label, color) in _gridNumbers)
			label.Modulate = new Color(color, opacity);
		if (opacity <= 0f)
			_grid.Visible = false;
	}

	// ---------------------------------------------------------------
	// Meta, checkpoints y semáforo
	// ---------------------------------------------------------------

	// Pórtico doble de medio hexágono, a −Spacing/2 y +Spacing/2 de la meta
	private void BuildFinish()
	{
		NVec2[] profile = TrackElementShapes.FinishProfile(_track.TrackWidth * 0.5f, _track.FinishClearance, _track.FinishHeight);
		var lines = new LineBatch();
		AddDoubleGate(lines, 0f, _track.FinishSpacing, profile, Palette.FinishGate);

		_finishFlash = new GateFlash(_track.FinishFlashDuration, 1f, FinishPeakBrightness, FinishPeakWhite);
		_finishMaterial = GlowMaterial();
		AddMesh("Finish", lines, _finishMaterial);
	}

	// Pórtico doble rectangular por checkpoint, con el panel del tiempo bajo el travesaño del arco de entrada
	private void BuildCheckpoints()
	{
		float hw = _track.TrackWidth * 0.5f;
		float w = hw + _track.CheckpointClearance;
		float height = _track.CheckpointHeight;
		NVec2[] profile = TrackElementShapes.CheckpointProfile(hw, _track.CheckpointClearance, height);
		float pixelSize = PixelSizeFor(PanelTextHeight);
		float panelHalf = w - PanelInset;

		for (int i = 0; i < _track.CheckpointDistances.Count; i++)
		{
			float s = _track.CheckpointDistances[i];
			var lines = new LineBatch();
			AddDoubleGate(lines, s, _track.CheckpointSpacing, profile, Palette.CheckpointGate);

			// Panel: marco colgado del travesaño del arco que se ve al acercarse
			float entry = s - _track.CheckpointSpacing * 0.5f;
			FramePoint f = _frame.At(entry);
			Vector3 tl = P(f, -panelHalf, height);
			Vector3 tr = P(f, panelHalf, height);
			Vector3 bl = P(f, -panelHalf, height - PanelHeight);
			Vector3 br = P(f, panelHalf, height - PanelHeight);
			lines.Add(tl, tr, Palette.CheckpointGate);
			lines.Add(tr, br, Palette.CheckpointGate);
			lines.Add(br, bl, Palette.CheckpointGate);
			lines.Add(bl, tl, Palette.CheckpointGate);

			var gate = new CheckpointGate
			{
				Flash = new GateFlash(_track.CheckpointFlashDuration, _track.CheckpointIdleBrightness,
					CheckpointPeakBrightness, CheckpointPeakWhite),
				Material = GlowMaterial()
			};
			AddMesh($"Checkpoint{i + 1}", lines, gate.Material);

			// Texto un poco por delante del panel, mirando a las naves que llegan (X = derecha, Y = arriba, Z = hacia atrás)
			FramePoint text = _frame.At(entry - 0.05f);
			var basis = new Basis(G(text.Right), G(text.Normal), -G(text.Tangent));
			float textH = height - PanelHeight * 0.5f;
			const float TextInset = 0.6f;

			gate.TimeLabel = MakeLabel(pixelSize, HorizontalAlignment.Left);
			gate.TimeLabel.Transform = new Transform3D(basis, P(text, -panelHalf + TextInset, textH));
			AddChild(gate.TimeLabel);

			gate.DeltaLabel = MakeLabel(pixelSize, HorizontalAlignment.Right);
			gate.DeltaLabel.Transform = new Transform3D(basis, P(text, panelHalf - TextInset, textH));
			AddChild(gate.DeltaLabel);

			_checkpoints.Add(gate);
		}
	}

	// Dos arcos iguales separados "spacing" m sobre el eje (centrados en "s"), unidos en cada vértice, con una X
	// en cada pilar (de la base de un arco al quiebro del otro). Uniones y X más tenues.
	private void AddDoubleGate(LineBatch lines, float s, float spacing, NVec2[] profile, Color color)
	{
		Vector3[] a = Arch(_frame.At(s - spacing * 0.5f), profile);
		Vector3[] b = Arch(_frame.At(s + spacing * 0.5f), profile);
		Color link = Dim(color, GateLinkBrightness);

		for (int k = 0; k < a.Length - 1; k++)
		{
			lines.Add(a[k], a[k + 1], color);
			lines.Add(b[k], b[k + 1], color);
		}
		for (int k = 0; k < a.Length; k++)
			lines.Add(a[k], b[k], link);

		int last = a.Length - 1;
		lines.Add(a[0], b[1], link);
		lines.Add(b[0], a[1], link);
		lines.Add(a[last], b[last - 1], link);
		lines.Add(b[last], a[last - 1], link);
	}

	// Semáforo colgado del travesaño del arco de entrada de la meta (el que mira a la parrilla): marco tenue con dos
	// tirantes y 5 luces octogonales; los octógonos interiores solo se ven con la luz encendida o en el destello.
	private void BuildStartLights(LineBatch fixedLines)
	{
		float top = _track.FinishHeight;
		float center = top - LightsBelowTop;
		FramePoint f = _frame.At(-_track.FinishSpacing * 0.5f - 0.1f);

		Color panel = Dim(Palette.StartPanel, LightsPanelBrightness);
		float hx = LightsPanelWidth * 0.5f;
		float hy = LightsPanelHeight * 0.5f;
		fixedLines.Add(P(f, -hx, center - hy), P(f, hx, center - hy), panel);
		fixedLines.Add(P(f, hx, center - hy), P(f, hx, center + hy), panel);
		fixedLines.Add(P(f, hx, center + hy), P(f, -hx, center + hy), panel);
		fixedLines.Add(P(f, -hx, center + hy), P(f, -hx, center - hy), panel);
		fixedLines.Add(P(f, -LightsTieX, center + hy), P(f, -LightsTieX, top), panel);
		fixedLines.Add(P(f, LightsTieX, center + hy), P(f, LightsTieX, top), panel);

		var lamps = new LineBatch();
		const int LightCount = 5;
		for (int i = 0; i < LightCount; i++)
		{
			float x = (i - (LightCount - 1) * 0.5f) * LightsSpacing;
			AddOctagon(lamps, f, x, center, LampRadius, new Vector2(i, 0f));
			foreach (float ring in LampRings)
				AddOctagon(lamps, f, x, center, LampRadius * ring, new Vector2(i, 1f));
		}

		_lightsMaterial = new ShaderMaterial { Shader = new Shader { Code = LightsShaderCode } };
		_lightsMaterial.SetShaderParameter("lamp_color", Rgb(Palette.StartLamp));
		_lightsMaterial.SetShaderParameter("lit_color", Rgb(Palette.StartLampLit));
		_lightsMaterial.SetShaderParameter("go_color", Rgb(Palette.StartLampGo));
		AddMesh("StartLights", lamps, _lightsMaterial);
	}

	private void AddOctagon(LineBatch lines, in FramePoint f, float x, float y, float radius, Vector2 uv)
	{
		for (int k = 0; k < 8; k++)
		{
			float a0 = Mathf.Pi / 8f + k * Mathf.Pi / 4f;
			float a1 = a0 + Mathf.Pi / 4f;
			Vector3 p0 = P(f, x + radius * Mathf.Cos(a0), y + radius * Mathf.Sin(a0));
			Vector3 p1 = P(f, x + radius * Mathf.Cos(a1), y + radius * Mathf.Sin(a1));
			lines.Add(p0, p1, Palette.StartLamp, uv, uv);
		}
	}

	// ---------------------------------------------------------------
	// Túneles
	// ---------------------------------------------------------------

	// Sección octogonal abierta por abajo: anillos azules cada RingSpacing m (con un segundo trazo que se ve con el
	// pulso), largueros cian en los 8 vértices, una diagonal por cara entre anillos (alternando como un tablero) y
	// bocas de anillo doble. Una sola malla y un material para todos los túneles.
	private void BuildTunnels()
	{
		if (_track.Tunnels.Count == 0)
			return;

		NVec2[] profile = TrackElementShapes.TunnelProfile(_track.TrackWidth * 0.5f, _track.TunnelClearance, _track.TunnelHeight);
		Color ring = Palette.TunnelRing;
		Color rail = Dim(Palette.TunnelRail, TunnelRailBrightness);
		Color lattice = Dim(Palette.TunnelRing, TunnelLatticeBrightness);
		var lines = new LineBatch();

		foreach (TrackSpan tunnel in _track.Tunnels)
		{
			float length = tunnel.Length;
			float[] rings = TrackElementShapes.TunnelRings(tunnel, _track.TunnelRingSpacing);
			var points = new Vector3[rings.Length][];
			for (int i = 0; i < rings.Length; i++)
				points[i] = Arch(_frame.At(rings[i]), profile);

			Vector2 Uv(float s) => new(s - tunnel.Start, length);
			var pulsing = new Vector2(0f, 1f);
			var pulsingThick = new Vector2(1f, 1f);

			for (int i = 0; i < rings.Length; i++)
			{
				AddPolyline(lines, points[i], ring, Uv(rings[i]), pulsing);
				AddPolyline(lines, Arch(_frame.At(rings[i] + TunnelThickGap), profile), ring, Uv(rings[i]), pulsingThick);
			}

			for (int i = 0; i < rings.Length - 1; i++)
			{
				Vector2 uvA = Uv(rings[i]);
				Vector2 uvB = Uv(rings[i + 1]);
				for (int k = 0; k < profile.Length; k++)
					lines.Add(points[i][k], points[i + 1][k], rail, uvA, uvB);
				for (int k = 0; k < profile.Length - 1; k++)
				{
					if ((i + k) % 2 == 0)
						lines.Add(points[i][k], points[i + 1][k + 1], lattice, uvA, uvB);
					else
						lines.Add(points[i][k + 1], points[i + 1][k], lattice, uvA, uvB);
				}
			}

			// Bocas: segundo anillo 1,2 m hacia fuera, unido por los vértices, y las dos con trazo doble siempre visible
			AddMouth(lines, profile, points[0], tunnel.Start, -TunnelMouthDepth, ring, Uv(tunnel.Start), pulsing);
			AddMouth(lines, profile, points[^1], tunnel.End, TunnelMouthDepth, ring, Uv(tunnel.End), pulsing);
		}

		_tunnelMaterial = new ShaderMaterial { Shader = new Shader { Code = TunnelShaderCode } };
		_tunnelMaterial.SetShaderParameter("pulse_speed", _track.TunnelPulseSpeed);
		_tunnelMaterial.SetShaderParameter("pulse_count", Mathf.Max(0, _track.TunnelPulseCount));
		_tunnelMaterial.SetShaderParameter("pulse_width", TunnelPulse.DefaultWidth);
		_tunnelMaterial.SetShaderParameter("pulse_color", Rgb(Palette.TunnelPulse));
		AddMesh("Tunnels", lines, _tunnelMaterial);
	}

	private void AddMouth(LineBatch lines, NVec2[] profile, Vector3[] inner, float s, float outward, Color color, Vector2 uv, Vector2 uv2)
	{
		float sign = Mathf.Sign(outward);
		Vector3[] outer = Arch(_frame.At(s + outward), profile);
		AddPolyline(lines, outer, color, uv, uv2);
		AddPolyline(lines, inner, color, uv, Vector2.Zero);   // trazo doble de la boca interior: sin pulso, siempre visible
		AddPolyline(lines, Arch(_frame.At(s + outward + sign * TunnelThickGap), profile), color, uv, Vector2.Zero);
		for (int k = 0; k < inner.Length; k++)
			lines.Add(inner[k], outer[k], color, uv, uv, uv2);
	}

	// ---------------------------------------------------------------
	// Avisos de curva y crestas
	// ---------------------------------------------------------------

	// Chevrons dobles (>>) cada ChevronSpacing m sobre la barrera exterior, mirando a la nave que llega y apuntando
	// hacia el giro, con un poste corto. La ola la pinta el shader con la distancia desde el inicio del aviso.
	private void BuildChevrons()
	{
		if (_track.CurveWarnings.Count == 0)
			return;

		var lines = new LineBatch();
		float top = _track.BarrierHeight;
		foreach (TrackSpan warning in _track.CurveWarnings)
		{
			BarrierSide side = TrackElementShapes.ChevronSide(TrackElementShapes.SignedTurn(_frame, warning));
			float sideSign = side == BarrierSide.Right ? 1f : -1f;
			float lateral = sideSign * _track.BarrierOffset;
			float toward = -sideSign;   // hacia dentro de la curva

			foreach (float s in TrackElementShapes.ChevronPositions(warning, _track.ChevronSpacing))
			{
				FramePoint f = _frame.At(s);
				var uv = new Vector2(s - warning.Start, 0f);
				Color c = Palette.CurveChevron;
				lines.Add(P(f, lateral, top), P(f, lateral, top + ChevronPost), c, uv, uv);

				float cy = top + ChevronCenter;
				foreach (float off in new[] { -0.45f, 0.45f })
				{
					foreach (float dy in new[] { 0f, ThickGap })
					{
						Vector3 upper = P(f, lateral + toward * (off - 0.5f), cy + 1f + dy);
						Vector3 tip = P(f, lateral + toward * (off + 0.5f), cy + dy);
						Vector3 lower = P(f, lateral + toward * (off - 0.5f), cy - 1f + dy);
						lines.Add(upper, tip, c, uv, uv);
						lines.Add(tip, lower, c, uv, uv);
					}
				}
			}
		}

		_chevronMaterial = new ShaderMaterial { Shader = new Shader { Code = ChevronShaderCode } };
		_chevronMaterial.SetShaderParameter("frequency", _track.ChevronWaveFrequency);
		_chevronMaterial.SetShaderParameter("cycle_length", ChevronCycleLength);
		_chevronMaterial.SetShaderParameter("base_brightness", ChevronWave.BaseBrightness);
		_chevronMaterial.SetShaderParameter("peak_brightness", ChevronWave.PeakBrightness);
		_chevronMaterial.SetShaderParameter("peak_white", ChevronWave.PeakWhite);
		_chevronMaterial.SetShaderParameter("white_color", Rgb(Palette.White));
		AddMesh("Chevrons", lines, _chevronMaterial);
	}

	// Balizas de cresta en las dos barreras: poste, marco (fijo) y 1–3 flechas ^ (con latido). La barrera naranja
	// la dibuja Track con el resto de la barrera.
	private void BuildCrests(LineBatch fixedLines)
	{
		if (_track.CrestWarnings.Count == 0)
			return;

		var arrows = new LineBatch();
		float top = _track.BarrierHeight;
		Color frame = Dim(Palette.CrestBeacon, BeaconFrameBrightness);

		foreach (float crest in _track.CrestWarnings)
		{
			foreach (CrestBeacon beacon in TrackElementShapes.CrestBeacons(crest, _track.CrestBeaconDistances))
			{
				FramePoint f = _frame.At(beacon.Distance);
				foreach (float side in new[] { -1f, 1f })
				{
					float barrier = side * _track.BarrierOffset;
					float cx = barrier - side * BeaconInward;
					float cy = top + BeaconPost + BeaconHalfHeight;
					fixedLines.Add(P(f, barrier, top), P(f, barrier, top + BeaconPost), frame);
					fixedLines.Add(P(f, cx - BeaconHalfWidth, cy - BeaconHalfHeight), P(f, cx + BeaconHalfWidth, cy - BeaconHalfHeight), frame);
					fixedLines.Add(P(f, cx + BeaconHalfWidth, cy - BeaconHalfHeight), P(f, cx + BeaconHalfWidth, cy + BeaconHalfHeight), frame);
					fixedLines.Add(P(f, cx + BeaconHalfWidth, cy + BeaconHalfHeight), P(f, cx - BeaconHalfWidth, cy + BeaconHalfHeight), frame);
					fixedLines.Add(P(f, cx - BeaconHalfWidth, cy + BeaconHalfHeight), P(f, cx - BeaconHalfWidth, cy - BeaconHalfHeight), frame);

					const float Gap = 0.55f;
					float y0 = -(beacon.Arrows - 1) * Gap * 0.5f - 0.15f;
					for (int i = 0; i < beacon.Arrows; i++)
					{
						foreach (float dy in new[] { 0f, ThickGap })
						{
							float y = cy + y0 + i * Gap + dy;
							Vector3 tip = P(f, cx, y + 0.45f);
							arrows.Add(P(f, cx - 0.75f, y), tip, Palette.CrestBeacon);
							arrows.Add(tip, P(f, cx + 0.75f, y), Palette.CrestBeacon);
						}
					}
				}
			}
		}

		_beaconMaterial = GlowMaterial();
		AddMesh("CrestBeacons", arrows, _beaconMaterial);
	}

	// ---------------------------------------------------------------
	// Utilidades
	// ---------------------------------------------------------------

	// Perfil (lateral, altura) en el plano de la sección del marco "f"
	private static Vector3[] Arch(in FramePoint f, NVec2[] profile)
	{
		var points = new Vector3[profile.Length];
		for (int k = 0; k < profile.Length; k++)
			points[k] = P(f, profile[k].X, profile[k].Y);
		return points;
	}

	private static void AddPolyline(LineBatch lines, Vector3[] points, Color color, Vector2 uv, Vector2 uv2)
	{
		for (int k = 0; k < points.Length - 1; k++)
			lines.Add(points[k], points[k + 1], color, uv, uv, uv2);
	}

	private static Vector3 P(in FramePoint f, float lateral, float height) => G(f.PointAt(lateral, height));

	private static Vector3 G(NVec3 v) => new(v.X, v.Y, v.Z);

	private static Vector3 Rgb(Color c) => new(c.R, c.G, c.B);

	// Color multiplicado por un brillo (opaco)
	private static Color Dim(Color c, float k) => new(c.R * k, c.G * k, c.B * k, 1f);

	private ShaderMaterial GlowMaterial()
	{
		var material = new ShaderMaterial { Shader = _glowShader };
		material.SetShaderParameter("white_color", Rgb(Palette.GateFlashWhite));
		return material;
	}

	private static void SetGlow(ShaderMaterial material, float brightness, float whiteMix)
	{
		material.SetShaderParameter("brightness", brightness);
		material.SetShaderParameter("white_mix", whiteMix);
	}

	private void AddMesh(string name, LineBatch lines, Material material)
	{
		ArrayMesh mesh = lines.ToMesh(material);
		if (mesh != null)
			AddChild(new MeshInstance3D { Name = name, Mesh = mesh });
	}

	// Metros por píxel de la fuente para que las mayúsculas midan "capHeight" m
	private static float PixelSizeFor(float capHeight) => capHeight / (LabelFontSize * FontCapHeight);

	private Label3D MakeLabel(float pixelSize, HorizontalAlignment alignment)
	{
		return new Label3D
		{
			Font = _font,
			FontSize = LabelFontSize,
			PixelSize = pixelSize,
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			OutlineSize = 0,
			DoubleSided = false,
			Shaded = false,
			Visible = false
		};
	}

	// Líneas de una malla: posiciones, colores y dos UV por vértice
	private sealed class LineBatch
	{
		private readonly List<Vector3> _verts = new();
		private readonly List<Color> _colors = new();
		private readonly List<Vector2> _uvs = new();
		private readonly List<Vector2> _uv2s = new();

		public void Add(Vector3 a, Vector3 b, Color color, Vector2 uvA = default, Vector2 uvB = default, Vector2 uv2 = default)
		{
			_verts.Add(a);
			_verts.Add(b);
			_colors.Add(color);
			_colors.Add(color);
			_uvs.Add(uvA);
			_uvs.Add(uvB);
			_uv2s.Add(uv2);
			_uv2s.Add(uv2);
		}

		public ArrayMesh ToMesh(Material material)
		{
			if (_verts.Count == 0)
				return null;

			var arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = _verts.ToArray();
			arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
			arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
			arrays[(int)Mesh.ArrayType.TexUV2] = _uv2s.ToArray();

			var mesh = new ArrayMesh();
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
			mesh.SurfaceSetMaterial(0, material);
			return mesh;
		}
	}
}
