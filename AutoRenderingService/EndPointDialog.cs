namespace AutoRenderingService;

public class EndPointDialog : Form
{
    private readonly TextBox _ipTextBox;
    private readonly NumericUpDown _portUpDown;

    public string IpAddress => _ipTextBox.Text.Trim();
    public int Port => (int)_portUpDown.Value;

    public EndPointDialog(string ip, int port)
    {
        Text = "Edit EndPoint";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(340, 150);

        var ipLabel = new Label
        {
            Text = "IP Address:",
            Location = new Point(16, 20),
            AutoSize = true,
        };

        _ipTextBox = new TextBox
        {
            Text = ip,
            Location = new Point(120, 17),
            Width = 196,
        };

        var portLabel = new Label
        {
            Text = "Port:",
            Location = new Point(16, 58),
            AutoSize = true,
        };

        _portUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 65535,
            Value = port,
            Location = new Point(120, 55),
            Width = 110,
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(120, 100),
            Size = new Size(90, 32),
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(226, 100),
            Size = new Size(90, 32),
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.AddRange([ipLabel, _ipTextBox, portLabel, _portUpDown, okButton, cancelButton]);
    }
}
