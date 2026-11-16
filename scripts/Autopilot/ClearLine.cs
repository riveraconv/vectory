using System;
using System.Collections.Generic;

namespace Vectory.Autopilot;

// Lo que decide un rival con las naves de delante (spec docs/rivales-3-contacto.md, 8)
public enum LineState
{
	Clear,      // nadie le tapa la trazada
	PassLeft,   // se aparta por la izquierda
	PassRight,  // se aparta por la derecha
	Follow      // sin hueco: se queda detrás
}

// Una nave de la carrera vista desde la pista: distancia sobre el eje desde la meta, lateral sobre la superficie
// (positivo = derecha), velocidad a lo largo del eje (m/s), su forma y su velocidad de lado (m/s, positivo = derecha)
public readonly record struct TrafficShip(int Id, float Distance, float Lateral, float Speed, float HalfWidth, float Front, float Back,
	bool Passable, float LateralSpeed = 0f);

// Parámetros de la trazada clara (m, s y m/s)
public struct ClearLineSettings
{
	public const float DefaultMinLookahead = 30f;
	public const float DefaultLookaheadTime = 1.5f;
	public const float DefaultCorridorMargin = 0.5f;
	public const float DefaultPassGap = 1f;
	public const float DefaultMaxLateralSpeed = 4f;
	public const float DefaultReturnDistance = 5f;
	public const float DefaultFollowTime = 0.5f;
	public const float DefaultFollowMin = 8f;

	public float MinLookahead;     // distancia de mirada: la mayor entre esta…
	public float LookaheadTime;    // …y estos segundos a su velocidad
	public float CorridorMargin;   // margen por lado del pasillo
	public float PassGap;          // hueco entre cascos al apartarse
	public float BarrierMargin;    // hueco con la barrera al apartarse (el de la trazada)
	public float MaxLateralSpeed;  // lo que cambia el desplazamiento por segundo, como mucho
	public float ReturnDistance;   // su cola, esto por delante del morro del otro, para volver a la trazada
	public float FollowTime;       // hueco al seguir…
	public float FollowMin;        // …y como mínimo
	public float FollowGain;       // 1/s: cuánto baja la velocidad por cada metro que le falta de hueco
	public float SpeedTolerance;   // m/s: hasta esto más rápida, cuenta como "a la misma velocidad"

	public static ClearLineSettings Default => new()
	{
		MinLookahead = DefaultMinLookahead,
		LookaheadTime = DefaultLookaheadTime,
		CorridorMargin = DefaultCorridorMargin,
		PassGap = DefaultPassGap,
		BarrierMargin = 1f,
		MaxLateralSpeed = DefaultMaxLateralSpeed,
		ReturnDistance = DefaultReturnDistance,
		FollowTime = DefaultFollowTime,
		FollowMin = DefaultFollowMin,
		FollowGain = 1f,
		SpeedTolerance = 0.3f
	};
}

// Trazada clara de un rival (spec docs/rivales-3-contacto.md, 8): lo mínimo para no comerse a nadie por detrás. Mira las
// naves de delante hasta la distancia de mirada; una que va igual o más lenta y se cruza con el pasillo de su trazada
// (su ancho, con margen) se la tapa. Entonces busca un hueco a un lado (el de más sitio, contando la barrera y las demás
// naves) y se aparta poco a poco, con la velocidad de la línea desplazada; sin hueco, la sigue a 0,5 s igualando su
// velocidad. Cuando la ha pasado y ese lado está libre, vuelve a la trazada. Sin defender, cerrar ni errores (spec 4).
// El pasillo va de su carril (la trazada con su desplazamiento) a la trazada: así también mira hacia donde vuelve. Y el
// desplazamiento empieza donde está la nave (en la salida, su columna de la parrilla; tras recolocarla, el centro): llega
// a la trazada a MaxLateralSpeed y solo con ese lado libre, como al volver de pasar, sin cerrarse en diagonal sobre otra.
// Lógica pura: no depende de Godot.
public sealed class ClearLine
{
	private const float MinRadius = 5f;          // m: la línea desplazada nunca se cuenta más cerrada
	private const float MaxCapDistance = 300f;   // m por delante que se miran para frenar a tiempo por fuera
	private const float OnLine = 0.05f;          // m: con menos desplazamiento, va por la trazada
	private const float LaneTolerance = 1f;      // m: una nave a menos de esto de su lateral va en su mismo carril
	private const float LateralForesight = 1f;   // s: al mirar si tiene sitio al lado, también donde estará la otra
	private const float MaxLead = 3f;            // m: el desplazamiento nunca va más allá de esto de donde está de verdad

