using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;



public class AsciiLayer : MonoBehaviour
{
    public enum State { Hidden, Appearing, Interactive, Returning, Disappearing }

    // ───────────────────────── DATOS ─────────────────────────
    [Header("Datos (arrastrar en el Inspector)")]
    [Tooltip("El .txt con el ASCII (UTF-8). No recortar filas en blanco: representan el fondo.")]
    public TextAsset asciiText;
    [Tooltip("Imagen de la que se saca el color de cada letra. Import: Read/Write ON, Compression None.")]
    public Texture2D colorSource;
    [Tooltip("ON si colorSource es tu PNG de ASCII con fondo blanco (toma el pixel más oscuro de cada celda). OFF si es la pintura original, de fondo oscuro.")]
    public bool sourceHasWhiteBackground = true;
    [Tooltip("Aclara los colores de las letras (los oscuros se ven poco sobre el fondo negro). 0 = sin cambios.")]
    [Range(0f, 0.6f)] public float colorLift = 0.25f;
    [Tooltip("Cámara AR. Si queda vacío usa Camera.main (la AR Camera debe tener el tag MainCamera).")]
    public Camera arCamera;

    [Header("Archivos en Assets/Resources (sin extensión)")]
    public string glyphsAtlasName = "glyphs";       // glyphs.png (sliceado en sprites)
    public string glyphCharsName = "glyph_chars";   // glyph_chars.txt (los caracteres, en el mismo orden)

    [Header("Tamaño físico del target (los mismos valores que en el Image Target de Vuforia, en metros)")]
    public float targetWidth = 0.30f;
    public float targetHeight = 0.54f;

    [Header("Tamaño de los caracteres")]
    [Tooltip("Escala del ASCII respecto al target. 1 = igual que la pintura.")]
    public float asciiScale = 1.35f;
    [Tooltip("Agrupa NxN caracteres del .txt en uno. 1 = original. 2 = letras el doble de grandes y 4 veces menos objetos.")]
    [Range(1, 6)] public int gridStep = 2;
    [Range(0.5f, 2f)] public float glyphFill = 1f;

    [Header("Origen del destello (0-1, desde abajo a la izquierda). Es la posición de la torre.")]
    public Vector2 originUV = new Vector2(0.41f, 0.60f);

    // ───────────────────────── APARICIÓN ─────────────────────────
    [Header("Aparición con destellos")]
    [Tooltip("Ventana de tiempo en la que van apareciendo todas las letras.")]
    public float appearDuration = 2f;
    [Tooltip("0 = se expande ordenado desde la torre. 1 = totalmente al azar.")]
    [Range(0f, 1f)] public float originBias = 0.6f;
    [Tooltip("Cuánto dura el parpadeo de cada letra antes de asentarse.")]
    public float flashDuration = 0.15f;
    public float flickerSpeed = 5f;

    // ───────────────────────── DISPERSIÓN ─────────────────────────
    [Header("Dispersión con el dedo")]
    [Tooltip("Radio de influencia del dedo, como fracción del ancho del target.")]
    [Range(0.02f, 0.5f)] public float touchRadius = 0.12f;
    [Tooltip("Fuerza del empujón (anchos de target por segundo). Ajustar a ojo.")]
    public float pushSpeed = 0.35f;
    [Tooltip("Cuánto frenan las letras dispersadas.")]
    public float drag = 2.5f;
    public float spinSpeed = 240f;
    [Tooltip("Qué fracción de letras dispersadas dispara la vuelta.")]
    [Range(0.1f, 1f)] public float scatteredFractionToReturn = 0.7f;
    [Tooltip("Segundos sin tocar para que vuelva solo (así nadie queda trabado).")]
    public float idleSecondsToReturn = 6f;

    // ───────────────────────── VUELTA ─────────────────────────
    [Header("Vuelta al idle")]
    public float returnTime = 1.2f;       // tiempo en que las letras se reacomodan
    public float returnSpring = 6f;       // qué tan rápido vuelven
    public float holdTime = 0.6f;         // pausa con el ASCII armado antes de apagarse
    public float disappearDuration = 1.5f;

    // ───────────────────────── FONDO OSCURO ─────────────────────────
    [Header("Fondo oscuro sobre la pintura (para que las letras resalten)")]
    public bool useDarkOverlay = true;
    [Range(0f, 1f)] public float overlayMaxAlpha = 0.85f;

    // ───────────────────────── EVENTOS ─────────────────────────
    [Header("Eventos (conectar con la animación idle de tu compañera)")]
    [Tooltip("CONECTAR 2: se dispara al empezar el ASCII. Apagar niebla y titileo.")]
    public UnityEvent onAsciiStarted;
    [Tooltip("CONECTAR 3: se dispara cuando las letras empiezan a apagarse. Volver a encender niebla y torre.")]
    public UnityEvent onIdleReturn;

