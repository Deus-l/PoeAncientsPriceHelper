using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;

namespace PoeAncientsPriceHelper;

internal sealed record OcrRow(string NormalizedName, string RawText, int CenterY, int Multiplier = 1);

internal sealed class OcrScanner : IDisposable
{
    // Two independent engines so per-line OCR can be split across two threads — Tesseract engines
    // are single-threaded internally, but separate instances on separate threads are fine.
    private readonly TesseractEngine _engineA;
    private readonly TesseractEngine _engineB;
    private readonly Action<string>? _log;
    private readonly bool _debug;
    private readonly object _logLock = new();
    private const float MinConfidence = 10f;
    // Small in-game text needs a lot of pixels for the LSTM recogniser. 3× (with the per-line
    // segmentation below) reads the ~20px-tall list rows reliably; 2× left names half-garbled.
    private const int UpscaleFactor = 3;
    private const int MinNameLength = 4;
    // A real row must contain a word at least this long. 4 (not 5) so two-short-word names
    // like "Void Flux" survive; OCR fragments are still mostly 1–3 char tokens.
    private const int MinWordLength = 4;

    // debug gates the diagnostic debug_ocr.png dump (see ScanWithCrop). The flag is injected rather
    // than read from App.DebugMode so this engine-level type stays free of UI/app statics.
    // ocrLanguage selects the Tesseract traineddata file ("eng" or "rus") — must exist in
    // tessdataDir, see AppConfig.OcrLanguage / PoeAncientsPriceHelper.csproj.
    public OcrScanner(string tessdataDir, string ocrLanguage, Action<string>? log = null, bool debug = false)
    {
        _engineA = CreateEngine(tessdataDir, ocrLanguage);
        _engineB = CreateEngine(tessdataDir, ocrLanguage);
        _log = log;
        _debug = debug;
    }

    private static TesseractEngine CreateEngine(string tessdataDir, string ocrLanguage)
    {
        var engine = new TesseractEngine(tessdataDir, ocrLanguage, EngineMode.Default);
        // Keep multi-word names together and stop Tesseract from re-estimating the DPI of our
        // already-upscaled crops (which otherwise floods stderr and skews internal scaling).
        engine.SetVariable("preserve_interword_spaces", "1");
        engine.SetVariable("user_defined_dpi", "300");
        return engine;
    }

    // Each row starts with ~3 cost-rune glyphs on the left, then "Nx ItemName". Cropping the
    // left glyph column removes the glyphs (which produce leading OCR garbage) while keeping the
    // quantity marker and the name. The old pure percent crop made OCR very sensitive to how wide
    // the user calibrated the region: selecting extra empty panel width also increased the cut and
    // could chop into the item name. Keep the preferred fraction, but clamp it to a sane pixel band.
    // (internal so the overlay can draw a box matching exactly what is OCR'd.)
    internal const double PreferredIconColumnFraction = 0.30;
    internal const int MinIconColumnPixels = 110;
    internal const int MaxIconColumnPixels = 190;
    internal const double RightTrimFraction = 0.02;

    public IReadOnlyList<OcrRow> Scan(Bitmap regionBitmap)
    {
        int primaryLeftCut = CalculateLeftCut(regionBitmap.Width);
        int rightCut = CalculateRightCut(regionBitmap.Width);
        var rows = ScanWithCrop(regionBitmap, primaryLeftCut, rightCut);

        // If the main crop reads almost nothing, the calibrated box probably includes more/less of
        // the icon column than usual. Try nearby left cuts and keep the fullest read. This fallback
        // only runs on bad frames, so the normal fast path stays at a single segmentation pass.
        if (rows.Count <= 2)
        {
            foreach (int leftCut in FallbackLeftCuts(regionBitmap.Width, primaryLeftCut))
            {
                var candidate = ScanWithCrop(regionBitmap, leftCut, rightCut);
                if (Score(candidate) > Score(rows))
                    rows = candidate;
                if (rows.Count >= 5) break;
            }
        }

        return rows;
    }

    internal static int CalculateLeftCut(int width)
    {
        if (width <= 1) return 0;
        int proportional = (int)(width * PreferredIconColumnFraction);
        int maxThatLeavesText = Math.Max(1, width - 140);
        int max = Math.Min(MaxIconColumnPixels, maxThatLeavesText);
        int min = Math.Min(MinIconColumnPixels, max);
        return Math.Clamp(proportional, min, max);
    }