	private readonly RacingLine _line;
	private readonly CurveTable _table;
	private readonly float _lapLength;
	private readonly float _barrierOffset;
	private readonly float _braking;
	private float _targetOffset;
	private bool _placed;
	private bool _holding;

	public ClearLineSettings Settings { get; set; } = ClearLineSettings.Default;

	// Ritmo: el margen sobre la velocidad de curva, como el del perfil
	public float Pace { get; set; } = AutopilotSettings.DefaultSpeedMargin;

	public LineState State { get; private set; }

	// Desplazamiento lateral respecto a la trazada (m, positivo = derecha)
	public float Offset { get; private set; }

	// Velocidad máxima de este paso (m/s); infinita sin límite
	public float SpeedCap { get; private set; } = float.PositiveInfinity;

	// Id de la nave que le tapa la trazada; -1 si ninguna
	public int Blocker { get; private set; } = -1;

	// Distancia de mirada del último paso (m)
	public float Lookahead { get; private set; }

	// Lo más lejos del eje que puede ir apartado (m a cada lado): la barrera menos su margen y su semiancho
	public float LateralLimit { get; private set; } = float.PositiveInfinity;

	public ClearLine(RacingLine line, CurveTable table, float lapLength, float barrierOffset, float braking)
	{
		_line = line;
		_table = table;
		_lapLength = lapLength;
		_barrierOffset = barrierOffset;
		_braking = braking;
	}

	// Sin nadie (al recolocar o al terminar): vuelve a la trazada al momento
	public void Reset()
	{
		_placed = false;
		_holding = false;
		State = LineState.Clear;
		Offset = 0f;
		_targetOffset = 0f;
		SpeedCap = float.PositiveInfinity;
		Blocker = -1;
	}

	public void Update(in TrafficShip self, IReadOnlyList<TrafficShip> others, float dt)
	{
		ClearLineSettings s = Settings;
		float speed = MathF.Max(0f, self.Speed);
		Lookahead = MathF.Max(s.MinLookahead, s.LookaheadTime * speed);
		LateralLimit = MathF.Max(0f, _barrierOffset - s.BarrierMargin - self.HalfWidth);
		SpeedCap = float.PositiveInfinity;

		// El desplazamiento se mide en el punto al que apunta el piloto: lo que se aparta de la trazada allí. La primera
		// vez (en la parrilla, o recolocada) y después de quedarse en su carril, parte de donde está de verdad.
		float aimLine = _line.LateralAt(self.Distance + AutopilotDriver.Lookahead(speed));
		float deviation = self.Lateral - aimLine;
		if (!_placed || _holding)
		{
			_placed = true;
			Offset = _targetOffset = deviation;
		}
		_holding = false;

		bool hold = false;
		int blocker = FindBlocker(self, others);
		Blocker = blocker >= 0 ? others[blocker].Id : -1;

		if (blocker >= 0)
		{
			TrafficShip b = others[blocker];
			int side = ChooseSide(self, b, others);
			if (side != 0)
			{
				// Al lado de la de delante, con su hueco, donde vaya ella ahora y sin pasar de la barrera
				State = side < 0 ? LineState.PassLeft : LineState.PassRight;
				float beside = Math.Clamp(b.Lateral + side * (self.HalfWidth + b.HalfWidth + s.PassGap), -LateralLimit, LateralLimit);
				_targetOffset = beside - aimLine;
				// Mientras su pasillo aún se cruza con la de delante, no se le echa encima
				if (Overlaps(self.Lateral, self.HalfWidth, b))
					SpeedCap = FollowCap(self, b);
			}
			else
			{
				// Detrás, en la trazada si puede volver a ella; si no, en su carril
				State = LineState.Follow;
				SpeedCap = FollowCap(self, b);
				hold = MathF.Abs(Offset) > OnLine && !CanReturn(self, others);
				_targetOffset = 0f;
			}
		}
		else
		{
			// Nadie le tapa: vuelve a la trazada si ese lado está libre; si no, se queda en su carril
			State = LineState.Clear;
			hold = MathF.Abs(Offset) > OnLine && !CanReturn(self, others);
			_targetOffset = 0f;
		}

		// Nunca se mueve de lado hacia donde ya hay una nave a su lado: ni al apartarse o volver, ni al seguir su trazada,
		// que en las curvas va de lado y no es la misma en todas las naves. Quedarse en su carril es apuntar a su mismo
		// lateral (no a la trazada con el desplazamiento de antes, que en una curva lo llevaría hacia la barrera).
		if (hold || !LaneFree(self, others, aimLine + _targetOffset))
		{
			_holding = true;
			Offset = _targetOffset = deviation;
		}
		else
		{
			// Poco a poco, y nunca más allá de MaxLead de donde va de verdad (si el piloto se queda atrás, no se acumula)
			float step = s.MaxLateralSpeed * dt;
			Offset = MathF.Abs(_targetOffset - Offset) <= step ? _targetOffset : Offset + MathF.Sign(_targetOffset - Offset) * step;
			Offset = Math.Clamp(Offset, deviation - MaxLead, deviation + MaxLead);
		}

		if (MathF.Abs(Offset) > OnLine || MathF.Abs(_targetOffset) > OnLine)
		{
			float offset = MathF.Abs(_targetOffset) > MathF.Abs(Offset) ? _targetOffset : Offset;
			SpeedCap = MathF.Min(SpeedCap, DisplacedCap(self, Math.Clamp(offset, -2f * LateralLimit, 2f * LateralLimit)));
		}
	}

