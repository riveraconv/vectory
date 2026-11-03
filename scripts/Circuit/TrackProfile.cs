using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Vectory.Circuit;

// Valor a lo largo de la vuelta definido por claves (distancia sobre el eje en planta, valor), sin Godot
// (spec docs/pista-3d.md, 2.1): la altura de la pista (m) o su peralte (grados).
//
// Entre claves, interpolación cúbica de Hermite monótona: sin escalones en el valor ni en la pendiente y
// sin pasarse de los valores de las claves (en un máximo o un mínimo la derivada es 0). La derivada en
// cada clave es la media de las secantes ponderada por la longitud de los tramos (exacta para una
// parábola, así una cresta definida con claves de una parábola sale con radio constante), limitada a
// 3 veces la secante menor (condición de Fritsch–Carlson para que no haya jorobas).
// La vuelta es cerrada: tras la última clave se interpola hacia la primera, pasando por la meta.
// Sin claves el valor es 0 en toda la vuelta.
public sealed class TrackProfile
{
	private readonly float[] _s;
	private readonly float[] _v;
	private readonly float[] _d;
	private readonly float _lap;

	public int KeyCount => _s.Length;

	// Sin claves o con todas a 0: el valor es 0 en toda la vuelta
	public bool IsZero { get; }

	// Mayor y menor valor de las claves (la interpolación no se pasa de ellos)
	public float MaxValue { get; }
	public float MinValue { get; }
	public float MaxAbsValue => MathF.Max(MathF.Abs(MaxValue), MathF.Abs(MinValue));

	// keys: (distancia en m desde la meta, valor). Las claves con la distancia fuera de la vuelta
	// [0, lapLength) o no mayor que la anterior aceptada se ignoran, con un aviso en warnings.
	public TrackProfile(IReadOnlyList<Vector2> keys, float lapLength, ICollection<string> warnings = null, string name = "profile")
	{
		_lap = lapLength;
		var s = new List<float>();
		var v = new List<float>();

		if (keys != null)
		{
			for (int i = 0; i < keys.Count; i++)
			{
				Vector2 key = keys[i];
				if (!(key.X >= 0f && key.X < lapLength))
				{
					warnings?.Add($"{name}: key {i} ({Format(key.X)} m) is outside the lap (0–{Format(lapLength)} m); ignored");
					continue;
				}
				if (s.Count > 0 && key.X <= s[^1])
				{
					warnings?.Add($"{name}: key {i} ({Format(key.X)} m) is out of order; ignored");
					continue;
				}
				s.Add(key.X);
				v.Add(key.Y);
			}
		}

		_s = s.ToArray();
		_v = v.ToArray();
		_d = new float[_s.Length];

		bool zero = true;
		float max = 0f;
		float min = 0f;
		for (int i = 0; i < _v.Length; i++)
		{
			zero &= _v[i] == 0f;
			max = i == 0 ? _v[i] : MathF.Max(max, _v[i]);
			min = i == 0 ? _v[i] : MathF.Min(min, _v[i]);
		}
		IsZero = zero;
		MaxValue = max;
		MinValue = min;

		if (_s.Length >= 2)
			ComputeDerivatives();
	}

	public float ValueAt(float distance)
	{
		Evaluate(distance, out float value, out _, out _);
		return value;
	}

	// Derivada del valor respecto a la distancia (por metro)
	public float SlopeAt(float distance)
	{
		Evaluate(distance, out _, out float slope, out _);
		return slope;
	}

	// Segunda derivada respecto a la distancia (por metro²)
	public float CurvatureAt(float distance)
	{
		Evaluate(distance, out _, out _, out float curvature);
		return curvature;
	}

	public void Evaluate(float distance, out float value, out float slope, out float curvature)
	{
		int k = _s.Length;
		if (k == 0 || IsZero)
		{
			value = 0f;
			slope = 0f;
			curvature = 0f;
			return;
		}
		if (k == 1)
		{
			value = _v[0];
			slope = 0f;
			curvature = 0f;
			return;
		}

		float x = PosMod(distance, _lap);

		// Tramo: la última clave con distancia <= x; antes de la primera, el tramo que cierra la vuelta
		int i = k - 1;
		for (int j = 0; j < k; j++)
		{
			if (_s[j] > x)
			{
				i = j - 1;
				break;
			}
		}
		if (i < 0)
		{
			i = k - 1;
			x += _lap;
		}

		int next = (i + 1) % k;
		float s0 = _s[i];
		float s1 = next == 0 ? _s[0] + _lap : _s[next];
		float h = s1 - s0;
		float u = h > 0f ? (x - s0) / h : 0f;

		float v0 = _v[i];
		float v1 = _v[next];
		float m0 = _d[i] * h;
		float m1 = _d[next] * h;

		float u2 = u * u;
		float u3 = u2 * u;
		value = (2f * u3 - 3f * u2 + 1f) * v0 + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * v1 + (u3 - u2) * m1;

		if (h <= 0f)
		{
			slope = 0f;
			curvature = 0f;
			return;
		}
		slope = ((6f * u2 - 6f * u) * v0 + (3f * u2 - 4f * u + 1f) * m0 + (-6f * u2 + 6f * u) * v1 + (3f * u2 - 2f * u) * m1) / h;
		curvature = ((12f * u - 6f) * v0 + (6f * u - 4f) * m0 + (-12f * u + 6f) * v1 + (6f * u - 2f) * m1) / (h * h);
	}

	// Mayor |derivada| de la vuelta, muestreando cada tramo (para el aviso de pendiente máxima)
	public float MaxAbsSlope(int samplesPerSegment = 32)
	{
		int k = _s.Length;
		if (k < 2 || IsZero)
			return 0f;

		float max = 0f;
		for (int i = 0; i < k; i++)
		{
			float s0 = _s[i];
			float s1 = i + 1 < k ? _s[i + 1] : _s[0] + _lap;
			for (int n = 0; n <= samplesPerSegment; n++)
				max = MathF.Max(max, MathF.Abs(SlopeAt(s0 + (s1 - s0) * n / samplesPerSegment)));
		}
		return max;
	}

	private void ComputeDerivatives()
	{
		int k = _s.Length;
		for (int i = 0; i < k; i++)
		{
			int prev = (i - 1 + k) % k;
			int next = (i + 1) % k;
			float hPrev = _s[i] - _s[prev] + (i == 0 ? _lap : 0f);
			float hNext = _s[next] - _s[i] + (next == 0 ? _lap : 0f);
			if (hPrev <= 0f || hNext <= 0f)
			{
				_d[i] = 0f;
				continue;
			}

			float mPrev = (_v[i] - _v[prev]) / hPrev;
			float mNext = (_v[next] - _v[i]) / hNext;
			if (mPrev * mNext <= 0f)
			{
				// Máximo, mínimo o tramo plano: derivada 0 (así no se pasa de la clave)
				_d[i] = 0f;
				continue;
			}

			float d = (hNext * mPrev + hPrev * mNext) / (hPrev + hNext);
			float limit = 3f * MathF.Min(MathF.Abs(mPrev), MathF.Abs(mNext));
			_d[i] = Math.Clamp(d, -limit, limit);
		}
	}

	private static float PosMod(float x, float m)
	{
		if (m <= 0f)
			return x;
		float r = x % m;
		return r < 0f ? r + m : r;
	}

	private static string Format(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
