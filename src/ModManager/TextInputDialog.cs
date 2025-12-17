namespace ModManager;

public sealed class TextInputDialog : Form
{
    private const int DefaultMargin = 15;

    private readonly Label label = new();
    private readonly TextBox input = new();
    private readonly Button accept = new();
    private readonly Button cancel = new();

    public TextInputDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        this.StartPosition = FormStartPosition.CenterParent;

        this.Controls.Add(label);
        this.Controls.Add(input);
        this.Controls.Add(accept);
        this.Controls.Add(cancel);

        label.TextAlign = System.Drawing.ContentAlignment.TopCenter;

        accept.Text = "Continue";
        cancel.Text = "Cancel";

        this.MinimumSize = new(460, 160);
        this.Size = this.MinimumSize;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;

        accept.Click += (s, e) =>
        {
            DialogResult = DialogResult.OK;
            this.Close();
        };

        cancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            this.Close();
        };

        this.Resize += (s, e) =>
        {
            OnResize();
        };

        OnResize();

        this.ResumeLayout(true);
    }

    private void OnResize()
    {
        cancel.Left = DefaultMargin;
        cancel.Top = ClientSize.Height - DefaultMargin - cancel.Height;

        accept.Left = ClientSize.Width - DefaultMargin - cancel.Width;
        accept.Top = ClientSize.Height - DefaultMargin - cancel.Height;

        label.Width = ClientSize.Width - (2 * DefaultMargin);
        label.Left = DefaultMargin;
        label.Top = DefaultMargin;

        input.Width = label.Width / 3 * 2;
        input.Left = DefaultMargin + (label.Width / 6);
        input.Top = label.Bottom;
    }

    public string Label
    {
        get => label.Text;
        set => label.Text = value;
    }

    public string Input
    {
        get => input.Text;
        set => input.Text = value;
    }

    public bool InputVisible
    {
        get => input.Visible;
        set => input.Visible = value;
    }
}
