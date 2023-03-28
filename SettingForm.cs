using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DisplayDevices
{
    public partial class SettingForm : Form
    {
        private GlassyPanel panel;
        private DisplayForm displayForm;
        public SettingForm(DisplayForm displayForm, GlassyPanel panel)
        {
            InitializeComponent();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            this.CenterToParent();
            this.displayForm = displayForm;
            this.panel = panel;
        }

        private void SettingForm_Load(object sender, EventArgs e)
        {
            this.ColumnTbx.Value = this.displayForm.NumDeviceColumn;
        }

        private void SettingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.panel.Hide();
            this.panel.SendToBack();
        }

        private void ConfirmBtn_Click(object sender, EventArgs e)
        {
            int numColumn = Convert.ToInt32(this.ColumnTbx.Text);
            this.displayForm.NumDeviceColumn = numColumn;
            this.displayForm.DisplayDevices();
            this.Close();
            this.panel.Hide();
            this.panel.SendToBack();
        }
    }
}
