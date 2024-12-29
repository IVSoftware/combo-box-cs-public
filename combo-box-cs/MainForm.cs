namespace combo_box_cs
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            comboBox.Items.AddRange(new[]
            {
                "zebra",
                "Zebra",
                "ZEBRA",
            });
        }
    }
}
