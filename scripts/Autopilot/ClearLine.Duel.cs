using System;
using System.Collections.Generic;

namespace Vectory.Autopilot;

// El duelo de la trazada clara (spec docs/rivales-4-personalidades.md, 4.3–4.5), con los rasgos de su piloto:
//  - SLIP: alcanzando a otra en recta, con recta por delante, se pega detrás en su rebufo (a SlipGap), alineada con ella.
//  - ATTACK en recta: a 120 m + 120 m × ataque de la frenada sale del rebufo por el lado con hueco. Sin hueco, con poco
//    ataque se queda detrás (FOLLOW) y con más sigue en el rebufo esperando.
//  - ATTACK en frenada: llegando a la frenada a menos de 15 m de la de delante, con hueco por el interior y ataque ≥ 0,5,
//    se tira por dentro y frena un poco más tarde (LateBraking).
//  - Aborta si al llegar a la frenada (por dentro, 30 m después) no tiene el morro a la mitad de la otra (con contacto
//    ≥ 0,7, a una cuarta parte): vuelve detrás y no lo intenta con ella en AbortWait.
//  - DEFEND: con alguien detrás a menos de 20 m (que se acerca o va en su rebufo) y 100 m hasta la frenada, decide una vez
//    por recta, con probabilidad = defensa, si cubre el interior de la siguiente curva. Un solo cambio de línea: se queda
//    ahí hasta salir de la curva. No se mueve hacia una nave con una cuarta parte de su largo en paralelo (con contacto
//    ≥ 0,7, sí mientras tenga menos de la mitad) ni si el cierre de lado llegaría a MaxClosingSpeed.
//  - Contacto < 0,5: al rozar de lado se aparta 1 m un momento (Yield).
// El hueco entre cascos al pasar (PassGap) lo pone quien la usa: 1 m − 0,7 × contacto.
public sealed partial class ClearLine
{
	private float _time;
	private int _attackId = -1;
	private int _attackSide;
	private bool _attackBraking;
	private float _attackCheckAt;       // m sobre el eje donde mira si aborta
	private bool _attackChecked;
	private int _defendSide;
	private int _defendDecided = -1;    // la frenada para la que ya decidió si defiende
	private bool _defendLeftStraight;
	private int _yieldSide;
	private float _yieldUntil;
	private readonly Dictionary<int, float> _abortedUntil = new();

	// Rasgos de duelo; null = solo la trazada clara de la spec 3
	public DuelTraits? Duel { get; set; }

	// Para decidir si defiende (inyectado: con la misma semilla, lo mismo)
	public Random DuelRandom { get; set; } = new(0);

	public DuelSettings DuelSettings { get; set; } = DuelSettings.Default;

	// Atacando por dentro en la frenada: frena un poco más tarde
	public bool LateBraking { get; private set; }

	// Id de la nave a la que ataca; -1 si ninguna
	public int AttackTarget => _attackId;

	// Hueco con la nave más cercana de delante en su carril y con la más cercana de detrás que le sigue (m; infinito si no hay)
	public float AheadGap { get; private set; } = float.PositiveInfinity;
	public float BehindGap { get; private set; } = float.PositiveInfinity;

	// Adelantamientos abortados (depuración)
	public int Aborts { get; private set; }

	public bool IsAttacking => State is LineState.AttackLeft or LineState.AttackRight;

	// Roce de lado con otra nave: con poco contacto, se aparta hacia away (−1 izquierda, +1 derecha)
	public void Yield(int away)
	{
		if (!Duel.HasValue || Duel.Value.Contact >= 0.5f || away == 0)
			return;
		_yieldSide = Math.Sign(away);
		_yieldUntil = _time + DuelSettings.YieldTime;
	}

	// Sin duelo en marcha: ni ataque ni defensa (las esperas tras abortar siguen)
	private void EndDuel()
	{
		_attackId = -1;
		_defendSide = 0;
		_yieldSide = 0;
		LateBraking = false;
		AheadGap = BehindGap = float.PositiveInfinity;
	}

