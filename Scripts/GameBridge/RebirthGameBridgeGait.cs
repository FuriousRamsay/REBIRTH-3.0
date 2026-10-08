using System.Collections.Generic;

#nullable disable

/// <summary>
/// Anticipating where a zombie's head (or chest) will be when a swing lands.
///
/// A zombie's head is not where the skeleton says it is: it moves with the body (a few m/s for runners) AND bobs and sways
/// relative to it in the zombie's gait. Each walk type (EntityAlive.walkType: normal, fat, cripple, crouch, bandit, crawler, spider ...)
/// has its own repeating pattern. A player learns to see the rhythm and swings where the head WILL be.
///
/// The tracker records, for the current target, the head and pelvis position relative to the zombie's root (feet) for the last
/// ~2.4 s, finds the period of the repeating pattern by autocorrelation, and predicts the offset one lead-time ahead by repeating
/// the pattern forward. The body itself is carried forward with its recent velocity. The period found for a walk type is
/// remembered, so the next zombie of that type is anticipated with less warm-up.
/// </summary>
public sealed class GaitTracker
{
    private const float Window = 2.4f;     // seconds of history kept
    private const float Grid = 0.02f;      // uniform resampling step (50 Hz)
    private const float MinPeriod = 0.35f, MaxPeriod = 1.4f;

    private struct Sample { public float T; public Vector3 Root, HeadOff, PelvisOff; }

    private readonly List<Sample> samples = new List<Sample>(256);
    private float period;                  // 0 = unknown
    private float periodStrength;
    private float nextAnalysis;
    private readonly int walkType;

    // Learned per walk type, shared by all trackers of the session.
    private static readonly Dictionary<int, float> LearnedPeriod = new Dictionary<int, float>();

    public GaitTracker(int walkType) { this.walkType = walkType; }

    public float Period { get { return period; } }
    public float Strength { get { return periodStrength; } }

    public void Add(float t, Vector3 root, Vector3 head, Vector3 pelvis)
    {
        if (samples.Count > 0 && t - samples[samples.Count - 1].T < 0.004f) return;
        samples.Add(new Sample { T = t, Root = root, HeadOff = head - root, PelvisOff = pelvis - root });
        while (samples.Count > 0 && t - samples[0].T > Window) samples.RemoveAt(0);
        if (t >= nextAnalysis && samples.Count > 40) { nextAnalysis = t + 0.25f; Analyse(t); }
    }

    /// <summary>Predicted head and pelvis positions `lead` seconds from `now`.</summary>
    public void Predict(float now, float lead, out Vector3 head, out Vector3 pelvis)
    {
        Sample cur = samples[samples.Count - 1];
        // Body: carry the root forward with its recent velocity (last ~0.25 s).
        Vector3 vel = Vector3.zero;
        for (int i = samples.Count - 2; i >= 0; i--)
            if (cur.T - samples[i].T >= 0.2f) { float dt = cur.T - samples[i].T; vel = (cur.Root - samples[i].Root) / dt; break; }
        vel.y = 0f;
        if (vel.magnitude > 9f) vel = vel.normalized * 9f;
        Vector3 root = cur.Root + vel * lead;

        // Pattern: repeat the learned period forward to the time of the swing.
        Vector3 headOff = cur.HeadOff, pelvisOff = cur.PelvisOff;
        float T = period > 0f ? period : (LearnedPeriod.ContainsKey(walkType) ? LearnedPeriod[walkType] : 0f);
        if (T > 0f && (periodStrength >= 0.45f || period <= 0f))
        {
            float tt = now + lead;
            while (tt > now - 0.02f) tt -= T;
            if (tt >= samples[0].T) { Vector3 h, p; if (OffsetsAt(tt, out h, out p)) { headOff = h; pelvisOff = p; } }
        }
        head = root + headOff;
        pelvis = root + pelvisOff;
    }

    private bool OffsetsAt(float t, out Vector3 head, out Vector3 pelvis)
    {
        head = Vector3.zero; pelvis = Vector3.zero;
        for (int i = 1; i < samples.Count; i++)
        {
            if (samples[i].T < t) continue;
            Sample a = samples[i - 1], b = samples[i];
            float k = b.T > a.T ? Mathf.Clamp01((t - a.T) / (b.T - a.T)) : 0f;
            head = Vector3.Lerp(a.HeadOff, b.HeadOff, k);
            pelvis = Vector3.Lerp(a.PelvisOff, b.PelvisOff, k);
            return true;
        }
        return false;
    }

    /// <summary>Find the period of the head-offset pattern: normalised autocorrelation of the resampled series.</summary>
    private void Analyse(float now)
    {
        float t0 = samples[0].T;
        int n = Mathf.FloorToInt((now - t0) / Grid);
        if (n < Mathf.CeilToInt((MinPeriod * 2f) / Grid)) return;
        var series = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 h, p;
            if (!OffsetsAt(t0 + i * Grid, out h, out p)) return;
            series[i] = h;
        }
        Vector3 mean = Vector3.zero;
        for (int i = 0; i < n; i++) mean += series[i];
        mean /= n;
        float energy = 0f;
        for (int i = 0; i < n; i++) { series[i] -= mean; energy += series[i].sqrMagnitude; }
        if (energy < 1e-5f) { period = 0f; periodStrength = 0f; return; }   // not bobbing at all

        int minLag = Mathf.CeilToInt(MinPeriod / Grid), maxLag = Mathf.Min(Mathf.FloorToInt(MaxPeriod / Grid), n - Mathf.CeilToInt(0.6f / Grid));
        float best = 0f; int bestLag = 0;
        var corr = new float[maxLag + 1];
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            float c = 0f, e1 = 0f, e2 = 0f;
            for (int i = 0; i + lag < n; i++) { c += Vector3.Dot(series[i], series[i + lag]); e1 += series[i].sqrMagnitude; e2 += series[i + lag].sqrMagnitude; }
            float norm = Mathf.Sqrt(e1 * e2);
            corr[lag] = norm > 1e-9f ? c / norm : 0f;
            if (corr[lag] > best) { best = corr[lag]; bestLag = lag; }
        }
        if (bestLag == 0) { period = 0f; periodStrength = 0f; return; }
        // The first strong peak, not a later multiple of it.
        for (int lag = minLag + 1; lag < maxLag; lag++)
            if (corr[lag] >= 0.9f * best && corr[lag] >= corr[lag - 1] && corr[lag] >= corr[lag + 1]) { bestLag = lag; break; }
        period = bestLag * Grid;
        periodStrength = corr[bestLag];
        if (periodStrength >= 0.6f) LearnedPeriod[walkType] = period;
    }

    public string Describe()
    {
        return "walkType " + walkType + ", period " + period.ToString("0.00") + "s, strength " + periodStrength.ToString("0.00")
            + (LearnedPeriod.ContainsKey(walkType) ? ", learned " + LearnedPeriod[walkType].ToString("0.00") + "s" : "");
    }
}
