using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VMS.TPS.Common.Model.API;

namespace EQD2Converter
{
    /// <summary>
    /// Interaction logic for PlanNameWindow.xaml
    /// </summary>
    public partial class PlanNameWindow : Window
    {
        public string PlanName;

        private ScriptContext scriptcontext;
        private List<string> ExistingPlanNames;

        public PlanNameWindow(ScriptContext scriptcontext, string planName)
        {
            InitializeComponent();

            this.PlanName = planName;
            this.scriptcontext = scriptcontext;

            CollectPlanNames();

            this.PlanNameTextBox.Text = planName;
        }

        private void CollectPlanNames()
        {
            List<string> plannames = new List<string>() { };
            foreach(var plan in this.scriptcontext.Course.PlanSetups)
            {
                plannames.Add(plan.Id);
            }
            this.ExistingPlanNames = plannames;
        }

        private void IsPlanNameAvailable(object sender, TextChangedEventArgs e)
        {
            if (this.ExistingPlanNames.Contains(this.PlanNameTextBox.Text) || this.PlanNameTextBox.Text.Length > 13 || this.PlanNameTextBox.Text.Length < 1)
            {
                this.PlanNameTextBoxSuccess.Content = "\u274C";
                this.PlanNameTextBoxSuccess.Foreground = Brushes.Red;
                this.OKbutton.IsEnabled = false;
            }
            else
            {
                this.PlanNameTextBoxSuccess.Content = "\u2714";
                this.PlanNameTextBoxSuccess.Foreground = Brushes.Green;
                this.OKbutton.IsEnabled = true;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.PlanName = this.PlanNameTextBox.Text;
            DialogResult = true;
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
