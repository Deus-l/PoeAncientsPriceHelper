using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PoeAncientsPriceHelper;

// Shows a scaled-down preview of the calibrated region so the user can verify they captured the
// exchange-stone panel (not a browser window, desktop, etc.) before the calibration is saved.
// Returns true to confirm, false to redo.
internal sealed class CalibrationPreview : Form
{
    public bool Confirmed { get; private set; }

    private readonly Bitmap _preview;
    private const int MaxPreviewWidth  = 640;
    private const int MaxPreviewHeight = 480;

    public CalibrationPreview(Bitmap preview)
    {
        _preview = preview;

        FormBorderStyle = FormBorderStyle.FixedSingle;
        TopMost          = true;
        ShowInTaskbar    = false;
        StartPosition    = FormStartPosition.CenterScreen;
        Text             = "Calibration preview — does this look right?";
        BackColor        = Color.FromArgb(30, 30, 30);
        MaximizeBox      = false;

        // Scale preview to fit while keeping aspect ratio.
        double scale = Math.Min((double)MaxPreviewWidth  / preview.Width,
                                (double)MaxPreviewHeight / preview.Height);
        scale = Math.Min(scale, 1.0);
        int pw = Math.Max(1, (int)(preview.Width  * scale));
        int ph = Math.Max(1, (int)(preview.Height * scale));

        const int padding = 12;
        const int btnH    = 36;
        const int btnW    = 140;
        const int gap     = 8;

        var pic = new PictureBox
        {
            Image    = preview,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(padding, padding),
            Size     = new Size(pw, ph),
            BorderStyle = BorderStyle.FixedSingle,
        };

        int btnY = padding + ph + gap;

        var confirmBtn = new Button
        {
            Text      = "✓  Yes, looks correct",
            Location  = new Point(padding, btnY),
            Size      = new Size(btnW + 20, btnH),
            BackColor = Color.FromArgb(0, 140, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
        };
        confirmBtn.FlatAppearance.BorderSize = 0;
        confirmBtn.Click += (_, _) => { Confirmed = true; Close(); };

        var redoBtn = new Button
        {
            Text      = "↺  Redo selection",
            Location  = new Point(padding + btnW + 20 + gap, btnY),
            Size      = new Size(btnW, btnH),
            BackColor = Color.FromArgb(160, 80, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 10),
        };
        redoBtn.FlatAppearance.BorderSize = 0;
        redoBtn.Click += (_, _) => { Confirmed = false; Close(); };

        ClientSize = new Size(pw + padding * 2,
                              btnY + btnH + padding);

        Controls.Add(pic);
        Controls.Add(confirmBtn);
        Controls.Add(redoBtn);
        AcceptButton = confirmBtn;
    }

    public static bool Show(Rectangle region)
    {
        bool result = false;
        var thread = new Thread(() =>
        {
            using var bmp = ScreenCapture.CaptureRegion(region);
            using var form = new CalibrationPreview(bmp);
            form.ShowDialog();
            result = form.Confirmed;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }
}