    // ───────────────────────── INTERNO ─────────────────────────
    class Cell
    {
        public int id;
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 home;
        public Vector2 vel;
        public float spin;
        public Color baseColor;
        public float dist01;
        public float appearTime;
        public float disappearTime;
        public bool scattered;
    }

    const float GlyphZ = -0.005f;   // las letras apenas por encima de la pintura (hacia el usuario)

    readonly List<Cell> cells = new List<Cell>();
    SpriteRenderer overlay;
    State state = State.Hidden;
    float stateTime;
    float idleTimer;
    int scatteredCount;
    int cols, rows;

    public State CurrentState => state;
    public bool IsBusy => state != State.Hidden;

    // ───────────────────────── API PÚBLICA ─────────────────────────

    /// <summary>CONECTAR 1: llamar desde el tap en la torre.</summary>
    public void ShowAscii()
    {
        if (state == State.Hidden) SetState(State.Appearing);
    }

    /// <summary>CONECTAR 4: llamar desde On Target Lost del Image Target.</summary>
    public void ResetToHidden()
    {
        if (state == State.Appearing || state == State.Interactive || state == State.Returning)
            onIdleReturn?.Invoke();
        SetState(State.Hidden);
    }

    [ContextMenu("TEST: mostrar ASCII")]
    void TestShow() { ShowAscii(); }