	// Lo que tiene que haber a un lado para pasar: su ancho y el hueco a cada lado
	private float RoomNeeded(in TrafficShip self) => 2f * (self.HalfWidth + Settings.PassGap);

	private void MeasureGaps(in TrafficShip self, IReadOnlyList<TrafficShip> others)
	{
		AheadGap = BehindGap = float.PositiveInfinity;
		for (int i = 0; i < others.Count; i++)
		{
			TrafficShip o = others[i];
			if (o.Id == self.Id || o.Passable)
				continue;
			if (MathF.Abs(o.Lateral - self.Lateral) > self.HalfWidth + o.HalfWidth + Settings.CorridorMargin)
				continue;
			float ds = Relative(o.Distance - self.Distance);
			if (ds > 0f)
				AheadGap = MathF.Min(AheadGap, MathF.Max(0f, ds - self.Front - o.Back));
			else
				BehindGap = MathF.Min(BehindGap, MathF.Max(0f, -ds - self.Back - o.Front));
		}
	}

	// Un ataque en marcha: sigue, termina (la ha pasado, o la curva se acabó) o aborta. true si ha decidido el estado.
	private bool UpdateAttack(in TrafficShip self, IReadOnlyList<TrafficShip> others, in TrackAhead ahead, float aimLine, ref bool hold)
	{
		if (_attackId < 0)
			return false;
		int index = IndexOf(others, _attackId);
		if (index < 0 || others[index].Passable)
		{
			_attackId = -1;
			return false;
		}

		TrafficShip a = others[index];
		ClearLineSettings s = Settings;
		float ds = Relative(a.Distance - self.Distance);
		// Ya la ha pasado: su cola, ReturnDistance por delante del morro de la otra
		if (-ds - self.Back - a.Front >= s.ReturnDistance)
		{
			_attackId = -1;
			return false;
		}

		if (!_attackChecked && Relative(self.Distance - _attackCheckAt) >= 0f)
		{
			_attackChecked = true;
			if (Alongside(self, a, ds) < (Duel.Value.Contact >= 0.7f ? 0.25f : 0.5f))
			{
				Abort(self, a, others, ref hold);
				return true;
			}
		}
		// Pasada la frenada sin adelantarla, al volver a la recta lo deja
		if (_attackChecked && ahead.OnStraight)
		{
			_attackId = -1;
			return false;
		}
		// Sin hueco a ese lado, aborta
		if (Room(self, a, _attackSide, others) < RoomNeeded(self) && Alongside(self, a, ds) <= 0f)
		{
			Abort(self, a, others, ref hold);
			return true;
		}

		ApplyAttack(self, a, aimLine);
		return true;
	}

	private void ApplyAttack(in TrafficShip self, in TrafficShip a, float aimLine)
	{
		State = _attackSide < 0 ? LineState.AttackLeft : LineState.AttackRight;
		float beside = Math.Clamp(a.Lateral + _attackSide * (self.HalfWidth + a.HalfWidth + Settings.PassGap), -LateralLimit, LateralLimit);
		_targetOffset = beside - aimLine;
		if (Overlaps(self.Lateral, self.HalfWidth, a))
			SpeedCap = PassCap(self, a);
		LateBraking = _attackBraking;
	}

	private void StartAttack(in TrafficShip self, in TrafficShip a, int side, bool braking, in TrackAhead ahead, float aimLine)
	{
		_attackId = a.Id;
		_attackSide = side;
		_attackBraking = braking;
		_attackChecked = false;
		float check = ahead.ToBraking + (braking ? DuelSettings.BrakingAttackCheck : 0f);
		_attackCheckAt = self.Distance + check;
		ApplyAttack(self, a, aimLine);
	}

	private void Abort(in TrafficShip self, in TrafficShip a, IReadOnlyList<TrafficShip> others, ref bool hold)
	{
		_abortedUntil[a.Id] = _time + DuelSettings.AbortWait;
		_attackId = -1;
		Aborts++;
		Follow(self, a, others, ref hold);
	}

