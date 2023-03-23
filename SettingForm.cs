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
        public SettingForm(Point startLocation, GlassyPanel panel)
        {
            InitializeComponent();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            this.Left = startLocation.X;
            this.Top = startLocation.Y;
            this.panel = panel;
        }

        private void SettingForm_Load(object sender, EventArgs e)
        {
            
        }

        private void SettingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.panel.Hide();
            this.panel.SendToBack();
        }
    }
}
