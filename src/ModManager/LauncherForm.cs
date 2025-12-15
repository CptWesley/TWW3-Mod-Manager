namespace ModManager;

public sealed class LauncherForm : Form
{
    private const int DefaultMargin = 15;

    private readonly Button startButton = new();

    public LauncherForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Resize += OnResize;
        SetName();
        SetupStartButton();

        this.MinimumSize = new(600, 350);
        this.Size = new(1200, 700);

        this.ResumeLayout(true);
    }

    private void SetupStartButton()
    {
        this.Controls.Add(this.startButton);
        startButton.Enabled = true;
        startButton.Text = "Launch Game";
        startButton.Width = 100;
        startButton.Height = 30;
    }

    private void OnResize(object? sender, EventArgs e)
    {
        if (sender is not LauncherForm form)
        {
            return;
        }

        var width = form.ClientSize.Width;
        var height = form.ClientSize.Height;

        startButton.Left = width - startButton.Width - DefaultMargin;
        startButton.Top = height - startButton.Height - DefaultMargin;
    }

    private void SetName()
    {
        var name = "CptWesley's Total War Warhammer III Mod Manager";
        this.Text = name;
        this.Name = name;
    }
}