	private void Follow(in TrafficShip self, in TrafficShip b, IReadOnlyList<TrafficShip> others, ref bool hold)
	{
		State = LineState.Follow;
		SpeedCap = MathF.Min(SpeedCap, FollowCap(self, b));
		hold = MathF.Abs(Offset) > 0.05f && !CanReturn(self, others);
		_targetOffset = 0f;
	}

	// Con una nave que le tapa la trazada: SLIP, ATTACK o FOLLOW por el duelo. false = lo de la spec 3 (pasar o seguir).
	private bool DecideDuel(in TrafficShip self, in TrafficShip b, IReadOnlyList<TrafficShip> others, in TrackAhead ahead, float aimLine, ref bool hold)
	{
		DuelSettings d = DuelSettings;
		DuelTraits traits = Duel.Value;
		if (_abortedUntil.TryGetValue(b.Id, out float until) && _time < until)
		{
			Follow(self, b, others, ref hold);
			return true;
		}

		// En recta, alcanzándola a una velocidad parecida
		bool similar = b.Speed >= self.Speed - d.SlipSpeedDifference && b.Speed >= d.SlipMinSpeed && self.Speed >= d.SlipMinSpeed;
		if (ahead.OnStraight && similar && (ahead.ToBraking >= d.SlipMinStraight || State == LineState.Slip))
		{
			float exit = d.SlipExitBase + d.SlipExitPerAttack * traits.Attack;
			if (ahead.ToBraking > exit)
			{
				Slip(self, b, aimLine);
				return true;
			}
			int side = ChooseSide(self, b, others);
			if (side != 0)
			{
				StartAttack(self, b, side, false, ahead, aimLine);
				return true;
			}
			if (traits.Attack < d.MinAttackToWait)
				Follow(self, b, others, ref hold);
			else
				Slip(self, b, aimLine);
			return true;
		}

		// En la frenada, por dentro
		float gap = Relative(b.Distance - self.Distance) - self.Front - b.Back;
		if (traits.Attack >= d.MinBrakingAttack && gap <= d.BrakingAttackDistance && ahead.ToBraking <= d.BrakingAttackWindow
			&& ahead.InsideSide != 0 && Room(self, b, ahead.InsideSide, others) >= RoomNeeded(self))
		{
			StartAttack(self, b, ahead.InsideSide, true, ahead, aimLine);
			return true;
		}
		return false;
	}

	// Detrás de la de delante, alineada con ella para ir en su rebufo, a SlipGap
	private void Slip(in TrafficShip self, in TrafficShip b, float aimLine)
	{
		DuelSettings d = DuelSettings;
		State = LineState.Slip;
		float gap = Relative(b.Distance - self.Distance) - self.Front - b.Back;
		float wanted = MathF.Max(d.SlipGapMin, d.SlipGapTime * MathF.Max(0f, self.Speed));
		SpeedCap = MathF.Max(0f, MathF.Max(0f, b.Speed) + Settings.FollowGain * (gap - wanted));
		_targetOffset = Math.Clamp(b.Lateral, -LateralLimit, LateralLimit) - aimLine;
	}

	// Defensa del interior: true si está defendiendo en este paso
	private bool UpdateDefense(in TrafficShip self, IReadOnlyList<TrafficShip> others, in TrackAhead ahead, float aimLine)
	{
		DuelSettings d = DuelSettings;
		if (_defendSide != 0)
		{
			if (!ahead.OnStraight)
				_defendLeftStraight = true;
			else if (_defendLeftStraight)
				_defendSide = 0;   // salió de la curva: vuelve a su trazada
		}
		else
		{
			if (!ahead.OnStraight || ahead.ToBraking < d.DefendMinToBraking || ahead.BrakingIndex == _defendDecided || ahead.InsideSide == 0)
				return false;
			if (FindChaser(self, others) < 0)
				return false;
			// Una vez por recta
			_defendDecided = ahead.BrakingIndex;
			if (DuelRandom.NextDouble() >= Duel.Value.Defense)
				return false;
			_defendSide = ahead.InsideSide;
			_defendLeftStraight = false;
		}
		if (_defendSide == 0)
			return false;

		State = _defendSide < 0 ? LineState.DefendLeft : LineState.DefendRight;
		_targetOffset = _defendSide * LateralLimit * d.DefendFraction - aimLine;
		return true;
	}