    internal static int CalculateRightCut(int width) =>
        Math.Clamp((int)(width * RightTrimFraction), 0, Math.Max(0, width - 1));

    private static IEnumerable<int> FallbackLeftCuts(int width, int primary)
    {
        int[] candidates =
        [
            primary - 60,
            primary - 35,
            primary + 35,
            primary + 60,
            (int)(width * 0.22),
            (int)(width * 0.38),
        ];

        var seen = new HashSet<int> { primary };
        foreach (int raw in candidates)
        {
            int cut = Math.Clamp(raw, 1, Math.Max(1, width - 140));
            if (seen.Add(cut))
                yield return cut;
        }
    }

    private static int Score(IReadOnlyList<OcrRow> rows) =>
        rows.Count * 1000 + rows.Sum(r => r.NormalizedName.Count(char.IsLetter));

    private IReadOnlyList<OcrRow> ScanWithCrop(Bitmap regionBitmap, int leftCut, int rightCut)
    {
        int cropW = Math.Max(1, regionBitmap.Width - leftCut - rightCut);
        using var cropped = CropBitmap(regionBitmap, leftCut, 0, cropW, regionBitmap.Height);
        // Grayscale + invert so the panel's light-on-dark text becomes the dark-on-light Tesseract
        // expects. No hard binarisation: Tesseract's own (Otsu + LSTM) preprocessing reads smooth
        // grayscale far better than a hand-rolled threshold, especially on small text — a manual
        // threshold throws away anti-aliasing and adds noise the recogniser then trips over.
        using var gray = ToInvertedGray(cropped);
        using var upscaled = Upscale(gray, UpscaleFactor);

        // Segment the panel into per-row bands by horizontal projection, then OCR each band on its
        // own as a single line (PSM SingleLine). This is the key robustness win over feeding the whole
        // panel to one segmentation mode: the beveled row dividers and any parchment margin no longer
        // break global layout analysis, every name is read in isolation, and the band centre gives an
        // accurate row position regardless of how tall/wide the user calibrated the region.
        var bands = DetectLineBands(upscaled);
        IReadOnlyList<OcrRow> rows = bands.Count >= 2
            ? OcrLines(upscaled, bands, regionBitmap.Height)
            : OcrWhole(upscaled, regionBitmap.Height);

        // When OCR catches few rows, dump the exact image fed to Tesseract for inspection. Debug-only:
        // for end users this would be needless disk churn (~every 100ms while a panel mis-detects).
        if (_debug && rows.Count <= 2)
        {
            try { upscaled.Save(Path.Combine(AppContext.BaseDirectory, "debug_ocr.png"), System.Drawing.Imaging.ImageFormat.Png); }
            catch { /* best-effort diagnostic */ }
        }
        return rows;
    }

    // Vertical text-line bands in the upscaled (inverted-gray) image, as [top,bottom) pixel ranges.
    // Rows of the list have many dark (text) pixels; the gaps between them have almost none, so a
    // horizontal projection of dark-pixel counts separates the lines cleanly. Thresholds are relative
    // to the brightest row, so this adapts to font weight and calibration size automatically.
    private static List<(int Top, int Bottom)> DetectLineBands(Bitmap upInvertedGray)
    {
        int w = upInvertedGray.Width, h = upInvertedGray.Height;
        var darkPerRow = new int[h];
        var data = upInvertedGray.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            var buf = new byte[stride * h];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            const byte DarkBelow = 128;   // inverted-gray: text pixels are dark (low value)
            // Project only the left portion. The names sit on the left/middle; a price-ratio column or
            // a parchment margin on the far right would otherwise add ink to EVERY row (text rows and
            // the gaps between them alike), erasing the gaps and merging the whole list into one band.
            int projW = Math.Max(1, (int)(w * 0.85));
            for (int y = 0; y < h; y++)
            {
                int row = y * stride, count = 0;
                for (int x = 0; x < projW; x++)
                    if (buf[row + x * 3] < DarkBelow) count++;   // gray → R==G==B, sample one channel
                darkPerRow[y] = count;
            }
        }
        finally { upInvertedGray.UnlockBits(data); }

        int maxDark = 0;
        for (int y = 0; y < h; y++) if (darkPerRow[y] > maxDark) maxDark = darkPerRow[y];
        if (maxDark == 0) return [];

