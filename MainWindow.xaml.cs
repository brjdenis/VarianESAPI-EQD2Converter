using EvilDICOM.Core;
using EvilDICOM.Core.Element;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core.Image;
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
                if (!structure.IsEmpty)
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
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

                Nullable<bool> result = dlg.ShowDialog();

                if (result == true)
                {
                    ConvertDose(false, dlg.FileName);
                    MessageBox.Show("Done!", "Message");
                }
                else
                {
                    MessageBox.Show("No dicom selected.", "Error");
                }
            }
            catch (Exception f)
            {
                MessageBox.Show(f.Message + "\n" + f.StackTrace, "Error");
            }
        }

        public void ReadFromDicom(string filePath)
        {
            // Not used, but included in case I need it at some point

            var dcm = DICOMObject.Read(filePath);
            ushort rows = (ushort)dcm.FindFirst(TagHelper.Rows).DData;
            ushort columns = (ushort)dcm.FindFirst(TagHelper.Columns).DData;

            List<byte> pixelData = (List<byte>)dcm.FindFirst(TagHelper.PixelData).DData_;

            List<double> offsetVector = (List<double>)dcm.FindFirst(TagHelper.GridFrameOffsetVector).DData_;
            int frames = offsetVector.Count;

            double scaling = 1.0;
            var _scaling = dcm.FindFirst(TagHelper.DoseGridScaling) as AbstractElement<double>;
            if (_scaling != null)
            {
                scaling = _scaling.Data;
            }

            int[,,] doseMatrix = new int[frames, columns, rows];

            int indexColumn = 0;
            int indexRow = 0;
            int indexFrame = 0;

            for (int i = 0; i < pixelData.Count(); i += 4)
            {
                // taken from https://stackoverflow.com/questions/55883798/wrong-output-pixel-colors-grayscale-using-evildicom
                // 32 bits

                int pixel = (int)(pixelData[i + 3] * 256 * 256 * 256 + pixelData[i + 2] * 256 * 256 + pixelData[i + 1] * 256 + pixelData[i]);

                doseMatrix[indexFrame, indexColumn, indexRow] = pixel;

                indexColumn += 1;
                if (indexColumn > columns - 1)
                {
                    indexColumn = 0;
                    indexRow += 1;
                }
                if (indexRow > rows - 1)
                {
                    indexFrame += 1;
                    indexColumn = 0;
                    indexRow = 0;
                }
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

        public int[,,] ConvertDose(bool preview = false, string filePath = "")
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

            this.originalArray = GetDoseVoxelsFromDose(dose);

            // If only preview is sought calculate scaling factor from max dose
            double scaling = 1.0;

            if (preview)
            {
                DoseValue maxDose = dose.DoseMax3D;
                double maxDoseVal = maxDose.Dose;

                if (maxDose.IsRelativeDoseValue)
                {
                    if (this.scriptcontext.ExternalPlanSetup.TotalDose.Unit == DoseValue.DoseUnit.cGy)
                    {
                        maxDoseVal = maxDoseVal * this.scriptcontext.ExternalPlanSetup.TotalDose.Dose / 10000.0;
                    }
                    else
                    {
                        maxDoseVal = maxDoseVal * this.scriptcontext.ExternalPlanSetup.TotalDose.Dose / 100.0;
                    }
                }

                if (maxDose.Unit == DoseValue.DoseUnit.cGy)
                {
                    maxDoseVal = maxDoseVal / 100.0;
                }

                Tuple<int, int> minMaxDose = GetMinMaxValues(doseMatrix, Xsize, Ysize, Zsize);

                scaling = maxDoseVal / minMaxDose.Item2;

                this.doseMin = minMaxDose.Item1 * scaling;
                this.doseMax = minMaxDose.Item2 * scaling;
            }

            else
            {
                var dcm = DICOMObject.Read(filePath);

                var _scaling = dcm.FindFirst(TagHelper.DoseGridScaling) as AbstractElement<double>;
                if (_scaling != null)
                {
                    scaling = _scaling.Data;
                }
            }

            this.scaling = scaling;

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

            if (!preview)
            {
                WriteBackToDicom(filePath, doseMatrix, Xsize, Ysize, Zsize);
                this.existingIndexes = new HashSet<Tuple<int, int, int>>() { }; // reset!
                return new int[0, 0, 0];
            }
            else
            {
                this.existingIndexes = new HashSet<Tuple<int, int, int>>() { }; // reset!
                return doseMatrix;
            }
        }


        public void WriteBackToDicom(string filePath, int[,,] doseMatrix, int Xsize, int Ysize, int Zsize)
        {
            List<byte> byteArray = ConvertToByte(doseMatrix, Xsize, Ysize, Zsize);
            var dcm = DICOMObject.Read(filePath);
            dcm.FindFirst(TagHelper.PixelData).DData_ = byteArray;
            dcm.Write(filePath);
        }


        public List<byte> ConvertToByte(int[,,] array, int columns, int rows, int frames)
        {
            // This function converts int[,,] into 32-bit byte array so that it can be written to dicom
            List<byte> byteArray = new List<byte>() { };

            for (int i = 0; i < frames; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    for (int k = 0; k < columns; k++)
                    {
                        byte[] intBytes = BitConverter.GetBytes(array[i, k, j]);
                        byteArray.Add(intBytes[0]);
                        byteArray.Add(intBytes[1]);
                        byteArray.Add(intBytes[2]);
                        byteArray.Add(intBytes[3]);
                    }
                }
            }
            return byteArray;
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
            int[,,] convdose = ConvertDose(true, "");
            Tuple<int, int> minMaxConverted = GetMinMaxValues(convdose, convdose.GetLength(1), convdose.GetLength(2), convdose.GetLength(0));
            
            PreviewWindow previewWindow = new PreviewWindow(this.scriptcontext, convdose, this.originalArray, 
                this.scaling, this.doseMin, this.doseMax, minMaxConverted.Item1, minMaxConverted.Item2);
            
            previewWindow.ShowDialog();
        }
    }
}