	// La nave más cercana de detrás que le persigue: a menos de DefendDistance, acercándose o en su rebufo
	private int FindChaser(in TrafficShip self, IReadOnlyList<TrafficShip> others)
	{
		int best = -1;
		float bestGap = float.MaxValue;
		for (int i = 0; i < others.Count; i++)
		{
			TrafficShip o = others[i];
			if (o.Id == self.Id || o.Passable)
				continue;
			float ds = Relative(o.Distance - self.Distance);
			if (ds >= 0f)
				continue;
			float gap = -ds - self.Back - o.Front;
			if (gap > DuelSettings.DefendDistance)
				continue;
			bool approaching = o.Speed > self.Speed + Settings.SpeedTolerance;
			bool inWake = MathF.Abs(o.Lateral - self.Lateral) <= self.HalfWidth;
			if ((approaching || inWake) && gap < bestGap)
			{
				bestGap = gap;
				best = i;
			}
		}
		return best;
	}

	// Al cubrir: no se mueve hacia una nave con una cuarta parte de su largo en paralelo (contacto ≥ 0,7: menos de la
	// mitad, aceptando el roce), ni si el cierre de lado llegaría a MaxClosingSpeed. Las de detrás no cuentan.
	private bool CanCover(in TrafficShip self, IReadOnlyList<TrafficShip> others, float target)
	{
		ClearLineSettings s = Settings;
		float move = target - self.Lateral;
		if (MathF.Abs(move) <= 0.05f)
			return true;
		float side = MathF.Sign(move);
		float contact = Duel?.Contact ?? 0f;
		for (int i = 0; i < others.Count; i++)
		{
			TrafficShip o = others[i];
			if (o.Id == self.Id || o.Passable)
				continue;
			float toward = side * (o.Lateral - self.Lateral);
			float reach = self.HalfWidth + o.HalfWidth + s.CorridorMargin;
			if (toward <= 1f || toward >= MathF.Abs(move) + reach)
				continue;
			float ds = Relative(o.Distance - self.Distance);
			float alongside = Alongside(o, self, -ds);   // cuánto de su largo tiene la otra en paralelo con él
			if (ds > 0f && ds - self.Front - o.Back < s.ReturnDistance)
				return false;   // a su lado y por delante: nunca
			if (alongside <= 0f)
				continue;
			bool lateDoor = contact >= 0.7f && alongside < 0.5f;
			if (alongside >= 0.25f && !lateDoor)
				return false;
			float closing = s.MaxLateralSpeed + MathF.Max(0f, -side * o.LateralSpeed);
			if (closing >= DuelSettings.MaxClosingSpeed)
				return false;
		}
		return true;
	}

	// Qué parte del largo de b tiene a en paralelo, por el morro de a: 0 si el morro de a no llega a la cola de b, 1 si
	// llega a su morro. ds: distancia de b respecto a a (positivo = b delante).
	private static float Alongside(in TrafficShip a, in TrafficShip b, float ds)
	{
		float length = MathF.Max(0.1f, b.Front + b.Back);
		float noseOverTail = a.Front + b.Back - ds;   // morro de a menos cola de b
		return Math.Clamp(noseOverTail / length, 0f, 1f);
	}

	private static int IndexOf(IReadOnlyList<TrafficShip> others, int id)
	{
		for (int i = 0; i < others.Count; i++)
		{
			if (others[i].Id == id)
				return i;
		}
		return -1;
	}
}