        // A row counts as "ink" above this; below it is an inter-line gap.
        int inkThreshold = Math.Max(3, (int)(maxDark * 0.07));
        int minBandHeight = Math.Max(UpscaleFactor * 5, h / 60);   // drop divider lines / specks
        int mergeGap = UpscaleFactor * 2;                          // join a band split by a thin gap

        var bands = new List<(int Top, int Bottom)>();
        int start = -1;
        for (int y = 0; y < h; y++)
        {
            bool ink = darkPerRow[y] >= inkThreshold;
            if (ink && start < 0) start = y;
            else if (!ink && start >= 0) { bands.Add((start, y)); start = -1; }
        }
        if (start >= 0) bands.Add((start, h));

        // Merge bands separated only by a hairline gap (a descender break, an anti-aliased divider).
        var merged = new List<(int Top, int Bottom)>();
        foreach (var b in bands)
        {
            if (merged.Count > 0 && b.Top - merged[^1].Bottom <= mergeGap)
                merged[^1] = (merged[^1].Top, b.Bottom);
            else
                merged.Add(b);
        }

        return merged.Where(b => b.Bottom - b.Top >= minBandHeight).ToList();
    }

    // OCR every detected band as a single line, split across the two engines so the ~10 lines per
    // panel are recognised on two threads. Band crops are produced sequentially (GDI on one Bitmap
    // isn't safe to call from two threads at once), then the PNGs are recognised in parallel.
    private IReadOnlyList<OcrRow> OcrLines(Bitmap upscaled, List<(int Top, int Bottom)> bands, int regionHeight)
    {
        int pad = UpscaleFactor;
        var jobs = new List<(byte[] Png, int CenterY)>(bands.Count);
        foreach (var (top, bottom) in bands)
        {
            int y0 = Math.Max(0, top - pad);
            int y1 = Math.Min(upscaled.Height, bottom + pad);
            using var band = CropBitmap(upscaled, 0, y0, upscaled.Width, y1 - y0);
            int centerY = Math.Clamp((top + bottom) / 2 / UpscaleFactor, 0, regionHeight - 1);
            jobs.Add((ToPng(band), centerY));
        }

        var diag = new List<string>();
        var tA = Task.Run(() => OcrJobSubset(_engineA, jobs, 0, diag));
        var tB = Task.Run(() => OcrJobSubset(_engineB, jobs, 1, diag));
        Task.WaitAll(tA, tB);

        var rows = new List<OcrRow>(tA.Result.Count + tB.Result.Count);
        rows.AddRange(tA.Result);
        rows.AddRange(tB.Result);
        rows.Sort((x, y) => x.CenterY.CompareTo(y.CenterY));

        if (rows.Count <= 2 && diag.Count > 0)
            lock (_logLock) { _log?.Invoke($"OCR raw {diag.Count} lines → " + string.Join(" | ", diag)); }

        return rows;
    }

    // Process the bands whose index has the given parity (engine A → even, B → odd).
    private List<OcrRow> OcrJobSubset(TesseractEngine engine, List<(byte[] Png, int CenterY)> jobs, int parity, List<string> diag)
    {
        var result = new List<OcrRow>();
        for (int i = parity; i < jobs.Count; i += 2)
        {
            var (png, centerY) = jobs[i];
            using var pix = Pix.LoadFromMemory(png);
            using var page = engine.Process(pix, PageSegMode.SingleLine);
            string text = page.GetText();
            float conf = page.GetMeanConfidence() * 100f;
            var (row, reject) = BuildRow(text, conf, centerY);
            if (row is not null) result.Add(row);
            lock (_logLock)
                diag.Add($"y={centerY} conf={conf:0} '{(text ?? "").Trim()}'{(reject is null ? "" : $" REJ:{reject}")}");
        }
        return result;
    }

    // Whole-panel fallback for when band detection fails (e.g. a mid-animation frame with no clear
    // gaps). SingleColumn lets Tesseract segment the lines itself; cruder than per-line but enough to
    // keep something on screen until a clean frame arrives.
    private IReadOnlyList<OcrRow> OcrWhole(Bitmap upscaled, int regionHeight)
    {
        byte[] png = ToPng(upscaled);
        using var pix = Pix.LoadFromMemory(png);
        using var page = _engineA.Process(pix, PageSegMode.SingleColumn);
        return ExtractRows(page, regionHeight, UpscaleFactor);
    }

    private static Bitmap CropBitmap(Bitmap src, int x, int y, int w, int h)
    {
        var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(dst);
        g.DrawImage(src, new Rectangle(0, 0, w, h), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
        return dst;
    }

    // Used only by the whole-panel fallback: walk Tesseract's text lines and apply the same row
    // filters as the per-line path.
    private IReadOnlyList<OcrRow> ExtractRows(Page page, int bitmapHeight, int scale = 1)
    {
        var rows = new List<OcrRow>();
        var diag = new List<string>();
        using var iter = page.GetIterator();
        iter.Begin();
        do
        {
            if (!iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var box)) continue;
            var text = iter.GetText(PageIteratorLevel.TextLine);
            float conf = iter.GetConfidence(PageIteratorLevel.TextLine);
            // Bounding box coords are in upscaled space — divide back to original coords
            int centerY = Math.Clamp((box.Y1 + (box.Y2 - box.Y1) / 2) / scale, 0, bitmapHeight - 1);

            var (row, reject) = BuildRow(text, conf, centerY);
            if (row is not null) rows.Add(row);
            diag.Add($"y={centerY} conf={conf:0} '{(text ?? "").Trim()}'{(reject is null ? "" : $" REJ:{reject}")}");
        }
        while (iter.Next(PageIteratorLevel.TextLine));

        if (rows.Count <= 2 && diag.Count > 0)
            lock (_logLock) { _log?.Invoke($"OCR raw {diag.Count} lines → " + string.Join(" | ", diag)); }

        return rows;
    }

    // Shared row gate: normalize, pull out the quantity marker, strip leading icon/quantity noise,
    // and reject empty / low-confidence / too-short / no-real-word reads. Returns the built row (or
    // null) plus a reject reason for diagnostics.
    private static (OcrRow? Row, string? Reject) BuildRow(string? text, float conf, int centerY)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, "empty");
        if (conf < MinConfidence) return (null, "lowconf");

        var normalizedRaw = NormalizeName(text);
        int multiplier = ExtractMultiplier(normalizedRaw);
        string normalized = StripTrailingNoise(StripLeadingNoise(normalizedRaw));
        if (normalized.Length < MinNameLength) return (null, "short");
        if (!HasLongWord(normalized, MinWordLength)) return (null, "noword");

        return (new OcrRow(normalized, text.Trim(), centerY, multiplier), null);
    }

    private static Bitmap Upscale(Bitmap src, int factor)
    {
        var dst = new Bitmap(src.Width * factor, src.Height * factor, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, 0, 0, dst.Width, dst.Height);
        return dst;
    }

    // The list shows a stack quantity either as "Nx" before the item name ("1x", "2x", "14x")
    // or as a trailing "(N)" suffix — e.g. "Runic Alloy (2)". NormalizeName strips the parens so
    // "(2)" becomes " 2" at the end of the string. Both forms must be detected so that
    // BuildPriceRows multiplies the unit price by the correct stack size.
    // [xх]: in Russian OCR mode Tesseract reads the marker using the Cyrillic unicharset, so the
    // glyph that looks like "x" comes back as Cyrillic х (U+0445), not Latin x (U+0078) — same
    // shape, different code point. Without matching both, the quantity marker is invisible in rus mode.
    internal static int ExtractMultiplier(string normalized)
    {
        // "Nx" / "Nх" leading marker (icon column, e.g. "2x rune")
        var m = Regex.Match(normalized, @"(?<![a-z0-9])(\d{1,3})\s*[xх](?![a-z0-9])");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n >= 1)
            return Math.Min(n, 999);

        // "(N)" trailing suffix: NormalizeName turns "(2)" into " 2" after the item name.
        // We look for the FIRST digit sequence after the LAST LETTER of the name — not the last
        // digit in the string — so that exchange-rate numbers or other OCR noise captured by a
        // wide calibration region (e.g. "масса 2 7" where 7 is the exchange rate) are ignored.
        // For gem names ("level N" / "уровень N") the level clause is stripped first so the
        // qty that follows it is visible as the first suffix digit.
        string s = normalized;
        var lvl = Regex.Match(s, @"\b(level|уровень)\s+\d+");
        if (lvl.Success) s = s.Remove(lvl.Index, lvl.Length).Trim();

        int lastLetter = -1;
        for (int i = s.Length - 1; i >= 0; i--)
            if (char.IsLetter(s[i])) { lastLetter = i; break; }
        if (lastLetter >= 0)
        {
            var q = Regex.Match(s.Substring(lastLetter + 1).TrimStart(), @"^(\d{1,3})");
            if (q.Success && int.TryParse(q.Groups[1].Value, out var qty) && qty >= 1)
                return Math.Min(qty, 999);
        }

        return 1;
    }

    // Strip leading noise: short/numeric tokens ("e", "l8"), then anything before the first
    // quantity marker ("1x", "11x"), then remaining leading non-alpha chars.
    // e.g. "krogin 1x ancient rune of decay"  → "ancient rune of decay"
    // e.g. "e l8 n 1x the greatwolf"          → "the greatwolf"
    internal static string StripLeadingNoise(string normalized)
    {
        var s = Regex.Replace(normalized, @"^(?:\S{1,2}\s+|\S*\d\S*\s+)+", "");
        // If a quantity marker still exists, drop everything before (and including) it
        var qm = Regex.Match(s, @"(?<!\w)\d+\s*[xх]\s+");
        if (qm.Success) s = s.Substring(qm.Index + qm.Length);
        // а-яё: keep Cyrillic letters too — without this range, every Russian name would be
        // wiped to an empty string here (none of its letters match the original [^a-z]+ class).
        s = Regex.Replace(s, @"^[^a-zа-яё]+", "");
        return s.Trim();
    }

    // Drop trailing 1–2 letter fragments the per-line OCR picks up from the row divider or the price
    // column to the right of the name ("scrap i", "splinters jp", "rune l"). No real item name ends in
    // a ≤2-letter word, so this only ever removes garbage.
    // Also strip a trailing bare number that comes from the "(N)" quantity notation after NormalizeName
    // turns "(1)" into " 1" — but guard against removing the level number on gem names ("level 19").
    // Without this, short Russian names like "руна основ" (10 chars) get "руна основ 1" (12 chars)
    // which scores 10/12 = 0.833 — just below the 0.84 fuzzy threshold — and silently misses.
    internal static string StripTrailingNoise(string normalized)
    {
        var s = Regex.Replace(normalized, @"(?:\s+[a-zа-яё]{1,2})+$", "").Trim();
        if (!Regex.IsMatch(s, @"\blevel\s+\d+$"))
            s = Regex.Replace(s, @"\s+\d+$", "").Trim();
        return s;
    }

    private static bool HasLongWord(string normalized, int minLen)
    {
        int run = 0;
        foreach (char c in normalized)
        {
            if (char.IsLetter(c)) { if (++run >= minLen) return true; }
            else run = 0;
        }
        return false;
    }

    // Grayscale (luminance) + invert, into a 24bpp gray bitmap. PoE's list panel is light text on a
    // dark background; Tesseract works best with dark text on light, so we invert here.
    private static Bitmap ToInvertedGray(Bitmap src)
    {
        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
        var srcData = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var dstData = dst.LockBits(new Rectangle(0, 0, dst.Width, dst.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int h = src.Height, ss = srcData.Stride, ds = dstData.Stride, w = src.Width;
            var sbuf = new byte[ss * h];
            var dbuf = new byte[ds * h];
            System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, sbuf, 0, sbuf.Length);
            for (int y = 0; y < h; y++)
            {
                int sr = y * ss, dr = y * ds;
                for (int x = 0; x < w; x++)
                {
                    int si = sr + x * 3;
                    // 24bpp is stored BGR. Luminance, then invert.
                    int lum = (sbuf[si + 2] * 299 + sbuf[si + 1] * 587 + sbuf[si] * 114) / 1000;
                    byte inv = (byte)(255 - lum);
                    int di = dr + x * 3;
                    dbuf[di] = dbuf[di + 1] = dbuf[di + 2] = inv;
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(dbuf, 0, dstData.Scan0, dbuf.Length);
        }
        finally { src.UnlockBits(srcData); dst.UnlockBits(dstData); }
        return dst;
    }

    private static byte[] ToPng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    internal static string NormalizeName(string text)
    {
        var s = text.ToLowerInvariant();
        s = Regex.Replace(s, @"[^\w\s]", " ");
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }

    public void Dispose() { _engineA.Dispose(); _engineB.Dispose(); }
}