    // ───────────────────────── INICIO ─────────────────────────
    void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
        BuildOverlay();
        BuildCells();
        SetState(State.Hidden);
    }

    void BuildOverlay()
    {
        if (!useDarkOverlay) return;
        var go = new GameObject("DarkOverlay");

        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.0025f);
        go.transform.localScale = new Vector3(targetWidth * asciiScale * 1.02f, targetHeight * asciiScale * 1.02f, 1f);

        Texture2D w = Texture2D.whiteTexture;
        overlay = go.AddComponent<SpriteRenderer>();
        overlay.sprite = Sprite.Create(w, new Rect(0, 0, w.width, w.height), new Vector2(0.5f, 0.5f), w.width);
        overlay.sortingOrder = 0;
        SetOverlayAlpha(0f);
    }

    void BuildCells()
    {
        if (asciiText == null)
        {
            Debug.LogError("AsciiLayer: falta asignar asciiText.");
            return;
        }

        var sprites = Resources.LoadAll<Sprite>(glyphsAtlasName).OrderBy(SpriteIndex).ToArray();
        var charsAsset = Resources.Load<TextAsset>(glyphCharsName);
        if (sprites.Length == 0 || charsAsset == null)
        {
            Debug.LogError("AsciiLayer: no encuentro '" + glyphsAtlasName + "' o '" + glyphCharsName + "' en Assets/Resources.");
            return;
        }
        string chars = charsAsset.text.Replace("\r", "").Replace("\n", "");
        var map = new Dictionary<char, Sprite>();
        for (int i = 0; i < chars.Length && i < sprites.Length; i++) map[chars[i]] = sprites[i];

        var lines = asciiText.text.Replace("\r", "").Split('\n').ToList();
        if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);

        // Grilla efectiva: se agrupan bloques de gridStep x gridStep
        rows = Mathf.CeilToInt(lines.Count / (float)gridStep);
        cols = Mathf.CeilToInt(lines.Max(l => l.Length) / (float)gridStep);

        float cellW = (targetWidth * asciiScale) / cols;
        float cellH = (targetHeight * asciiScale) / rows;

        Vector2 originLocal = new Vector2((originUV.x - 0.5f) * targetWidth, (originUV.y - 0.5f) * targetHeight);
        float maxDist = 0.0001f;

        for (int r = 0; r < rows; r++)
        {
            for (int col = 0; col < cols; col++)
            {
                char ch = DominantChar(lines, r * gridStep, col * gridStep);
                if (ch == ' ') continue;
                if (!map.TryGetValue(ch, out Sprite sp)) continue;

                float u = (col + 0.5f) / cols;
                float v = 1f - (r + 0.5f) / rows;

                var go = new GameObject("c");
                go.transform.SetParent(transform, false);

                Vector2 b = sp.bounds.size;
                float s = Mathf.Min(cellW / b.x, cellH / b.y) * glyphFill;
                go.transform.localScale = Vector3.one * s;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingOrder = 10;

                var cell = new Cell
                {
                    id = cells.Count,
                    t = go.transform,
                    sr = sr,
                    home = new Vector2((u - 0.5f) * targetWidth * asciiScale, (v - 0.5f) * targetHeight * asciiScale),
                    baseColor = Color.Lerp(SampleColor(u, v), Color.white, colorLift)
                };
                go.transform.localPosition = Home3(cell);
                cell.dist01 = Vector2.Distance(cell.home, originLocal);
                if (cell.dist01 > maxDist) maxDist = cell.dist01;
                cells.Add(cell);
            }
        }

        foreach (var c in cells) c.dist01 /= maxDist;
    }

    // Devuelve el carácter más frecuente del bloque; si más de la mitad es fondo, lo deja vacío.
    char DominantChar(List<string> lines, int r0, int c0)
    {
        var counts = new Dictionary<char, int>();
        int total = 0, nonBlank = 0;
        for (int r = r0; r < r0 + gridStep && r < lines.Count; r++)
            for (int c = c0; c < c0 + gridStep && c < lines[r].Length; c++)
            {
                total++;
                char ch = lines[r][c];
                if (ch == ' ' || ch == '\t') continue;
                nonBlank++;
                counts.TryGetValue(ch, out int n);
                counts[ch] = n + 1;
            }
        if (nonBlank == 0 || nonBlank * 2 < total) return ' ';
        char best = ' ';
        int bestN = 0;
        foreach (var kv in counts)
            if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
        return best;
    }

    static int SpriteIndex(Sprite s)
    {
        int i = s.name.LastIndexOf('_');
        return (i >= 0 && int.TryParse(s.name.Substring(i + 1), out int n)) ? n : 0;
    }

    // Color de una letra: promedio de la zona de la imagen que le corresponde, ponderado por brillo
    // (así toma el color del trazo y no se ensucia con el negro del fondo).
    Color SampleColor(float u, float v)
    {
        if (colorSource == null) return Color.white;

        int w = colorSource.width, h = colorSource.height;
        int bw = Mathf.Max(1, w / cols), bh = Mathf.Max(1, h / rows);
        int x0 = Mathf.Clamp(Mathf.RoundToInt(u * w - bw / 2f), 0, w - bw);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(v * h - bh / 2f), 0, h - bh);
        Color[] px = colorSource.GetPixels(x0, y0, bw, bh);

        if (sourceHasWhiteBackground)
        {
            // PNG de ASCII sobre blanco: descarta el blanco y toma el pixel más oscuro de la celda
            // (el color "puro" de la letra, sin el suavizado de los bordes).
            bool found = false;
            Color best = Color.white;
            foreach (var p in px)
            {
                float s = p.r + p.g + p.b;
                if (s >= 2.88f) continue;                       // casi blanco = fondo
                if (!found || s < best.r + best.g + best.b) { best = p; found = true; }
            }
            if (!found) return new Color(0.6f, 0.6f, 0.6f, 1f);
            best.a = 1f;
            return best;
        }

        // Imagen sobre fondo oscuro (la pintura): promedio ponderado por brillo.
        Color acc = Color.black;
        float sum = 0f;
        foreach (var p in px)
        {
            float l = p.grayscale;
            acc += p * l;
            sum += l;
        }
        if (sum < 0.001f) return new Color(0.6f, 0.6f, 0.6f, 1f);
        Color res = acc / sum;
        res.a = 1f;
        return res;
    }

    Vector3 Home3(Cell c) { return new Vector3(c.home.x, c.home.y, GlyphZ); }

    // ───────────────────────── MÁQUINA DE ESTADOS ─────────────────────────
    void SetState(State s)
    {
        state = s;
        stateTime = 0f;

        Debug.Log("[AsciiLayer] Estado cambiado a: " + s, this);

        switch (s)
        {
            case State.Hidden:
                foreach (var c in cells) c.sr.enabled = false;
                SetOverlayAlpha(0f);
                break;

            case State.Appearing:
                ResetCells();
                foreach (var c in cells)
                {
                    c.sr.enabled = true;
                    c.sr.color = Color.clear;
                    c.appearTime = Mathf.Lerp(c.dist01, Random.value, originBias) * appearDuration;
                }
                onAsciiStarted?.Invoke();
                break;

            case State.Interactive:
                foreach (var c in cells) c.sr.color = c.baseColor;
                idleTimer = 0f;
                break;

            case State.Returning:
                break;

            case State.Disappearing:
                ResetCells();   // deja todas las letras exactamente en su lugar
                foreach (var c in cells)
                {
                    c.sr.color = c.baseColor;
                    // se apagan convergiendo hacia la torre (al revés de como aparecieron)
                    c.disappearTime = Mathf.Lerp(1f - c.dist01, Random.value, originBias) * disappearDuration;
                }
                onIdleReturn?.Invoke();
                break;
        }
    }

    void ResetCells()
    {
        foreach (var c in cells)
        {
            c.t.localPosition = Home3(c);
            c.t.localRotation = Quaternion.identity;
            c.vel = Vector2.zero;
            c.scattered = false;
        }
        scatteredCount = 0;
    }

    void Update()
    {
        if (state == State.Hidden) return;
        stateTime += Time.deltaTime;

        switch (state)
        {
            case State.Appearing: UpdateAppearing(); break;
            case State.Interactive: UpdateInteractive(); break;
            case State.Returning: UpdateReturning(); break;
            case State.Disappearing: UpdateDisappearing(); break;
        }
    }

    // ───────────────────────── ESTADOS ─────────────────────────
    void UpdateAppearing()
    {
        SetOverlayAlpha(Mathf.Clamp01(stateTime / (appearDuration * 0.5f)) * overlayMaxAlpha);

        foreach (var c in cells)
        {
            float a = Mathf.Clamp01((stateTime - c.appearTime) / Mathf.Max(0.05f, flashDuration));
            Color col = c.baseColor;
            col.a = a;
            c.sr.color = col;
        }

        if (stateTime >= appearDuration + flashDuration)
            SetState(State.Interactive);
    }

    void UpdateInteractive()
    {
        float dt = Time.deltaTime;
        idleTimer += dt;
        HandleTouch();

        float damp = Mathf.Exp(-drag * dt);
        foreach (var c in cells)
        {
            if (!c.scattered) continue;
            c.t.localPosition += (Vector3)(c.vel * dt);
            c.t.Rotate(0f, 0f, c.spin * dt);
            c.vel *= damp;
        }

        bool enoughScattered = scatteredCount >= cells.Count * scatteredFractionToReturn;
        if (enoughScattered || idleTimer >= idleSecondsToReturn) SetState(State.Returning);
    }

    void UpdateReturning()
    {
        float k = 1f - Mathf.Exp(-returnSpring * Time.deltaTime);
        foreach (var c in cells)
        {
            if (!c.scattered) continue;
            c.t.localPosition = Vector3.Lerp(c.t.localPosition, Home3(c), k);
            c.t.localRotation = Quaternion.Slerp(c.t.localRotation, Quaternion.identity, k);
        }

        if (stateTime >= returnTime + holdTime) SetState(State.Disappearing);
    }

    void UpdateDisappearing()
    {
        foreach (var c in cells)
        {
            float t = stateTime - c.disappearTime;
            if (t < 0f)
            {
                c.sr.color = c.baseColor;
                continue;
            }

            float fade = Mathf.Clamp01(t / flashDuration);
            c.sr.color = Color.Lerp(c.baseColor, Color.clear, fade);
        }

        SetOverlayAlpha((1f - Mathf.Clamp01(stateTime / disappearDuration)) * overlayMaxAlpha);

        if (stateTime >= disappearDuration + flashDuration) 
            SetState(State.Hidden);
    }

    // ───────────────────────── DEDO ─────────────────────────
    void HandleTouch()
    {
        Vector2 screenPos = Vector2.zero;
        bool pressed = false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            pressed = true;
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            pressed = true;
            screenPos = Mouse.current.position.ReadValue();
        }
        if (!pressed) return;
        idleTimer = 0f;

        Ray ray = arCamera.ScreenPointToRay(screenPos);
        var plane = new Plane(transform.forward, transform.position);
        if (!plane.Raycast(ray, out float enter)) return;
        Vector3 local3 = transform.InverseTransformPoint(ray.GetPoint(enter));
        Vector2 finger = new Vector2(local3.x, local3.y);

        float radius = touchRadius * targetWidth;
        float push = pushSpeed * targetWidth;

        foreach (var c in cells)
        {
            Vector2 d = (Vector2)c.t.localPosition - finger;
            float dist = d.magnitude;
            if (dist > radius) continue;

            float k = 1f - dist / radius;
            Vector2 dir = dist > 1e-5f ? d / dist : Random.insideUnitCircle.normalized;
            c.vel += (dir + Random.insideUnitCircle * 0.5f) * push * k;

            if (!c.scattered)
            {
                c.scattered = true;
                c.spin = Random.Range(-spinSpeed, spinSpeed);
                scatteredCount++;
            }
        }
    }

    // ───────────────────────── AUXILIARES ─────────────────────────
    bool Flicker(int id, float t)
    {
        return Mathf.PerlinNoise(id * 0.713f + 0.31f, t * flickerSpeed) > 0.45f;
    }

    void SetOverlayAlpha(float a)
    {
        if (overlay == null) return;
        overlay.enabled = a > 0.001f;
        overlay.color = new Color(0f, 0f, 0f, a);
    }
}
