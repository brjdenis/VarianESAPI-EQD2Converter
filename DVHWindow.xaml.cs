using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
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
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace EQD2Converter
{
    /// <summary>
    /// Interaction logic for DVHWindow.xaml
    /// </summary>
    public partial class DVHWindow : Window
    {
        public ScriptContext scriptcontext;
        private List<double[,]> convertedArray;

        public PlotModel PlotModelDVH { get; set; }

        public LinearAxis AxisXDVH = new LinearAxis { Position = AxisPosition.Bottom, Minimum = 0, Title = "Dose [Gy]"};
        public LinearAxis AxisYDVH = new LinearAxis { Position = AxisPosition.Left, Minimum = 0 , Title = "Volume [%]"};

        public List<DataGridDoseData> DataGridDoseDataList = new List<DataGridDoseData>() { };
        public ListCollectionView DataGridDoseDataCollection { get; set; }

        public DVHWindow(ScriptContext scriptcontext, List<double[,]> _convertedArray, double scaling)
        {
            this.scriptcontext = scriptcontext;
            this.convertedArray = InvertArrayYAxis(_convertedArray);

            this.PlotModelDVH = CreatePlotModelDVH();

            InitializeComponent();

            this.PlotDVH.Model = this.PlotModelDVH;
            PopulateStructureList();

            SetController();
            this.SizeChanged += LayoutUpdate;
        }

        public class DataGridDoseData
        {
            public string Structure { get; set; }
            public string StructureVolume { get; set; }
            public string DoseCoverEclipse { get; set; }
            public string DoseCoverConverted { get; set; }
            public string MaxDoseEclipse { get; set; }
            public string MaxDoseConverted { get; set; }
            public string MeanDoseEclipse { get; set; }
            public string MeanDoseConverted { get; set; }
            public string MinDoseEclipse { get; set; }
            public string MinDoseConverted { get; set; }
        }

        public void PopulateDataGrid(List<DataGridDoseData> datagridData)
        {
            this.DataGridDoseDataList = datagridData;
            ListCollectionView collectionView = new ListCollectionView(this.DataGridDoseDataList);
            collectionView.GroupDescriptions.Add(new PropertyGroupDescription("Data"));
            this.DataGridDoseDataCollection = collectionView;
            this.DataGrid1.ItemsSource = this.DataGridDoseDataCollection;
        }

        private void LayoutUpdate(object sender, EventArgs e)
        {
            this.PlotDVH.Width = this.PlotOriginalColumn.ActualWidth;
            this.PlotDVH.Height = this.PlotOriginalRow.ActualHeight;

        }

        public List<double[,]> InvertArrayYAxis(List<double[,]> array)
        {
            // Have to do this ... because I really have no idea how to code better
            int Xsize = array[0].GetLength(0);
            int Ysize = array[0].GetLength(1);
            int Zsize = array.Count;

            List<double[,]> newArray = new List<double[,]>() { };

            for (int k = 0; k < Zsize; k++)
            {
                double[,] temp = new double[Xsize, Ysize];
                for (int i = 0; i < Xsize; i++)
                {
                    for (int j = 0; j < Ysize; j++)
                    {
                        temp[i, Ysize - j - 1] = array[k][i, j];
                    }
                }
                newArray.Add(temp);
            }
            return newArray;
        }

        public List<string> GetStructureList()
        {
            List<string> structureList = new List<string>() { };

            foreach (var structure in this.scriptcontext.StructureSet.Structures.OrderBy(u => u.Id).ToList())
            {
                if (!structure.IsEmpty & structure.DicomType != "SUPPORT")
                {
                    structureList.Add(structure.Id);
                }
            }
            return structureList;
        }

        public void PopulateStructureList()
        {
            List<string> structureList = GetStructureList();

            this.StructureListView.ItemsSource = structureList;
            //this.StructureListView.SelectAll();
        }

        public List<string> GetSelectedStructures()
        {
            List<string> structureList = new List<string>() {  };
            foreach(var item in this.StructureListView.SelectedItems)
            {
                structureList.Add(item.ToString());
            }
            return structureList;
        }

        private void StructureListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.PlotModelDVH != null)
            {
                AddSeriesDVH(this.PlotModelDVH);
                this.PlotModelDVH.InvalidatePlot(true);
                this.PlotModelDVH.InvalidatePlot(true);
            }
        }

        public PlotModel CreatePlotModelDVH()
        {
            var plotModel = new PlotModel { };
            plotModel.Axes.Add(this.AxisXDVH);
            plotModel.Axes.Add(this.AxisYDVH);

            return plotModel;
        }

        public void SetController()
        {
            var myController = new PlotController();
            this.PlotDVH.Controller = myController;

            myController.UnbindMouseWheel();

            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.Control);
            myController.UnbindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None);
            myController.UnbindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, 2);
            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None);
            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None, 2);

            myController.BindMouseWheel(OxyModifierKeys.Control, OxyPlot.PlotCommands.ZoomWheelFine);
            myController.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, OxyPlot.PlotCommands.ZoomRectangle);
            myController.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, 2, OxyPlot.PlotCommands.ResetAt);
            myController.BindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None, OxyPlot.PlotCommands.PanAt);

        }


        public void AddSeriesDVH(PlotModel plotModel)
        {
            List<string> structureList = GetSelectedStructures();

            List<DataGridDoseData> datagridData = new List<DataGridDoseData>() { };

            RemoveSeries(plotModel);

            double plotXMax = 0;

            double maxDoseEclipse = 0;
            double maxDoseConverted = 0;

            double minDoseEclipse = 0;
            double minDoseConverted = 0;

            double meanDoseEclipse = 0;
            double meanDoseConverted = 0;
            double maxVolume = 100;

            double voxelVolume = 0;
            double doseCoverageEclipse = 0;
            double doseCoverageConverted = 0;

            foreach (var str in structureList)
            {
                Structure structure = this.scriptcontext.StructureSet.Structures.First(u => u.Id == str);

                DVHData dataEclipse;
                Tuple<List<double>, double> calcConverted;
                List<double> dataConverted;

                try
                {
                    dataEclipse = GetEclipseDVH(structure);
                    calcConverted = GetDosesForStructure(structure);
                }
                catch
                {
                    continue;
                }

                dataConverted = calcConverted.Item1;
                voxelVolume = calcConverted.Item2;

                doseCoverageEclipse = dataEclipse.Coverage;

                var calcdvh = CalculateHistogram(dataConverted, doseCoverageEclipse);
                List<DataPoint> pointsConverted = calcdvh.Item1;
                minDoseConverted = calcdvh.Item2;
                maxDoseConverted = calcdvh.Item3;
                meanDoseConverted = calcdvh.Item4;

                doseCoverageConverted = dataConverted.Count() * voxelVolume / structure.Volume;

                DVHPoint[] pointsEclipse = dataEclipse.CurveData;

                double doseScaling = 1.0;
                if (pointsEclipse[0].DoseValue.Unit == DoseValue.DoseUnit.cGy)
                {
                    doseScaling = 0.01;
                }
                
                maxDoseEclipse = dataEclipse.MaxDose.Dose * doseScaling;
                meanDoseEclipse = dataEclipse.MeanDose.Dose * doseScaling;
                minDoseEclipse = dataEclipse.MinDose.Dose * doseScaling;

                var series = new LineSeries
                {
                    Title = structure.Id + " (Eclipse)",
                    Tag = structure.Id + "_Eclipse",
                    Color = structure.Color.ToOxyColor(),
                    TrackerFormatString = "{0}\n{1}: {2:0.##}\n{3}: {4:0.##}",
                };

                foreach (var point in pointsEclipse)
                {
                    DataPoint oxypoint = new DataPoint(point.DoseValue.Dose * doseScaling, point.Volume);
                    series.Points.Add(oxypoint);

                    if (oxypoint.X > maxDoseEclipse)
                    {
                        maxDoseEclipse = oxypoint.X;
                    }
                    if (oxypoint.Y > maxVolume)
                    {
                        maxVolume = oxypoint.Y;
                    }
                }
                plotModel.Series.Add(series);

                var seriesConv = new LineSeries
                {
                    Title = structure.Id + " (Converted)",
                    Tag = structure.Id + "_Converted",
                    Color = structure.Color.ToOxyColor(),
                    LineStyle = LineStyle.Dash,
                    TrackerFormatString = "{0}\n{1}: {2:0.##}\n{3}: {4:0.##}",
                };

                foreach (var point in pointsConverted)
                {
                    seriesConv.Points.Add(point);
                }
                plotModel.Series.Add(seriesConv);

                // add data to table
                DataGridDoseData item = new DataGridDoseData()
                {
                    Structure = structure.Id,
                    StructureVolume = structure.Volume.ToString("F2"),
                    DoseCoverEclipse = doseCoverageEclipse.ToString("F2"),
                    DoseCoverConverted = doseCoverageConverted.ToString("F2"),
                    MaxDoseEclipse = maxDoseEclipse.ToString("F2"),
                    MaxDoseConverted = maxDoseConverted.ToString("F2"),
                    MeanDoseEclipse = meanDoseEclipse.ToString("F2"),
                    MeanDoseConverted = meanDoseConverted.ToString("F2"),
                    MinDoseEclipse = minDoseEclipse.ToString("F2"),
                    MinDoseConverted = minDoseConverted.ToString("F2")
                };
                datagridData.Add(item);

                if (maxDoseConverted > plotXMax)
                {
                    plotXMax = maxDoseConverted;
                }
                if (maxDoseEclipse > plotXMax)
                {
                    plotXMax = maxDoseEclipse;
                }
            }

            this.AxisXDVH.Maximum = plotXMax;
            this.AxisYDVH.Maximum = 100;

            PopulateDataGrid(datagridData);
        }

        public void RemoveSeries(PlotModel plotmodel)
        {
            if (plotmodel.Series.ToList().Count > 0)
            {
                foreach (var series in plotmodel.Series.ToList()) // must use ToList() otherwise you will get errros
                {
                    plotmodel.Series.Remove(series);
                }
            }
        }

        public DVHData GetEclipseDVH(Structure structure)
        {
            DVHData dvh = this.scriptcontext.ExternalPlanSetup.GetDVHCumulativeData(
                                                                 structure,
                                                                 DoseValuePresentation.Absolute,
                                                                 VolumePresentation.Relative,
                                                                 0.1);

            return dvh;
        }
        
        public int GetIndexFromCoordinate(double coord, double origin, double direction, double res)
        {
            return Convert.ToInt32((coord - origin) / (direction * res));
        }

        public Tuple<List<double>, double> GetDosesForStructure(Structure structure)
        {
            // Calculated DVH from the converted dose matrix! This is a very bad approximation, to be honest.
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

            double volume = Xres * Yres * Zres / 1000.0;

            List<double> doseValues = new List<double>() { };

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
                        if (profilePoints[p])
                        {
                            doseValues.Add(this.convertedArray[k][imin + p, j]);
                        }
                    }
                }
            }
            return Tuple.Create(doseValues, volume);
        }

        public Tuple<List<DataPoint>, double, double, double> CalculateHistogram(List<double> data, double doseCoverageEclipse)
        {
            double dataLength = (double)data.Count();

            int N = 1000;  // (N - 1) bins

            // volume is the volume of the voxel
            double doseMax = 0;
            double doseMin = Double.MaxValue;
            double doseMean = 0;
            foreach (double item in data)
            {
                if (item > doseMax)
                {
                    doseMax = item;
                }
                if (item < doseMin)
                {
                    doseMin = item;
                }
                doseMean += item;
            }

            doseMean = doseMean / data.Count();

            double deltaDose = doseMax / (N - 1);

            double[] doseEdge = new double[N];

            for (int i = 0; i < N; i++)
            {
                doseEdge[i] = i * deltaDose;
            }

            doseEdge[0] -= Double.Epsilon;
            doseEdge[N - 1] += Double.Epsilon;

            List<DataPoint> doseHits = new List<DataPoint> (){ };

            for (int i = 0; i < N; i++)
            {
                double doseAt = doseEdge[i];
                int hits = 0;
                for (int j = 0; j < data.Count(); j++)
                {
                    if (data[j] > doseAt)
                    {
                        hits += 1;
                    }
                }
                // multiply with dose coverage factor from Eclipse just to get similar plot
                // this is getting extremely silly... but as an approximation it will work
                doseHits.Add(new DataPoint(doseAt, 100.0 * (hits / dataLength) * doseCoverageEclipse));
            }
            return Tuple.Create(doseHits, doseMin, doseEdge[N - 1], doseMean);
        }
    }
}