	// La nave más cercana de delante que le tapa la trazada (índice en others; -1 si ninguna)
	private int FindBlocker(in TrafficShip self, IReadOnlyList<TrafficShip> others)
	{
		int best = -1;
		float bestGap = float.MaxValue;
		for (int i = 0; i < others.Count; i++)
		{
			TrafficShip o = others[i];
			if (o.Id == self.Id || o.Passable)
				continue;
			float ds = Relative(o.Distance - self.Distance);
			if (ds <= 0f)
				continue;
			float gap = ds - self.Front - o.Back;
			if (gap > Lookahead || o.Speed > self.Speed + Settings.SpeedTolerance)
				continue;
			// Su pasillo: de su carril a la trazada, con su ancho
			float lineLateral = _line.LateralAt(o.Distance);
			float low = lineLateral + MathF.Min(0f, Offset) - self.HalfWidth;
			float high = lineLateral + MathF.Max(0f, Offset) + self.HalfWidth;
			float reach = o.HalfWidth + Settings.CorridorMargin;
			if (o.Lateral <= low - reach || o.Lateral >= high + reach)
				continue;
			if (gap < bestGap)
			{
				bestGap = gap;
				best = i;
			}
		}
		return best;
	}

	// Lado para pasar a la de delante: -1 izquierda, +1 derecha, 0 sin hueco. El de más sitio; si ya se apartaba por
	// un lado y sigue habiendo hueco, ese.
	private int ChooseSide(in TrafficShip self, in TrafficShip blocker, IReadOnlyList<TrafficShip> others)
	{
		float left = Room(self, blocker, -1, others);
		float right = Room(self, blocker, 1, others);
		float need = 2f * (self.HalfWidth + Settings.PassGap);
		bool leftFits = left >= need;
		bool rightFits = right >= need;
		if (State == LineState.PassLeft && leftFits)
			return -1;
		if (State == LineState.PassRight && rightFits)
			return 1;
		if (leftFits && (!rightFits || left > right))
			return -1;
		return rightFits ? 1 : 0;
	}

	// Sitio libre a un lado de la de delante (m): de su borde hasta la barrera (con su margen) o hasta la nave más
	// cercana de ese lado entre él y la mirada. Una nave a su lado, entre él y ese hueco, lo cierra.
	private float Room(in TrafficShip self, in TrafficShip blocker, int side, IReadOnlyList<TrafficShip> others)
	{
		ClearLineSettings s = Settings;
		float edge = blocker.Lateral + side * blocker.HalfWidth;
		float limit = side * (_barrierOffset - s.BarrierMargin);
		float room = side * (limit - edge);

		for (int i = 0; i < others.Count; i++)
		{
			TrafficShip o = others[i];
			if (o.Id == self.Id || o.Id == blocker.Id || o.Passable)
				continue;
			float ds = Relative(o.Distance - self.Distance);
			if (ds < -(self.Back + o.Front + s.ReturnDistance) || ds - self.Front - o.Back > Lookahead)
				continue;

			// Una nave a su lado, entre él y el hueco, no le deja apartarse hacia allí
			bool alongside = MathF.Abs(ds) < self.Front + self.Back + o.Front + o.Back;
			float toward = side * (o.Lateral - self.Lateral);
			if (alongside && toward > 0f && side * (o.Lateral - edge) < 2f * self.HalfWidth + s.PassGap + o.HalfWidth)
				return 0f;

			float gap = side * (o.Lateral - edge) - o.HalfWidth;
			if (side * (o.Lateral - blocker.Lateral) > 0f)
				room = MathF.Min(room, gap);
		}
		return room;
	}

