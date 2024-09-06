using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace lehimleme
{
    public partial class ModelNameForm : Form
    {
        public string ModelName { get; private set; }
        public ModelNameForm()
        {
            InitializeComponent();
        }

        private void btnKaydet_Click(object sender, EventArgs e)
        {
            ModelName = txtModelName.Text;
            this.DialogResult = DialogResult.Yes;
            this.Close();
        }

        private void btnIptal_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
