using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace EQD2Converter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ScriptContext scriptcontext;

        public List<DataGridStructures> DataGridStructuresList = new List<DataGridStructures>() { };
        public ListCollectionView DataGridStructuresCollection { get; set; }

        public int numberOfFractions;

        public double scaling;
        public double scaling2; // scaling evaluation dose (may be different than scaling if plan imported into eclipse)
        public double scaling3; // aux scaling for those plan that were imported into eclipse

        public HashSet<Tuple<int, int, int>> existingIndexes = new HashSet<Tuple<int, int, int>>() { };

        public delegate int calculateFunction(int dose, double alphabeta, double scaling);

        public int[,,] originalArray;

        public double doseMax;
        public double doseMin;

        public MainWindow(ScriptContext scriptcontext)
        {
            this.scriptcontext = scriptcontext;
            this.numberOfFractions = (int)scriptcontext.ExternalPlanSetup.NumberOfFractions;

            InitializeComponent();

            this.ComboBox.ItemsSource = new List<string> { "Ascending", "Descending" };
            this.ComboBox.SelectedIndex = 0;

            this.ComboBox2.ItemsSource = new List<string> { "EQD2", "BED" , "Multiply by a/b"};
            this.ComboBox2.SelectedIndex = 0;

            PopulateDataGrid();

        }

        public class DataGridStructures
        {
            public string Structure { get; set; }
            public string AlphaBeta { get; set; }
        }


        public void PopulateDataGrid()
        {
            List<DataGridStructures> datagrid = new List<DataGridStructures>() { };

            foreach (var structure in scriptcontext.StructureSet.Structures.OrderBy(u => u.Id).ToList())
            {
                if (!structure.IsEmpty & structure.DicomType != "SUPPORT" & structure.DicomType != "MARKER" & structure.DicomType != "BOLUS")
                {
                    DataGridStructures item = new DataGridStructures()
                    {
                        Structure = structure.Id,
                        AlphaBeta = "",
                    };
                    datagrid.Add(item);
                }
            }

            this.DataGridStructuresList = datagrid;
            ListCollectionView collectionView = new ListCollectionView(this.DataGridStructuresList);
            collectionView.GroupDescriptions.Add(new PropertyGroupDescription("Structures"));
            this.DataGridStructuresCollection = collectionView;
            this.DataGrid1.ItemsSource = this.DataGridStructuresCollection;
        }


        private double ConvertTextToDouble(string text)
        {
            if (Double.TryParse(text, out double result))
            {
                return result;
            }
            else
            {
                return Double.NaN;
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txt = sender as TextBox;
            int ind = txt.CaretIndex;
            txt.Text = txt.Text.Replace(",", ".");
            txt.CaretIndex = ind;
        }

        private void DataGrid_SourceUpdated(object sender, DataTransferEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            PlanNameWindow planNameWindow = new PlanNameWindow(this.scriptcontext, this.scriptcontext.ExternalPlanSetup.Id + "_" + this.ComboBox2.SelectedValue.ToString());
            planNameWindow.ShowDialog();

            string newPlanName = planNameWindow.PlanName;
            ExternalPlanSetup newPlan = (ExternalPlanSetup)null;
            
            if (planNameWindow.DialogResult.HasValue && planNameWindow.DialogResult.Value)
            {

                try
                {
                    newPlan = this.scriptcontext.Course.AddExternalPlanSetupAsVerificationPlan(this.scriptcontext.StructureSet, this.scriptcontext.ExternalPlanSetup);
                    newPlan.Id = newPlanName;
                }
                catch
                {
                    MessageBox.Show("Cannot create plan " + newPlanName + ".", "Error");
                    return;
                }

                // Add a waiting window here
                this.Cursor = Cursors.Wait;
                var waitWindow = new WaitingWindow();
                waitWindow.Show();

                try
                {
                    ConvertDose(newPlan, false);
                }
                catch (Exception f)
                {
                    waitWindow.Close();
                    this.Cursor = null;
                    MessageBox.Show(f.Message + "\n" + f.StackTrace, "Error");
                }

                waitWindow.Close();
                this.Cursor = null;

                MessageBox.Show("A new verification plan was created with a modified dose distribution.\n\n" +
                        "Voxel value to dose scaling factor (original dose): " + this.scaling.ToString() + "\n" +
                        "Voxel value to dose scaling factor (evaluation dose): " + this.scaling2.ToString() + "\n" +
                        "Voxel value to voxel value scaling factor (evaluation dose): " + this.scaling3.ToString(), "Message");
            }
            else
            {
                return;
            }
        }


        public int[,,] GetDoseVoxelsFromDose(Dose dose)
        {
            int Xsize = dose.XSize;
            int Ysize = dose.YSize;
            int Zsize = dose.ZSize;

            int[,,] doseMatrix = new int[Zsize, Xsize, Ysize];

            // Get whole dose matrix from context
            for (int k = 0; k < Zsize; k++)
            {
                int[,] plane = new int[Xsize, Ysize];
                dose.GetVoxels(k, plane);

                for (int i = 0; i < Xsize; i++)
                {
                    for (int j = 0; j < Ysize; j++)
                    {
                        doseMatrix[k, i, j] = plane[i, j];
                    }
                }
            }
            return doseMatrix;
        }

        public double GetMaxDoseVal(Dose dose, ExternalPlanSetup plan)
        {
            DoseValue maxDose = dose.DoseMax3D;
            double maxDoseVal = maxDose.Dose;

            if (maxDose.IsRelativeDoseValue)
            {
                if (plan.TotalDose.Unit == DoseValue.DoseUnit.cGy)
                {
                    maxDoseVal = maxDoseVal * plan.TotalDose.Dose / 10000.0;
                }
                else
                {
                    maxDoseVal = maxDoseVal * plan.TotalDose.Dose / 100.0;
                }
            }

            if (maxDose.Unit == DoseValue.DoseUnit.cGy)
            {
                maxDoseVal = maxDoseVal / 100.0;
            }
            return maxDoseVal;
        }

        public int[,,] ConvertDose(ExternalPlanSetup newPlan, bool preview = false)
        {
            Dose dose = this.scriptcontext.ExternalPlanSetup.Dose;

            int Xsize = dose.XSize;
            int Ysize = dose.YSize;
            int Zsize = dose.ZSize;

            double Xres = dose.XRes;
            double Yres = dose.YRes;
            double Zres = dose.ZRes;

            VVector Xdir = dose.XDirection;
            VVector Ydir = dose.YDirection;
            VVector Zdir = dose.ZDirection;

            VVector doseOrigin = dose.Origin;

            int[,,] doseMatrix = GetDoseVoxelsFromDose(dose);

            this.originalArray = GetDoseVoxelsFromDose(dose); // a copy

            double maxDoseVal = GetMaxDoseVal(dose, this.scriptcontext.ExternalPlanSetup);

            Tuple<int, int> minMaxDose = GetMinMaxValues(doseMatrix, Xsize, Ysize, Zsize);

            double scaling = maxDoseVal / minMaxDose.Item2;
            this.scaling = scaling;

            this.doseMin = minMaxDose.Item1 * scaling;
            this.doseMax = minMaxDose.Item2 * scaling;

            Dictionary<Structure, double> structureDict = new Dictionary<Structure, double>() { };

            foreach (var row in this.DataGridStructuresList)
            {
                if (row.AlphaBeta != null && row.AlphaBeta != "" && ConvertTextToDouble(row.AlphaBeta) != Double.NaN)
                {
                    Structure structure = this.scriptcontext.StructureSet.Structures.First(id => id.Id == row.Structure);
                    double alphabeta = ConvertTextToDouble(row.AlphaBeta);

                    structureDict.Add(structure, alphabeta);
                }
            }

            IOrderedEnumerable<KeyValuePair<Structure, double>> sortedDict;

            if (this.ComboBox.SelectedValue.ToString() == "Descending")
            {
                sortedDict = from entry in structureDict orderby entry.Value descending select entry;
            }
            else
            {
                sortedDict = from entry in structureDict orderby entry.Value ascending select entry;
            }

            foreach (var str in sortedDict)
            {
                Structure structure = str.Key;
                double alphabeta = str.Value;

                if (this.ComboBox2.SelectedValue.ToString() == "EQD2")
                {
                    OverridePixels(structure, alphabeta, doseMatrix, scaling, Xsize, Ysize, Zsize,
                         Xres, Yres, Zres, Xdir, Ydir, Zdir, doseOrigin, CalculateEQD2);
                }
                else if (this.ComboBox2.SelectedValue.ToString() == "BED")
                {
                    OverridePixels(structure, alphabeta, doseMatrix, scaling, Xsize, Ysize, Zsize,
                         Xres, Yres, Zres, Xdir, Ydir, Zdir, doseOrigin, CalculateBED);
                }
                else
                {
                    OverridePixels(structure, alphabeta, doseMatrix, scaling, Xsize, Ysize, Zsize,
                         Xres, Yres, Zres, Xdir, Ydir, Zdir, doseOrigin, MultiplyByAlphaBeta);
                }
            }

            this.existingIndexes = new HashSet<Tuple<int, int, int>>() { }; // reset!

            if (!preview)
            {
                CreatePlanAndAddDose(Xsize, Ysize, Zsize, doseMatrix, maxDoseVal, newPlan);
                return new int[0, 0, 0];
            }
            else
            {
                return doseMatrix;
            }
        }

        public void CreatePlanAndAddDose(int Xsize, int Ysize, int Zsize, int[,,] doseMatrix, double doseMaxOriginal, ExternalPlanSetup newPlan)
        {
            int fractions = (int)this.scriptcontext.ExternalPlanSetup.NumberOfFractions;
            DoseValue dosePerFraction = this.scriptcontext.ExternalPlanSetup.DosePerFraction;
            double treatPercentage = this.scriptcontext.ExternalPlanSetup.TreatmentPercentage;
            
            newPlan.SetPrescription(fractions, dosePerFraction, treatPercentage);

            double normalization = this.scriptcontext.ExternalPlanSetup.PlanNormalizationValue;
            if (!Double.IsNaN(normalization))
            {
                newPlan.PlanNormalizationValue = normalization;
            }
            else
            {
                newPlan.PlanNormalizationValue = 100;
            }

            EvaluationDose evalDose = newPlan.CopyEvaluationDose(this.scriptcontext.ExternalPlanSetup.Dose);

            double maxDoseVal = GetMaxDoseVal(evalDose, newPlan);
            
            Tuple<int, int> minMaxDoseInt = GetMinMaxValues(GetDoseVoxelsFromDose(evalDose), Xsize, Ysize, Zsize);
            int maxInt = minMaxDoseInt.Item2;

            double scaling2 = maxDoseVal / maxInt;
            this.scaling2 = scaling2;

            // scaling2 is used when a plan is imported into eclipse. In this case, the voxel values are correctly set,
            // however the internal scaling factor differes from the original one after Evaluation dose is copied. Don't know why exactly.
            // I solved this by introducing an additional scaling factor that in normal cases should equal 1.
            // This factor is used to renormalize the voxels so that if no conversion is done, the result is equal to the original.
            double scaling3 = doseMaxOriginal / maxDoseVal;
            this.scaling3 = scaling3;

            if (maxInt * scaling3 > 2147483647.0)
            {
                throw new InvalidDataException("Maximum integer value exceeded. Conversion failed.");
            }
            else
            {
                for (int k = 0; k < Zsize; k++)
                {
                    int[,] plane = new int[Xsize, Ysize];
                    for (int i = 0; i < Xsize; i++)
                    {
                        for (int j = 0; j < Ysize; j++)
                        {
                            plane[i, j] = (int)(doseMatrix[k, i, j] * scaling3);
                        }
                    }
                    evalDose.SetVoxels(k, plane);
                }
            }
        }


        public Tuple<int, int> GetMinMaxValues(int[,,] array, int Xsize, int Ysize, int Zsize)
        {
            int min = Int32.MaxValue;
            int max = 0;

            for (int i = 0; i < Xsize; i++)
            {
                for (int j = 0; j < Ysize; j++)
                {
                    for (int k = 0; k < Zsize; k++)
                    {
                        int temp = array[k, i, j];

                        if (temp > max)
                        {
                            max = temp;
                        }
                        else if (temp < min)
                        {
                            min = temp;
                        }
                    }
                }
            }
            return Tuple.Create(min, max);
        }

        public int GetIndexFromCoordinate(double coord, double origin, double direction, double res)
        {
            return Convert.ToInt32((coord - origin) / (direction * res));
        }


        public void OverridePixels(Structure structure, double alphabeta, int[,,] doseMatrix, double scaling, int Xsize, int Ysize, int Zsize,
            double Xres, double Yres, double Zres, VVector Xdir, VVector Ydir, VVector Zdir, VVector doseOrigin, calculateFunction functionCalculate)
        {
            // The following is valid only for HFS, HFP, FFS, FFP orientations that do not mix x,y,z
            double sx = Xdir.x + Ydir.x + Zdir.x;
            double sy = Xdir.y + Ydir.y + Zdir.y;
            double sz = Xdir.z + Ydir.z + Zdir.z;

            var bounds = structure.MeshGeometry.Bounds;

            double x0 = bounds.X;
            double x1 = x0 + bounds.SizeX;
            double y0 = bounds.Y;
            double y1 = y0 + bounds.SizeY;
            double z0 = bounds.Z;
            double z1 = z0 + bounds.SizeZ;

            int imin = GetIndexFromCoordinate(x0, doseOrigin.x, sx, Xres);
            int imax = GetIndexFromCoordinate(x1, doseOrigin.x, sx, Xres);

            int jmin = GetIndexFromCoordinate(y0, doseOrigin.y, sy, Yres);
            int jmax = GetIndexFromCoordinate(y1, doseOrigin.y, sy, Yres);

            int kmin = GetIndexFromCoordinate(z0, doseOrigin.z, sz, Zres);
            int kmax = GetIndexFromCoordinate(z1, doseOrigin.z, sz, Zres);

            if (imin > imax)
            {
                int t = imin;
                imin = imax;
                imax = t;
            }
            if (jmin > jmax)
            {
                int t = jmin;
                jmin = jmax;
                jmax = t;
            }
            if (kmin > kmax)
            {
                int t = kmin;
                kmin = kmax;
                kmax = t;
            }

            imax += 2;
            imin -= 2;
            jmax += 2;
            jmin -= 2;
            kmax += 2;
            kmin -= 2;

            if (imin < 0)
            {
                imin = 0;
            }
            if (imax > Xsize - 1)
            {
                imax = Xsize - 1;
            }

            if (jmin < 0)
            {
                jmin = 0;
            }
            if (jmax > Ysize - 1)
            {
                jmax = Ysize - 1;
            }

            if (kmin < 0)
            {
                kmin = 0;
            }
            if (kmax > Zsize - 1)
            {
                kmax = Zsize - 1;
            }

            int nx = imax - imin + 1;
            int ny = jmax - jmin + 1;
            int nz = kmax - kmin + 1;

            for (int k = kmin; k <= kmax; k++)
            {
                for (int j = jmin; j <= jmax; j++)
                {
                    double y = doseOrigin.y + j * Yres * sy;
                    double z = doseOrigin.z + k * Zres * sz;

                    double xstart = doseOrigin.x + imin * Xres * sx;
                    double xstop = doseOrigin.x + imax * Xres * sx;

                    var profilePoints = structure.GetSegmentProfile(new VVector(xstart, y, z), new VVector(xstop, y, z), new BitArray(nx)).Select(profilePoint => profilePoint.Value).ToArray();

                    for (int p = 0; p < profilePoints.Length; p++)
                    {
                        Tuple<int, int, int> newIndices = Tuple.Create(k, imin + p, j);
                        
                        if (profilePoints[p] && this.existingIndexes.Contains(newIndices) == false)
                        {
                            int dose = doseMatrix[k, imin + p, j];
                            doseMatrix[k, imin + p, j] = functionCalculate(dose, alphabeta, scaling);

                            this.existingIndexes.Add(newIndices);
                        }
                    }
                }
            }
        }


        public int CalculateEQD2(int dose, double alphabeta, double scaling)
        {
            return Convert.ToInt32((dose * (alphabeta + dose * scaling / this.numberOfFractions) / (alphabeta + 2.0)));
        }

        public int CalculateBED(int dose, double alphabeta, double scaling)
        {
            return Convert.ToInt32(dose * (1 + dose * scaling / (this.numberOfFractions * alphabeta)));
        }

        public int MultiplyByAlphaBeta(int dose, double alphabeta, double scaling)
        {
            return Convert.ToInt32(dose * alphabeta);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Show waiting window temporarily
            this.Cursor = Cursors.Wait;
            var waitWindow = new WaitingWindow();
            waitWindow.Show();

            int[,,] convdose = ConvertDose((ExternalPlanSetup)null, true);
            Tuple<int, int> minMaxConverted = GetMinMaxValues(convdose, convdose.GetLength(1), convdose.GetLength(2), convdose.GetLength(0));

            PreviewWindow previewWindow = new PreviewWindow(this.scriptcontext, convdose, this.originalArray,
                this.scaling, this.doseMin, this.doseMax, minMaxConverted.Item1, minMaxConverted.Item2);

            waitWindow.Close();
            this.Cursor = null;

            previewWindow.ShowDialog();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            HelpWindow helpwindow = new HelpWindow();
            helpwindow.Owner = this;
            helpwindow.Show();
        }
    }
}