	// Puede volver a la trazada: entre él y ella no hay ninguna nave a su lado ni que haya pasado hace menos de
	// ReturnDistance (su cola, esa distancia por delante del morro del otro)
	private bool CanReturn(in TrafficShip self, IReadOnlyList<TrafficShip> others) =>
		LaneFree(self, others, _line.LateralAt(self.Distance));

	// Puede moverse de lado hasta target: no hay ninguna nave a su lado (con ReturnDistance de margen por delante y por
	// detrás) hacia ese lado, entre él y target. Las que van justo detrás o delante en su carril no cuentan.
	private bool LaneFree(in TrafficShip self, IReadOnlyList<TrafficShip> others, float target)
	{
		ClearLineSettings s = Settings;
		float move = target - self.Lateral;
		if (MathF.Abs(move) <= OnLine)
			return true;
		float side = MathF.Sign(move);
		for (int i = 0; i < others.Count; i++)
		{
			TrafficShip o = others[i];
			if (o.Id == self.Id || o.Passable)
				continue;
			float ds = Relative(o.Distance - self.Distance);
			if (ds < -(self.Back + o.Front + s.ReturnDistance) || ds > self.Front + o.Back + s.ReturnDistance)
				continue;
			// Donde está y donde estará si sigue moviéndose de lado: las dos pueden estar cerrándose a la vez
			float toward = side * (o.Lateral - self.Lateral);
			float towardLater = side * (o.Lateral + o.LateralSpeed * LateralForesight - self.Lateral);
			float reach = self.HalfWidth + o.HalfWidth + s.CorridorMargin;
			if (toward > LaneTolerance && MathF.Min(toward, towardLater) < MathF.Abs(move) + reach)
				return false;
		}
		return true;
	}

	// Un pasillo de semiancho halfWidth centrado en center se cruza con la nave o (con el margen a cada lado)
	private bool Overlaps(float center, float halfWidth, in TrafficShip o) =>
		MathF.Abs(o.Lateral - center) < halfWidth + o.HalfWidth + Settings.CorridorMargin;

	// Detrás de la de delante: su velocidad, menos cuanto más le falte para el hueco de FollowTime (mínimo FollowMin)
	private float FollowCap(in TrafficShip self, in TrafficShip b)
	{
		ClearLineSettings s = Settings;
		float gap = Relative(b.Distance - self.Distance) - self.Front - b.Back;
		float wanted = MathF.Max(s.FollowMin, s.FollowTime * MathF.Max(0f, self.Speed));
		return MathF.Max(0f, MathF.Max(0f, b.Speed) + s.FollowGain * (gap - wanted));
	}

	// La velocidad de la línea desplazada: la de la tabla para su radio (más abierto o más cerrado según el lado), con el
	// ritmo, y lo bastante baja para frenar a tiempo hasta los puntos de delante
	private float DisplacedCap(in TrafficShip self, float offset)
	{
		int start = _line.IndexAt(self.Distance);
		float reach = MathF.Min(MaxCapDistance, self.Speed * self.Speed / (2f * MathF.Max(1f, _braking)) + Lookahead);
		int points = Math.Min(_line.Count - 1, (int)MathF.Ceiling(reach / _line.PointSpacing));
		float cap = float.PositiveInfinity;
		for (int k = 0; k <= points; k++)
		{
			int i = _line.Wrap(start + k);
			float curvature = _line.Curvature[i];
			float denominator = 1f + curvature * offset;
			float radius = denominator > 0f ? MathF.Abs(denominator / MathF.Max(MathF.Abs(curvature), 1e-6f)) : MinRadius;
			float v = _table.SpeedFor(MathF.Max(MinRadius, radius)) * Pace;
			float distance = k * _line.PointSpacing;
			cap = MathF.Min(cap, MathF.Sqrt(v * v + 2f * _braking * distance));
		}
		return cap;
	}

	// Distancia sobre la vuelta entre −media vuelta y +media vuelta
	private float Relative(float ds)
	{
		float half = _lapLength * 0.5f;
		ds %= _lapLength;
		if (ds > half)
			ds -= _lapLength;
		else if (ds <= -half)
			ds += _lapLength;
		return ds;
	}
}
